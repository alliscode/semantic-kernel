// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;
using Microsoft.SemanticKernel.Process.Workflows.Actions;
using Microsoft.SemanticKernel.Process.Workflows.PowerFx;

namespace Microsoft.SemanticKernel.Process.Workflows;

internal sealed class ProcessActionVisitor : DialogActionVisitor
{
    private readonly ProcessBuilder _processBuilder;
    private readonly ProcessStepBuilder _unhandledErrorStep;
    private readonly ProcessActionEnvironment _environment;
    private readonly Dictionary<ActionId, ProcessAction> _actions;

    public ProcessActionVisitor(ProcessBuilder processBuilder, ProcessActionStepContext currentContext, ProcessActionEnvironment environment)
    {
        this._actions = [];
        this._processBuilder = processBuilder;
        this._environment = environment;
        this.CurrentContext = currentContext;
        this._unhandledErrorStep =
            processBuilder.AddStep(
                "unhandled_error",
                (kernel, context) =>
                {
                    // Handle unhandled errors here
                    Console.WriteLine("*** PROCESS ERROR - Unhandled error"); // %%% DEVTRACE
                    return Task.CompletedTask;
                });
    }

    public void Complete()
    {
        Console.WriteLine("> COMPLETE"); // %%% DEVTRACE
        var finalContext = this.MoveToNewContext("final");
        finalContext.EdgeBuilder.StopProcess();
    }

    private ProcessActionStepContext CurrentContext { get; set; }

    protected override void Visit(ActionScope item)
    {
        Trace(item, isSkipped: false);

        this.MoveToNewContext(item.Id.Value);
    }

    protected override void Visit(ConditionGroup item)
    {
        Trace(item, isSkipped: false);

        foreach (var condition in item.Conditions)
        {
            // Visit each action in the condition group
            condition.Accept(this);
        }
    }

