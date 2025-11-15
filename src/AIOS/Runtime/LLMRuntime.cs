using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AIOS.Runtime;

/// <summary>
/// LLM运行时实现 - 基于Semantic Kernel
/// </summary>
public class LLMRuntime : ILLMRuntime
{
    private readonly ILogger<LLMRuntime> _logger;
    private readonly Dictionary<string, IKernel> _kernels = new();
    private readonly Dictionary<string, IChatCompletionService> _chatServices = new();
    private long _totalRequests;
    private bool _initialized;

    public LLMRuntime(ILogger<LLMRuntime> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing LLM Runtime...");

        try
        {
            // 初始化OpenAI
            await InitializeOpenAIAsync();
            
            // 初始化Ollama
            await InitializeOllamaAsync();
            
            // 初始化HuggingFace
            await InitializeHuggingFaceAsync();

            _initialized = true;
            _logger.LogInformation("LLM Runtime initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize LLM Runtime");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetProvidersAsync()
    {
        await EnsureInitializedAsync();
        return _kernels.Keys.ToList();
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(
        string provider,
        string prompt,
        LLMOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (!_kernels.TryGetValue(provider, out var kernel))
        {
            throw new ArgumentException($"Provider '{provider}' not found");
        }

        options ??= new LLMOptions();

        var executionSettings = GetPromptExecutionSettings(options);
        
        var startTime = DateTimeOffset.UtcNow;
        var result = await kernel.InvokePromptAsync(prompt, new(executionSettings), cancellationToken: cancellationToken);
        
        Interlocked.Increment(ref _totalRequests);
        
        _logger.LogInformation(
            "Text generation completed for provider {Provider} in {Duration}ms",
            provider,
            (DateTimeOffset.UtcNow - startTime).TotalMilliseconds);

        return result.ToString();
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> ChatAsync(
        string provider,
        IReadOnlyList<ChatMessage> messages,
        LLMOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (!_chatServices.TryGetValue(provider, out var chatService))
        {
            throw new ArgumentException($"Provider '{provider}' not found");
        }

        options ??= new LLMOptions();

        var chatHistory = new ChatHistory();
        foreach (var message in messages)
        {
            chatHistory.AddMessage(message.Role, message.Content);
        }

        var executionSettings = GetPromptExecutionSettings(options);
        
        var startTime = DateTimeOffset.UtcNow;
        var response = await chatService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings: executionSettings,
            cancellationToken: cancellationToken);

        Interlocked.Increment(ref _totalRequests);
        
        var duration = DateTimeOffset.UtcNow - startTime;
        
        return new ChatResponse
        {
            Content = response.Content!,
            Model = response.ModelId!,
            TokenUsage = response.Metadata?.GetValueOrDefault("Usage", 0) as int? ?? 0,
            Duration = duration
        };
    }

    /// <inheritdoc/>
    public async Task<ModelInfo> GetModelInfoAsync(string provider)
    {
        await EnsureInitializedAsync();

        if (!_kernels.TryGetValue(provider, out var kernel))
        {
            throw new ArgumentException($"Provider '{provider}' not found");
        }

        // 获取模型信息
        return new ModelInfo
        {
            Name = provider,
            Provider = provider,
            Version = "1.0.0",
            CreatedAt = DateTimeOffset.UtcNow,
            Capabilities = new Dictionary<string, object>
            {
                ["supports_chat"] = true,
                ["supports_text_generation"] = true
            }
        };
    }

    /// <inheritdoc/>
    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Shutting down LLM Runtime...");
        _kernels.Clear();
        _chatServices.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public long GetTotalRequests() => _totalRequests;

    private async Task InitializeOpenAIAsync()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion("gpt-3.5-turbo", apiKey)
                .Build();

            _kernels["openai"] = kernel;
            _chatServices["openai"] = kernel.GetRequiredService<IChatCompletionService>();
            _logger.LogInformation("OpenAI provider initialized");
        }
    }

    private async Task InitializeOllamaAsync()
    {
        var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";
        try
        {
            var kernel = Kernel.CreateBuilder()
                .AddOllamaChatCompletion("llama2", ollamaUrl)
                .Build();

            _kernels["ollama"] = kernel;
            _chatServices["ollama"] = kernel.GetRequiredService<IChatCompletionService>();
            _logger.LogInformation("Ollama provider initialized");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Ollama provider");
        }
    }

    private async Task InitializeHuggingFaceAsync()
    {
        var hfToken = Environment.GetEnvironmentVariable("HUGGINGFACE_API_TOKEN");
        if (!string.IsNullOrEmpty(hfToken))
        {
            try
            {
                var kernel = Kernel.CreateBuilder()
                    .AddHuggingFaceChatCompletion("microsoft/DialoGPT-medium", apiKey: hfToken)
                    .Build();

                _kernels["huggingface"] = kernel;
                _chatServices["huggingface"] = kernel.GetRequiredService<IChatCompletionService>();
                _logger.LogInformation("HuggingFace provider initialized");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize HuggingFace provider");
            }
        }
    }

    private PromptExecutionSettings GetPromptExecutionSettings(LLMOptions options)
    {
        return new OpenAIPromptExecutionSettings
        {
            Temperature = (float)options.Temperature,
            MaxTokens = options.MaxTokens,
            TopP = (float)options.TopP,
            ExtensionData = options.AdditionalParameters
        };
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }
}
