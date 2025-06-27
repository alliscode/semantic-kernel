// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel.Process.Workflows;

internal static class ActionScopeTypes
{
    public const string Topic = nameof(Topic);
    public const string Global = nameof(Global);
    public const string System = nameof(System);
}

internal sealed class ProcessActionScope : Dictionary<string, FormulaValue>;

internal sealed class ProcessActionScopes : Dictionary<string, ProcessActionScope>;

internal delegate Task ProcessActionHandler(KernelProcessStepContext context, ProcessActionScopes scopes, RecalcEngine engine, Kernel kernel);

/// <summary>
/// Step context for the current step in a process.
/// </summary>
internal sealed class ProcessActionStepContext(string id)
{
    public string Id => id;

    /// <summary>
    /// The edge builder for the current step.
    /// </summary>
    public required ProcessStepEdgeBuilder EdgeBuilder { get; init; }

    /// <summary>
    /// The actions.
    /// </summary>
    public List<ProcessAction> Actions { get; init; } = [];
}

internal static class ProcessActionScopeExtensions
{
    public static RecordValue BuildRecord(this ProcessActionScope scope)
    {
        RecordType recordType = RecordType.Empty();

        int index = 0;
        NamedValue[] namedValues = new NamedValue[scope.Count];
        foreach (KeyValuePair<string, FormulaValue> kvp in scope)
        {
            recordType = recordType.Add(kvp.Key, kvp.Value.Type);
            namedValues[index] = new NamedValue(kvp.Key, kvp.Value);
            ++index;
        }

        return FormulaValue.NewRecordFromFields(recordType, namedValues);
    }

    public static ProcessActionScope AssignValue(this ProcessActionScopes scopes, string? scopeName, string? varName, FormulaValue value)
    {
        if (scopeName is null)
        {
            throw new InvalidOperationException("Scope name cannot be null.");
        }

        if (varName is null)
        {
            throw new InvalidOperationException("Variable name cannot be null.");
        }

        if (!scopes.TryGetValue(scopeName, out ProcessActionScope? scope))
        {
            throw new InvalidOperationException("Unknown scope: " + scopeName);
        }

        scope[varName] = value;

        return scope;
    }
}
