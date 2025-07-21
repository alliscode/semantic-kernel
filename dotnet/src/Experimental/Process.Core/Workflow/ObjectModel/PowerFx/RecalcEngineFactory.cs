﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace Microsoft.SemanticKernel.Process.Workflows.PowerFx;

internal static class RecalcEngineFactory
{
    public static RecalcEngine Create(ProcessActionScopes scopes, int maximumExpressionLength)
    {
        RecalcEngine engine = new(CreateConfig());

        SetScope(ActionScopeType.Topic);
        SetScope(ActionScopeType.Global);
        SetScope(ActionScopeType.System);

        return engine;

        void SetScope(ActionScopeType scope)
        {
            RecordValue record = scopes.BuildRecord(scope);
            engine.UpdateVariable(scope.Name, record);
        }

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
