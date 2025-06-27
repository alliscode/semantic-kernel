// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;

namespace Microsoft.SemanticKernel.Process.Workflows.Actions;

internal sealed class SendActivityAction : ProcessAction<SendActivity>
{
    private readonly ProcessActionEnvironment _environment;

    public SendActivityAction(SendActivity source, ProcessActionEnvironment environment)
        : base(source)
    {
        if (source.Activity is null)
        {
            throw new KernelException("SendActivity action must have an activity defined."); // %%% EXCEPTION TYPES
        }

        this._environment = environment;
    }

    public override async Task HandleAsync(KernelProcessStepContext context, ProcessActionScopes scopes, RecalcEngine engine, Kernel kernel)
    {
        Console.WriteLine($"!!! {nameof(SendActivityAction)} [{this.Id}]");

        await this._environment.ActivityNotificationHandler(this.Action.Activity!).ConfigureAwait(false); // %%% NULL OVERRIDE
    }
}
