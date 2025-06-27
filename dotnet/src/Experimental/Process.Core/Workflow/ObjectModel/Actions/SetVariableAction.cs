// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel.Process.Workflows.Actions;

internal sealed class SetVariableAction : ProcessAction
{
    public static SetVariableAction? From(SetVariable source)
    {
        if (source.Value is null) // %%% CONST || source.Value.IsExpression)
        {
            return null;
        }

        if (source.Variable?.Path is null)
        {
            throw new KernelException("SetVariable action must have a variable path defined."); // %%% EXCEPTION TYPES
        }

        string expression = source.Value.ExpressionText ?? string.Empty;
        //if (expression.StartsWith("=", StringComparison.OrdinalIgnoreCase)) // %%% CONST
        //{
        //    expression = expression.Substring(1);
        //}

        return new SetVariableAction(source.Id, expression, source.Variable.Path);
    }

    private readonly string _expression;
    private readonly PropertyPath _target;

    public SetVariableAction(ActionId id, string expression, PropertyPath target)
        : base(id)
    {
        this._expression = expression;
        this._target = target;
    }

    public override Task HandleAsync(KernelProcessStepContext context, ProcessActionScopes scopes, RecalcEngine engine, Kernel kernel)
    {
        FormulaValue result;
        try
        {
            result = engine.Eval(this._expression);
        }
        catch (Exception)
        {
            throw; // %%% TODO: WRAP AND RETHROW (OR REMOVE HANDLER)
        }

        if (this._target.VariableName == "NewTask") // %%% SYSTEM SCOPE
        {
            result = StringValue.New("HOW MANY LICKS DOES IT TAKE?");
        }

        if (result is ErrorValue errorVal)
        {
            throw new InvalidOperationException("PowerFX error: " + errorVal.Errors[0].Message); // %%% EXCEPTION TYPES
        }

        Console.WriteLine($"!!! {nameof(SetVariableAction)} [{this.Id}] {this._target.VariableScopeName}.{this._target.VariableName}={result}"); // %%% DEBUG
        engine.SetScopedVariable(scopes, this._target.VariableScopeName, this._target.VariableName, result);

        return Task.CompletedTask;
    }
}
