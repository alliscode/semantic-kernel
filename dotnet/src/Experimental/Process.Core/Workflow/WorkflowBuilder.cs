﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel;
internal class WorkflowBuilder
{
    private readonly Dictionary<string, ProcessStepBuilder> _stepBuilders = [];
    private readonly Dictionary<string, CloudEvent> _inputEvents = [];

    public async Task<KernelProcess?> BuildProcessAsync(Workflow workflow)
    {
        var stepBuilders = new Dictionary<string, ProcessStepBuilder>();

        if (workflow.Nodes is null || workflow.Nodes.Count == 0)
        {
            throw new ArgumentException("Workflow nodes are not specified.");
        }

        if (workflow.Inputs is null)
        {
            throw new ArgumentException("Workflow inputs are not specified.");
        }

        // TODO: Process metadata
        // TODO: Process inputs
        // TODO: Process outputs
        // TODO: Process variables
        // TODO: Process schemas

        ProcessBuilder processBuilder = new(workflow.Name);

        if (workflow.Inputs.Events?.CloudEvents is not null)
        {
            foreach (CloudEvent inputEvent in workflow.Inputs.Events.CloudEvents)
            {
                await this.AddInputEventAsync(inputEvent, processBuilder).ConfigureAwait(false);
            }
        }

        // Process the nodes
        foreach (var step in workflow.Nodes)
        {
            await this.AddStepAsync(step, processBuilder).ConfigureAwait(false);
        }

        // Process the orchestration
        if (workflow.Orchestration is not null)
        {
            await this.BuildOrchestrationAsync(workflow.Orchestration, processBuilder).ConfigureAwait(false);
        }

        return processBuilder.Build();
    }

    #region Inputs

    private Task AddInputEventAsync(CloudEvent inputEvent, ProcessBuilder processBuilder)
    {
        this._inputEvents[inputEvent.Type] = inputEvent;
        return Task.CompletedTask;
    }

    #endregion

    #region Nodes and Steps

    internal async Task AddStepAsync(Node node, ProcessBuilder processBuilder)
    {
        Verify.NotNull(node);

        if (node.Type == "dotnet")
        {
            await this.BuildDotNetStepAsync(node, processBuilder).ConfigureAwait(false);
        }
        else if (node.Type == "python")
        {
            await this.BuildPythonStepAsync(node, processBuilder).ConfigureAwait(false);
        }
        else if (node.Type == "declarative")
        {
            await this.BuildDeclarativeStepAsync(node, processBuilder).ConfigureAwait(false);
        }
        else
        {
            throw new ArgumentException($"Unsupported node type: {node.Type}");
        }
    }

    private Task<KernelProcessStepInfo> BuildDeclarativeStepAsync(Node node, ProcessBuilder processBuilder)
    {
        throw new NotImplementedException();
    }

    private Task<KernelProcessStepInfo> BuildPythonStepAsync(Node node, ProcessBuilder processBuilder)
    {
        throw new KernelException("Python nodes are not supported in the dotnet runtime.");
    }

    private Task BuildDotNetStepAsync(Node node, ProcessBuilder processBuilder)
    {
        Verify.NotNull(node);

        if (node.Agent is null || string.IsNullOrEmpty(node.Agent.Type) || string.IsNullOrEmpty(node.Agent.Id))
        {
            throw new ArgumentException($"The agent specified in the Node with id {node.Id} is not fully specified.");
        }

        // For dotnet node type, the agent type specifies the assembly qualified namespace of the class to be executed.
        Type? dotnetAgentType = null;
        try
        {
            dotnetAgentType = Type.GetType(node.Agent.Type);
        }
        catch (TypeLoadException tle)
        {
            throw new KernelException($"Failed to load the agent for node with id {node.Id}.", tle);
        }

        if (dotnetAgentType == null)
        {
            throw new KernelException("The agent type specified in the node is not found.");
        }

        var stepBuilder = processBuilder.AddStepFromType(dotnetAgentType, id: node.Agent.Id);
        this._stepBuilders[node.Id] = stepBuilder;
        return Task.CompletedTask;
    }

    #endregion

    #region Orchestration

