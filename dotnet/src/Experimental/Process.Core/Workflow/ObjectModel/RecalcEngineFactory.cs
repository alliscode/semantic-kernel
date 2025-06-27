// Copyright (c) Microsoft. All rights reserved.

using Microsoft.PowerFx;

namespace Microsoft.SemanticKernel.Process.Workflows;

internal static class RecalcEngineFactory
{
    public static RecalcEngine Create(int maximumExpressionLength)
    {
        return new(CreateConfig());

        PowerFxConfig CreateConfig()
        {
            PowerFxConfig config =
                new(Features.PowerFxV1)
                {
                    MaximumExpressionLength = maximumExpressionLength
                };

            config.EnableSetFunction();

            return config;
        }
    }
}
