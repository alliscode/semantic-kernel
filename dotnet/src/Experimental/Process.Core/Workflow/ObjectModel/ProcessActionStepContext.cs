// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel;

internal static class ActionScopeTypes
{
    public const string Topic = nameof(Topic);
    public const string Global = nameof(Global);
    public const string System = nameof(System);
}

internal sealed class ProcessActionScope : Dictionary<string, FormulaValue>;

internal sealed class ProcessActionScopes : Dictionary<string, ProcessActionScope>;

/// <summary>
/// Step context for the current step in a process.
/// </summary>
internal sealed class ProcessActionStepContext
{
    ///// <summary>
    ///// Step Builder for the current step.
    ///// </summary>
    //public required ProcessStepBuilder StepBuilder { get; set; }

    /// <summary>
    /// The edge builder for the current step.
    /// </summary>
    public required ProcessStepEdgeBuilder EdgeBuilder { get; set; }

    /// <summary>
    /// The actions.
    /// </summary>
    public List<Func<Kernel, KernelProcessStepContext, RecalcEngine, ProcessActionScopes, Task>> Actions { get; set; } = [];
}
