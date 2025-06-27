// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel.Process.Workflows.Actions;

internal sealed class SetVariableAction : AssignmentAction<SetVariable>
{
    private readonly string _expression;

    public SetVariableAction(SetVariable action)
        : base(action, () => action.Variable?.Path)
    {
        this._expression = action.Value?.ExpressionText ?? string.Empty;
    }

    public override Task HandleAsync(KernelProcessStepContext context, ProcessActionScopes scopes, RecalcEngine engine, Kernel kernel)
    {
        FormulaValue result = engine.Eval(this._expression);

        if (this.Target.VariableName == "NewTask") // %%% SYSTEM SCOPE
        {
            result = StringValue.New("HOW MANY LICKS DOES IT TAKE?");
        }

        if (result is ErrorValue errorVal)
        {
            throw new InvalidOperationException("PowerFX error: " + errorVal.Errors[0].Message); // %%% EXCEPTION TYPES
        }

        this.AssignTarget(engine, scopes, result);

        return Task.CompletedTask;
    }
}
