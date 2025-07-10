// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.SemanticKernel.Process;
internal class LocalDelegateStep : LocalStep
{
    private readonly KernelProcessDelegateStepInfo _delegateStep;
    private readonly LocalUserStateStore _stateStore;

    public LocalDelegateStep(KernelProcessDelegateStepInfo stepInfo, Kernel kernel, LocalUserStateStore stateStore, string? parentProcessId = null, string? instanceId = null, LocalUserStateStore? userStateStore = null) : base(stepInfo, kernel, parentProcessId, instanceId, userStateStore: stateStore)
    {
        this._delegateStep = stepInfo;
        this._stateStore = stateStore;
    }

    internal override KernelProcessStep CreateStepInstance()
    {
        var delegateStep = (KernelProcessDelegateStepInfo)this._stepInfo;
        return new KernelDelegateProcessStep(delegateStep.StepFunction);
    }
}
