// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;
using Microsoft.SemanticKernel.Process.Workflows.ObjectModel.PowerFx;
using Xunit;

namespace Microsoft.SemanticKernel.Process.UnitTests.Workflow;

#pragma warning disable CA1308 // Ignore "Normalize strings to uppercase" warning for test cases

public sealed class RecalcEngineTests
{
    private readonly RecalcEngine _engine = RecalcEngineFactory.Create(100);

    [Fact]
    public void EvaluateConstant()
    {
        this.EvaluateExpression(0m, "0");
        this.EvaluateExpression(-1m, "-1");
        this.EvaluateExpression(true, "true");
        this.EvaluateExpression(false, "false");
        this.EvaluateExpression((string?)null, string.Empty);
        this.EvaluateExpression("Hi", "\"Hi\"");
    }

    [Fact]
    public void EvaluateInvalid()
    {
        this._engine.UpdateVariable("Scoped.Value", DecimalValue.New(33));
        this.EvaluateFailure("Hi");
        this.EvaluateFailure("True");
        this.EvaluateFailure("TRUE");
        this.EvaluateFailure("=1", canParse: false);
        this.EvaluateFailure("=1+2", canParse: false);
        this.EvaluateFailure("CustomValue");
        this.EvaluateFailure("CustomValue + 1");
        this.EvaluateFailure("Scoped.Value");
        this.EvaluateFailure("Scoped.Value + 1");
        this.EvaluateFailure("\"BEGIN-\" & Scoped.Value & \"-END\"");
    }

    [Fact]
    public void EvaluateFormula()
    {
        NamedValue[] recordValues =
            [
                new NamedValue("Label", StringValue.New("Test")),
                new NamedValue("Value", DecimalValue.New(54)),
            ];
        FormulaValue complexValue = FormulaValue.NewRecordFromFields(recordValues);
        this._engine.UpdateVariable("CustomLabel", StringValue.New("Note"));
        this._engine.UpdateVariable("CustomValue", DecimalValue.New(42));
        this._engine.UpdateVariable("Scoped", complexValue);

        this.EvaluateExpression(2m, "1 + 1");
        this.EvaluateExpression(42m, "CustomValue");
        this.EvaluateExpression(43m, "CustomValue + 1");
        this.EvaluateExpression("Note", "CustomLabel");
        //this.EvaluateExpression("Note", "\"{CustomLabel}\"");
        this.EvaluateExpression("BEGIN-42-END", "\"BEGIN-\" & CustomValue & \"-END\"");
        this.EvaluateExpression(54m, "Scoped.Value");
        this.EvaluateExpression(55m, "Scoped.Value + 1");
        this.EvaluateExpression("Test", "Scoped.Label");
        //this.EvaluateExpression("Test", "\"{Scoped.Label}\"");
    }

    private void EvaluateFailure(string sourceExpression, bool canParse = true)
    {
        CheckResult checkResult = this._engine.Check(sourceExpression);
        Assert.False(checkResult.IsSuccess);
        ParseResult parseResult = this._engine.Parse(sourceExpression);
        Assert.Equal(canParse, parseResult.IsSuccess);
        Assert.Throws<AggregateException>(() => this._engine.Eval(sourceExpression));
    }

    private void EvaluateExpression<T>(T expectedResult, string sourceExpression)
    {
        CheckResult checkResult = this._engine.Check(sourceExpression);
        Assert.True(checkResult.IsSuccess);
        ParseResult parseResult = this._engine.Parse(sourceExpression);
        Assert.True(parseResult.IsSuccess);
        FormulaValue valueResult = this._engine.Eval(sourceExpression);
        if (expectedResult is null)
        {
            Assert.Null(valueResult.ToObject());
        }
        else
        {
            Assert.IsType<T>(valueResult.ToObject());
            Assert.Equal(expectedResult, valueResult.ToObject());
        }
    }
}
