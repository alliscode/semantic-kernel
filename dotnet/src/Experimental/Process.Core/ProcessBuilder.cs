// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Microsoft.SemanticKernel.Process;

/// <summary>
/// Provides functionality for incrementally defining a process.
/// </summary>
public class ProcessBuilder : ProcessStepBuilder
{
    public ProcessBuilder(string name)
        : base(
              name: name,
              stepType: typeof(ProcessBuilder).AssemblyQualifiedName!,
              stateType: typeof(DefaultState).AssemblyQualifiedName!,
              functions: new List<KernelFunction>())
    {
    }

    public string? EntryPointId { get; internal set; }

    public IList<ProcessStepBuilder> StepProxies { get; set; } = new();

    public Dictionary<string, ProcessStepBuilder> StepProxiesDict { get; set; } = new();

    public ProcessStepBuilder AddStepFromType<TStep>(string? name = null, string? id = null, bool isEntryPoint = false) where TStep : ProcessStepBase
    {
        string stepId = id ?? Guid.NewGuid().ToString("n");
        string stepName = name ?? typeof(TStep).Name;

        if (isEntryPoint && this.EntryPointId is not null)
        {
            throw new InvalidOperationException("An entry point has already been set for this process.");
        }

        string? stateTypeAqn = null;
        string stepTypeAqn = typeof(TStep).AssemblyQualifiedName!;

        if (typeof(TStep).IsAssignableFrom(typeof(ProcessStep<>)))
        {
            stateTypeAqn = typeof(TStep).GetGenericArguments()[0].AssemblyQualifiedName;
        }
        else
        {
            stateTypeAqn = typeof(DefaultState).AssemblyQualifiedName;
        }


        if (typeof(TStep).BaseType.IsGenericType && typeof(TStep).BaseType.GetGenericTypeDefinition() == typeof(ProcessStep<>))
        {
            stateTypeAqn = typeof(TStep).BaseType.GetGenericArguments()[0].AssemblyQualifiedName;
        }
        else
        {
            stateTypeAqn = typeof(DefaultState).AssemblyQualifiedName;
        }

        var kernelPlugin = KernelPluginFactory.CreateFromType<TStep>();
        var proxy = new ProcessStepBuilder(stepName, stepTypeAqn, stateTypeAqn, kernelPlugin) { Id = stepId };

        if (isEntryPoint)
        {
            this.EntryPointId = proxy.Id;
        }

        this.StepProxies.Add(proxy);
        this.StepProxiesDict[stepName] = proxy;
        return proxy;
    }

    public ProcessStepBuilder AddStepFromProcess(ProcessBuilder kernelProcess)
    {
        this.StepProxies.Add(kernelProcess);
        this.StepProxiesDict[kernelProcess.Name] = kernelProcess;
        return kernelProcess;
    }

    public override IEnumerable<KernelFunction> Functions => this.StepProxies.SelectMany(proxy => proxy.Functions);

    public override int FunctionCount => base.FunctionCount;

    public override bool TryGetFunction(string name, [NotNullWhen(true)] out KernelFunction? function) =>
        this.Functions.ToDictionary(f => f.Name).TryGetValue(name, out function);

    //public override ProcessDto ToDto()
    //{
    //    // TODO: All the validations
    //    if (this.EntryPointId is null)
    //    {
    //        throw new InvalidOperationException("An entry point must be set for the process.");
    //    }

    //    return new ProcessDto
    //    {
    //        Id = this.Id,
    //        Name = this.Name,
    //        StepProxies = this.StepProxies.Select(proxy => proxy.ToDto()).ToList(),
    //        StepType = typeof(ProcessBuilder).AssemblyQualifiedName!,
    //        State = typeof(DefaultState).AssemblyQualifiedName!,
    //        OutputEdges = this.OutputEdges.Select(kvp => new KeyValuePair<string, List<EdgeDto>>(kvp.Key, kvp.Value.Select(edge => edge.ToDto()).ToList())).ToDictionary(),
    //        EntryPointId = this.EntryPointId
    //    };
    //}
}
