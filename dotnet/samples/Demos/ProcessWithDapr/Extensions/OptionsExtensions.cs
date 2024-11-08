// Copyright (c) Microsoft. All rights reserved.

using ProcessWithDapr.Config;
using ProcessWithDapr.Options;

namespace ProcessWithDapr.Extensions;

internal static class OptionsExtensions
{
    public static IServiceCollection AddOptions(this IServiceCollection services, ConfigurationManager configuration)
    {
        AddOptions<OpenAIOptions>(OpenAIOptions.PropertyName);
        AddOptions<TechHelpOptions>(TechHelpOptions.PropertyName);

        return services;

        void AddOptions<TOptions>(string propertyName)
        where TOptions : class
        {
            services.AddOptions<TOptions>(configuration.GetSection(propertyName));
        }
    }

    internal static void AddOptions<TOptions>(this IServiceCollection services, IConfigurationSection section)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
