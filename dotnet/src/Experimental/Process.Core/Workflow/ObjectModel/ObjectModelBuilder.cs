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
    public void Build(string topicYaml)
    {
        var walker = new ProcessActionWalker(this._engine);
        walker.ProcessYaml(topicYaml);
    }
}
