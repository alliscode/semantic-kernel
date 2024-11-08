// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using ProcessWithDapr.Options;

namespace ProcessWithDapr.Extensions;

internal static class SemanticKernelExtension
{
    public const string SKClientName = "SKClient";

    /// <summary>
    /// Add Semantic Kernel services
    /// </summary>
    internal static IServiceCollection AddSemanticKernelServices(this IServiceCollection services, ConfigurationManager config)
    {
        var kernelBuilder = services.AddKernel();
        var httpClientFactory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        var openAIOptions = config.GetSection(OpenAIOptions.PropertyName).Get<OpenAIOptions>();
        var azureOpenAIOptions = config.GetSection(AzureOpenAIOptions.PropertyName).Get<AzureOpenAIOptions>();

        if (openAIOptions is not null && openAIOptions.IsEnabled)
        {
            ConfigureOpenAI(kernelBuilder, openAIOptions, httpClientFactory);
        }
        else if (azureOpenAIOptions is not null && azureOpenAIOptions.IsEnabled)
        {
            ConfigureAzureOpenAI(kernelBuilder, azureOpenAIOptions, httpClientFactory);
        }

        return services;
    }

    private static void ConfigureOpenAI(IKernelBuilder kernelBuilder, OpenAIOptions openAIOptions, IHttpClientFactory httpClientFactory)
    {
        if (!string.IsNullOrEmpty(openAIOptions.ModelId))
        {
            kernelBuilder.AddOpenAIChatCompletion(
                openAIOptions.ModelId,
                openAIOptions.ApiKey,
                httpClient: httpClientFactory.CreateClient(SKClientName));
        }

        if (!string.IsNullOrEmpty(openAIOptions.EmbeddingModelId))
        {
            kernelBuilder.AddOpenAITextEmbeddingGeneration(
                openAIOptions.EmbeddingModelId,
                openAIOptions.ApiKey,
                httpClient: httpClientFactory.CreateClient(SKClientName));
        }

        kernelBuilder.AddOpenAITextToImage(
            openAIOptions.ApiKey,
            httpClient: httpClientFactory.CreateClient(SKClientName));
    }

    private static void ConfigureAzureOpenAI(IKernelBuilder kernelBuilder, AzureOpenAIOptions azureOpenAIOptions, IHttpClientFactory httpClientFactory)
    {
        if (!string.IsNullOrEmpty(azureOpenAIOptions.ModelId))
        {
            kernelBuilder.AddAzureOpenAIChatCompletion(
                azureOpenAIOptions.ModelId,
                azureOpenAIOptions.Endpoint,
                azureOpenAIOptions.ApiKey,
                httpClient: httpClientFactory.CreateClient(SKClientName));
        }

        if (!string.IsNullOrEmpty(azureOpenAIOptions.ImageModelId))
        {
            kernelBuilder.AddAzureOpenAITextToImage(
                azureOpenAIOptions.ImageModelId,
                azureOpenAIOptions.Endpoint,
                azureOpenAIOptions.ApiKey,
                apiVersion: "2023-12-01-preview",
                httpClient: httpClientFactory.CreateClient(SKClientName));
        }

        if (!string.IsNullOrEmpty(azureOpenAIOptions.EmbeddingModelId))
        {
            kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
                azureOpenAIOptions.ModelId,
                azureOpenAIOptions.Endpoint,
                azureOpenAIOptions.ApiKey,
                httpClient: httpClientFactory.CreateClient(SKClientName));
        }
    }
}
