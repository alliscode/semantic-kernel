// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Provides functionality for incrementally defining a process step.
/// </summary>
public class ProcessStepBuilder
{
    private readonly IEnumerable<KernelFunction> _functions;
    private readonly Dictionary<string, KernelFunction> _functionsDict;
    private string _eventNamespace;

    public string? Id { get; init; }

    public string Name { get; init; }

    public Type? StepType { get; init; }

    public Type? StateType { get; init; }

    public IDictionary<string, IList<ProcessEdgeBuilder>> OutputEdges { get; set; }

    public ProcessStepBuilder(string name, string stepType, string stateType, IEnumerable<KernelFunction> functions)
    {
        this.Name = name;
        this.StepType = stepType;
        this.StateType = stateType;
        this.OutputEdges = new();

        this._functions = functions;
        this._eventNamespace = $"{name}_{this.Id}";
        this._functionsDict = new Dictionary<string, KernelFunction>(StringComparer.OrdinalIgnoreCase);

        if (functions is not null)
        {
            foreach (KernelFunction f in functions)
            {
                Validator.ThrowIfNull(f);

                var cloned = f.Clone(name);
                this._functionsDict.Add(cloned.Name, cloned);
            }
        }
    }

    public virtual ProcessEdgeBuilder OnEvent(string eventType)
    {
        // scope the event to this instance of this step
        var scopedEventId = this.StepScopedEventId(eventType);
        return new ProcessEdgeBuilder(this, scopedEventId);
    }

    public virtual ProcessEdgeBuilder OnFunctionResult(string functionName)
    {
        return this.OnEvent($"{functionName}.OnResult");
    }

    //public virtual StepDto ToDto()
    //{
    //    ArgumentNullException.ThrowIfNull(this.Id);

    //    return new StepDto
    //    {
    //        Id = this.Id,
    //        Name = this.Name,
    //        StepType = this.StepType,
    //        State = this.StateType,
    //        OutputEdges = this.OutputEdges.Select(kvp => new KeyValuePair<string, List<EdgeDto>>(kvp.Key, kvp.Value.Select(edge => edge.ToDto()).ToList())).ToDictionary(),
    //    };
    //}

    public virtual IEnumerable<KernelFunction> Functions => this._functions;

    public virtual int FunctionCount => this._functionsDict.Count;

    public virtual bool TryGetFunction(string name, [NotNullWhen(true)] out KernelFunction? function) =>
        this._functionsDict.TryGetValue(name, out function);

    internal void LinkTo(string eventType, ProcessFunctionTarget functionTarget)
    {
        var outputTargets = new List<OutputTarget2> { functionTarget };
        var edge = new Edge2(this, outputTargets);

        if (!this.OutputEdges.TryGetValue(eventType, out List<Edge2>? edges) || edges == null)
        {
            edges = [];
            this.OutputEdges[eventType] = edges;
        }

        edges.Add(edge);
    }

    private string StepScopedEventId(string eventType)
    {
        return $"{this._eventNamespace}.{eventType}";
    }
}
