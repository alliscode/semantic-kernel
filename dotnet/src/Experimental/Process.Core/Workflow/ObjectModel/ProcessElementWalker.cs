// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.Bot.ObjectModel;
using Microsoft.Bot.ObjectModel.Yaml;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel.Process.Workflows;

internal sealed class ProcessActionWalker : BotElementWalker
{
    private readonly ProcessActionVisitor _visitor;

    public ProcessActionWalker(ProcessBuilder processBuilder, ProcessActionEnvironment processEnvironment)
    {
        this._visitor = CreateActionVisitor(processBuilder, processEnvironment);
    }

    public void ProcessYaml(string yaml)
    {
        Console.WriteLine("### PARSING YAML");
        var root = YamlSerializer.Deserialize<BotElement>(yaml) ?? throw new KernelException("Failed to deserialize YAML content into BotElement.");
        Console.WriteLine("### INTERPRETING MODEL");
        this.Visit(root);
        this._visitor.Complete();
        Console.WriteLine("### PROCESS CREATED");
    }

    public override bool DefaultVisit(BotElement definition)
    {
        if (definition is DialogAction action)
        {
            action.Accept(this._visitor);
        }

        return true;
    }

    private static ProcessActionVisitor CreateActionVisitor(ProcessBuilder processBuilder, ProcessActionEnvironment processEnvironment)
    {
        ProcessActionScopes scopes = new()
        {
            [ActionScopeTypes.Topic] = [],
            [ActionScopeTypes.Global] = [],
            [ActionScopeTypes.System] = []
        };

        scopes[ActionScopeTypes.System]["LastMessage"] = StringValue.New("ANYTHING"); // %%% INPUT MESSAGE ??? (NOT WORKING)

        ProcessStepBuilder initStep =
            processBuilder.AddStep(
                "init",
                async (kernal, context) =>
                {
                    Console.WriteLine("!!! INIT WORKFLOW");
                    await context.SetUserStateAsync("scopes", scopes).ConfigureAwait(false);
                });

        processBuilder.OnInputEvent("message").SendEventTo(new ProcessFunctionTargetBuilder(initStep));

        ProcessActionStepContext context = new("root")
        {
            EdgeBuilder = initStep.OnFunctionResult("Invoke")
        };

        return new ProcessActionVisitor(processBuilder, context, processEnvironment);
    }
}
