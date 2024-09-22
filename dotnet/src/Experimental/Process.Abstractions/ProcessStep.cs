// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Base implementation of a Step in a Process.
/// </summary>
/// <typeparam name="TState">The type used for persisted state.</typeparam>
public abstract class ProcessStepBase<TState> where TState : class, new()
{
    /// <summary>
    /// A mapping of output edges from the Step using the .
    /// </summary>
    private readonly Dictionary<string, List<ProcessEdge>> _outputEdges = new();

    /// <summary>
    /// The state object of type TState.
    /// </summary>
    internal TState State { get; init; }

    /// <summary>
    /// Called when the Step is activated.
    /// </summary>
    /// <param name="state">An instance of the state that holds state data for the step.</param>
    /// <returns>An instance of <see cref="ValueTask"/></returns>
    internal abstract ValueTask _ActivateAsync(TState state);

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessStepBase{TState}"/> class.
    /// </summary>
    protected ProcessStepBase()
    {
        this.State = new TState();
        this._outputEdges = new Dictionary<string, List<ProcessEdge>>();
    }

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
}

/// <summary>
/// Process Step. Derive from this class to create a new Step for a Process.
/// </summary>
public abstract class ProcessStep : ProcessStepBase<ProcessStepState>
{
    /// <inheritdoc/>
    internal override ValueTask _ActivateAsync(ProcessStepState state)
    {
        return this.ActivateAsync(state);
    }

    /// <inheritdoc/>
    public virtual ValueTask ActivateAsync(ProcessStepState state)
    {
        return this._ActivateAsync(state);
    }
}

/// <summary>
/// Process Step. Derive from this class to create a new Step with user-defined state of type TState for a Process.
/// </summary>
/// <typeparam name="TState">An instance of TState used for user-defined state.</typeparam>
public abstract class ProcessStep<TState> : ProcessStepBase<ProcessStepState<TState>> where TState : class, new()
{
    /// <inheritdoc/>
    internal override ValueTask _ActivateAsync(ProcessStepState<TState> state)
    {
        Verify.NotNull(state);

        // initialize the state if it is null
        if (state.State is null)
        {
            state.State = new TState();
        }

        return this.ActivateAsync(state);
    }

    /// <inheritdoc/>
    public virtual ValueTask ActivateAsync(ProcessStepState<TState> state)
    {
        return this._ActivateAsync(state);
    }
}
