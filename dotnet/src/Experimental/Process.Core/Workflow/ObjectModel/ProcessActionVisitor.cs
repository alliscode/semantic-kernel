// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel;

internal class ProcessActionVisitor :  DialogActionVisitor
{
    private readonly RecalcEngine _engine;
    private readonly Dictionary<string, Dictionary<string, FormulaValue>> _scopes = new()
    {
        ["Topic"] = [],
        ["Global"] = [],
        ["System"] = []
    };

    public ProcessActionVisitor(RecalcEngine engine)
    {
        this._engine = engine;
    }

    protected override void Visit(UnknownDialogAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(InvokeFlowAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(InvokeAIBuilderModelAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(WaitForConnectorTrigger item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(InvokeConnectorAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(InvokeSkillAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(AdaptiveCardPrompt item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(Question item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(CSATQuestion item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(OAuthInput item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ActionScope item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(Foreach item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(BeginDialog item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(RepeatDialog item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ActivateExternalTrigger item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(DisableTrigger item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ReplaceDialog item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(CancelAllDialogs item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(CancelDialog item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ClearAllVariables item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(BreakLoop item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ContinueLoop item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ResetVariable item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(EditTable item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(EditTableV2 item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(EmitEvent item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(EndDialog item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(GetActivityMembers item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(GetConversationMembers item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(GotoAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(HttpRequestAction item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ConditionGroup item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(EndConversation item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(RecognizeIntent item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SendActivity item)
    {
        Console.WriteLine(item);
        // TODO: This maps to sending output to the user.
    }

    protected override void Visit(TransferConversation item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(TransferConversationV2 item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SetVariable item)
    {
        Console.WriteLine(item);
        if (item.Value is not null && item.Value.IsExpression)
        {
            if (item.Variable?.Path is null)
            {
                return;
            }

            var expression = item.Value.ExpressionText;
            if (expression.StartsWith("=", StringComparison.OrdinalIgnoreCase))
            {
                expression = expression.Substring(1);
            }

            FormulaValue? result = null;
            try
            {
                result = this._engine.Eval(expression);
            }
            catch (Exception)
            {
                throw;
            }

            if (result is ErrorValue errorVal)
            {
                throw new InvalidOperationException("PowerFX error: " + errorVal.Errors[0].Message);
            }

            var value = result;
            this.SetScopedVariable(item.Variable.Path.VariableScopeName, item.Variable.Path.VariableName, value);
        }
    }

    private void SetScopedVariable(string? scopeName, string? varName, FormulaValue value)
    {
        if (scopeName is null)
        {
            throw new InvalidOperationException("Scope name cannot be null.");
        }

        if (varName is null)
        {
            throw new InvalidOperationException("Variable name cannot be null.");
        }

        if (!this._scopes.TryGetValue(scopeName, out var scope))
        {
            throw new InvalidOperationException("Unknown scope: " + scopeName);
        }

        scope[varName] = value;

        // Rebuild scope record and update engine
        var scopeRecord = this.BuildRecord(scope);
        this._engine.UpdateVariable(scopeName, scopeRecord);
    }

    private RecordValue BuildRecord(Dictionary<string, FormulaValue> fields)
    {
        var recordType = RecordType.Empty();
        foreach (var kvp in fields)
        {
            recordType = recordType.Add(kvp.Key, kvp.Value.Type);
        }

        return FormulaValue.NewRecordFromFields(recordType,
            [.. fields.Select(kvp => new NamedValue(kvp.Key, kvp.Value))]);
    }

    private void RefreshAllScopes()
    {
        foreach (var scope in this._scopes)
        {
            var record = this.BuildRecord(scope.Value);
            this._engine.UpdateVariable(scope.Key, record);
        }
    }

    protected override void Visit(SetTextVariable item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(ParseValue item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SignOutUser item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(LogCustomTelemetryEvent item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(DeleteActivity item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(UpdateActivity item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(DisconnectedNodeContainer item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(CreateSearchQuery item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SearchKnowledgeSources item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SearchAndSummarizeWithCustomModel item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(SearchAndSummarizeContent item)
    {
        Console.WriteLine(item);
    }

    protected override void Visit(AnswerQuestionWithAI item)
    {
        Console.WriteLine(item);
        // This should map to a step invokes a custom model to answer a question.
    }

    protected override void Visit(InvokeCustomModelAction item)
    {
        Console.WriteLine(item);
    }
}
