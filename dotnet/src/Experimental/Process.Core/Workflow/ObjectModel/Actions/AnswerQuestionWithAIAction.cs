// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Process.Workflows.Actions;

internal sealed class AnswerQuestionWithAIAction : ProcessAction
{
    public static AnswerQuestionWithAIAction From(AnswerQuestionWithAI source)
    {
        if (source.UserInput is null || string.IsNullOrWhiteSpace(source.UserInput.ExpressionText))
        {
            throw new InvalidOperationException("UserInput and ExpressionText must be defined for AnswerQuestionWithAI action."); // %%% EXCEPTION TYPES
        }

        if (source.Variable?.Path is null)
        {
            throw new KernelException("SetVariable action must have a variable path defined."); // %%% EXCEPTION TYPES
        }

        return new AnswerQuestionWithAIAction(source.Id, source.UserInput.ExpressionText, source.Variable.Path);
    }

    private readonly string _inputText;
    private readonly PropertyPath _responseTarget;

    public AnswerQuestionWithAIAction(ActionId id, string expressionText, PropertyPath responseTarget)
        : base(id)
    {
        this._inputText = expressionText;
        this._responseTarget = responseTarget;
    }

    public override async Task HandleAsync(KernelProcessStepContext context, ProcessActionScopes scopes, RecalcEngine engine, Kernel kernel)
    {
        IChatCompletionService chatCompletion = kernel.Services.GetRequiredService<IChatCompletionService>();
        FormulaValue expressionResult = engine.Eval(this._inputText);
        if (expressionResult is not StringValue stringResult)
        {
            throw new InvalidOperationException("UserInput expression must evaluate to a string.");
        }

        Console.WriteLine($"!!! {nameof(AnswerQuestionWithAIAction)} [{this.Id}] {this._responseTarget.VariableScopeName}.{this._responseTarget.VariableName}={stringResult}"); // %%% DEBUG

        ChatHistory history = [];
        history.AddUserMessage(stringResult.Value);
        ChatMessageContent response = await chatCompletion.GetChatMessageContentAsync(history).ConfigureAwait(false);
        StringValue responseValue = FormulaValue.New(response.ToString());

        engine.SetScopedVariable(scopes, this._responseTarget.VariableScopeName, this._responseTarget.VariableName, responseValue);
    }
}
