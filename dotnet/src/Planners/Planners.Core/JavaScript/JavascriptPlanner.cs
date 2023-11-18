// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Json.Schema;
using Json.Schema.Generation;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.TemplateEngine;
using Microsoft.SemanticKernel.TemplateEngine.Basic;
using Microsoft.SemanticKernel.TemplateEngine.Handlebars;

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
    private readonly Dictionary<string, object> _pluginDict = new();

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

        this._initialPlanPromptManifest = EmbeddedResource.Read("JavaScript.JSPrompt.json");

        // Create context and logger
        this._logger = this._kernel.LoggerFactory.CreateLogger(this.GetType());
    }

    /// <summary>
    /// Runs the planner.
    /// </summary>
    /// <param name="goal">The goal to be reached by the planner.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="Task"/></returns>
    public async Task<string> ExecuteAsync(string goal, CancellationToken cancellationToken = default)
    {
        try
        {
            var textToolsPlugin = new TextTools(this._kernel);
            this._kernel.ImportFunctions(textToolsPlugin, "textTools");

            var functionsManual = this.GenerateFunctionDescriptions(this._kernel, cancellationToken);
            var context =
                    this._kernel.CreateNewContext(
                        new()
                        {
                        { "goal", goal },
                        { "functions", functionsManual },
                        });

            ChatHistory chatHistory = await this.ReadPromptManifestAsync(this._initialPlanPromptManifest, context, cancellationToken).ConfigureAwait(false);
            IChatCompletion chatCompletion = this._kernel.GetService<IChatCompletion>();
            string planResultString = await chatCompletion.GenerateMessageAsync(chatHistory, cancellationToken: cancellationToken).ConfigureAwait(false);

            using var engine = new V8ScriptEngine();
            foreach (var kvp in this._pluginDict)
            {
                dynamic expando = kvp.Value;
                engine.AddHostObject(kvp.Key, expando);
            }

            engine.Execute(planResultString);
            var output = engine.Script.Run();

            var result = JsonSerializer.Serialize(output);
            return result;
        }
        catch (Exception ex)
        {

            throw;
        }
    }

    private string GenerateFunctionDescriptions(IKernel kernel, CancellationToken cancellationToken)
    {
        AttributeHandler.AddHandler(new SystemAttributeHandler());
        var jsFunctionSigatures = new List<string>();

        var functionsByPlugin = this._kernel.Functions.GetFunctionViews()
            .GroupBy(x => x.PluginName, x => x, (key, group) => new
            {
                Name = key,
                Functions = group.ToList()
            });

        var typeDefBuilder = new StringBuilder();
        foreach (var plugin in functionsByPlugin)
        {
            dynamic pluginProxy = new ExpandoObject();
            foreach (FunctionView skFunction in plugin.Functions)
            {
                var jsDocBuilder = new StringBuilder();
                var tsSignatureBuilder = new StringBuilder();

                string functionName = skFunction.Name;
                tsSignatureBuilder.Append($"{skFunction.PluginName}.{skFunction.Name}(");
                jsDocBuilder.AppendLine($"/**\n * {skFunction.Description}");

                if (functionName == "Time")
                {
                    int x = 3;
                }

                int paramNum = 0;
                var requiredProperties = new List<string>();
                foreach (var parameterView in skFunction.Parameters)
                {
                    string typeName = "";
                    JsonSchema? parameterSchema = null;
                    bool isPrimative;

                    if (parameterView.Schema is null)
                    {
                        typeName = parameterView.ParameterType!.Name;
                        parameterSchema = new JsonSchemaBuilder().FromType(parameterView.ParameterType).Description(parameterView.Description ?? "").Build();
                        isPrimative = parameterView.ParameterType!.IsPrimitive || parameterView.ParameterType == typeof(string);
                    }
                    else
                    {
                        typeName = JsonSchema.FromText(parameterView.Schema.RootElement.GetRawText()).GetJsonType()?.ToString() ?? "null";
                        parameterSchema = JsonSchema.FromText(parameterView.Schema.RootElement.GetRawText());
                        isPrimative = parameterSchema.GetJsonType() != SchemaValueType.Object;
                    }

                    if (!isPrimative)
                    {
                        typeDefBuilder.AppendLine($"{{{typeName}}}: {JsonSerializer.Serialize(parameterSchema)}");
                    }

                    string parameterName = parameterView.Name;
                    tsSignatureBuilder.Append($"{(paramNum > 0 ? ", " : "")}{parameterName}");
                    jsDocBuilder.AppendLine($" * @param {{{typeName}}} {parameterName} - {parameterView.Description}");
                    paramNum++;
                }

                bool returnIsPrimative;
                string returnTypeName = "";
                JsonSchema? returnParameterSchema = null;
                if (skFunction.ReturnParameter.Schema is null)
                {
                    Verify.NotNull(skFunction.ReturnParameter.ParameterType);
                    var returnType = skFunction.ReturnParameter.ParameterType;
                    if (returnType.BaseType == typeof(Task) && (returnType.GenericTypeArguments.Length == 0))
                    {
                        returnType = skFunction.ReturnParameter.ParameterType.GenericTypeArguments[0];
                    }

                    returnTypeName = returnType.Name;
                    returnParameterSchema = new JsonSchemaBuilder().FromType(returnType).Description(skFunction.ReturnParameter.Description ?? "").Build();
                    returnIsPrimative = returnType!.IsPrimitive;
                }
                else
                {
                    returnTypeName = JsonSchema.FromText(skFunction.ReturnParameter.Schema.RootElement.GetRawText()).GetJsonType()?.ToString() ?? "Null";
                    returnParameterSchema = JsonSchema.FromText(skFunction.ReturnParameter.Schema.RootElement.GetRawText());
                    returnIsPrimative = returnParameterSchema.GetJsonType() != SchemaValueType.Object;
                }

                tsSignatureBuilder.Append(')');
                string returnObjectDescription = $"{returnTypeName} - {skFunction.ReturnParameter.Description}";
                jsDocBuilder.AppendLine($" * @return {(returnIsPrimative ? JsonSerializer.Serialize(returnParameterSchema) : returnObjectDescription)}");
                jsDocBuilder.AppendLine(" */");
                jsFunctionSigatures.Add($"{jsDocBuilder}{tsSignatureBuilder}");

                var pluginBuilder = pluginProxy as IDictionary<string, object>;
                var skFunctionProxy = this.BuildFunctionProxy(kernel, skFunction.PluginName, skFunction.Parameters, skFunction.Name, skFunction.ReturnParameter);
                pluginBuilder!.Add(functionName, skFunctionProxy);
            }

            this._pluginDict.Add(plugin.Name, pluginProxy);
        }

        string jsSignatures = $"{typeDefBuilder}\n{string.Join("\n\n", jsFunctionSigatures)}";
        return jsSignatures;
    }

    private Delegate BuildFunctionProxy(IKernel kernel, string pluginName, IReadOnlyList<ParameterView> parameterViews, string functionName, ReturnParameterView returnParameter)
    {
        try
        {
            // TODO: Support types for remote plugins
            var expressionParams = parameterViews.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToList();
            var expressionObjParams = parameterViews.Select(p => Expression.Parameter(typeof(object), p.Name)).ToList();

            var varsDictExp = Expression.Variable(typeof(Dictionary<string, ParamView>));
            var paramViewExp = Expression.Variable(typeof(ParamView));
            var kernelExp = Expression.Constant(kernel);
            var loggerExp = Expression.Constant(this._logger);
            var pluginNameExp = Expression.Constant(pluginName);
            var functionNameExp = Expression.Constant(functionName);
            var returnTypeExp = Expression.Constant(returnParameter.ParameterType);

            var invokeFunction = typeof(JavascriptPlanner).GetMethod("InvokeSkFunction", BindingFlags.NonPublic | BindingFlags.Static)!;
            var varsDictAdd = typeof(Dictionary<string, ParamView>).GetMethod(
                name: "Add",
                bindingAttr: BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(string), typeof(ParamView) },
                modifiers: null)!;

            // create a new dictionary and assign it to varsDict
            var body = new List<Expression>
            {
                Expression.Assign(varsDictExp, Expression.New(typeof(Dictionary<string, ParamView>)))
            };

            for (int i = 0; i < parameterViews.Count; i++)
            {
                // Create a new ParamView and assign it to paramView
                var paramAsObject = Expression.Convert(expressionObjParams[i]/*expressionParams[i]*/, typeof(object));
                body.Add(
                    Expression.Assign(
                        left: paramViewExp,
                        right: Expression.New(
                            constructor: typeof(ParamView).GetConstructor(new[] { typeof(object), typeof(Type) })!,
                            arguments: new Expression[] { paramAsObject, Expression.Constant(parameterViews[i].ParameterType) })));

                // Add the paramView to the varsDict
                body.Add(Expression.Call(varsDictExp, varsDictAdd, Expression.Constant(parameterViews[i].Name), paramViewExp));
            }

            if (pluginName == "llmTools")
            {
                int x = 3;
            }

            // Invoke the SKFunction
            var invokeResult = Expression.Call(invokeFunction, new Expression[] { kernelExp, loggerExp, pluginNameExp, functionNameExp, returnTypeExp, varsDictExp });
            body.Add(invokeResult);

            var block = Expression.Block(new[] { varsDictExp, paramViewExp }, body);

            var lambda = Expression.Lambda(block, false, expressionObjParams /*expressionParams*/);
            var proxy = lambda.Compile();
            return proxy;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static object InvokeSkFunction(IKernel kernel, ILogger logger, string pluginName, string functionName, Type returnType, Dictionary<string, ParamView> variables)
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

    private class TextTools
    {
        private readonly IKernel _kernel;

        public TextTools(IKernel kernel)
        {
            this._kernel = kernel;
        }

        [SKFunction]
        [System.ComponentModel.Description("Extracts details from unstructed text.")]
        [return: System.ComponentModel.Description("A list of details that have been extracted from the provided text, one for each of the questions provided.")]
        public TextQuestionsResponse ParseTextForDetails([System.ComponentModel.Description("A block of text and questions to ask about the text.")] TextQuestions questions)
        {
            var context =
                this._kernel.CreateNewContext(
                    new()
                    {
                        { "statements", "" },
                        { "questions", string.Join("\n", questions.Questions.Select((q, i) => $"{i}: {q}")) },
                    });

            var templateFactory = new HandlebarsPromptTemplateFactory();
            var handlebarsPrompt = "Hello AI, my name is {{name}}. What is the origin of my name?";


            // Create the semantic function with Handlebars
            var skfunction = kernel.CreateSemanticFunction(
                promptTemplate: prompt,
                functionName: "MyFunction",
                promptTemplateConfig: new PromptTemplateConfig()
                {
                    TemplateFormat = templateFormat
                },
                promptTemplateFactory:
            );

            //ChatHistory chatHistory = new ChatHistory();
            //IChatCompletion chatCompletion = this._kernel.GetService<IChatCompletion>();
            //string planResultString = await chatCompletion.GenerateMessageAsync(chatHistory, cancellationToken: cancellationToken).ConfigureAwait(false);

            var result = new List<string>();
            return new TextQuestionsResponse { Details = result };
        }
    }

    [TypeConverter(typeof(JsonTypeConverter<TextQuestions>))]
    private class TextQuestions
    {
        [System.ComponentModel.Description("Questions to answer by extracting details from the previded text.")]
        public List<string> Questions { get; set; } = new List<string>();

        [System.ComponentModel.Description("The text to be searched.")]
        public string Text { get; set; } = "";
    }

    [TypeConverter(typeof(JsonTypeConverter<TextQuestionsResponse>))]
    private class TextQuestionsResponse
    {
        [System.ComponentModel.Description("Details extracted from the specified text. One for each of the questions asked.")]
        public List<string> Details { get; set; } = new List<string>();
    }

    private class JsonTypeConverter<T> : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => true;

        /// <summary>
        /// This method is used to convert object from string to actual type. This will allow to pass object to
        /// native function which requires it.
        /// </summary>
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            return JsonSerializer.Deserialize((string)value, typeof(T));
        }

        /// <summary>
        /// This method is used to convert actual type to string representation, so it can be passed to AI
        /// for further processing.
        /// </summary>
        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            return JsonSerializer.Serialize(value);
        }
    }

    private class SystemAttributeHandler : IAttributeHandler<System.ComponentModel.DescriptionAttribute>
    {
        public void AddConstraints(SchemaGenerationContextBase context, Attribute attribute)
        {
            if (attribute is System.ComponentModel.DescriptionAttribute descriptionAttribute)
            {
                context.Intents.Insert(0, new Json.Schema.Generation.Intents.DescriptionIntent(descriptionAttribute.Description));
            }
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

    private class ParamView
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

    private class PluginContainer
    {
        public PluginContainer(string name)
        {
            this.Name = name;
        }

        public string Name { get; set; } = "";

        public IList<FunctionView>? Functions { get; set; } = new List<FunctionView>();
    }
}
