// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;
using Microsoft.PowerFx;

namespace Microsoft.SemanticKernel.Process.Workflows;

internal abstract class ProcessAction(ActionId id)
{
    public ActionId Id => id;

    public abstract Task HandleAsync(KernelProcessStepContext context, ProcessActionScopes scopes, RecalcEngine engine, Kernel kernel);
}
