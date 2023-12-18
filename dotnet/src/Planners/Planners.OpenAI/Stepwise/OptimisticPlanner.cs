// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using static Microsoft.SemanticKernel.Planning.FunctionCallingStepwisePlanner;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Json.More;
using System;

namespace Microsoft.SemanticKernel.Planning.Stepwise;

internal class OptimisticOperation
{
    public string Id { get; set; } = "";
    public Func<Task<FunctionResult>> Function {  get; set; } = () => throw new NotImplementedException();
    public Func<FunctionResult, CancellationToken, Task<bool>> Verification {  get; set; } = (_, _) => throw new NotImplementedException();
} 

public class OptimisticPlanner
{
    /// <summary>
    /// The configuration for the StepwisePlanner
    /// </summary>
    private FunctionCallingStepwisePlannerConfig Config { get; }

    private readonly LinkedList<OptimisticOperation> _operations = new();
    private readonly List<Task<(string id, bool isValid)>> _verifyTasks = new();
    private readonly CancellationTokenSource _processingTokenSource;
    private readonly CancellationTokenSource _verificationTokenSource;

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
        this._verifyStepPrompt = this.Config.GetStepPromptTemplate?.Invoke() ?? EmbeddedResource.Read("Stepwise.Optimistic.VerifyStepPrompt.txt");
        this.Config.ExcludedPlugins.Add(StepwisePlannerPluginName);

        this._processingTokenSource = new CancellationTokenSource();
        this._verificationTokenSource = new CancellationTokenSource();
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

                    // We ignore canceled tasks because they may be dure to rollbacks
                    if (!this._verificationTokenSource.IsCancellationRequested && !(await finishedTask.ConfigureAwait(false)).isValid)
                    {
                        // The operation verification failed, cancel all further operations and roll back.
                        // TODO: This should be smarter about how it chooses operation to cancel.
                        this._verificationTokenSource.Cancel();
                    }
                }
                await Task.Delay(100).ConfigureAwait(false);
            }

            int x = 3;
        });
