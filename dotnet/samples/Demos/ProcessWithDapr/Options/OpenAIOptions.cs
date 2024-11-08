// Copyright (c) Microsoft. All rights reserved.

namespace ProcessWithDapr.Options;

/// <summary>
/// Represents the configuration options for OpenAI services.
/// </summary>
internal class OpenAIOptions
{
    /// <summary>
    /// The property name used in configuration files.
    /// </summary>
    public const string PropertyName = "AIServices:OpenAI";

    /// <summary>
    /// Gets or sets the model identifier.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the embedding model identifier.
    /// </summary>
    public string EmbeddingModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key for accessing OpenAI services.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the OpenAI services are enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}
