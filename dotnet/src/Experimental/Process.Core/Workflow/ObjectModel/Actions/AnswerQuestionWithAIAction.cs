// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Process.Workflows.Actions;

internal sealed class AnswerQuestionWithAIAction : AssignmentAction<AnswerQuestionWithAI>
{
    public AnswerQuestionWithAIAction(AnswerQuestionWithAI action)
        : base(action, () => action.Variable?.Path)
    {
        if (action.UserInput is null ||
            string.IsNullOrWhiteSpace(action.UserInput.ExpressionText))
        {
            throw new InvalidOperationException("UserInput and ExpressionText must be defined for AnswerQuestionWithAI action."); // %%% EXCEPTION TYPES
        }
    }

    public override async Task HandleAsync(KernelProcessStepContext context, ProcessActionScopes scopes, RecalcEngine engine, Kernel kernel)
    {
        IChatCompletionService chatCompletion = kernel.Services.GetRequiredService<IChatCompletionService>();
        FormulaValue expressionResult = engine.Eval(this.Action.UserInput!.ExpressionText);
        if (expressionResult is not StringValue stringResult)
        {
            throw new InvalidOperationException("UserInput expression must evaluate to a string.");
        }

        ChatHistory history = [];
        history.AddUserMessage(stringResult.Value);
        ChatMessageContent response = await chatCompletion.GetChatMessageContentAsync(history).ConfigureAwait(false);
        StringValue responseValue = FormulaValue.New(response.ToString());

        this.AssignTarget(engine, scopes, responseValue);
    }
}
