// Copyright (c) Microsoft. All rights reserved.

namespace ProcessWithDapr.Options;

internal class AzureOpenAIOptions
{
    public const string PropertyName = "AIServices:AzureOpenAI";

    public string ModelId { get; set; } = string.Empty;

    public string ImageModelId { get; set; } = string.Empty;

    public string EmbeddingModelId { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
}
