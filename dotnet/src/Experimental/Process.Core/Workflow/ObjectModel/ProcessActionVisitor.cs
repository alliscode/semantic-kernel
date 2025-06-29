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
    private readonly Dictionary<ActionId, ProcessStepBuilder> _steps;
    private readonly Stack<ProcessActionStepContext> _contextStack;
    private readonly List<(ActionId TargetId, ProcessStepEdgeBuilder SourceEdge)> _linkCache;

    public ProcessActionVisitor(ProcessBuilder processBuilder, ProcessActionEnvironment environment, ProcessStepBuilder sourceStep)
    {
        ProcessActionStepContext rootContext =
            new()
            {
                EdgeBuilder = sourceStep.OnFunctionResult("Invoke") // %%% DRY
            };
        this._contextStack = [];
        this._contextStack.Push(rootContext);
        this._steps = [];
        this._linkCache = [];
        this._processBuilder = processBuilder;
        this._environment = environment;
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
        // Close the current context
        this.CurrentContext.EdgeBuilder.StopProcess();

        // Process the cached links
        foreach ((ActionId targetId, ProcessStepEdgeBuilder sourceEdge) in this._linkCache)
        {
            // Link the queued context to the step
            ProcessStepBuilder step = this._steps[targetId]; // %%% TRY
            Console.WriteLine($"> CONNECTING {sourceEdge.Source.StepId} => {targetId}");
            sourceEdge.SendEventTo(new ProcessFunctionTargetBuilder(step));
        }
        this._linkCache.Clear();

        // Visitor is complete, all actions have been processed
        Console.WriteLine("> COMPLETE"); // %%% DEVTRACE
    }

    private ProcessActionStepContext CurrentContext => this._contextStack.Peek();

    protected override void Visit(ActionScope item)
    {
        Trace(item, isSkipped: false);

        this.AddContainer(item.Id.Value);
    }

    protected override void Visit(ConditionGroup item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new ConditionGroupAction(item));

        // Visit each action in the condition group
        int index = 1;
        foreach (ConditionItem condition in item.Conditions)
        {
            ProcessStepBuilder step = this.CreateContainerStep(this.CurrentContext, condition.Id ?? $"{item.Id.Value}_item{index}");
            this._contextStack.Push(
                new ProcessActionStepContext()
                {
                    EdgeBuilder = step.OnFunctionResult("Invoke") // %%% DRY
                });

            condition.Accept(this);

            this._contextStack.Pop();
            ++index;
        }
    }

    protected override void Visit(GotoAction item)
    {
        Trace(item, isSkipped: false);

        this.AddContainer(item.Id.Value);
        // Store the link for processing after all actions have steps.
        this._linkCache.Add((item.ActionId, this.CurrentContext.EdgeBuilder));
        // Create an orphaned context for continuity
        this.AddDead(item.Id.Value);
    }

    protected override void Visit(EndConversation item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new EndConversationAction(item));
        // Stop the process, this is a terminal action
        this.CurrentContext.EdgeBuilder.StopProcess();
        // Create an orphaned context for continuity
        this.AddDead(item.Id.Value);
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

    #region Not implemented

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

    #endregion

    private void AddAction(ProcessAction? action)
    {
        if (action is not null)
        {
            // Add the action to the existing context
            ProcessStepBuilder step = this.CreateActionStep(this.CurrentContext, action);
            this._steps[action.Id] = step;
            this.ContinueWith(step);
        }
    }

    private void AddContainer(string contextId)
    {
        ProcessStepBuilder step = this.CreateContainerStep(this.CurrentContext, contextId);
        this.ContinueWith(step);
    }

    private void AddDead(string contextId)
    {
        ProcessStepBuilder step = this.CreateContainerStep(this.CurrentContext, $"dead_{contextId}");
        this.CurrentContext.EdgeBuilder = step.OnFunctionResult("Invoke"); // %%% DRY
    }

    private ProcessStepBuilder CreateContainerStep(ProcessActionStepContext currentContext, string contextId)
    {
        return
            this._processBuilder.AddStep(
                contextId,
                (kernel, context) =>
                {
                    Console.WriteLine($"!!! STEP [{contextId}]"); // %%% DEVTRACE
                    return Task.CompletedTask;
                });
    }

    // This implementation accepts the context as a parameter in order to pin the context closure.
    // The step cannot reference this.CurrentContext directly, as this will always be the final context.
    private ProcessStepBuilder CreateActionStep(ProcessActionStepContext currentContext, ProcessAction action)
    {
        return
            this._processBuilder.AddStep(
                action.Id.Value,
                async (kernel, context) =>
                {
                    try
                    {
                        Console.WriteLine($"!!! STEP [{action.Id}]"); // %%% DEVTRACE
                        RecalcEngine engine = RecalcEngineFactory.Create(this._environment.MaximumExpressionLength);
                        await engine.ExecuteActionsAsync(context, [action], kernel, cancellationToken: default).ConfigureAwait(false);
                    }
                    catch (ProcessActionException)
                    {
                        Console.WriteLine($"*** STEP [{action.Id}] ERROR - Action failure"); // %%% DEVTRACE
                        throw;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"*** STEP [{action.Id}] ERROR - {exception.GetType().Name}\n{exception.Message}"); // %%% DEVTRACE
                        throw;
                    }
                });
    }

    private void ContinueWith(ProcessStepBuilder newStep)
    {
        // ERROR: Capture unhandled errors for the given step
        newStep.OnFunctionError("Invoke").SendEventTo(new ProcessFunctionTargetBuilder(this._unhandledErrorStep));

        // IN: Target the given step when the previous step ends
        this.CurrentContext.EdgeBuilder.SendEventTo(new ProcessFunctionTargetBuilder(newStep));

        // OUT: Capture function result handler for susequent step
        this.CurrentContext.EdgeBuilder = newStep.OnFunctionResult("Invoke");
    }

    private static void Trace(DialogAction item, bool isSkipped = true)
    {
        Console.WriteLine($"> {(isSkipped ? "EMPTY" : "VISIT")} - {item.GetType().Name} [{item.Id.Value}]"); // %%% DEVTRACE
    }
}
