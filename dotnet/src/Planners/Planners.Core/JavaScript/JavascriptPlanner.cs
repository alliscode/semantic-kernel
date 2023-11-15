// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Json.Schema;
using Json.Schema.Generation;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.TemplateEngine;
using Microsoft.SemanticKernel.TemplateEngine.Basic;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace - Using NS of Plan
namespace Microsoft.SemanticKernel.Planners;
#pragma warning restore IDE0130

public sealed class JavascriptPlanner
{
    private readonly IKernel _kernel;
    private readonly IChatCompletion _chatCompletion;
    private readonly ILogger? _logger;
    private readonly JavascriptPlannerConfig _config;
    private readonly BasicPromptTemplateFactory _promptTemplateFactory;
    private readonly string _initialPlanPromptManifest;

    private const string RestrictedPluginName = "JavaScriptPlanner_Excluded";


    /// <summary>
    /// Initialize a new instance of the <see cref="JavascriptPlanner"/> class.
    /// </summary>
    /// <param name="kernel">The semantic kernel instance.</param>
    /// <param name="config">The planner configuration.</param>
    public JavascriptPlanner(
        IKernel kernel,
        JavascriptPlannerConfig? config = null)
    {
        Verify.NotNull(kernel);
        this._kernel = kernel;
        this._chatCompletion = kernel.GetService<IChatCompletion>();

        // Initialize prompt renderer
        this._promptTemplateFactory = new BasicPromptTemplateFactory(this._kernel.LoggerFactory);

        // Set up Config with default values and excluded plugins
        this._config = config ?? new();
        this._config.ExcludedPlugins.Add(RestrictedPluginName);

        this._initialPlanPromptManifest = EmbeddedResource.Read("JSPrompt.json");

        // Create context and logger
        this._logger = this._kernel.LoggerFactory.CreateLogger(this.GetType());
    }

