#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using NS of Plan
namespace Microsoft.SemanticKernel.Planners;
#pragma warning restore IDE0130

/// <summary>
/// Configuration for Stepwise planner instances.
/// </summary>
public sealed class JavascriptPlannerConfig : PlannerConfigBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionCallingStepwisePlannerConfig"/>
    /// </summary>
    public JavascriptPlannerConfig()
    {
        this.MaxTokens = 4000;
    }
}
