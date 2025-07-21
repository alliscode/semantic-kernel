﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;

namespace Microsoft.SemanticKernel.Process.Workflows;

internal sealed record class ProcessActionContext(RecalcEngine Engine, ProcessActionScopes Scopes, Kernel Kernel);

internal abstract class ProcessAction<TAction>(TAction model) : ProcessAction(model) where TAction : DialogAction
{
    public new TAction Model => (TAction)base.Model;
}

internal abstract class ProcessAction(DialogAction model)
{
    public ActionId Id => model.Id;

    public DialogAction Model => model;

    public async Task ExecuteAsync(ProcessActionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Execute each action in the current context
            await this.HandleAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (ProcessWorkflowException exception)
        {
            Console.WriteLine($"*** ACTION [{this.Id}] ERROR - {exception.GetType().Name}\n{exception.Message}"); // %%% DEVTRACE
            throw;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"*** ACTION [{this.Id}] ERROR - {exception.GetType().Name}\n{exception.Message}"); // %%% DEVTRACE
            throw new ProcessWorkflowException($"Unexpected failure executing action #{this.Id} [{this.GetType().Name}]", exception);
        }
    }

    protected abstract Task HandleAsync(ProcessActionContext context, CancellationToken cancellationToken);
}
