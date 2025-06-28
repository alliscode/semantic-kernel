// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
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

    public override Task HandleAsync(KernelProcessStepContext context, ProcessActionScopes scopes, RecalcEngine engine, Kernel kernel, CancellationToken cancellationToken)
    {
        FormulaValue result = engine.Eval(this._expression);

        if (result is ErrorValue errorVal)
        {
            throw new ProcessActionException($"Unable to evaluate expression: {this._expression}.  Error: {errorVal.Errors[0].Message}");
        }

        this.AssignTarget(engine, scopes, result);

        return Task.CompletedTask;
    }
}
