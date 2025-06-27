// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel.Process.Workflows;

internal abstract class ProcessAction<TAction>(TAction action) : ProcessAction(action) where TAction : DialogAction
{
    public new TAction Action => action;
}

internal abstract class ProcessAction(DialogAction action)
{
    public ActionId Id => action.Id;

    public DialogAction Action => action;

    public abstract Task HandleAsync(KernelProcessStepContext context, ProcessActionScopes scopes, RecalcEngine engine, Kernel kernel);
}

internal abstract class AssignmentAction<TAction> : ProcessAction<TAction> where TAction : DialogAction
{
    protected AssignmentAction(TAction action, Func<PropertyPath?> resolver)
        : base(action)
    {
        this.Target =
            resolver.Invoke() ??
            throw new KernelException("SetVariable action must have a variable path defined."); // %%% EXCEPTION TYPES;
    }

    public PropertyPath Target { get; }

    protected void AssignTarget(RecalcEngine engine, ProcessActionScopes scopes, FormulaValue result)
    {
        Console.WriteLine($"!!! {this.GetType().Name} [{this.Id}] {this.Target.VariableScopeName}.{this.Target.VariableName}={result}"); // %%% DEBUG
        engine.SetScopedVariable(scopes, this.Target.VariableScopeName, this.Target.VariableName, result);
    }
}
