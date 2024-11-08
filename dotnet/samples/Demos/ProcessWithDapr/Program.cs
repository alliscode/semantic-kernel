// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using Microsoft.SemanticKernel;
using ProcessWithDapr.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

builder.Services.AddHttpClient(
    SemanticKernelExtension.SKClientName,
    client =>
    {
        // This client will have an extended timeout for longer processes
        client.Timeout = TimeSpan.FromSeconds(200);
    }
).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.CheckCertificateRevocationList = false;
    return handler;
});

// Configure services
builder.Services
    .AddSingleton<ILogger>(sp => sp.GetRequiredService<ILogger<Program>>())
    .AddOptions(builder.Configuration)
    .AddSemanticKernelServices(builder.Configuration)
    .AddLogging((logging) =>
    {
        logging.AddConsole();
        logging.AddDebug();
    });

// ################ Configure Dapr for Processes ################
builder.Services.AddActors(static options =>
{
    // Register the actors required to run Processes
    options.AddProcessActors();
});
// ################ Configure Dapr for Processes ################

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("############### Running in {EnvironmentName} environment. ##################", builder.Environment.EnvironmentName);

if (app.Environment.IsDevelopment())
{
    // Use the developer exception page for debugging
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    });

    // In development we use dotnet user-secrets and add them to the configuration
    builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
}
else
{
    // Configure the HTTP request pipeline.
    app.UseHttpsRedirection();
    app.UseAuthorization();
}

app.MapControllers();
app.MapActorsHandlers();
app.Run();
