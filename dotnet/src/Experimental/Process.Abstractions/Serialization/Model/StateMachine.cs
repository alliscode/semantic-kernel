using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkflowEngine.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WorkflowEngine.Models
{
    /// <summary>
    /// Represents a variable definition in the workflow
    /// </summary>
    public class StateMachineVariable
    {
        /// <summary>
        /// Gets or sets the name of the variable
        /// </summary>
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the variable
        /// </summary>
        [YamlMember(Alias = "type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the variable
        /// </summary>
        [YamlMember(Alias = "description")]
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents an actor in the workflow system
    /// </summary>
    public class StateMachineActor
    {
        /// <summary>
        /// Gets or sets the agent name for this actor
        /// </summary>
        [YamlMember(Alias = "agent")]
        public string Agent { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the input variable mappings for this actor
        /// </summary>
        [YamlMember(Alias = "inputs")]
        public Dictionary<string, string> Inputs { get; set; } = [];

        /// <summary>
        /// Gets or sets the output variable mappings for this actor
        /// </summary>
        [YamlMember(Alias = "outputs")]
        public Dictionary<string, string> Outputs { get; set; } = [];

        /// <summary>
        /// Gets or sets the thread name for this actor
        /// </summary>
        [YamlMember(Alias = "thread")]
        public string Thread { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-in-the-loop mode for this actor
        /// </summary>
        [YamlMember(Alias = "humanInLoopMode")]
        public string HumanInLoopMode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this actor should stream output
        /// </summary>
        [YamlMember(Alias = "streamOutput")]
        public bool StreamOutput { get; set; } = false;

        /// <summary>
        /// Gets or sets the list of events that this actor can generate
        /// </summary>
        [YamlMember(Alias = "events")]
        public List<string> Events { get; set; } = [];
    }

    /// <summary>
    /// Represents a state in the workflow
    /// </summary>
    public class StateMachineState
    {
        /// <summary>
        /// Gets or sets the name of the state
        /// </summary>
        [YamlMember(Alias = "Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of actors associated with this state
        /// </summary>
        [YamlMember(Alias = "Actors")]
        public List<StateMachineActor> Actors { get; set; } = [];
    }

    /// <summary>
    /// Represents a transition between states
    /// </summary>
    public class StateMachineTransition
    {
        /// <summary>
        /// Gets or sets the source state name for this transition
        /// </summary>
        [YamlMember(Alias = "from")]
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target state name for this transition
        /// </summary>
        [YamlMember(Alias = "to")]
        public string To { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional condition
        /// </summary>
        [YamlMember(Alias = "condition")]
        public string? Condition { get; set; }
    }

    /// <summary>
    /// Root workflow definition containing all workflow components
    /// </summary>
    public class StateMachineDefinition
    {
        /// <summary>
        /// Gets or sets the list of variables defined in this workflow
        /// </summary>
        [YamlMember(Alias = "Variables")]
        public List<StateMachineVariable> Variables { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of states defined in this workflow
        /// </summary>
        [YamlMember(Alias = "States")]
        public List<StateMachineState> States { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of transitions defined in this workflow
        /// </summary>
        [YamlMember(Alias = "Transitions")]
        public List<StateMachineTransition> Transitions { get; set; } = [];

        /// <summary>
        /// The starting state name
        /// </summary>
        [YamlMember(Alias = "startstate")]
        public string StartState { get; set; } = string.Empty;
    }
}

namespace WorkflowEngine.Serialization
{
    /// <summary>
    /// Service for serializing and deserializing workflow definitions
    /// </summary>
    public class WorkflowSerializer
    {
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        /// <summary>
        /// Initializes a new instance of the WorkflowSerializer class
        /// </summary>
        public WorkflowSerializer()
        {
            this._serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            this._deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        /// <summary>
        /// Deserializes YAML content to a WorkflowDefinition object
        /// </summary>
        /// <param name="yamlContent">YAML content as string</param>
        /// <returns>Deserialized WorkflowDefinition</returns>
        /// <exception cref="ArgumentException">Thrown when yamlContent is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when deserialization fails</exception>
        public StateMachineDefinition DeserializeFromYaml(string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                throw new ArgumentException("YAML content cannot be null or empty", nameof(yamlContent));
            }

            try
            {
                return this._deserializer.Deserialize<StateMachineDefinition>(yamlContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to deserialize YAML content: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deserializes YAML from a file to a WorkflowDefinition object
        /// </summary>
        /// <param name="filePath">Path to the YAML file</param>
        /// <returns>Deserialized WorkflowDefinition</returns>
        /// <exception cref="ArgumentException">Thrown when filePath is null or empty</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="InvalidOperationException">Thrown when reading or deserialization fails</exception>
        public async Task<StateMachineDefinition> DeserializeFromFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            try
            {
                var yamlContent = await this.ReadAllTextAsync(filePath).ConfigureAwait(false);
                return this.DeserializeFromYaml(yamlContent);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException($"Failed to read and deserialize file '{filePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Serializes a WorkflowDefinition object to YAML string
        /// </summary>
        /// <param name="workflow">WorkflowDefinition to serialize</param>
        /// <returns>YAML string representation</returns>
        /// <exception cref="ArgumentNullException">Thrown when workflow is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when serialization fails</exception>
        public string SerializeToYaml(StateMachineDefinition workflow)
        {
            if (workflow == null)
            {
                throw new ArgumentNullException(nameof(workflow));
            }

            try
            {
                return this._serializer.Serialize(workflow);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to serialize workflow to YAML: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Serializes a WorkflowDefinition object to a YAML file
        /// </summary>
        /// <param name="workflow">WorkflowDefinition to serialize</param>
        /// <param name="filePath">Output file path</param>
        /// <exception cref="ArgumentNullException">Thrown when workflow is null</exception>
        /// <exception cref="ArgumentException">Thrown when filePath is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when serialization or file writing fails</exception>
        public async Task SerializeToFileAsync(StateMachineDefinition workflow, string filePath)
        {
            if (workflow == null)
            {
                throw new ArgumentNullException(nameof(workflow));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            try
            {
                var yamlContent = this.SerializeToYaml(workflow);
                await this.WriteAllTextAsync(filePath, yamlContent).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException($"Failed to serialize workflow to file '{filePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reads all text from a file asynchronously (compatible with .NET Standard 2.0)
        /// </summary>
        /// <param name="path">The file path to read from</param>
        /// <returns>The file content as a string</returns>
        private async Task<string> ReadAllTextAsync(string path)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Writes all text to a file asynchronously (compatible with .NET Standard 2.0)
        /// </summary>
        /// <param name="path">The file path to write to</param>
        /// <param name="contents">The content to write</param>
        private async Task WriteAllTextAsync(string path, string contents)
        {
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                await writer.WriteAsync(contents).ConfigureAwait(false);
            }
        }
    }
}

namespace WorkflowEngine.Extensions
{
    /// <summary>
    /// Extension methods for working with workflow definitions
    /// </summary>
    public static class WorkflowDefinitionExtensions
    {
        /// <summary>
        /// Gets all transitions from a specific state
        /// </summary>
        /// <param name="workflow">The workflow definition to search</param>
        /// <param name="stateName">The name of the source state</param>
        /// <returns>Collection of transitions originating from the specified state</returns>
        public static IEnumerable<StateMachineTransition> GetTransitionsFrom(this StateMachineDefinition workflow, string stateName)
        {
            return workflow.Transitions.Where(t => t.From.Equals(stateName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets all transitions to a specific state
        /// </summary>
        /// <param name="workflow">The workflow definition to search</param>
        /// <param name="stateName">The name of the target state</param>
        /// <returns>Collection of transitions targeting the specified state</returns>
        public static IEnumerable<StateMachineTransition> GetTransitionsTo(this StateMachineDefinition workflow, string stateName)
        {
            return workflow.Transitions.Where(t => t.To.Equals(stateName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
