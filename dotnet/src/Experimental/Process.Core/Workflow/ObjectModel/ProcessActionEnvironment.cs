// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.ObjectModel;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Builder for converting CPS Topic ObjectModel YAML definition in a process.
/// </summary>
public sealed class ProcessActionEnvironment
{
    /// <summary>
    /// %%%
    /// </summary>
    internal static ProcessActionEnvironment Default { get; } = new();

    /// <summary>
    /// %%%
    /// </summary>
    public int MaximumExpressionLength { get; init; } = 3000;

    /// <summary>
    /// %%%
    /// </summary>
    /// <param name="activity"></param>
    /// <returns></returns>
    public Task ActivityNotificationHandler(ActivityTemplateBase activity)
    {
        Console.WriteLine($"\nACTIVITY: {activity.GetType().Name}");

        if (activity is MessageActivityTemplate messageActivity)
        {
            //Console.WriteLine($"\t{messageActivity.Summary}"); // %%% REMOVE
            Console.WriteLine(string.Concat(messageActivity.Text.Select(t => t.ToString())) + Environment.NewLine);
        }

        return Task.CompletedTask;
    }
}
