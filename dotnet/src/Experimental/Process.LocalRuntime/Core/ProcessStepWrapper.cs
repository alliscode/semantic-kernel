// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.SemanticKernel.Process.LocalRuntime.Core;


/// <summary>
/// Wrapper that allows a sub-process to be executed as a step within a parent process.
/// </summary>
internal sealed class ProcessStepWrapper : IProcessStep, IAsyncDisposable
{
    private readonly KernelProcess _processDefinition;
    private readonly ProcessContext _parentContext;
    private readonly ProcessOrchestrator _orchestrator;
    private readonly ILogger _logger;

    public ProcessStepWrapper(KernelProcess processDefinition, ProcessContext parentContext)
    {
        Verify.NotNull(processDefinition);
        Verify.NotNull(parentContext);

        this._processDefinition = processDefinition;
        this._parentContext = parentContext;

        // Create child context
        var childContext = parentContext.CreateChildContext(
            processDefinition.State.RunId ?? $"{parentContext.ProcessId}_{Guid.NewGuid()}");

        this._orchestrator = new ProcessOrchestrator(processDefinition, childContext);
        this._logger = parentContext.Kernel.LoggerFactory?.CreateLogger<ProcessStepWrapper>()
            ?? new NullLogger<ProcessStepWrapper>();
    }

    public string Id => this._processDefinition.State.RunId
        ?? throw new InvalidOperationException("Process step must have a RunId");

    public string Name => this._processDefinition.State.StepId
        ?? throw new InvalidOperationException("Process step must have a StepId");

    public async Task ExecuteAsync(StepMessage message, ProcessContext context)
    {
        if (string.IsNullOrWhiteSpace(message.TargetEventId))
        {
            throw new KernelException("Sub-process steps require a target event ID.");
        }

        try
        {
            // Convert step message to kernel process event
            var nestedEvent = new KernelProcessEvent
            {
                Id = message.TargetEventId,
                Data = message.Data,
                Visibility = KernelProcessEventVisibility.Internal // Internal to prevent re-emission
            };

            this._logger.LogDebug("Executing sub-process '{ProcessName}' with event '{EventId}'",
                this.Name, nestedEvent.Id);

            // Execute the sub-process synchronously (maintains current behavior)
            await this._orchestrator.ExecuteOnceAsync(nestedEvent).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error executing sub-process '{ProcessName}'", this.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets the step info for this sub-process.
    /// </summary>
    /// <returns>The KernelProcess representing this sub-process.</returns>
    public async Task<KernelProcessStepInfo> GetStepInfoAsync()
    {
        return await this._orchestrator.GetProcessInfoAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a message to the sub-process.
    /// </summary>
    /// <param name="processEvent">The event to send.</param>
    /// <returns>A task representing the message sending.</returns>
    public async Task SendMessageAsync(KernelProcessEvent processEvent)
    {
        await this._orchestrator.SendMessageAsync(processEvent).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await this._orchestrator.DisposeAsync().ConfigureAwait(false);
    }
}
