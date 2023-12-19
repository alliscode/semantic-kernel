// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using static Microsoft.SemanticKernel.Planning.FunctionCallingStepwisePlanner;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Json.More;
using System;
using System.Text.Json.Serialization;
using System.IO;

namespace Microsoft.SemanticKernel.Planning.Stepwise;

internal class OptimisticOperation
{
    public string Id { get; set; } = "";

    public int HistoryOffset { get; set; } = 0;

    public Func<OptimisticOperation, Task> Failed { get; set; } = (_) => throw new NotImplementedException();
}

internal class OptimisticOperation<T> : OptimisticOperation
{
    public Func<CancellationToken, Task<T>> Function {  get; set; } = (_) => throw new NotImplementedException();

    public Func<T, CancellationToken, Task<bool>> Verification {  get; set; } = (_, _) => throw new NotImplementedException();
}

internal class VerifierResponse
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; } = false;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}

internal class StepResult
{
    public ActionType ActionType { get; set; }

    public string ToolCallInfo { get; set; } = "";

    public string Result { get; set; } = "";

    public FunctionCallingStepwisePlannerResult? FinalResult { get; set; } = null;
}

internal enum ActionType
{
    Continue,
    ToolCall,
    FinalResult
}

public class OptimisticPlanner
{
    /// <summary>
    /// The configuration for the StepwisePlanner
    /// </summary>
    private FunctionCallingStepwisePlannerConfig Config { get; }

    //private readonly LinkedList<OptimisticOperation> _operations = new();
    private readonly Dictionary<string, OptimisticOperation> _operationsDict = new();
    private readonly List<Task<(string id, bool isValid)>> _verifyTasks = new();
    private readonly CancellationTokenSource _processingTokenSource;
    private CancellationTokenSource _verificationTokenSource;

    private readonly string _generatePlanYaml;
    private readonly string _verifyPlanPrompt;
    private readonly string _stepPrompt;
    private readonly string _verifyStepPrompt;

    /// <summary>
    /// The name to use when creating semantic functions that are restricted from plan creation
    /// </summary>
    private const string StepwisePlannerPluginName = "StepwisePlanner_Excluded";

    /// <summary>
    /// The user message to add to the chat history for each step of the plan.
    /// </summary>
    private const string StepwiseUserMessage = "Perform the next step of the plan if there is more work to do. When you have reached a final answer, use the UserInteraction_SendFinalAnswer function to communicate this back to the user.";

    // Context variable keys
    private const string AvailableFunctionsKey = "available_functions";
    private const string InitialPlanKey = "initial_plan";
    private const string GoalKey = "goal";

    private static StreamWriter s_logWriter;

    /// <summary>
    /// Initialize a new instance of the <see cref="FunctionCallingStepwisePlanner"/> class.
    /// </summary>
    /// <param name="config">The planner configuration.</param>
    public OptimisticPlanner(
        FunctionCallingStepwisePlannerConfig? config = null)
    {
        this.Config = config ?? new();
        this._generatePlanYaml = this.Config.GetPromptTemplate?.Invoke() ?? EmbeddedResource.Read("Stepwise.Optimistic.GeneratePlan.yaml");
        this._verifyPlanPrompt = this.Config.GetPromptTemplate?.Invoke() ?? EmbeddedResource.Read("Stepwise.Optimistic.VerifyPlan.yaml");
        this._stepPrompt = this.Config.GetStepPromptTemplate?.Invoke() ?? EmbeddedResource.Read("Stepwise.Optimistic.StepPrompt.txt");
        this._verifyStepPrompt = this.Config.GetStepPromptTemplate?.Invoke() ?? EmbeddedResource.Read("Stepwise.Optimistic.VerifyStepPrompt.yaml");
        this.Config.ExcludedPlugins.Add(StepwisePlannerPluginName);

        this._processingTokenSource = new CancellationTokenSource();
        this._verificationTokenSource = new CancellationTokenSource();

        var filePath = @"C:\Users\bentho\OneDrive - Microsoft\scratchpad\sk\Demo\demo1.txt";
        var fs = File.Create(filePath);
        s_logWriter = new StreamWriter(fs);
    }

