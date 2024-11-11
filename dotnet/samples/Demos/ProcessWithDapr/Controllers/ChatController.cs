// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using ProcessWithDapr.Contracts;
using ProcessWithDapr.Processes.Chat;

namespace ProcessWithDapr.Controllers;

/// <summary>
/// A controller for chat.
/// </summary>
[ApiController]
public class ChatController : ControllerBase
{
    private static readonly Dictionary<string, DaprKernelProcessContext> s_processes = new();

    [HttpPost("helpDeskChat/{chatId}/messages")]
    public async Task<IActionResult> AddMessageAsync(string chatId, [FromBody] ChatMessageRequest request)
    {
        if (!s_processes.TryGetValue(chatId, out var chatProcessContext))
        {
            var chatProcess = ChatProcess.NewProcess();
            chatProcessContext = await chatProcess.StartAsync(new KernelProcessEvent() { Id = ChatProcess.InputEvents.SubmitUserMessage, Data = request.Message }, processId: chatId);
            s_processes.Add(chatId, chatProcessContext);
        }
        else
        {
            await chatProcessContext.StartWithEventAsync(new KernelProcessEvent() { Id = ChatProcess.InputEvents.SubmitUserMessage, Data = request.Message });
        }

        var state = await chatProcessContext.GetStateAsync();
        var techHelpStep = state.Steps.Where(s => s.State.Name == nameof(TechHelpStep)).FirstOrDefault();
        var techHelpState = techHelpStep.State as KernelProcessStepState<TechHelpState>;
        if (techHelpState == null)
        {
            return this.StatusCode(500);
        }

        var lastResponse = techHelpState.State.LastResponse;
        return this.Ok(lastResponse);
    }
}
