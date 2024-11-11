// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ProcessWithDapr.Config;

namespace ProcessWithDapr.Processes.Chat;

public class ChatProcess
{
    public static class InputEvents
    {
        public const string SubmitUserMessage = "SubmitUserMessage";
    }

    public static class OutputEvents
    {
        public const string ResponseAvailable = "ResponseAvailable";
    }

    public ChatProcess()
    {
    }

    /// <summary>
    /// Creates a new instance of the Chat Process.
    /// </summary>
    /// <returns>A new Chat Process as an instance of <see cref="KernelProcess"/>.</returns>
    public static KernelProcess NewProcess()
    {
        // Create the process builder.
        ProcessBuilder processBuilder = new("ChatProcess");

        // Add the tech help step.
        var techHelpStep = processBuilder.AddStepFromType<TechHelpStep>(nameof(TechHelpStep));

        processBuilder.OnInputEvent(InputEvents.SubmitUserMessage)
            .SendEventTo(new ProcessFunctionTargetBuilder(techHelpStep, "ProcessInput"));

        // Build the process.
        return processBuilder.Build();
    }
}

//################## Router ##################

public class MessageRouter : KernelProcessStep
{
    public MessageRouter()
    {
    }

    public Task RouteMessageAsync(string message)
    {
        // Route the message to the appropriate step.
        return Task.CompletedTask;
    }
}

[DataContract]
public class MessageRouterState
{
    [DataMember]
    public ChatHistory ChatHistory { get; set; }
}

//################## Tech help ##################

public enum TechHelpEvents
{
    TechHelpResponse
}

public class TechHelpStep : KernelProcessStep<TechHelpState>
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly TechHelpOptions _config;
    private TechHelpState _state = new();
    private ChatHistory _chatHistory = new();

    public TechHelpStep(Kernel kernel,  IOptions<TechHelpOptions> configOptions)
    {
        this._kernel = kernel;
        this._chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        this._config = configOptions.Value;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<TechHelpState> state)
    {
        this._state = state.State!;


        // Initialize the ChatHistory if it is not already initialized.
        if (this._state.ChatHistory == null)
        {
            this._chatHistory = new ChatHistory(this._config.SystemMessage);
            this._state.ChatHistory = JsonSerializer.Serialize(this._chatHistory);
        }
        else
        {
            this._chatHistory = JsonSerializer.Deserialize<ChatHistory>(this._state.ChatHistory);
        }

        return base.ActivateAsync(state);
    }

    [KernelFunction("ProcessInput")]
    public async Task ProcessInputAsync(KernelProcessStepContext context, string message)
    {
        // Process the input message.
        this._chatHistory.AddMessage(AuthorRole.User, message);
        var response = await this._chatCompletionService.GetChatMessageContentAsync(message);
        this._chatHistory.AddMessage(AuthorRole.System, response?.ToString() ?? "");

        // Send the response event.
        await context.EmitEventAsync(new()
        {
            Id = Enum.GetName(TechHelpEvents.TechHelpResponse)!,
            Data = response
        });

        this._state.LastResponse = response?.ToString() ?? "";
        this._state.ChatHistory = JsonSerializer.Serialize(this._chatHistory);
    }
}

[DataContract]
public class TechHelpState
{
    [DataMember]
    public string ChatHistory { get; set; }

    [DataMember]
    public string LastResponse { get; set; }
}