    protected override void Visit(GotoAction item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new GotoActionAction(item));
    }

    protected override void Visit(EndConversation item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new EndConversationAction(item));
    }

    protected override void Visit(BeginDialog item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new BeginDialogAction(item));
    }

    protected override void Visit(UnknownDialogAction item)
    {
        Trace(item);
    }

    protected override void Visit(EndDialog item)
    {
        Trace(item);
    }

    protected override void Visit(AnswerQuestionWithAI item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new AnswerQuestionWithAIAction(item));
    }

    protected override void Visit(SetVariable item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new SetVariableAction(item));
    }

    protected override void Visit(SetTextVariable item)
    {
        Trace(item);
    }

    protected override void Visit(EditTable item)
    {
        Trace(item);
    }

    protected override void Visit(EditTableV2 item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new EditTableV2Action(item));
    }

    protected override void Visit(ParseValue item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new ParseValueAction(item));
    }

    protected override void Visit(SendActivity item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new SendActivityAction(item, this._environment));
    }

    protected override void Visit(GetActivityMembers item)
    {
        Trace(item);
    }

    protected override void Visit(UpdateActivity item)
    {
        Trace(item);
    }

    protected override void Visit(DeleteActivity item)
    {
        Trace(item);
    }

    protected override void Visit(InvokeFlowAction item)
    {
        Trace(item);
    }

    protected override void Visit(InvokeAIBuilderModelAction item)
    {
        Trace(item);
    }

    protected override void Visit(WaitForConnectorTrigger item)
    {
        Trace(item);
    }

    protected override void Visit(InvokeConnectorAction item)
    {
        Trace(item);
    }

    protected override void Visit(InvokeSkillAction item)
    {
        Trace(item);
    }

    protected override void Visit(AdaptiveCardPrompt item)
    {
        Trace(item);
    }

    protected override void Visit(Question item)
    {
        Trace(item);
    }

    protected override void Visit(CSATQuestion item)
    {
        Trace(item);
    }

    protected override void Visit(OAuthInput item)
    {
        Trace(item);
    }

    protected override void Visit(Foreach item)
    {
        Trace(item);
    }

    protected override void Visit(RepeatDialog item)
    {
        Trace(item);
    }

    protected override void Visit(ActivateExternalTrigger item)
    {
        Trace(item);
    }

    protected override void Visit(DisableTrigger item)
    {
        Trace(item);
    }

    protected override void Visit(ReplaceDialog item)
    {
        Trace(item);
    }

    protected override void Visit(CancelAllDialogs item)
    {
        Trace(item);
    }

    protected override void Visit(CancelDialog item)
    {
        Trace(item);
    }

    protected override void Visit(ClearAllVariables item)
    {
        Trace(item);
    }

    protected override void Visit(BreakLoop item)
    {
        Trace(item);
    }

    protected override void Visit(ContinueLoop item)
    {
        Trace(item);
    }

    protected override void Visit(ResetVariable item)
    {
        Trace(item);
    }

    protected override void Visit(EmitEvent item)
    {
        Trace(item);
    }

    protected override void Visit(GetConversationMembers item)
    {
        Trace(item);
    }

    protected override void Visit(HttpRequestAction item)
    {
        Trace(item);
    }

    protected override void Visit(RecognizeIntent item)
    {
        Trace(item);
    }

    protected override void Visit(TransferConversation item)
    {
        Trace(item);
    }

    protected override void Visit(TransferConversationV2 item)
    {
        Trace(item);
    }

    protected override void Visit(SignOutUser item)
    {
        Trace(item);
    }

    protected override void Visit(LogCustomTelemetryEvent item)
    {
        Trace(item);
    }

    protected override void Visit(DisconnectedNodeContainer item)
    {
        Trace(item);
    }

    protected override void Visit(CreateSearchQuery item)
    {
        Trace(item);
    }

    protected override void Visit(SearchKnowledgeSources item)
    {
        Trace(item);
    }

    protected override void Visit(SearchAndSummarizeWithCustomModel item)
    {
        Trace(item);
    }

    protected override void Visit(SearchAndSummarizeContent item)
    {
        Trace(item);
    }

    protected override void Visit(InvokeCustomModelAction item)
    {
        Trace(item);
    }

    private void AddAction(ProcessAction? action)
    {
        if (action is not null)
        {
            // Add the action to the existing context
            this.CurrentContext.Actions.Add(action);
            this._actions[action.Id] = action;
        }
    }

    private ProcessActionStepContext MoveToNewContext(string contextId)
    {
        ProcessStepBuilder step = this.CreateActionStep(this.CurrentContext, contextId);
        return this.CreateNewContext(contextId, step);
    }

    // This implementation accepts the context as a parameter in order to pin the context closure.
    // The step cannot reference this.CurrentContext directly, as this will always be the final context.
    private ProcessStepBuilder CreateActionStep(ProcessActionStepContext currentContext, string contextId)
    {
        // Creating a step to execute all the current actions
        return
            this._processBuilder.AddStep(
                currentContext.Id,
                async (kernel, context) =>
                {
                    try
                    {
                        Console.WriteLine($"!!! STEP [{currentContext.Id}]"); // %%% DEVTRACE
                        if (currentContext.Actions.Count > 0)
                        {
                            // %%% ACTIONS (N) => ACTION (1)
                            RecalcEngine engine = RecalcEngineFactory.Create(this._environment.MaximumExpressionLength);
                            await engine.ExecuteActionsAsync(context, currentContext.Actions, kernel, cancellationToken: default).ConfigureAwait(false);
                        }
                    }
                    catch (ProcessActionException)
                    {
                        Console.WriteLine($"*** STEP [{currentContext.Id}] ERROR - Action failure"); // %%% DEVTRACE
                        throw;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"*** STEP [{currentContext.Id}] ERROR - {exception.GetType().Name}\n{exception.Message}"); // %%% DEVTRACE
                        throw;
                    }
                });
    }

    private ProcessActionStepContext CreateNewContext(string contextId, ProcessStepBuilder newStep)
    {
        // IN: Target the given step when the previous step ends
        this.CurrentContext.EdgeBuilder.SendEventTo(new ProcessFunctionTargetBuilder(newStep));

        // ERROR: Capture unhandled errors for the given step
        newStep.OnFunctionError("Invoke").SendEventTo(new ProcessFunctionTargetBuilder(this._unhandledErrorStep));

        // NEW: Define a context for the subsequent step
        return
            this.CurrentContext =
                new ProcessActionStepContext(contextId)
                {
                    // OUT: Capture function result handler for susequent step
                    EdgeBuilder = newStep.OnFunctionResult("Invoke")
                };
    }

    private static void Trace(DialogAction item, bool isSkipped = true)
    {
        Console.WriteLine($"> {(isSkipped ? "EMPTY" : "VISIT")} - {item.GetType().Name} [{item.Id.Value}]"); // %%% DEVTRACE
    }
}
