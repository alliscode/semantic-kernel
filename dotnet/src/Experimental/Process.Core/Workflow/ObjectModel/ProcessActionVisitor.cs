// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;
using Microsoft.SemanticKernel.Process.Workflows.Actions;

namespace Microsoft.SemanticKernel.Process.Workflows;

internal sealed class ProcessActionVisitor : DialogActionVisitor
{
    private readonly ProcessBuilder _processBuilder;
    private readonly ProcessStepBuilder _unhandledErrorStep;
    private readonly ProcessActionEnvironment _environment;

    public ProcessActionVisitor(ProcessBuilder processBuilder, ProcessActionStepContext currentContext, ProcessActionEnvironment environment)
    {
        this._processBuilder = processBuilder;
        this._environment = environment;
        this.CurrentContext = currentContext;
        this._unhandledErrorStep =
            processBuilder.AddStep(
                "unhandled_error",
                (kernel, context) =>
                {
                    // Handle unhandled errors here
                    Console.WriteLine("*** PROCESS ERROR - Unhandled error");
                    return Task.CompletedTask;
                });
    }

    public void Complete()
    {
        Console.WriteLine("> COMPLETE"); // %%% DEBUG
        var finalContext = this.MoveToNewContext("FINAL"); // %%% ID ???
        finalContext.EdgeBuilder.StopProcess();
    }

    private ProcessActionStepContext CurrentContext { get; set; }

    protected override void Visit(ActionScope item)
    {
        Trace(item, isSkipped: false);

        this.MoveToNewContext(item.Id.Value);
    }

    protected override void Visit(UnknownDialogAction item)
    {
        Trace(item);
    }

    protected override void Visit(BeginDialog item)
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

    protected override void Visit(EditTable item)
    {
        Trace(item);
    }

    protected override void Visit(EditTableV2 item)
    {
        Trace(item);
    }

    protected override void Visit(EmitEvent item)
    {
        Trace(item);
    }

    protected override void Visit(EndDialog item)
    {
        Trace(item);
    }

    protected override void Visit(GetActivityMembers item)
    {
        Trace(item);
    }

    protected override void Visit(GetConversationMembers item)
    {
        Trace(item);
    }

    protected override void Visit(GotoAction item)
    {
        Trace(item);
    }

    protected override void Visit(HttpRequestAction item)
    {
        Trace(item);
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

    protected override void Visit(EndConversation item)
    {
        Trace(item);
    }

    protected override void Visit(RecognizeIntent item)
    {
        Trace(item);
    }

    protected override void Visit(SendActivity item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new SendActivityAction(item, this._environment));
    }

    protected override void Visit(TransferConversation item)
    {
        Trace(item);
    }

    protected override void Visit(TransferConversationV2 item)
    {
        Trace(item);
    }

    protected override void Visit(ParseValue item)
    {
        Trace(item);
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

    protected override void Visit(SignOutUser item)
    {
        Trace(item);
    }

    protected override void Visit(LogCustomTelemetryEvent item)
    {
        Trace(item);
    }

    protected override void Visit(DeleteActivity item)
    {
        Trace(item);
    }

    protected override void Visit(UpdateActivity item)
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

    protected override void Visit(AnswerQuestionWithAI item)
    {
        Trace(item, isSkipped: false);

        this.AddAction(new AnswerQuestionWithAIAction(item));
    }

    protected override void Visit(InvokeCustomModelAction item)
    {
        Trace(item);
    }

    private static void Trace(DialogAction item, bool isSkipped = true)
    {
        Console.WriteLine($"> {(isSkipped ? "EMPTY" : "VISIT")} - {item.GetType().Name} [{item.Id.Value}]"); // %%% TELEMETRY
    }

    private void AddAction(ProcessAction? action)
    {
        if (action is not null)
        {
            // Add the action to the existing context
            this.CurrentContext.Actions.Add(action);
        }
    }

    private ProcessActionStepContext MoveToNewContext(string contextId)
    {
        return this.CurrentContext = this.MoveToNewContext(this.CurrentContext, contextId);
    }

    private ProcessActionStepContext MoveToNewContext(ProcessActionStepContext currentContext, string contextId)
    {
        // 1. Close the current context by creating a step to execute all the current actions
        ProcessStepBuilder newStep =
            this._processBuilder.AddStep(
                currentContext.Id,
                async (kernel, context) =>
                {
                    try
                    {
                        Console.WriteLine($"!!! STEP [{currentContext.Id}]"); // %%% DEBUG
                        if (currentContext.Actions.Count > 0)
                        {
                            RecalcEngine engine = RecalcEngineFactory.Create(this._environment.MaximumExpressionLength);
                            await engine.ExecuteActionsAsync(context, currentContext.Actions, kernel).ConfigureAwait(false);
                        }
                    }
                    catch (ProcessActionException)
                    {
                        Console.WriteLine($"*** STEP [{currentContext.Id}] ERROR - Action failure"); // %%% DEBUG
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"*** STEP [{currentContext.Id}] ERROR\n{ex.Message}"); // %%% DEBUG
                        throw;
                    }
                });

        // 2. When the current step ends, activate the next step
        currentContext.EdgeBuilder.SendEventTo(new ProcessFunctionTargetBuilder(newStep));
        //    or capture any unhandled errors
        newStep.OnFunctionError("Invoke").SendEventTo(new ProcessFunctionTargetBuilder(this._unhandledErrorStep));

        // 3. Move to the new step context
        return
            new ProcessActionStepContext(contextId)
            {
                EdgeBuilder = newStep.OnFunctionResult("Invoke")
            };
    }
}