    /// <summary>
    /// Execute a plan
    /// </summary>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="question">The question to answer</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>Result containing the model's response message and chat history.</returns>
    public async Task<FunctionCallingStepwisePlannerResult> ExecuteAsync(
        Kernel kernel,
        string question,
        CancellationToken cancellationToken = default)
    {
        var logger = kernel.LoggerFactory.CreateLogger(this.GetType()) ?? NullLogger.Instance;
        this.ProcessOperationTasks(this._processingTokenSource.Token);

        try
        {
            var result = await  PlannerInstrumentation.InvokePlanAsync(
                static (OptimisticPlanner plan, Kernel kernel, string? question, CancellationToken cancellationToken)
                    => plan.ExecuteCoreAsync(kernel, question!, cancellationToken),
                this, kernel, question, logger, cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            this._processingTokenSource.Cancel();
            this._verificationTokenSource.Cancel();
        }
    }

    private static void WriteLogLine(string message)
    {
        s_logWriter.WriteLine(message);
        s_logWriter.Flush();
    }

    private void ProcessOperationTasks(CancellationToken cancellationToken)
    {
#pragma warning disable VSTHRD110 // Observe result of async calls
        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (this._verifyTasks.Count != 0)
                {
                    Task<(string id, bool isValid)> finishedTask = await Task.WhenAny(this._verifyTasks).ConfigureAwait(false);
                    this._verifyTasks.Remove(finishedTask);
                    (string id, bool isValid) finishedVerifyTask = await finishedTask.ConfigureAwait(false);

                    // We ignore canceled tasks because they may be dure to rollbacks
                    if (!this._verificationTokenSource.IsCancellationRequested && !finishedVerifyTask.isValid)
                    {
                        // The operation verification failed, cancel all further operations and roll back.
                        // TODO: This should be smarter about how it chooses operation to cancel.
                        this._verificationTokenSource.Cancel();
                        OptimisticOperation failedOperation = this._operationsDict[finishedVerifyTask.id];
                        await failedOperation.Failed(failedOperation).ConfigureAwait(false);
                    }
                }
                await Task.Delay(100).ConfigureAwait(false);
            }

            int x = 3;
        }, cancellationToken);
