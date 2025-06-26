// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel;

internal sealed class ProcessActionVisitor : DialogActionVisitor
{
    private readonly ProcessBuilder _processBuilder;

    public ProcessActionVisitor(RecalcEngine engine, ProcessBuilder processBuilder, ProcessActionStepContext currentContext)
    {
        this._processBuilder = processBuilder;
        this.CurrentContext = currentContext;
    }

    private ProcessActionStepContext CurrentContext { get; set; }

    private void MoveToNewContext(string contextId)
    {
        var actions = this.CurrentContext.Actions;

        // 1. Close the current context by creating a step to execute all the current actions
        var stepBuilder = this._processBuilder.AddStep(contextId, async (kernel, context) =>
        {
            await ExecuteActionsAsync(kernel, context, actions).ConfigureAwait(false);
        });

        // 2. When the current step ends, activate the next step
        this.CurrentContext.EdgeBuilder.SendEventTo(new ProcessFunctionTargetBuilder(stepBuilder));

        // 2. Move to the new step context
        this.CurrentContext = new ProcessActionStepContext
        {
            EdgeBuilder = stepBuilder.OnFunctionResult("Invoke")
        };
    }

    private static async Task ExecuteActionsAsync(Kernel kernel, KernelProcessStepContext context, List<Func<Kernel, KernelProcessStepContext, RecalcEngine, ProcessActionScopes, Task>> actions)
    {
        var scopes = await context.GetUserStateAsync<ProcessActionScopes>("scopes").ConfigureAwait(false);
        var record = BuildRecord(scopes[ActionScopeTypes.Topic]);

        RecalcEngine engine = EngineFactory.CreateDefault();
        engine.UpdateVariable(ActionScopeTypes.Topic, record);

        foreach (var action in actions)
        {
            // Execute each action in the current context
            await action.Invoke(kernel, context, engine, scopes).ConfigureAwait(false);
        }
    }

    private static void SetScopedVariable(RecalcEngine engine, ProcessActionScopes scopes, string? scopeName, string? varName, FormulaValue value)
    {
        if (scopeName is null)
        {
            throw new InvalidOperationException("Scope name cannot be null.");
        }

        if (varName is null)
        {
            throw new InvalidOperationException("Variable name cannot be null.");
        }

        if (!scopes.TryGetValue(scopeName, out var scope))
        {
            throw new InvalidOperationException("Unknown scope: " + scopeName);
        }

        scope[varName] = value;

        // Rebuild scope record and update engine
        var scopeRecord = BuildRecord(scope);
        engine.UpdateVariable(scopeName, scopeRecord);
    }

    private static RecordValue BuildRecord(ProcessActionScope fields)
    {
        var recordType = RecordType.Empty();
        foreach (var kvp in fields)
        {
            recordType = recordType.Add(kvp.Key, kvp.Value.Type);
        }

        return FormulaValue.NewRecordFromFields(recordType,
            [.. fields.Select(kvp => new NamedValue(kvp.Key, kvp.Value))]);
    }

    protected override void Visit(UnknownDialogAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(InvokeFlowAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(InvokeAIBuilderModelAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(WaitForConnectorTrigger item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(InvokeConnectorAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(InvokeSkillAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(AdaptiveCardPrompt item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(Question item)
    {
        Console.WriteLine(item);

        this.MoveToNewContext(item.Id.Value);
    }

    protected override void Visit(CSATQuestion item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(OAuthInput item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ActionScope item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(Foreach item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(BeginDialog item)
    {
        this.MoveToNewContext(item.Id.Value);
    }

    protected override void Visit(RepeatDialog item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ActivateExternalTrigger item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(DisableTrigger item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ReplaceDialog item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(CancelAllDialogs item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(CancelDialog item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ClearAllVariables item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(BreakLoop item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ContinueLoop item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ResetVariable item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(EditTable item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(EditTableV2 item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(EmitEvent item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(EndDialog item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(GetActivityMembers item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(GetConversationMembers item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(GotoAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(HttpRequestAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ConditionGroup item)
    {
        Console.WriteLine(item);
        foreach (var conditions in item.Conditions)
        {
            // Visit each action in the condition group
            conditions.Accept(this);
        }
    }

    protected override void Visit(EndConversation item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(RecognizeIntent item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SendActivity item)
    {
        Console.WriteLine(item);
        // TODO: This maps to sending output to the user.
    }

    protected override void Visit(TransferConversation item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(TransferConversationV2 item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SetVariable item)
    {
        Console.WriteLine($"Adding Step from {item.GetType().Name} node with Id {item.Id.Value}");

        // Create a new step for the SetVariable action
        this.MoveToNewContext(item.Id.Value);

        if (item.Value is not null && item.Value.IsExpression)
        {
            if (item.Variable?.Path is null)
            {
                throw new KernelException("SetVariable action must have a variable path defined.");
            }

            var expression = item.Value.ExpressionText;
            if (expression.StartsWith("=", StringComparison.OrdinalIgnoreCase))
            {
                expression = expression.Substring(1);
            }

            this.CurrentContext.Actions.Add(
                (kernel, context, engine, scopes) =>
                {
                    FormulaValue result;
                    try
                    {
                        result = engine.Eval(expression);
                    }
                    catch (Exception)
                    {
                        throw; // %%% TODO: WRAP AND RETHROW (OR REMOVE HANDLER)
                    }

                    if (result is ErrorValue errorVal)
                    {
                        throw new InvalidOperationException("PowerFX error: " + errorVal.Errors[0].Message);
                    }

                    SetScopedVariable(engine, scopes, item.Variable.Path.VariableScopeName, item.Variable.Path.VariableName, result);

                    return Task.CompletedTask;
                });
        }
    }

    protected override void Visit(SetTextVariable item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ParseValue item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SignOutUser item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(LogCustomTelemetryEvent item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(DeleteActivity item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(UpdateActivity item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(DisconnectedNodeContainer item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(CreateSearchQuery item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SearchKnowledgeSources item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SearchAndSummarizeWithCustomModel item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SearchAndSummarizeContent item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(AnswerQuestionWithAI item)
    {
        if (item.UserInput is null || string.IsNullOrWhiteSpace(item.UserInput.ExpressionText))
        {
            throw new InvalidOperationException("UserInput and ExpressionText must be defined for AnswerQuestionWithAI action.");
        }

        if (item.Variable?.Path is null)
        {
            throw new KernelException("SetVariable action must have a variable path defined.");
        }

        // Close the current context before adding the new action
        this.MoveToNewContext(item.Id.Value);

        this.CurrentContext.Actions.Add(async (kernel, context, engine, scopes) =>
        {
            var chatCompletion = kernel.Services.GetRequiredService<IChatCompletionService>() ?? throw new InvalidOperationException("ChatCompletionService is not registered in the service collection.");
            var expressionResult = engine.Eval(item.UserInput.ExpressionText);
            if (expressionResult is not StringValue str)
            {
                throw new InvalidOperationException("UserInput expression must evaluate to a string.");
            }

            ChatHistory history = new();
            history.AddUserMessage(str.Value);
            var response = await chatCompletion.GetChatMessageContentsAsync(history).ConfigureAwait(false);
            var value = FormulaValue.New(response.ToString());

            SetScopedVariable(engine, scopes, item.Variable.Path.VariableScopeName, item.Variable.Path.VariableName, value);
        });
    }

    protected override void Visit(InvokeCustomModelAction item)
    {
        Console.WriteLine(item);
    }
}
