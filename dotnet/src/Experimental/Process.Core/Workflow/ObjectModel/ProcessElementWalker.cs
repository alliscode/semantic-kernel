// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Bot.ObjectModel;
using Microsoft.Bot.ObjectModel.Yaml;
using Microsoft.PowerFx;

namespace Microsoft.SemanticKernel;
internal class ProcessActionWalker : BotElementWalker
{
    private readonly RecalcEngine _engine;
    private readonly ProcessActionVisitor _visitor;

    public ProcessActionWalker(RecalcEngine engine)
    {
        this._engine = engine;
        this._visitor = new ProcessActionVisitor(this._engine);
    }

    public void ProcessYaml(string yaml)
    {
        var root = YamlSerializer.Deserialize<BotElement>(yaml) ?? throw new KernelException("Failed to deserialize YAML content into BotElement.");
        this.Visit(root);
    }

    public override bool DefaultVisit(BotElement definition)
    {
        if (definition is DialogAction action)
        {
            action.Accept(this._visitor);
        }

        return true;
    }
}
