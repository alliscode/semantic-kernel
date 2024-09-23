// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Base implementation of a Step in a Process.
/// </summary>
public class ProcessStepBase
{
    /// <summary>
    /// A mapping of output edges from the Step using the .
    /// </summary>
    private readonly Dictionary<string, List<ProcessEdge>> _outputEdges = new();

    /// <summary>
    /// The state object of type TState.
    /// </summary>
    internal ProcessStepState State { get; init; }

    /// <summary>
    /// A read-only collection of event Ids that this Step can emit.
    /// </summary>
    protected IReadOnlyCollection<string> EventIds => this._outputEdges.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Retrieves the output edges for a given event Id. Returns an empty list if the event Id is not found.
    /// </summary>
    /// <param name="eventId">The Id of an event.</param>
    /// <returns></returns>
    protected IReadOnlyCollection<ProcessEdge> GetOutputEdges(string eventId)
    {
        if (this._outputEdges.TryGetValue(eventId, out List<ProcessEdge>? edges))
        {
            return edges.AsReadOnly();
        }

        return new List<ProcessEdge>().AsReadOnly();
    }

    /// <summary>
    /// Called when the Step is activated.
    /// </summary>
    /// <param name="state">An instance of the state that holds state data for the step.</param>
    /// <returns>An instance of <see cref="ValueTask"/></returns>
    internal virtual ValueTask _ActivateAsync(ProcessStepState state)
    {
        return default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessStepBase"/> class.
    /// </summary>
    public ProcessStepBase(ProcessStepState state)
    {
        Verify.NotNull(state);
        this.State = state;
        this._outputEdges = [];
    }
}

/// <summary>
/// Process Step. Derive from this class to create a new Step for a Process.
/// </summary>
public class ProcessStep : ProcessStepBase
{
    /// <inheritdoc/>
    internal override ValueTask _ActivateAsync(ProcessStepState state)
    {
        Verify.NotNull(state);
        return this.ActivateAsync(state);
    }

    /// <inheritdoc/>
    public virtual ValueTask ActivateAsync(ProcessStepState state)
    {
        return default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessStep"/> class.
    /// </summary>
    /// <param name="state"></param>
    protected ProcessStep(ProcessStepState? state = null)
        : base(state ?? new())
    {
    }
}

/// <summary>
/// Process Step. Derive from this class to create a new Step with user-defined state of type TState for a Process.
/// </summary>
/// <typeparam name="TState">An instance of TState used for user-defined state.</typeparam>
public class ProcessStep<TState> : ProcessStepBase where TState : class, new()
{
    internal new ProcessStepState<TState> State => base.State as ProcessStepState<TState> ?? new();

    /// <inheritdoc/>
    internal override ValueTask _ActivateAsync(ProcessStepState state)
    {
        var genericState = state as ProcessStepState<TState>;
        Verify.NotNull(genericState);

        // initialize the state if it is null
        if (genericState.State is null)
        {
            genericState.State = new TState();
        }

        return this.ActivateAsync(genericState);
    }

    /// <inheritdoc/>
    public virtual ValueTask ActivateAsync(ProcessStepState<TState> state)
    {
        return this._ActivateAsync(state);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessStep"/> class.
    /// </summary>
    /// <param name="state">The state associated with this step.</param>
    public ProcessStep(ProcessStepState<TState>? state = null)
        : base(state ?? new())
    {
    }
}
