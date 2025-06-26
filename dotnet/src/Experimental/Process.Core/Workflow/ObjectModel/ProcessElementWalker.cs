// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.Bot.ObjectModel;
using Microsoft.Bot.ObjectModel.Yaml;
using Microsoft.PowerFx;

namespace Microsoft.SemanticKernel;
internal class ProcessActionWalker : BotElementWalker
{
    private readonly RecalcEngine _engine;
    private readonly ProcessBuilder _processBuilder;
    private ProcessActionVisitor? _visitor;

    public ProcessActionWalker(RecalcEngine engine, ProcessBuilder processBuilder)
    {
        this._engine = engine;
        this._processBuilder = processBuilder;

        //var context = new StepContext
        //{
        //    StepBuilder = processBuilder.AddStepFromType<ObjectModelProcessStep>("root")
        //};

        //this._visitor = new ProcessActionVisitor(this._engine, processBuilder, context);
    }

    public void ProcessYaml(string yaml)
    {
        var root = YamlSerializer.Deserialize<BotElement>(yaml) ?? throw new KernelException("Failed to deserialize YAML content into BotElement.");
        this.Visit(root);
    }

    public override bool DefaultVisit(BotElement definition)
    {
        if (definition is TriggerBase trigger && this._visitor is null)
        {
            var context = new StepContext
            {
                EdgeBuilder = this._processBuilder.OnInputEvent("message")
            };

            this._visitor = new ProcessActionVisitor(this._engine, this._processBuilder, context);
        }

        if (definition is DialogAction action)
        {
            if (this._visitor is null)
            {
                throw new KernelException("Visitor is not initialized. Ensure that the visitor is set before visiting actions.");
            }

            action.Accept(this._visitor);
        }

        return true;
    }
}