    public async Task ExecuteAsync(string goal, CancellationToken cancellationToken = default)
    {
        var functionsManual = await this.GenerateFunctionDescriptions(this._kernel, cancellationToken).ConfigureAwait(false);
        var context =
                this._kernel.CreateNewContext(
                    new()
                    {
                    { "goal", goal },
                    { "functions", functionsManual },
                    });

        ChatHistory chatHistory = await this.ReadPromptManifestAsync(this._initialPlanPromptManifest, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GenerateFunctionDescriptions(IKernel kernel, CancellationToken cancellationToken)
    {
        var jsonSchemaBuilder = new JsonSchemaBuilder();
        var jsFunctionSigatures = new List<string>();

        foreach (FunctionView skFunction in this._kernel.Functions.GetFunctionViews())
        {
            dynamic plugin = new ExpandoObject();
            var jsDocBuilder = new StringBuilder();
            var tsSignatureBuilder = new StringBuilder();

            string functionName = $"{skFunction.PluginName}_{skFunction.Name}";
            tsSignatureBuilder.Append($"{functionName}(");
            jsDocBuilder.AppendLine($"/**\n * {skFunction.Description}");

            int paramNum = 0;
            var requiredProperties = new List<string>();
            foreach (var parameterView in skFunction.Parameters)
            {
                string typeName = parameterView switch
                {
                    var p when p.Schema != null => JsonSchema.FromText(p.Schema.RootElement.GetRawText()).GetJsonType()?.ToString() ?? "null",
                    var p when p.ParameterType != null => p.ParameterType.Name!,
                    _ => throw new InvalidOperationException($"Could not determind the schema for parameter with name: {parameterView.Name}.")
                };

                JsonSchema parameterSchema = parameterView switch
                {
                    var p when p.Schema != null => JsonSchema.FromText(p.Schema.RootElement.GetRawText()),
                    var p when p.ParameterType != null => jsonSchemaBuilder.FromType(p.ParameterType).Description(p.Description ?? "").Build(),
                    _ => throw new InvalidOperationException($"Could not determind the schema for parameter with name: {parameterView.Name}.")
                };

                string parameterName = parameterView.Name;


                tsSignatureBuilder.Append($"{(paramNum > 0 ? ", " : "")}{parameterName}");
                jsDocBuilder.AppendLine($" * @param {{{typeName}}} {parameterName} - {parameterView.Description}");
                paramNum++;
            }

            string returnTypeName = skFunction.ReturnParameter switch
            {
                var p when p.Schema != null => JsonSchema.FromText(p.Schema.RootElement.GetRawText()).GetJsonType()?.ToString() ?? "Null",
                var p when p.ParameterType != null => p.ParameterType.Name!,
                _ => throw new InvalidOperationException($"Could not determind the type name for return parameter.")
            };

            JsonSchema returnParameterSchema = skFunction.ReturnParameter switch
            {
                var p when p.Schema != null => JsonSchema.FromText(p.Schema.RootElement.GetRawText()),
                var p when p.ParameterType != null => jsonSchemaBuilder.FromType(p.ParameterType).Description(p.Description ?? "").Build(),
                _ => throw new InvalidOperationException($"Could not determind the schema for return parameter.")
            };

            tsSignatureBuilder.Append(')');
            jsDocBuilder.AppendLine($" * @return {JsonSerializer.Serialize(returnParameterSchema)}");
            jsDocBuilder.AppendLine(" */");
            jsFunctionSigatures.Add($"{jsDocBuilder}{tsSignatureBuilder}");

            var pluginBuilder = plugin as IDictionary<string, object>;
            var skFunctionProxy = BuildFunctionProxy(kernel, skFunction.PluginName, skFunction);
            pluginBuilder!.Add(functionName, skFunctionProxy);
        }

        string jsSignatures = string.Join("\n\n", jsFunctionSigatures);
        //return JsonSerializer.Serialize(allFunctions);
        return jsSignatures;
    }

    private Delegate BuildFunctionProxy(IKernel kernel, string pluginName, MethodInfo method)
    {
        try
        {
            var methodParmas = method.GetParameters();
            var expressionParams = methodParmas.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToList();

            var varsDict = Expression.Variable(typeof(Dictionary<string, ParamView>));
            var paramView = Expression.Variable(typeof(ParamView));
            var kernelExp = Expression.Constant(kernel);
            var loggerExp = Expression.Constant(_logger);
            var pluginNameExp = Expression.Constant(pluginName);
            var functionName = Expression.Constant(method.Name);
            var returnType = Expression.Constant(method.ReturnType);


            var invokeFunction = typeof(JavascriptPlanner).GetMethod("InvokeSkFunction", BindingFlags.Public | BindingFlags.Static)!;
            var varsDictAdd = typeof(Dictionary<string, ParamView>).GetMethod(
                name: "Add",
                bindingAttr: BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(string), typeof(ParamView) },
                modifiers: null)!;

            var body = new List<Expression>();

            // create a new dictionary and assign it to varsDict
            body.Add(Expression.Assign(varsDict, Expression.New(typeof(Dictionary<string, ParamView>))));

            for (int i = 0; i < methodParmas.Length; i++)
            {
                // Create a new ParamView and assign it to paramView
                var paramAsObject = Expression.Convert(expressionParams[i], typeof(object));
                body.Add(
                    Expression.Assign(
                        left: paramView,
                        right: Expression.New(
                            constructor: typeof(ParamView).GetConstructor(new[] { typeof(object), typeof(Type) })!,
                            arguments: new Expression[] { paramAsObject, Expression.Constant(methodParmas[i].ParameterType) })));

                // Add the paramView to the varsDict
                body.Add(Expression.Call(varsDict, varsDictAdd, Expression.Constant(methodParmas[i].Name), paramView));
            }

            // Invoke the SKFunction

            var invokeResult = Expression.Call(invokeFunction, new Expression[] { kernelExp, loggerExp, pluginNameExp, functionName, returnType, varsDict });
            body.Add(invokeResult);

            var block = Expression.Block(new[] { varsDict, paramView }, body);

            var lambda = Expression.Lambda(block, false, expressionParams);
            var proxy = lambda.Compile();
            return proxy;
        }
        catch (Exception ex)
        {

            throw;
        }
    }

    private static string SerializeObject(object? obj, Type? targetType)
    {
        // Strings just parse to themselves.
        if (obj is string)
        {
            return (string)obj;
        }

        return JsonSerializer.Serialize(obj);
    }

    public static object InvokeSkFunction(IKernel kernel, ILogger logger, string pluginName, string functionName, Type returnType, Dictionary<string, ParamView> variables)
    {
        try
        {
            logger.LogInformation("ENTERED INVOKESKFUNCTION!!");
            var contextVars = new ContextVariables();
            foreach (var kvp in variables)
            {
                contextVars.Add(kvp.Key, SerializeObject(kvp.Value.Value, kvp.Value.Type));
            }

            var function = kernel.Functions.GetFunction(pluginName, functionName);
            FunctionResult output = function.InvokeAsync(kernel.CreateNewContext(contextVars), requestSettings: null, CancellationToken.None).Result;

            object? objValue = output.GetValue<object>();
            return objValue;

            // TODO: Make it work for remote plugins without taking a dependency on OpenAI package.
            //if (objValue is RestApiOperationResponse apiResponse)
            //{
            //    //var strResponse = apiResponse.Content as string ?? string.Empty;
            //    //return strResponse;
            //    return apiResponse.Content;
            //}
            //else
            //{
            //    //var strResponse = JsonSerializer.Serialize(objValue);
            //    //return strResponse;
            //    return objValue;
            //}
        }
        catch (Exception ex)
        {

            throw;
        }
    }

    public class ParamView
    {
        public ParamView(object value, Type type)
        {
            Value = value;
            Type = type;
        }

        public object? Value { get; set; } = null;

        public Type? Type { get; set; } = null;
    }

    private async Task<ChatHistory> ReadPromptManifestAsync(string manifest, SKContext context, CancellationToken cancellationToken)
    {
        JsonSerializerOptions jsonOptions =
        new()
        {
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var chatCompletion = this._kernel.GetService<IChatCompletion>();
        var promptTemplate = chatCompletion.CreateNewChat();
        var chatMessages = JsonSerializer.Deserialize<ChatPromptMessage[]>(manifest, jsonOptions);

        foreach (var message in chatMessages!)
        {
            var formattedMessage = await this._promptTemplateFactory
                .Create(message.Content, new PromptTemplateConfig())
                .RenderAsync(context, cancellationToken).ConfigureAwait(false);

            promptTemplate.AddMessage(new AuthorRole(message.Role), formattedMessage);
        }

        return promptTemplate;
    }

    private record ChatPromptMessage(string Role, string Content);
}
