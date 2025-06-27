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
        engine.UpdateVariable(scopeName, scopeRecord);
    }

    public static async Task ExecuteActionsAsync(this RecalcEngine engine, KernelProcessStepContext context, IEnumerable<ProcessAction> actions, Kernel kernel)
    {
        ProcessActionScopes scopes = await context.GetUserStateAsync<ProcessActionScopes>("scopes").ConfigureAwait(false);

        foreach (ProcessAction action in actions)
        {
            // Execute each action in the current context
            Console.WriteLine($"!!! ACTION [{action.Id}]"); // %%% DEBUG
            await action.HandleAsync(context, scopes, engine, kernel).ConfigureAwait(false);

            RecordValue record1 = scopes[ActionScopeTypes.Topic].BuildRecord();
            engine.UpdateVariable(ActionScopeTypes.Topic, record1);
            // %%% OTHER SCOPE TYPES ???
            RecordValue record2 = scopes[ActionScopeTypes.Global].BuildRecord();
            engine.UpdateVariable(ActionScopeTypes.Global, record2);
            RecordValue record3 = scopes[ActionScopeTypes.System].BuildRecord();
            engine.UpdateVariable(ActionScopeTypes.System, record3);
        }
    }
}