#pragma warning restore VSTHRD110 // Observe result of async calls
    }

    private async Task<FunctionResult> ExecuteOperationAsync(Func<Task<FunctionResult>> function, Func<FunctionResult, CancellationToken, Task<bool>> verification, CancellationToken cancellationToken)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(this._verificationTokenSource.Token, cancellationToken);

        // store the operation in the graph
        var opId = Guid.NewGuid().ToString("n");
        var operation = new OptimisticOperation
        {
            Id = opId,
            Function = function,
            Verification = verification
        };

        this._operations.AddLast(operation);

        // wait for the function to complete
        var operationResult = await function().ConfigureAwait(false);

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
    private async Task<string> GeneratePlanAsync(string question, Kernel kernel, ILogger logger, CancellationToken cancellationToken)
    {
        var generatePlanFunction = kernel.CreateFunctionFromPromptYaml(this._generatePlanYaml);
        string functionsManual = await this.GetFunctionsManualAsync(kernel, logger, cancellationToken).ConfigureAwait(false);
        var generatePlanArgs = new KernelArguments
        {
            [AvailableFunctionsKey] = functionsManual,
            [GoalKey] = question
        };

        var generatePlanResult = await this.ExecuteOperationAsync(
            function: () => kernel.InvokeAsync(generatePlanFunction, generatePlanArgs, cancellationToken),
            verification: (plan, ct) =>
            {
                var proposedPlan = plan.GetValue<string>() ?? "";
                return this.VerifyGeneratedPlanAsync(question, proposedPlan, kernel, logger, ct);
            }, cancellationToken).ConfigureAwait(false);

        return generatePlanResult.GetValue<string>() ?? throw new KernelException("Failed get a completion for the plan.");
    }

    private async Task<bool> VerifyGeneratedPlanAsync(string question, string plan, Kernel kernel, ILogger logger, CancellationToken cancellationToken)
    {
        var generatePlanFunction = kernel.CreateFunctionFromPromptYaml(this._verifyPlanPrompt);
        string functionsManual = await this.GetFunctionsManualAsync(kernel, logger, cancellationToken).ConfigureAwait(false);
        var generatePlanArgs = new KernelArguments
        {
            [AvailableFunctionsKey] = functionsManual,
            [GoalKey] = question,
            ["plan"] = plan
        };
        var generatePlanResult = await kernel.InvokeAsync(generatePlanFunction, generatePlanArgs, cancellationToken).ConfigureAwait(false);
        return generatePlanResult.GetValue<bool?>() ?? throw new KernelException("Failed get a completion for the plan.");
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

        // Create and invoke a kernel function to generate the initial plan
        var initialPlan = await this.GeneratePlanAsync(question, clonedKernel, logger, cancellationToken).ConfigureAwait(false);


        var chatHistoryForSteps = await this.BuildChatHistoryForStepAsync(question, initialPlan, clonedKernel, promptTemplateFactory, cancellationToken).ConfigureAwait(false);

        for (int i = 0; i < this.Config.MaxIterations; i++)
        {
            // sleep for a bit to avoid rate limiting
            if (i > 0)
            {
                await Task.Delay(this.Config.MinIterationTimeMs, cancellationToken).ConfigureAwait(false);
            }

            // For each step, request another completion to select a function for that step
            chatHistoryForSteps.AddUserMessage(StepwiseUserMessage);
            var chatResult = await this.GetCompletionWithFunctionsAsync(chatHistoryForSteps, clonedKernel, chatCompletion, stepExecutionSettings, logger, cancellationToken).ConfigureAwait(false);
            chatHistoryForSteps.Add(chatResult);

            // Check for function response
            if (!this.TryGetFunctionResponse(chatResult, out IReadOnlyList<OpenAIFunctionToolCall>? functionResponses, out string? functionResponseError))
            {
                // No function response found. Either AI returned a chat message, or something went wrong when parsing the function.
                // Log the error (if applicable), then let the planner continue.
                if (functionResponseError is not null)
                {
                    chatHistoryForSteps.AddUserMessage(functionResponseError);
                }
                continue;
            }

            // Check for final answer in the function response
            foreach (OpenAIFunctionToolCall functionResponse in functionResponses)
            {
                if (this.TryFindFinalAnswer(functionResponse, out string finalAnswer, out string? finalAnswerError))
                {
                    if (finalAnswerError is not null)
                    {
                        // We found a final answer, but failed to parse it properly.
                        // Log the error message in chat history and let the planner try again.
                        chatHistoryForSteps.AddUserMessage(finalAnswerError);
                        continue;
                    }

                    // Success! We found a final answer, so return the planner result.
                    return new FunctionCallingStepwisePlannerResult
                    {
                        FinalAnswer = finalAnswer,
                        ChatHistory = chatHistoryForSteps,
                        Iterations = i + 1,
                    };
                }
            }

            // Look up function in kernel
            foreach (OpenAIFunctionToolCall functionResponse in functionResponses)
            {
                if (clonedKernel.Plugins.TryGetFunctionAndArguments(functionResponse, out KernelFunction? pluginFunction, out KernelArguments? arguments))
                {
                    try
                    {
                        // Execute function and add to result to chat history
                        var result = (await clonedKernel.InvokeAsync(pluginFunction, arguments, cancellationToken).ConfigureAwait(false)).GetValue<object>();
                        chatHistoryForSteps.AddMessage(AuthorRole.Tool, ParseObjectAsString(result), metadata: new Dictionary<string, object?>(1) { { OpenAIChatMessageContent.ToolIdProperty, functionResponse.Id } });
                    }
                    catch (KernelException)
                    {
                        chatHistoryForSteps.AddUserMessage($"Failed to execute function {functionResponse.FullyQualifiedName}. Try something else!");
                    }
                }
                else
                {
                    chatHistoryForSteps.AddUserMessage($"Function {functionResponse.FullyQualifiedName} does not exist in the kernel. Try something else!");
                }
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

    private async Task<ChatMessageContent> GetCompletionWithFunctionsAsync(
        ChatHistory chatHistory,
        Kernel kernel,
        IChatCompletionService chatCompletion,
        OpenAIPromptExecutionSettings openAIExecutionSettings,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        openAIExecutionSettings.ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions;

        await this.ValidateTokenCountAsync(chatHistory, kernel, logger, openAIExecutionSettings, cancellationToken).ConfigureAwait(false);
        return await chatCompletion.GetChatMessageContentAsync(chatHistory, openAIExecutionSettings, kernel, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetFunctionsManualAsync(Kernel kernel, ILogger logger, CancellationToken cancellationToken)
    {
        return await kernel.Plugins.GetJsonSchemaFunctionsManualAsync(this.Config, null, logger, false, cancellationToken).ConfigureAwait(false);
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

    private bool TryGetFunctionResponse(ChatMessageContent chatMessage, [NotNullWhen(true)] out IReadOnlyList<OpenAIFunctionToolCall>? functionResponses, out string? errorMessage)
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

    private bool TryFindFinalAnswer(OpenAIFunctionToolCall functionResponse, out string finalAnswer, out string? errorMessage)
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

    private async Task ValidateTokenCountAsync(
        ChatHistory chatHistory,
        Kernel kernel,
        ILogger logger,
        OpenAIPromptExecutionSettings openAIExecutionSettings,
        CancellationToken cancellationToken)
    {
        string functionManual = string.Empty;

        // If using functions, get the functions manual to include in token count estimate
        if (openAIExecutionSettings.ToolCallBehavior == ToolCallBehavior.EnableKernelFunctions)
        {
            functionManual = await this.GetFunctionsManualAsync(kernel, logger, cancellationToken).ConfigureAwait(false);
        }

        var tokenCount = chatHistory.GetTokenCount(additionalMessage: functionManual);
        if (tokenCount >= this.Config.MaxPromptTokens)
        {
            throw new KernelException("ChatHistory is too long to get a completion. Try reducing the available functions.");
        }
    }
}
