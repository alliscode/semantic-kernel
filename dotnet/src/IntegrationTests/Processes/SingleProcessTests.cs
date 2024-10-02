// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using SemanticKernel.IntegrationTests.Agents;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;

namespace SemanticKernel.IntegrationTests.Processes;
public sealed class SingleProcessTests
{
    private readonly IKernelBuilder _kernelBuilder = Kernel.CreateBuilder();
    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<OpenAIAssistantAgentTests>()
            .Build();

    /// <summary>
    /// Tests a linear process with no state.
    /// </summary>
    /// <returns>A <see cref="Task"/></returns>
    [Fact]
    public async Task LinearProcessAsync()
    {
        // Arrange
        OpenAIConfiguration configuration = this._configuration.GetSection("OpenAI").Get<OpenAIConfiguration>()!;
        this._kernelBuilder.AddOpenAIChatCompletion(
            modelId: configuration.ModelId!,
            apiKey: configuration.ApiKey);

        Kernel kernel = this._kernelBuilder.Build();
        var process = this.CreateLinearProcess().Build();

        // Act
        var procesHandle = await process.StartAsync(kernel, new() { Id = "Start", Data = "Go" });
        var processInfo = await procesHandle.GetInfoAsync();

        // Assert
        var repeatStepState = processInfo.Steps.Where(s => s.State.Name == nameof(RepeatStep)).Single().State as KernelProcessStepState<StepState>;
        Assert.NotNull(repeatStepState?.State);
        Assert.Equal("Go Go", repeatStepState.State.LastMessage);
    }

    /// <summary>
    /// Tests a linear process with no state.
    /// </summary>
    /// <returns>A <see cref="Task"/></returns>
    [Fact]
    public async Task NestedLinearProcessAsync()
    {
        // Arrange
        OpenAIConfiguration configuration = this._configuration.GetSection("OpenAI").Get<OpenAIConfiguration>()!;
        this._kernelBuilder.AddOpenAIChatCompletion(
            modelId: configuration.ModelId!,
            apiKey: configuration.ApiKey);

        Kernel kernel = this._kernelBuilder.Build();

        var processBuilder = new ProcessBuilder("NestedProcess");
        var echoStep = processBuilder.AddStepFromType<EchoStep>();
        var nestedStep = processBuilder.AddStepFromProcess(this.CreateLinearProcess());

        processBuilder.OnExternalEvent("Start")
            .SendEventTo(new ProcessFunctionTargetBuilder(echoStep));

        echoStep.OnFunctionResult("Echo")
            .SendEventTo(nestedStep.GetTargetForExternalEvent("Start"));

        var process = processBuilder.Build();

        // Act
        var procesHandle = await process.StartAsync(kernel, new() { Id = "Start", Data = "Go" });
        var processInfo = await procesHandle.GetInfoAsync();

        // Assert
        var innerProcess = processInfo.Steps.Where(s => s.State.Name == "LinearProcess").Single() as KernelProcess;
        Assert.NotNull(innerProcess);
        var repeatStepState = innerProcess.Steps.Where(s => s.State.Name == nameof(RepeatStep)).Single().State as KernelProcessStepState<StepState>;
        Assert.NotNull(repeatStepState?.State);
        Assert.Equal("Go Go", repeatStepState.State.LastMessage);
    }

    private ProcessBuilder CreateLinearProcess()
    {
        var processBuilder = new ProcessBuilder("LinearProcess");
        var echoStep = processBuilder.AddStepFromType<EchoStep>();
        var repeatStep = processBuilder.AddStepFromType<RepeatStep>();

        processBuilder.OnExternalEvent("Start")
            .SendEventTo(new ProcessFunctionTargetBuilder(echoStep));

        echoStep.OnFunctionResult("Echo")
            .SendEventTo(new ProcessFunctionTargetBuilder(repeatStep, parameterName: "message"));

        return processBuilder;
    }

    /// <summary>
    /// A step that echos its input.
    /// </summary>
    private sealed class EchoStep : KernelProcessStep
    {
        [KernelFunction]
        public string Echo(string message) => message;
    }

    /// <summary>
    /// A step that repeats its input.
    /// </summary>
    private sealed class RepeatStep : KernelProcessStep<StepState>
    {
        private readonly StepState _state = new();

        public override ValueTask ActivateAsync(KernelProcessStepState<StepState> state)
        {
            if (state.State is null)
            {
                state.State = this._state;
            }

            return default;
        }

        [KernelFunction]
        public string Repeat(string message, int count = 2)
        {
            var output = string.Join(" ", Enumerable.Repeat(message, count));
            this._state.LastMessage = output;
            return output;
        }
    }

    private sealed class StepState
    {
        public string? LastMessage { get; set; }
    }
}
