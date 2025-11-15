using Microsoft.Extensions.Logging;
using AIOS.Runtime;

namespace AIOS.MAF;

/// <summary>
/// LLM代理能力 - 文本生成和对话能力
/// </summary>
public class LLMCapability : IAgentCapability
{
    public string Name => "llm_text_generation";
    public string Description => "Generate text using large language models";
    public string Version => "1.0.0";
    
    public IReadOnlyList<CapabilityParameter> Parameters => new[]
    {
        new CapabilityParameter
        {
            Name = "prompt",
            Type = "string",
            Description = "The prompt to generate text from",
            Required = true
        },
        new CapabilityParameter
        {
            Name = "provider",
            Type = "string",
            Description = "LLM provider to use",
            Required = false,
            DefaultValue = "openai",
            AllowedValues = new[] { "openai", "ollama", "huggingface" }
        },
        new CapabilityParameter
        {
            Name = "max_tokens",
            Type = "integer",
            Description = "Maximum number of tokens to generate",
            Required = false,
            DefaultValue = 1000
        },
        new CapabilityParameter
        {
            Name = "temperature",
            Type = "number",
            Description = "Temperature for generation",
            Required = false,
            DefaultValue = 0.7
        }
    };

    public bool ValidateParameters(Dictionary<string, object> parameters)
    {
        return parameters.ContainsKey("prompt") &&
               parameters["prompt"] is string prompt &&
               !string.IsNullOrWhiteSpace(prompt);
    }
}

/// <summary>
/// LLM代理 - 基于大语言模型的智能代理
/// </summary>
public class LLMAgent : BaseAgent
{
    private readonly ILLMRuntime _llmRuntime;
    private readonly List<string> _supportedProviders = new();

    public LLMAgent(
        ILogger<LLMAgent> logger,
        string id,
        string name,
        string description,
        ILLMRuntime llmRuntime) : base(logger, id, name, description)
    {
        _llmRuntime = llmRuntime;
        
        // 添加LLM能力
        AddCapability(new LLMCapability());
        
        // 添加元数据
        AddMetadata("type", "llm_agent");
        AddMetadata("category", "ai_assistant");
    }

    /// <summary>
    /// 初始化支持的提供商
    /// </summary>
    public async Task InitializeProvidersAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _llmRuntime.GetProvidersAsync();
        _supportedProviders.AddRange(providers);
        
        AddMetadata("supported_providers", _supportedProviders);
        Logger.LogInformation("LLM Agent {Name} initialized with providers: {Providers}", Name, string.Join(", ", providers));
    }

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        await InitializeProvidersAsync(cancellationToken);
    }

    protected override async Task<AgentResult> OnExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // 从上下文中提取参数
            var parameters = context.Parameters;
            var prompt = parameters.GetValueOrDefault("prompt")?.ToString() ?? context.Task;
            var provider = parameters.GetValueOrDefault("provider")?.ToString() ?? "openai";
            var maxTokens = int.Parse(parameters.GetValueOrDefault("max_tokens")?.ToString() ?? "1000");
            var temperature = double.Parse(parameters.GetValueOrDefault("temperature")?.ToString() ?? "0.7");

            // 验证提供商
            if (!_supportedProviders.Contains(provider))
            {
                return new AgentResult
                {
                    Success = false,
                    ErrorMessage = $"Unsupported provider: {provider}. Available: {string.Join(", ", _supportedProviders)}"
                };
            }

            // 构建LLM选项
            var options = new LLMOptions
            {
                MaxTokens = maxTokens,
                Temperature = temperature
            };

            // 检查是否为聊天模式
            if (context.History.Any())
            {
                var messages = new List<Runtime.ChatMessage>();
                
                // 添加历史消息
                foreach (var history in context.History)
                {
                    messages.Add(new Runtime.ChatMessage
                    {
                        Role = "user",
                        Content = history.Task
                    });
                    
                    // 假设历史结果作为助手回复
                    if (history.Metadata.TryGetValue("last_result", out var result))
                    {
                        messages.Add(new Runtime.ChatMessage
                        {
                            Role = "assistant",
                            Content = result.ToString()
                        });
                    }
                }
                
                // 添加当前消息
                messages.Add(new Runtime.ChatMessage
                {
                    Role = "user",
                    Content = prompt
                });

                var response = await _llmRuntime.ChatAsync(provider, messages, options, cancellationToken);
                
                return new AgentResult
                {
                    Success = true,
                    Data = response.Content,
                    TokenUsage = response.TokenUsage,
                    Metadata = new Dictionary<string, object>
                    {
                        ["model"] = response.Model,
                        ["provider"] = provider
                    }
                };
            }
            else
            {
                // 文本生成模式
                var response = await _llmRuntime.GenerateTextAsync(provider, prompt, options, cancellationToken);
                
                return new AgentResult
                {
                    Success = true,
                    Data = response,
                    Metadata = new Dictionary<string, object>
                    {
                        ["provider"] = provider
                    }
                };
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "LLM Agent {Name} execution failed", Name);
            return new AgentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 添加自定义系统提示
    /// </summary>
    public void AddSystemPrompt(string systemPrompt)
    {
        AddMetadata("system_prompt", systemPrompt);
    }

    /// <summary>
    /// 设置默认提供商
    /// </summary>
    public void SetDefaultProvider(string provider)
    {
        if (_supportedProviders.Contains(provider))
        {
            AddMetadata("default_provider", provider);
        }
    }
}

/// <summary>
/// 代码生成代理 - 专门用于代码生成的LLM代理
/// </summary>
public class CodeGenerationAgent : LLMAgent
{
    public CodeGenerationAgent(
        ILogger<CodeGenerationAgent> logger,
        string id,
        string name,
        ILLMRuntime llmRuntime) : base(logger, id, name, "Specialized agent for code generation", llmRuntime)
    {
        AddSystemPrompt("You are an expert code generation assistant. Generate clean, well-documented code following best practices.");
        AddMetadata("specialization", "code_generation");
        AddMetadata("supported_languages", new[] { "csharp", "python", "javascript", "rust" });
    }
}

/// <summary>
/// 数据分析代理 - 专门用于数据分析的LLM代理
/// </summary>
public class DataAnalysisAgent : LLMAgent
{
    public DataAnalysisAgent(
        ILogger<DataAnalysisAgent> logger,
        string id,
        string name,
        ILLMRuntime llmRuntime) : base(logger, id, name, "Specialized agent for data analysis", llmRuntime)
    {
        AddSystemPrompt("You are a data analysis expert. Provide insights, trends, and actionable recommendations based on data.");
        AddMetadata("specialization", "data_analysis");
        AddMetadata("supported_formats", new[] { "json", "csv", "xml", "sql" });
    }
}
