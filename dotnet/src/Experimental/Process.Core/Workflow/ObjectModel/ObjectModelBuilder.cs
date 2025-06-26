// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;
using static Microsoft.SemanticKernel.KernelProcess;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Builder for converting CPS Topic ObjectModel YAML definition in a process.
/// </summary>
public class ObjectModelBuilder : BotElementWalker
{
    private readonly RecalcEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectModelBuilder"/> class.
    /// </summary>
    public ObjectModelBuilder()
    {
        Features toenable = Features.PowerFxV1;
        var config = new PowerFxConfig(toenable);
        config.EnableSetFunction();
        config.MaximumExpressionLength = 2000;
        this._engine = new RecalcEngine(config);
    }

    /// <summary>
    /// Builds a process from the provided YAML definition of a CPS Topic ObjectModel.
    /// </summary>
    /// <param name="topicYaml"></param>
    /// <returns></returns>
    public KernelProcess Build(string topicYaml)
    {
        ProcessBuilder processBuilder = new("topic");
        var walker = new ProcessActionWalker(this._engine, processBuilder);
        walker.ProcessYaml(topicYaml);
        return processBuilder.Build();
    }
}

/// <summary>
/// Step context for the current step in a process.
/// </summary>
public class StepContext
{
    /// <summary>
    /// Step Builder for the current step.
    /// </summary>
    //public required ProcessStepBuilder StepBuilder { get; set; }

    /// <summary>
    /// The edge builder for the current step.
    /// </summary>
    public required ProcessStepEdgeBuilder EdgeBuilder { get; set; }

    /// <summary>
    /// The actions.
    /// </summary>
    public List<Func<Kernel, KernelProcessStepContext, RecalcEngine, Dictionary<string, Dictionary<string, FormulaValue>>, Task>> Actions { get; set; } = [];
}
