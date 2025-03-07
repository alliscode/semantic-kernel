// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process.Serialization;

namespace Process.Runtime.Core;
internal static class ProcessStepInfoExtensions
{
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
        processStepState.State = JsonSerializer.Serialize(kernelProcessStepState);
        return processStepState;
    }

    public static ProcessFunctionTarget ToProcessFunctionTarget(this KernelProcessFunctionTarget kernelProcessFunctionTarget)
    {
        var processFunctionTarget = new ProcessFunctionTarget();
        processFunctionTarget.FunctionName = kernelProcessFunctionTarget.FunctionName;
        processFunctionTarget.ParameterName = kernelProcessFunctionTarget.ParameterName;
        processFunctionTarget.StepId = kernelProcessFunctionTarget.StepId;
        processFunctionTarget.TargetEventId = kernelProcessFunctionTarget.TargetEventId;
        return processFunctionTarget;
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
