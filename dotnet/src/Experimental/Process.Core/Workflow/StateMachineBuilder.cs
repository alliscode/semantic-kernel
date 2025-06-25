// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Process;
using Microsoft.SemanticKernel.Process.Internal;
using WorkflowEngine.Models;

namespace Microsoft.SemanticKernel;

/// <summary>
/// State Machine Builder for FDL (Foundry Definition Language) state machines.
/// </summary>
public static class StateMachineBuilder
{
    /// <summary>
    /// Builds a process from an FDL State Machine
    /// </summary>
    /// <param name="stateMachine"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotImplementedException"></exception>
    public static KernelProcess Build(StateMachineDefinition stateMachine)
    {
        var stateStepMap = new Dictionary<string, ProcessStepBuilder>();
        var processBuilder = new ProcessBuilder("FDL");

        foreach (var state in stateMachine.States)
        {
            ProcessStepBuilder builder = processBuilder.AddStepFromType<FDLStep, FDLStepState>(new FDLStepState { Actors = state.Actors }, state.Name);
            stateStepMap[state.Name] = builder;
        }

        foreach (var transition in stateMachine.Transitions)
        {
            if (!stateStepMap.TryGetValue(transition.From, out var fromStep))
            {
                throw new InvalidOperationException($"Transition from state '{transition.From}' not found.");
            }
            if (!stateStepMap.TryGetValue(transition.To, out var toStep))
            {
                throw new InvalidOperationException($"Transition to state '{transition.To}' not found.");
            }

            // Build the edge from the 'from' step
            var edgeBuilder = fromStep.OnEvent("StateComplete");

            // If the transition has a condition, we can add it to the edge
            if (transition.Condition != null)
            {
                KernelProcessEdgeCondition edgeCondition = new(
                    (e, s) =>
                    {
                        var wrapper = new DeclarativeConditionContentWrapper
                        {
                            State = s,
                            Event = e.Data
                        };

                        var result = JMESPathConditionEvaluator.EvaluateCondition(wrapper, transition.Condition);
                        return Task.FromResult(result);
                    }, transition.Condition);

                edgeBuilder = edgeBuilder.OnCondition(edgeCondition);
            }

            // The edge points to the 'to' step
            edgeBuilder = edgeBuilder.SendEventTo(new ProcessFunctionTargetBuilder(toStep));
        }

        return processBuilder.Build();
    }
}

/// <summary>
/// Represents a step in a state-based kernel process workflow.
/// </summary>
public class FDLStep : KernelProcessStep<FDLStepState>
{
    private FDLStepState? _state;

    /// <summary>
    /// Activates the process step by initializing its state.
    /// </summary>
    /// <param name="state">The state object containing the current step's state data. If <see cref="KernelProcessStepState{T}.State"/> is
    /// null, a new instance of <see cref="FDLStepState"/> is created.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous activation operation.</returns>
    public override ValueTask ActivateAsync(KernelProcessStepState<FDLStepState> state)
    {
        this._state = state.State ?? new FDLStepState();
        return new ValueTask(Task.CompletedTask);
    }

    /// <summary>
    /// Executes a step in the state machine asynchronously.
    /// </summary>
    [KernelFunction]
    public Task ExecuteAsync(KernelProcessContext context)
    {
        // Implement the logic for executing the step in the state machine
        // This could involve invoking a function, processing data, etc.

        foreach (var actor in this._state!.Actors)
        {
            // Here we would invoke the actor, which could be a Foundry agent.
            // 1. Read from state store to get inputs, messages_in, and thread.
            // 2. Invoke the actor on the thread with the inputs and messages_in. Pay attention the the options for streaming output and handling HITL.
            // 3. Write the outputs, messages_out to the state store.
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// State object for FDL steps in a state machine.
/// </summary>
public class FDLStepState
{
    /// <summary>
    /// The list of actors to invoke in the state machine step.
    /// </summary>
    public List<StateMachineActor> Actors { get; set; } = [];
}
