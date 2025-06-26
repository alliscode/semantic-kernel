// Copyright (c) Microsoft. All rights reserved.

using Microsoft.PowerFx;

namespace Microsoft.SemanticKernel;

internal static class EngineFactory
{
    private static readonly PowerFxConfig s_defaultConfig = CreateDefaultConfig();

    public static RecalcEngine CreateDefault() => new(s_defaultConfig);

    private static PowerFxConfig CreateDefaultConfig()
    {
        PowerFxConfig config =
            new(Features.PowerFxV1)
            {
                MaximumExpressionLength = 2000
            };

        config.EnableSetFunction();

        return config;
    }
}