#pragma warning restore VSTHRD110 // Observe result of async calls
    }

    private async Task<T> ExecuteOperationAsync<T>(Func<CancellationToken, Task<T>> function, Func<T, CancellationToken, Task<bool>> verification, Func<OptimisticOperation, Task> failed, int chatHistoryOffset, CancellationToken cancellationToken)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(this._verificationTokenSource.Token, cancellationToken);

        // store the operation in the graph
        var opId = Guid.NewGuid().ToString("n");
        var operation = new OptimisticOperation<T>
        {
            Id = opId,
            HistoryOffset = chatHistoryOffset,
            Function = function,
            Verification = verification,
            Failed = failed,
        };

        this._operationsDict.Add(opId, operation);
        //this._operations.AddLast(operation);

        // wait for the function to complete
        var operationResult = await function(cts.Token).ConfigureAwait(false);

        async Task<(string, bool)> WrapVerification(Task<bool> innerTask)
        {
            var result = await innerTask.ConfigureAwait(false);
            return (opId, result);
        }

        // kick off the verification in the background
        this._verifyTasks.Add(WrapVerification(verification(operationResult, cts.Token)));
        return operationResult;
    }

    /******************************* Generate and verify plan **********************************/

    // Create and invoke a kernel function to generate the initial plan
    private async Task<string> GeneratePlanAsync(string question, Kernel kernel, string functionsManual, ILogger logger, CancellationToken cancellationToken)
    {
        var generatePlanFunction = kernel.CreateFunctionFromPromptYaml(this._generatePlanYaml);
        var generatePlanArgs = new KernelArguments
        {
            [AvailableFunctionsKey] = functionsManual,
            [GoalKey] = question
        };

        WriteLogLine("Starting plan generation...");
        var generatePlanResult = await this.ExecuteOperationAsync(
            function: (ct) => kernel.InvokeAsync(generatePlanFunction, generatePlanArgs, ct),
            verification: (plan, ct) =>
            {
                WriteLogLine($"Retrieved plan: {plan}");
                var proposedPlan = plan.GetValue<string>() ?? "";
                return this.VerifyGeneratedPlanAsync(question, proposedPlan, kernel, functionsManual, logger, ct);
            },
            failed: (op) =>
            {
                return Task.Delay(1);
            }, 0, cancellationToken).ConfigureAwait(false);

        return generatePlanResult.GetValue<string>() ?? throw new KernelException("Failed get a completion for the plan.");
    }

    private async Task<bool> VerifyGeneratedPlanAsync(string question, string plan, Kernel kernel, string functionsManual, ILogger logger, CancellationToken cancellationToken)
    {
        WriteLogLine("Starting plan verification");
        var planVerificationFunction = kernel.CreateFunctionFromPromptYaml(this._verifyPlanPrompt);
        var planVerificationArgs = new KernelArguments
        {
            [AvailableFunctionsKey] = functionsManual,
            [GoalKey] = question,
            ["plan"] = plan
        };

        var planVerificationResult = await kernel.InvokeAsync(planVerificationFunction, planVerificationArgs, cancellationToken).ConfigureAwait(false);

        try
        {
            var parsedResult = JsonSerializer.Deserialize<VerifierResponse>(planVerificationResult.GetValue<string>()!)!;
            WriteLogLine($"Retrieved plan verification - IsValid: {parsedResult.IsValid}, Reason: {parsedResult.Reason}");
            return parsedResult.IsValid;
        }
        catch (Exception ex)
        {
            WriteLogLine($"Plan verification step failed to parse response. Assuming the plan is invalid: {ex}");
            return planVerificationResult.GetValue<bool?>() ?? throw new KernelException("Failed get a completion for the plan.");
        }
    }

    /********************************************************************************************/

    private Task<bool> VerifyFinalResultAsync(string question, string result, Kernel kernel, ILogger logger, CancellationToken cancellationToken, bool fail = false)
    {
        WriteLogLine("Starting final result verification");
        WriteLogLine("Retrieved final result verification: true");
        return Task.FromResult(true);
    }

    private async Task<bool> VerifyToolCallStepAsync(string question, string plan, string action, string result, Kernel kernel, ILogger logger, CancellationToken cancellationToken, bool fail = false)
    {
        WriteLogLine("Starting step verification");
        var stepVerificationFunction = kernel.CreateFunctionFromPromptYaml(this._verifyStepPrompt);
        var stepVerificationArgs = new KernelArguments
        {
            [GoalKey] = question,
            ["plan"] = plan,
            ["action"] = action,
            ["result"] = result
        };

        var stepVerificationResult = await kernel.InvokeAsync(stepVerificationFunction, stepVerificationArgs, cancellationToken).ConfigureAwait(false);

        // For testing purposes only
        if (fail)
        {
            WriteLogLine($"Retrieved step verification - IsValid: false, Reason: Testing");
            return false;
        }

        try
        {
            var parsedResult = JsonSerializer.Deserialize<VerifierResponse>(stepVerificationResult.GetValue<string>()!)!;
            WriteLogLine($"Retrieved step verification - IsValid: {parsedResult.IsValid}, Reason: {parsedResult.Reason}");
            return parsedResult.IsValid;
        }
        catch (Exception ex)
        {
            WriteLogLine($"Step verification step failed to parse response. Assuming the step is invalid: {ex}");
            return stepVerificationResult.GetValue<bool?>() ?? throw new KernelException("Failed get a completion for the step.");
        }
    }

    /********************************************************************************************/

    private async Task<FunctionCallingStepwisePlannerResult> ExecuteCoreAsync(
        Kernel kernel,
        string question,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNullOrWhiteSpace(question);
        Verify.NotNull(kernel);
        IChatCompletionService chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        ILoggerFactory loggerFactory = kernel.LoggerFactory;
        ILogger logger = loggerFactory.CreateLogger(this.GetType()) ?? NullLogger.Instance;
        var promptTemplateFactory = new KernelPromptTemplateFactory(loggerFactory);
        var stepExecutionSettings = this.Config.ExecutionSettings ?? new OpenAIPromptExecutionSettings();

        // Clone the kernel so that we can add planner-specific plugins without affecting the original kernel instance
        var clonedKernel = kernel.Clone();
        clonedKernel.ImportPluginFromType<UserInteraction>();

        // Create the function manual
        var functionsManual = await GetFunctionsManualAsync(kernel, logger, this.Config, cancellationToken).ConfigureAwait(false);

        // Create and invoke a kernel function to generate the initial plan
        var initialPlan = await this.GeneratePlanAsync(question, clonedKernel, functionsManual, logger, cancellationToken).ConfigureAwait(false);
        var chatHistoryForSteps = await this.BuildChatHistoryForStepAsync(question, initialPlan, clonedKernel, promptTemplateFactory, cancellationToken).ConfigureAwait(false);

        for (int i = 0; i < this.Config.MaxIterations; i++)
        {
            // sleep for a bit to avoid rate limiting
            if (i > 0)
            {
                await Task.Delay(this.Config.MinIterationTimeMs, cancellationToken).ConfigureAwait(false);
            }

            if (this._verificationTokenSource.IsCancellationRequested)
            {
                this._verificationTokenSource = new CancellationTokenSource();
            }

            // Run operation here, saving the length of the chat history so that I can rewind to that point if verification fails.
            ValidateTokenCountAsync(chatHistoryForSteps, functionsManual, this.Config.MaxPromptTokens);
            var stepResult = await this.ExecuteOperationAsync(
                function: (ct) =>
                {
                    return this.RunNextStepAsync(clonedKernel, chatHistoryForSteps, chatCompletion, stepExecutionSettings, i, ct);
                },
                verification: (sr, ct) =>
                {
                    Task<bool> verification = sr.ActionType switch
                    {
                        ActionType.Continue => Task.FromResult(true),
                        ActionType.ToolCall => this.VerifyToolCallStepAsync(question, initialPlan, sr.ToolCallInfo, sr.Result, clonedKernel, logger, ct, fail: i == 0),
                        ActionType.FinalResult => this.VerifyFinalResultAsync(question, sr.FinalResult!.FinalAnswer, clonedKernel, logger, ct),
                        _ => throw new NotImplementedException(),
                    };

                    return verification;
                },
                failed: (op) =>
                {
                    chatHistoryForSteps.RemoveRange(op.HistoryOffset, chatHistoryForSteps.Count - op.HistoryOffset);
                    return Task.CompletedTask;
                }, chatHistoryForSteps.Count,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (stepResult.ActionType == ActionType.FinalResult)
            {
                // Success! We found a final answer, so return the planner result.
                // But first... we need to ake sure all the pending verifications have finished
                await Task.WhenAll(this._verifyTasks).ConfigureAwait(false);

                if (this._verificationTokenSource.IsCancellationRequested)
                {
                    continue;
                }

                return stepResult.FinalResult!;
            }
        }

        // We've completed the max iterations, but the model hasn't returned a final answer.
        return new FunctionCallingStepwisePlannerResult
        {
            FinalAnswer = string.Empty,
            ChatHistory = chatHistoryForSteps,
            Iterations = this.Config.MaxIterations,
        };
    }

    private async Task<StepResult> RunNextStepAsync(Kernel kernel, ChatHistory chatHistory, IChatCompletionService chatCompletion, OpenAIPromptExecutionSettings executionSettings, int iteration, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new StepResult { ActionType = ActionType.Continue };
        }

        // For each step, request another completion to select a function for that step
        chatHistory.AddUserMessage(StepwiseUserMessage);
        var chatResult = await GetCompletionWithFunctionsAsync(chatHistory, kernel, chatCompletion, executionSettings, CancellationToken.None).ConfigureAwait(false);
        chatHistory.Add(chatResult);

        // Check for function response
        if (!TryGetFunctionResponse(chatResult, out IReadOnlyList<OpenAIFunctionToolCall>? functionResponses, out string? functionResponseError))
        {
            // No function response found. Either AI returned a chat message, or something went wrong when parsing the function.
            // Log the error (if applicable), then let the planner continue.
            if (functionResponseError is not null)
            {
                chatHistory.AddUserMessage(functionResponseError);
            }
            return new StepResult { ActionType = ActionType.Continue };
        }

        // Check for final answer in the function response
        foreach (OpenAIFunctionToolCall functionResponse in functionResponses)
        {
            if (TryFindFinalAnswer(functionResponse, out string finalAnswer, out string? finalAnswerError))
            {
                if (finalAnswerError is not null)
                {
                    // We found a final answer, but failed to parse it properly.
                    // Log the error message in chat history and let the planner try again.
                    chatHistory.AddUserMessage(finalAnswerError);
                    continue;
                }

                return new StepResult
                {
                    ActionType = ActionType.FinalResult,
                    FinalResult = new FunctionCallingStepwisePlannerResult
                    {
                        FinalAnswer = finalAnswer,
                        ChatHistory = chatHistory,
                        Iterations = iteration
                    }
                };
            }
        }

        // Look up function in kernel
        foreach (OpenAIFunctionToolCall functionResponse in functionResponses)
        {
            if (kernel.Plugins.TryGetFunctionAndArguments(functionResponse, out KernelFunction? pluginFunction, out KernelArguments? arguments))
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new StepResult { ActionType = ActionType.Continue };
                    }

                    // Execute function and add result to chat history
                    WriteLogLine("Starting step execution...");
                    var result = (await kernel.InvokeAsync(pluginFunction, arguments, CancellationToken.None).ConfigureAwait(false)).GetValue<object>();
                    string resultAsString = ParseObjectAsString(result);
                    chatHistory.AddMessage(AuthorRole.Tool, resultAsString, metadata: new Dictionary<string, object?>(1) { { OpenAIChatMessageContent.ToolIdProperty, functionResponse.Id } });

                    WriteLogLine($"Retrieved step result: {result}");
                    return new StepResult
                    {
                        ActionType = ActionType.ToolCall,
                        ToolCallInfo = $"FunctionCall: {functionResponse.FunctionName}({string.Join(",", functionResponse.Arguments?.Values)})",
                        Result = resultAsString
                    };
                }
                catch (KernelException)
                {
                    chatHistory.AddUserMessage($"Failed to execute function {functionResponse.FullyQualifiedName}. Try something else!");
                    return new StepResult { ActionType = ActionType.Continue };
                }
            }
            else
            {
                chatHistory.AddUserMessage($"Function {functionResponse.FullyQualifiedName} does not exist in the kernel. Try something else!");
                return new StepResult { ActionType = ActionType.Continue };
            }
        }

        return new StepResult { ActionType = ActionType.Continue };
    }

    private static async Task<ChatMessageContent> GetCompletionWithFunctionsAsync(
        ChatHistory chatHistory,
        Kernel kernel,
        IChatCompletionService chatCompletion,
        OpenAIPromptExecutionSettings openAIExecutionSettings,
        CancellationToken cancellationToken)
    {
        openAIExecutionSettings.ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions;
        return await chatCompletion.GetChatMessageContentAsync(chatHistory, openAIExecutionSettings, kernel, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> GetFunctionsManualAsync(Kernel kernel, ILogger logger, FunctionCallingStepwisePlannerConfig config, CancellationToken cancellationToken)
    {
        return await kernel.Plugins.GetJsonSchemaFunctionsManualAsync(config, null, logger, false, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ChatHistory> BuildChatHistoryForStepAsync(
        string goal,
        string initialPlan,
        Kernel kernel,
        KernelPromptTemplateFactory promptTemplateFactory,
        CancellationToken cancellationToken)
    {
        var chatHistory = new ChatHistory();

        // Add system message with context about the initial goal/plan
        var arguments = new KernelArguments
        {
            [GoalKey] = goal,
            [InitialPlanKey] = initialPlan
        };
        var systemMessage = await promptTemplateFactory.Create(new PromptTemplateConfig(this._stepPrompt)).RenderAsync(kernel, arguments, cancellationToken).ConfigureAwait(false);

        chatHistory.AddSystemMessage(systemMessage);

        return chatHistory;
    }

    private static bool TryGetFunctionResponse(ChatMessageContent chatMessage, [NotNullWhen(true)] out IReadOnlyList<OpenAIFunctionToolCall>? functionResponses, out string? errorMessage)
    {
        OpenAIChatMessageContent? openAiChatMessage = chatMessage as OpenAIChatMessageContent;
        Verify.NotNull(openAiChatMessage, nameof(openAiChatMessage));

        functionResponses = null;
        errorMessage = null;
        try
        {
            functionResponses = openAiChatMessage.GetOpenAIFunctionToolCalls();
        }
        catch (JsonException)
        {
            errorMessage = "That function call is invalid. Try something else!";
        }

        return functionResponses is { Count: > 0 };
    }

    private static bool TryFindFinalAnswer(OpenAIFunctionToolCall functionResponse, out string finalAnswer, out string? errorMessage)
    {
        finalAnswer = string.Empty;
        errorMessage = null;

        if (functionResponse.PluginName == "UserInteraction" && functionResponse.FunctionName == "SendFinalAnswer")
        {
            if (functionResponse.Arguments is { Count: > 0 } arguments && arguments.TryGetValue("answer", out object? valueObj))
            {
                finalAnswer = ParseObjectAsString(valueObj);
            }
            else
            {
                errorMessage = "Returned answer in incorrect format. Try again!";
            }
            return true;
        }
        return false;
    }

    private static string ParseObjectAsString(object? valueObj)
    {
        string resultStr = string.Empty;

        if (valueObj is RestApiOperationResponse apiResponse)
        {
            resultStr = apiResponse.Content as string ?? string.Empty;
        }
        else if (valueObj is string valueStr)
        {
            resultStr = valueStr;
        }
        else if (valueObj is JsonElement valueElement)
        {
            if (valueElement.ValueKind == JsonValueKind.String)
            {
                resultStr = valueElement.GetString() ?? "";
            }
            else
            {
                resultStr = valueElement.ToJsonString();
            }
        }
        else
        {
            resultStr = JsonSerializer.Serialize(valueObj);
        }

        return resultStr;
    }

    private static void ValidateTokenCountAsync(
        ChatHistory chatHistory,
        string functionsManual,
        int maxPromptTokens)
    {
        var tokenCount = chatHistory.GetTokenCount(additionalMessage: functionsManual);
        if (tokenCount >= maxPromptTokens)
        {
            throw new KernelException("ChatHistory is too long to get a completion. Try reducing the available functions.");
        }
    }
}
