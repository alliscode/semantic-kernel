// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.SemanticKernel.Process.LocalRuntime.Core;

/// <summary>
/// Processes edge groups for AllOf functionality, accumulating parameters from multiple sources.
/// </summary>
internal sealed class EdgeGroupProcessor
{
    private readonly string _groupId;
    private readonly HashSet<string> _requiredMessages;
    private readonly HashSet<string> _absentMessages;
    private readonly Dictionary<string, object?> _messageData;
    private readonly Func<Dictionary<string, object?>, IReadOnlyDictionary<string, object?>>? _inputMapping;

    /// <summary>
    /// Gets the unique identifier for this edge group.
    /// </summary>
    public string GroupId => this._groupId;

    /// <summary>
    /// Gets the accumulated message data.
    /// </summary>
    public Dictionary<string, object?> MessageData => this._messageData;

    /// <summary>
    /// Initializes a new instance of the EdgeGroupProcessor.
    /// </summary>
    /// <param name="groupId">The unique group identifier.</param>
    /// <param name="requiredMessageKeys">The set of required message keys.</param>
    /// <param name="inputMapping">Optional function to transform accumulated data.</param>
    public EdgeGroupProcessor(
        string groupId,
        IEnumerable<string> requiredMessageKeys,
        Func<Dictionary<string, object?>, IReadOnlyDictionary<string, object?>>? inputMapping = null)
    {
        this._groupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
        this._requiredMessages = requiredMessageKeys != null ? new HashSet<string>(requiredMessageKeys) : throw new ArgumentNullException(nameof(requiredMessageKeys));
        this._absentMessages = new HashSet<string>(this._requiredMessages);
        this._messageData = new Dictionary<string, object?>();
        this._inputMapping = inputMapping;
    }

    /// <summary>
    /// Attempts to process a message and determine if all required messages have been received.
    /// </summary>
    /// <param name="message">The step message to process.</param>
    /// <param name="result">The result parameters if all messages are available.</param>
    /// <returns>True if all required messages have been received; otherwise, false.</returns>
    public bool TryGetResult(StepMessage message, out Dictionary<string, object?> result)
    {
        result = new Dictionary<string, object?>();

        // Use SourceId as the message key - it already contains the correct format (StepName.EventName)
        var messageKey = message.SourceId;


        // Store the message data
        this._messageData[messageKey] = message.Data;

        // Mark this message as received
        this._absentMessages.Remove(messageKey);


        // Check if all required messages have been received
        if (this._absentMessages.Count > 0)
        {
            return false; // Still waiting for more messages
        }

        // All messages received - prepare result
        if (this._inputMapping != null)
        {
            // Use custom mapping function
            var mappedResult = this._inputMapping(this._messageData);
            result = mappedResult.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        else
        {
            // Use direct mapping
            result = new Dictionary<string, object?>(this._messageData);
        }

        return true;
    }

    /// <summary>
    /// Rehydrates the processor state from stored data.
    /// </summary>
    /// <param name="messageData">The previously stored message data.</param>
    public void RehydrateFromStorage(Dictionary<string, object?> messageData)
    {
        this._messageData.Clear();
        this._absentMessages.Clear();
        this._absentMessages.UnionWith(this._requiredMessages);

        foreach (var kvp in messageData)
        {
            this._messageData[kvp.Key] = kvp.Value;
            this._absentMessages.Remove(kvp.Key);
        }
    }

    /// <summary>
    /// Checks if all required messages have been received.
    /// </summary>
    /// <returns>True if complete; otherwise, false.</returns>
    public bool IsComplete => this._absentMessages.Count == 0;

    /// <summary>
    /// Gets the current completion status.
    /// </summary>
    /// <returns>A tuple containing total required and received counts.</returns>
    public (int Required, int Received) GetStatus()
    {
        return (this._requiredMessages.Count, this._requiredMessages.Count - this._absentMessages.Count);
    }
}