    private Task BuildOrchestrationAsync(List<OrchestrationStep> orchestrationSteps, ProcessBuilder processBuilder)
    {
        // If there are no orchestration steps, return
        if (orchestrationSteps.Count == 0)
        {
            return Task.CompletedTask;
        }

        // Process the orchestration steps
        foreach (var step in orchestrationSteps)
        {
            ListenCondition? listenCondition = step.ListenFor;
            if (listenCondition is null)
            {
                throw new ArgumentException("A complete listen_for condition is required for orchestration steps.");
            }

            List<ThenAction>? thenActions = step.Then;
            if (thenActions is null || thenActions.Count == 0)
            {
                throw new ArgumentException("At least one then action is required for orchestration steps.");
            }

            ProcessStepEdgeBuilder? edgeBuilder = null;

            if (listenCondition.AllOf != null && listenCondition.AllOf.Count > 0)
            {
                MessageSourceBuilder GetSourceBuilder(ListenEvent listenEvent)
                {
                    var sourceBuilder = this.FindSourceBuilder(new() { Event = listenEvent.Event, From = listenEvent.From }, processBuilder);
                    return new MessageSourceBuilder
                    {
                        Source = this._stepBuilders[listenEvent.From],
                        Type = listenEvent.Event
                    };
                }

                // Handle AllOf condition
                edgeBuilder = processBuilder.ListenFor().AllOf(listenCondition.AllOf.Select(c => GetSourceBuilder(c)).ToList());
            }
            else if (!string.IsNullOrWhiteSpace(listenCondition.Event) && !string.IsNullOrWhiteSpace(listenCondition.From))
            {
                // Find the source of the edge, it could either be a step, or an input event.
                if (this._stepBuilders.TryGetValue(listenCondition.From, out ProcessStepBuilder? sourceStepBuilder))
                {
                    // The source is a step.
                    edgeBuilder = sourceStepBuilder.OnEvent(listenCondition.Event);
                }
                else if (listenCondition.From.Equals("$.inputs.events", StringComparison.OrdinalIgnoreCase) && this._inputEvents.ContainsKey(listenCondition.Event))
                {
                    // The source is an input event.
                    edgeBuilder = processBuilder.OnInputEvent(listenCondition.Event);
                }
                else
                {
                    throw new ArgumentException($"An orchestration is referencing a node with Id {listenCondition.From} that does not exist.");
                }
            }
            else
            {
                throw new ArgumentException("A complete listen_for condition is required for orchestration steps.");
            }

            // Now that we have a validated edge source, we can add the then actions
            foreach (var action in thenActions)
            {
                if (action is null || string.IsNullOrWhiteSpace(action.Node))
                {
                    throw new ArgumentException("A complete then action is required for orchestration steps.");
                }

                if (!this._stepBuilders.TryGetValue(action.Node, out ProcessStepBuilder? destinationStepBuilder))
                {
                    throw new ArgumentException($"An orchestration is referencing a node with Id {action.Node} that does not exist.");
                }

                // Add the edge to the node
                edgeBuilder = edgeBuilder.SendEventTo(new(destinationStepBuilder));
            }
        }

        return Task.CompletedTask;
    }

    #endregion

    #region FromProcess

    public static Task<Workflow> BuildWorkflow(KernelProcess process)
    {
        Verify.NotNull(process);

        Workflow workflow = new Workflow();
        workflow.Nodes = new List<Node>();

        var orchestration = new List<OrchestrationStep>();
        var steps = process.Steps;
        foreach (var step in steps)
        {
            workflow.Nodes?.Add(BuildNode(step, orchestration));
        }

        workflow.Orchestration = orchestration;
        return Task.FromResult(workflow);
    }

    private static Node BuildNode(KernelProcessStepInfo step, List<OrchestrationStep> orchestrationSteps)
    {
        var node = new Node()
        {
            Id = step.State?.Id,
            Type = "dotnet",
            Agent = new WorkflowAgent()
            {
                Type = step.InnerStepType.AssemblyQualifiedName,
                Id = step.State.Id
            }
        };

        foreach (var edge in step.Edges)
        {
            OrchestrationStep orchestrationStep = new()
            {
                ListenFor = new ListenCondition()
                {
                    From = step.State.Id,
                    Event = edge.Key
                },
                Then = [.. edge.Value.Select(e => new ThenAction()
                {
                    Node = e.OutputTarget.StepId
                })]
            };

            orchestrationSteps.Add(orchestrationStep);
        }

        return node;
    }

    /// <summary>
    /// Find the source of the edge, it could either be a step, or an input event.
    /// </summary>
    /// <param name="listenCondition"></param>
    /// <param name="processBuilder"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private ProcessStepEdgeBuilder FindSourceBuilder(ListenEvent listenCondition, ProcessBuilder processBuilder)
    {
        Verify.NotNull(listenCondition);

        ProcessStepEdgeBuilder? edgeBuilder = null;

        // Find the source of the edge, it could either be a step, or an input event.
        if (this._stepBuilders.TryGetValue(listenCondition.From, out ProcessStepBuilder? sourceStepBuilder))
        {
            // The source is a step.
            edgeBuilder = sourceStepBuilder.OnEvent(listenCondition.Event);
        }
        else if (listenCondition.From.Equals("$.inputs.events", StringComparison.OrdinalIgnoreCase) && this._inputEvents.ContainsKey(listenCondition.Event))
        {
            // The source is an input event.
            edgeBuilder = processBuilder.OnInputEvent(listenCondition.Event);
        }
        else
        {
            throw new ArgumentException($"An orchestration is referencing a node with Id {listenCondition.From} that does not exist.");
        }

        return edgeBuilder;
    }

    #endregion
}
