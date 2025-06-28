// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel.Process.Workflows.Actions;

internal sealed class ParseValueAction : AssignmentAction<ParseValue>
{
    public ParseValueAction(ParseValue source)
        : base(source, () => source.Variable?.Path)
    {
    }

    public override Task HandleAsync(KernelProcessStepContext context, ProcessActionScopes scopes, RecalcEngine engine, Kernel kernel, CancellationToken cancellationToken)
    {
        var value = this.Action.Value;
        var valueType = this.Action.ValueType;
        var displayName = this.Action.DisplayName;

        this.AssignTarget(engine, scopes, BuildRecord());

        return Task.CompletedTask;

        static RecordValue BuildRecord() // HACKED 
        {
            return FormulaValue.NewRecordFromFields(
                new NamedValue("instruction_or_question", BuildAnswer()),
                new NamedValue("next_speaker", BuildAnswer()));
        }

        static RecordValue BuildAnswer()
        {
            return FormulaValue.NewRecordFromFields(
                new NamedValue("answer", StringValue.New("test answer")),
                new NamedValue("reason", StringValue.New("test reason")));
        }
    }
}
