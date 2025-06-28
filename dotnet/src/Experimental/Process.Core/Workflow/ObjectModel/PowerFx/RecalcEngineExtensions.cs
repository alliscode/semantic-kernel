// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel.Process.Workflows.PowerFx;

internal static class RecalcEngineExtensions
{
    public static void SetScopedVariable(this RecalcEngine engine, ProcessActionScopes scopes, string scopeName, string varName, FormulaValue value)
    {
        // Validate inputs and assign value.
        ProcessActionScope scope = scopes.AssignValue(scopeName, varName, value);

        // Rebuild scope record and update engine
        RecordValue scopeRecord = scope.BuildRecord();
        engine.DeleteFormula(scopeName);
        engine.UpdateVariable(scopeName, scopeRecord);
    }

    public static async Task ExecuteActionsAsync(this RecalcEngine engine, KernelProcessStepContext context, IEnumerable<ProcessAction> actions, Kernel kernel, CancellationToken cancellationToken)
    {
        ProcessActionScopes scopes = await context.GetUserStateAsync<ProcessActionScopes>("scopes").ConfigureAwait(false);

        SetScope(ActionScopeTypes.Topic);
        SetScope(ActionScopeTypes.Global);
        SetScope(ActionScopeTypes.System);

        foreach (ProcessAction action in actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Execute each action in the current context
                Console.WriteLine($"!!! ACTION {action.GetType().Name} [{action.Id}]"); // %%% DEVTRACE
                await action.HandleAsync(context, scopes, engine, kernel, cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessActionException exception)
            {
                Console.WriteLine($"*** ACTION [{action.Id}] ERROR - {exception.GetType().Name}\n{exception.Message}"); // %%% DEVTRACE
                throw;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"*** ACTION [{action.Id}] ERROR - {exception.GetType().Name}\n{exception.Message}"); // %%% DEVTRACE
                throw new ProcessActionException($"Unexpected failure executing action #{action.Id} [{action.GetType().Name}]", exception);
            }
        }

        void SetScope(string scopeName)
        {
            RecordValue record = scopes[scopeName].BuildRecord();
            engine.UpdateVariable(scopeName, record);
        }
    }
}
