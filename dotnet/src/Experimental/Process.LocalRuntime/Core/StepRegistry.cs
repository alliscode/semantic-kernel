// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.SemanticKernel.Process.LocalRuntime.Core;


/// <summary>
/// Registry for managing process steps.
/// </summary>
internal sealed class StepRegistry
{
    private readonly Dictionary<string, IProcessStep> _steps = new();

    /// <summary>
    /// Registers a step in the registry.
    /// </summary>
    /// <param name="step">The step to register.</param>
    public void RegisterStep(IProcessStep step)
    {
        this._steps[step.Name] = step;
        // Also register by Id if different from Name
        if (step.Id != step.Name)
        {
            this._steps[step.Id] = step;
        }
    }

    /// <summary>
    /// Gets a step by its name.
    /// </summary>
    /// <param name="stepName">The name of the step.</param>
    /// <returns>The step, or null if not found.</returns>
    public IProcessStep? GetStep(string stepName)
    {
        return this._steps.TryGetValue(stepName, out var step) ? step : null;
    }

    /// <summary>
    /// Gets all registered steps.
    /// </summary>
    /// <returns>All registered steps.</returns>
    public IEnumerable<IProcessStep> GetAllSteps()
    {
        return this._steps.Values.ToList();
    }

    /// <summary>
    /// Checks if a step is registered.
    /// </summary>
    /// <param name="stepName">The name of the step.</param>
    /// <returns>True if the step is registered, false otherwise.</returns>
    public bool ContainsStep(string stepName)
    {
        return this._steps.ContainsKey(stepName);
    }
}