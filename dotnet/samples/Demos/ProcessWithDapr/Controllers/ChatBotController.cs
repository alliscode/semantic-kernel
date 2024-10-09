// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;

namespace ProcessWithDapr.Controllers;

/// <summary>
/// A controller for chatbot.
/// </summary>
[ApiController]
public class ChatBotController : ControllerBase
{
    private readonly Dictionary<string, KernelProcess> _processMap = [];
    private readonly Kernel _kernel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatBotController"/> class.
    /// </summary>
    /// <param name="kernel">An instance of <see cref="Kernel"/></param>
    public ChatBotController(Kernel kernel)
    {
        this._kernel = kernel;
    }

    [HttpGet("chatbots/{chatBotId}/{message}")]
    public async Task<IActionResult> PostAsync(string chatBotId, string message)
    {
        var process = this.GetProcess(chatBotId);
        await process.StartAsync(this._kernel, new KernelProcessEvent() { Id = CommonEvents.StartProcess, Data = "foo" });

        return this.Ok(chatBotId);
    }

    private KernelProcess GetProcess(string processId)
    {
        if (this._processMap.TryGetValue(processId, out var process))
        {
            return process;
        }

        ProcessBuilder processBuilder = new("Test Process");

        var kickoffStep = processBuilder.AddStepFromType<KickoffStep>();
        var myAStep = processBuilder.AddStepFromType<AStep>();
        var myBStep = processBuilder.AddStepFromType<BStep>();
        var myCStep = processBuilder.AddStepFromType<CStep>();

        processBuilder
            .OnInputEvent(CommonEvents.StartProcess)
            .SendEventTo(new ProcessFunctionTargetBuilder(kickoffStep));

        kickoffStep
            .OnEvent(CommonEvents.StartARequested)
            .SendEventTo(new ProcessFunctionTargetBuilder(myAStep));

        kickoffStep
            .OnEvent(CommonEvents.StartBRequested)
            .SendEventTo(new ProcessFunctionTargetBuilder(myBStep));

        myAStep
            .OnEvent(CommonEvents.AStepDone)
            .SendEventTo(new ProcessFunctionTargetBuilder(myCStep, parameterName: "astepdata"));

        myBStep
            .OnEvent(CommonEvents.BStepDone)
            .SendEventTo(new ProcessFunctionTargetBuilder(myCStep, parameterName: "bstepdata"));

        myCStep
            .OnEvent(CommonEvents.CStepDone)
            .SendEventTo(new ProcessFunctionTargetBuilder(kickoffStep));

        myCStep
            .OnEvent(CommonEvents.ExitRequested)
            .StopProcess();

        process = processBuilder.Build();
        process = process with { State = process.State with { Id = processId } };
        this._processMap[processId] = process;
        return process;
    }

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    // These classes are dynamically instantiated by the processes used in tests.

    /// <summary>
    /// Kick off step for the process.
    /// </summary>
    private sealed class KickoffStep : KernelProcessStep
    {
        public static class Functions
        {
            public const string KickOff = nameof(KickOff);
        }

        [KernelFunction(Functions.KickOff)]
        public async ValueTask PrintWelcomeMessageAsync(KernelProcessStepContext context)
        {
            await context.EmitEventAsync(new() { Id = CommonEvents.StartARequested, Data = "Get Going A" });
            await context.EmitEventAsync(new() { Id = CommonEvents.StartBRequested, Data = "Get Going B" });
        }
    }

    /// <summary>
    /// A step in the process.
    /// </summary>
    private sealed class AStep : KernelProcessStep
    {
        [KernelFunction]
        public async ValueTask DoItAsync(KernelProcessStepContext context)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await context.EmitEventAsync(new() { Id = CommonEvents.AStepDone });
        }
    }

    /// <summary>
    /// A step in the process.
    /// </summary>
    private sealed class BStep : KernelProcessStep
    {
        [KernelFunction]
        public async ValueTask DoItAsync(KernelProcessStepContext context)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            await context.EmitEventAsync(new() { Id = CommonEvents.BStepDone });
        }
    }

    /// <summary>
    /// A step in the process.
    /// </summary>
    private sealed class CStep : KernelProcessStep
    {
        private int CurrentCycle { get; set; } = 0;

        public CStep()
        {
            this.CurrentCycle = 0;
        }

        [KernelFunction]
        public async ValueTask DoItAsync(KernelProcessStepContext context, string astepdata, string bstepdata)
        {
            this.CurrentCycle++;
            if (this.CurrentCycle == 3)
            {
                // Exit the processes
                await context.EmitEventAsync(new() { Id = CommonEvents.ExitRequested });
                return;
            }

            // Cycle back to the start
            await context.EmitEventAsync(new() { Id = CommonEvents.CStepDone });
        }
    }

    /// <summary>
    /// Common Events used in the process.
    /// </summary>
    private static class CommonEvents
    {
        public const string UserInputReceived = nameof(UserInputReceived);
        public const string CompletionResponseGenerated = nameof(CompletionResponseGenerated);
        public const string WelcomeDone = nameof(WelcomeDone);
        public const string AStepDone = nameof(AStepDone);
        public const string BStepDone = nameof(BStepDone);
        public const string CStepDone = nameof(CStepDone);
        public const string StartARequested = nameof(StartARequested);
        public const string StartBRequested = nameof(StartBRequested);
        public const string ExitRequested = nameof(ExitRequested);
        public const string StartProcess = nameof(StartProcess);
    }
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
}
