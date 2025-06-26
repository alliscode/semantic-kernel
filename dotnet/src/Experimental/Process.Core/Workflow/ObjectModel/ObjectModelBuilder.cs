// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;

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
        this._engine = EngineFactory.CreateDefault();
    }

    /// <summary>
    /// Builds a process from the provided YAML definition of a CPS Topic ObjectModel.
    /// </summary>
    /// <param name="topicYaml"></param>
    /// <returns></returns>
    public KernelProcess Build(string topicYaml)
    {
        ProcessBuilder processBuilder = new("topic");
        ProcessActionWalker walker = new(this._engine, processBuilder);
        walker.ProcessYaml(topicYaml);
        return processBuilder.Build();
    }
}
