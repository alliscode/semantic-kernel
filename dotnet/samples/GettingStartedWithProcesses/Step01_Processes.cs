// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GettingStartedWithProcesses;

/// <summary>
/// Demonstrate creation of <see cref="KernelProcess"/> and
/// eliciting its response to three explicit user messages.
/// </summary>
public class Step01_Processes(ITestOutputHelper output) : BaseTest(output)
{
    [Fact]
    public async Task UseSimpleProcessAsync()
    {
        ProcessBuilder process = new("ChatBot");
        var introStep = process.AddStepFromType<IntroStep>();
        var userInputStep = process.AddStepFromType<UserInputStep>();
        var responseStep = process.AddStepFromType<ChatBotResponseStep>();

        // When the intro is complete, notify the userInput step
        introStep
            .OnEvent(ChatBotEvents.IntroComplete)
            .SendEventTo(new ProcessFunctionTargetBuilder(userInputStep, "GetUserInput"));

        // When the userInput step emits an exit event, send it to the end step
        userInputStep
            .OnFunctionResult("GetUserInput")
            .StopProcess();

        // When the userInput step emits a user input event, send it to the assistantResponse step
        userInputStep
            .OnEvent(ChatBotEvents.UserInputReceived)
            .SendEventTo(new ProcessFunctionTargetBuilder(responseStep, "GetChatResponse", "userMessage"));

        // When the assistantResponse step emits a response, send it to the userInput step
        responseStep
            .OnEvent(ChatBotEvents.AssistantResponseGenerated)
            .SendEventTo(new ProcessFunctionTargetBuilder(userInputStep, "GetUserInput"));

        KernelProcess kernelProcess = process.Build();
    }

    public class IntroStep : ProcessStep
    {
        [KernelFunction()]
        public void PrintIntroMessage()
        {
        }
    }

    public class UserInputStep : ProcessStep<UserInputState>
    {
        private UserInputState? _state;

        public override ValueTask ActivateAsync(ProcessStepState<UserInputState> state)
        {
            state.State ??= new();
            _state = state.State;

            _state.UserInputs.Add("Hello");
            _state.UserInputs.Add("How are you?");
            _state.UserInputs.Add("exit");

            return ValueTask.CompletedTask;
        }

        [KernelFunction("GetUserInput")]
        public async ValueTask GetUserInputAsync(ProcessStepContext context)
        {
            var input = _state!.UserInputs[_state.CurrentInputIndex];
            _state.CurrentInputIndex++;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                // Emit the exit event
                await context.EmitEventAsync(new() { Id = ChatBotEvents.Exit });
                return;
            }

            // Emit the user input
            await context.EmitEventAsync(new() { Id = ChatBotEvents.UserInputReceived, Data = input });
        }
    }

    public class ChatBotResponseStep : ProcessStep<ChatBotState>
    {
        internal ChatBotState? _state;

        public override ValueTask ActivateAsync(ProcessStepState<ChatBotState> state)
        {
            _state = state.State ?? new();
            _state.ChatMessages ??= new();
            return ValueTask.CompletedTask;
        }

        [KernelFunction("GetChatResponse")]
        public async Task GetChatResponseAsync(ProcessStepContext context, string userMessage, Kernel _kernel)
        {
            _state!.ChatMessages.Add(new(AuthorRole.User, userMessage));
            IChatCompletionService chatService = _kernel.Services.GetRequiredService<IChatCompletionService>();
            ChatMessageContent response = await chatService.GetChatMessageContentAsync(_state.ChatMessages);
            if (response != null)
            {
                _state.ChatMessages.Add(response!);
            }

            // emit event: assistantResponse
            await context.EmitEventAsync(new ProcessEvent { Id = ChatBotEvents.AssistantResponseGenerated, Data = response });
        }
    }

    public class ChatBotState
    {
        internal ChatHistory ChatMessages { get; set; } = new();
    }

    public record UserInputState
    {
        public List<string> UserInputs { get; init; } = new();

        public int CurrentInputIndex { get; set; } = 0;
    }

    public static class ChatBotEvents
    {
        public static readonly string IntroComplete = "introComplete";
        public static readonly string UserInputReceived = "userInputReceived";
        public static readonly string AssistantResponseGenerated = "assistantResponseGenerated";
        public static readonly string Exit = "exit";
    }
}
