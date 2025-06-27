// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel.Process.Workflows;

internal static class RecalcEngineExtensions
{
    public static void SetScopedVariable(this RecalcEngine engine, ProcessActionScopes scopes, string? scopeName, string? varName, FormulaValue value)
    {
        // Validate inputs and assign value.
        ProcessActionScope scope = scopes.AssignValue(scopeName, varName, value);

        // Rebuild scope record and update engine
        RecordValue scopeRecord = scope.BuildRecord();
        engine.DeleteFormula(scopeName);
        engine.UpdateVariable(scopeName, scopeRecord);
    }

    public static async Task ExecuteActionsAsync(this RecalcEngine engine, KernelProcessStepContext context, IEnumerable<ProcessAction> actions, Kernel kernel)
    {
        ProcessActionScopes scopes = await context.GetUserStateAsync<ProcessActionScopes>("scopes").ConfigureAwait(false);

        SetScope(ActionScopeTypes.Topic);
        SetScope(ActionScopeTypes.Global);
        SetScope(ActionScopeTypes.System);

        foreach (ProcessAction action in actions)
        {
            try
            {
                // Execute each action in the current context
                Console.WriteLine($"!!! ACTION [{action.Id}]"); // %%% DEBUG
                await action.HandleAsync(context, scopes, engine, kernel).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"*** ACTION [{action.Id}] ERROR\n{exception.Message}"); // %%% DEBUG
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
