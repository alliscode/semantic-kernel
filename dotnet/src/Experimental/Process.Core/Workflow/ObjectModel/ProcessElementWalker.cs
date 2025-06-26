// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Bot.ObjectModel;
using Microsoft.Bot.ObjectModel.Yaml;
using Microsoft.PowerFx;

namespace Microsoft.SemanticKernel;

internal sealed class ProcessActionWalker : BotElementWalker
{
    private readonly RecalcEngine _engine;
    private readonly ProcessBuilder _processBuilder;
    private readonly ProcessActionVisitor _visitor;

    public ProcessActionWalker(RecalcEngine engine, ProcessBuilder processBuilder)
    {
        this._engine = engine;
        this._processBuilder = processBuilder;
        this._visitor = CreateActionVisitor(this._engine, this._processBuilder);
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

    private static ProcessActionVisitor CreateActionVisitor(RecalcEngine engine, ProcessBuilder processBuilder) // %%% INTERNAL
    {
        ProcessActionScopes scopes = new()
        {
            [ActionScopeTypes.Topic] = [],
            [ActionScopeTypes.Global] = [],
            [ActionScopeTypes.System] = []
        };

        var initStep = processBuilder.AddStep("init", async (kernal, context) =>
        {
            await context.SetUserStateAsync("scopes", scopes).ConfigureAwait(false);
        });

        processBuilder.OnInputEvent("message").SendEventTo(new ProcessFunctionTargetBuilder(initStep));

        ProcessActionStepContext context = new()
        {
            EdgeBuilder = initStep.OnFunctionResult("Invoke")
        };

        return new ProcessActionVisitor(engine, processBuilder, context);
    }
}
