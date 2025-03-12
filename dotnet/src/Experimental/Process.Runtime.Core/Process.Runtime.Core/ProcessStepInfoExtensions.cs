// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process.Internal;
using Microsoft.SemanticKernel.Process.Serialization;

namespace Process.Runtime.Core;
internal static class ProcessStepInfoExtensions
{
    private static JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        //TypeInfoResolver = new ProcessStateTypeResolver(Assembly.GetExecutingAssembly()),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ProcessStepInfo ToProcessStepInfo(this KernelProcessStepInfo kernelProcessStep)
    {
        var processStepInfo = new ProcessStepInfo();
        processStepInfo.InnerStepDotnetType = kernelProcessStep.InnerStepType.AssemblyQualifiedName;
        processStepInfo.State = kernelProcessStep.State.ToProcessStepState();

        foreach (var kvp in kernelProcessStep.Edges)
        {
            var edgeList = new ProcessEdgeList();
            var edges = kvp.Value.Select(edge => new ProcessEdge()
            {
                SourceStepId = edge.SourceStepId,
                OutputTarget = edge.OutputTarget.ToProcessFunctionTarget(),
            }).ToList();
            edgeList.Edges.AddRange(edges);
            processStepInfo.Edges.Add(kvp.Key, edgeList);
        }

        if (kernelProcessStep is KernelProcess kernelProcess)
        {
            processStepInfo.Process = new ProcessStep()
            {
                Steps = { kernelProcess.Steps.Select(s => s.ToProcessStepInfo()).ToList() }
            };
        }
        else if (kernelProcessStep is KernelProcessMap kernelProcessMap)
        {
            processStepInfo.Map = new MapStep()
            {
                Operation = kernelProcessMap.Operation.ToProcessStepInfo(),
            };
        }

        return processStepInfo;
    }

    public static ProcessStepState ToProcessStepState(this KernelProcessStepState kernelProcessStepState)
    {
        var processStepState = new ProcessStepState();
        processStepState.Id = kernelProcessStepState.Id;
        processStepState.Name = kernelProcessStepState.Name;
        processStepState.State = JsonSerializer.Serialize<object>(kernelProcessStepState, options: s_jsonSerializerOptions);
        processStepState.Version = kernelProcessStepState.Version;
        return processStepState;
    }

    public static ProcessFunctionTarget ToProcessFunctionTarget(this KernelProcessFunctionTarget kernelProcessFunctionTarget)
    {
        var processFunctionTarget = new ProcessFunctionTarget();
        processFunctionTarget.FunctionName = kernelProcessFunctionTarget.FunctionName;
        processFunctionTarget.ParameterName = kernelProcessFunctionTarget.ParameterName;
        processFunctionTarget.StepId = kernelProcessFunctionTarget.StepId;

        if (kernelProcessFunctionTarget.TargetEventId is not null)
        {
            processFunctionTarget.TargetEventId = kernelProcessFunctionTarget.TargetEventId;
        }

        return processFunctionTarget;
    }

    public static KernelProcess ToKernelProcess(this ProcessStepInfo processStepInfo)
    {
        Verify.NotNull(processStepInfo, nameof(processStepInfo));
        if (processStepInfo.StepTypeInfoCase != ProcessStepInfo.StepTypeInfoOneofCase.Process)
        {
            throw new KernelException($"Unable to create kernel process from {processStepInfo.StepTypeInfoCase}");
        }

        var state = processStepInfo.State.ToKernelProcessState();
        var steps = processStepInfo.Process.Steps.Select(s => s.ToKernelProcessStepInfo()).ToList();
        var edges = processStepInfo.Edges.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Edges.Select(e => e.ToKernelProcessEdge()).ToList());

        var kernelProcess = new KernelProcess(state, steps, edges);
        return kernelProcess;
    }

    public static KernelProcessStepInfo ToKernelProcessStepInfo(this ProcessStepInfo processStepInfo)
    {
        Verify.NotNull(processStepInfo, nameof(processStepInfo));
        if (processStepInfo.StepTypeInfoCase == ProcessStepInfo.StepTypeInfoOneofCase.Process)
        {
            throw new KernelException($"Unable to create kernel process step info from {processStepInfo.StepTypeInfoCase}");
        }

        var state = processStepInfo.State.ToKernelProcessStepState(Type.GetType(processStepInfo.InnerStepDotnetType));
        var edges = processStepInfo.Edges.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Edges.Select(e => e.ToKernelProcessEdge()).ToList());
        var kernelProcessStepInfo = new KernelProcessStepInfo(Type.GetType(processStepInfo.InnerStepDotnetType), state, edges);

        return kernelProcessStepInfo;
    }

    public static KernelProcessState ToKernelProcessState(this ProcessStepState processStepState)
    {
        Verify.NotNull(processStepState, nameof(processStepState));
        return new KernelProcessState(processStepState.Name, processStepState.Version, processStepState.Id);
    }

    public static KernelProcessStepState ToKernelProcessStepState(this ProcessStepState processStepState, Type dotnetType)
    {
        Verify.NotNull(processStepState, nameof(processStepState));

        var stateType = dotnetType.ExtractStateType(out Type? userStateType, null);

        var kernelProcessStepState = JsonSerializer.Deserialize(processStepState.State, stateType, options: s_jsonSerializerOptions);
        return kernelProcessStepState as KernelProcessStepState;
    }

    public static KernelProcessEdge ToKernelProcessEdge(this ProcessEdge processEdge)
    {
        Verify.NotNull(processEdge, nameof(processEdge));
        return new KernelProcessEdge(processEdge.SourceStepId, processEdge.OutputTarget.ToKernelProcessFunctionTarget());
    }

    public static KernelProcessFunctionTarget ToKernelProcessFunctionTarget(this ProcessFunctionTarget processFunctionTarget)
    {
        Verify.NotNull(processFunctionTarget, nameof(processFunctionTarget));
        return new KernelProcessFunctionTarget(processFunctionTarget.FunctionName, processFunctionTarget.ParameterName, processFunctionTarget.StepId, processFunctionTarget.TargetEventId);
    }
    //TODO: Complete this.
    //public static KernelProcess ToKernelProcess(this ProcessStepInfo processStepInfo)
    //{
    //    Verify.NotNull(processStepInfo, nameof(processStepInfo));

    //    if (processStepInfo.StepTypeInfoCase != ProcessStepInfo.StepTypeInfoOneofCase.Process)
    //    {
    //        throw new KernelException($"Unable to create kernel process from {processStepInfo.StepTypeInfoCase}");
    //    }

    //    var kernelProcess = new KernelProcess()
    //}
}
