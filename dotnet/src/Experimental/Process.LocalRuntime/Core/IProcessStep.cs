// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Process.LocalRuntime.Core;


/// <summary>
/// Interface for all step types in the simplified process architecture.
/// </summary>
internal interface IProcessStep
{
    /// <summary>
    /// The unique identifier of the step.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The name of the step.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the step with the provided message.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="context">The process context.</param>
    /// <returns>A task representing the step execution.</returns>
    Task ExecuteAsync(StepMessage message, ProcessContext context);
}