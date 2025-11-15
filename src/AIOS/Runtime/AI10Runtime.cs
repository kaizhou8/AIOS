using Microsoft.Extensions.AI;
using Microsoft.ML;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.Sqlite;
using System.Text.Json;

namespace AIOS.Runtime;

/// <summary>
/// .NET 10 AI 运行时接口
/// </summary>
public interface IAI10Runtime
{
    // 统一AI客户端
    IChatClient GetChatClient(string provider);
    IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingClient(string provider);
    
    // 机器学习功能
    MLContext GetMLContext();
    PredictionEngine<TInput, TOutput> GetPredictionEngine<TInput, TOutput>() 
        where TInput : class
        where TOutput : class, new();
    
    // ONNX推理
    InferenceSession GetOnnxSession(string modelPath);
    Task<OgaResult> GenerateWithOnnxAsync(string modelPath, string prompt);
    
    // 向量搜索
    IVectorStore GetVectorStore(string storeName);
    Task<IReadOnlyList<VectorSearchResult>> SearchVectorsAsync(string query, int topK = 5);
    
    // 智能缓存
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
    
    // 性能监控
    AI10Metrics GetMetrics();
}

/// <summary>
/// .NET 10 AI 运行时实现
/// </summary>
public class AI10Runtime : IAI10Runtime, IDisposable
{
    private readonly Dictionary<string, IChatClient> _chatClients = new();
    private readonly Dictionary<string, IEmbeddingGenerator<string, Embedding<float>>> _embeddingClients = new();
    private readonly Dictionary<string, IVectorStore> _vectorStores = new();
    private readonly Dictionary<string, InferenceSession> _onnxSessions = new();
    private readonly Dictionary<string, object> _cache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    
    private readonly MLContext _mlContext = new(seed: 0);
    private readonly ILogger<AI10Runtime> _logger;
    private readonly AI10Metrics _metrics = new();

    public AI10Runtime(ILogger<AI10Runtime> logger)
    {
        _logger = logger;
        InitializeAI10Features();
    }

    private void InitializeAI10Features()
    {
        _logger.LogInformation("🚀 Initializing .NET 10 AI features...");
        
        InitializeChatClients();
        InitializeEmbeddingClients();
        InitializeVectorStores();
        InitializeOnnxModels();
        
        _logger.LogInformation("✅ .NET 10 AI features initialized successfully");
    }

    private void InitializeChatClients()
    {
        try
        {
            // OpenAI GPT-4o
            var openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrEmpty(openaiKey))
            {
                _chatClients["openai-gpt4o"] = new OpenAIChatClient(
                    apiKey: openaiKey,
                    modelId: "gpt-4o"
                );
                _logger.LogInformation("✅ OpenAI GPT-4o client initialized");
            }

            // Azure OpenAI
            var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
            var azureKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
            if (!string.IsNullOrEmpty(azureEndpoint) && !string.IsNullOrEmpty(azureKey))
            {
                _chatClients["azure-openai"] = new AzureOpenAIChatClient(
                    endpoint: new Uri(azureEndpoint),
                    apiKey: azureKey,
                    modelId: "gpt-4o"
                );
                _logger.LogInformation("✅ Azure OpenAI client initialized");
            }

            // 本地Ollama
            _chatClients["ollama"] = new OllamaChatClient(
                endpoint: new Uri("http://localhost:11434"),
                modelId: "llama3.1:8b"
            );
            _logger.LogInformation("✅ Ollama client initialized");

            // 本地DeepSeek
            _chatClients["deepseek"] = new OllamaChatClient(
                endpoint: new Uri("http://localhost:11434"),
                modelId: "deepseek-coder:33b"
            );
            _logger.LogInformation("✅ DeepSeek client initialized");

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to initialize some chat clients");
        }
    }

    private void InitializeEmbeddingClients()
    {
        try
        {
            // OpenAI嵌入
            var openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrEmpty(openaiKey))
            {
                _embeddingClients["openai-embedding"] = new OpenAIEmbeddingGenerator(
                    apiKey: openaiKey,
                    modelId: "text-embedding-3-small"
                );
                _logger.LogInformation("✅ OpenAI embedding client initialized");
            }

            // 本地嵌入模型
            _embeddingClients["local-embedding"] = new OllamaEmbeddingGenerator(
                endpoint: new Uri("http://localhost:11434"),
                modelId: "nomic-embed-text"
            );
            _logger.LogInformation("✅ Local embedding client initialized");

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to initialize some embedding clients");
        }
    }

    private void InitializeVectorStores()
    {
        try
        {
            // SQLite向量存储
            _vectorStores["sqlite"] = new SqliteVectorStore(
                connectionString: "Data Source=aios_vectors.db"
            );
            _logger.LogInformation("✅ SQLite vector store initialized");

            // 内存向量存储
            _vectorStores["memory"] = new VolatileVectorStore();
            _logger.LogInformation("✅ Memory vector store initialized");

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to initialize vector stores");
        }
    }

    private void InitializeOnnxModels()
    {
        try
        {
            var modelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
            Directory.CreateDirectory(modelsPath);

            // 代码分析模型
            var codeAnalysisModel = Path.Combine(modelsPath, "code_analysis.onnx");
            if (File.Exists(codeAnalysisModel))
            {
                _onnxSessions["code_analysis"] = new InferenceSession(codeAnalysisModel);
                _logger.LogInformation("✅ Code analysis ONNX model loaded");
            }

            // 情感分析模型
            var sentimentModel = Path.Combine(modelsPath, "sentiment_analysis.onnx");
            if (File.Exists(sentimentModel))
            {
                _onnxSessions["sentiment_analysis"] = new InferenceSession(sentimentModel);
                _logger.LogInformation("✅ Sentiment analysis ONNX model loaded");
            }

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to initialize ONNX models");
        }
    }

    public IChatClient GetChatClient(string provider)
    {
        if (!_chatClients.TryGetValue(provider, out var client))
            throw new ArgumentException($"Provider '{provider}' not found");
        
        Interlocked.Increment(ref _metrics.TotalAIRequests);
        return client;
    }

    public IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingClient(string provider)
    {
        if (!_embeddingClients.TryGetValue(provider, out var client))
            throw new ArgumentException($"Embedding provider '{provider}' not found");
        
        Interlocked.Increment(ref _metrics.TotalEmbeddingRequests);
        return client;
    }

    public MLContext GetMLContext() => _mlContext;

    public PredictionEngine<TInput, TOutput> GetPredictionEngine<TInput, TOutput>() 
        where TInput : class
        where TOutput : class, new()
    {
        return _mlContext.Model.CreatePredictionEngine<TInput, TOutput>(null);
    }

    public InferenceSession GetOnnxSession(string modelPath)
    {
        if (!_onnxSessions.TryGetValue(modelPath, out var session))
        {
            session = new InferenceSession(modelPath);
            _onnxSessions[modelPath] = session;
        }
        return session;
    }

    public async Task<OgaResult> GenerateWithOnnxAsync(string modelPath, string prompt)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"ONNX model not found: {modelPath}");

        try
        {
            using var model = new Model(modelPath);
            using var tokenizer = new Tokenizer(model);
            
            var tokens = tokenizer.Encode(prompt);
            var generatorParams = new GeneratorParams(model);
            generatorParams.SetSearchOption("max_length", 1000);
            
            using var generator = new Generator(model, generatorParams);
            generator.AppendTokens(tokens);
            
            var output = string.Empty;
            while (!generator.IsDone())
            {
                generator.GenerateNextToken();
                var newToken = generator.GetSequence(0)[^1];
                output += tokenizer.Decode(new[] { newToken });
            }

            Interlocked.Increment(ref _metrics.TotalOnnxRequests);
            
            return new OgaResult
            {
                Content = output,
                TokensUsed = generator.GetSequence(0).Length,
                Model = Path.GetFileName(modelPath)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating with ONNX model: {ModelPath}", modelPath);
            throw;
        }
    }

    public IVectorStore GetVectorStore(string storeName)
    {
        if (!_vectorStores.TryGetValue(storeName, out var store))
            throw new ArgumentException($"Vector store '{storeName}' not found");
        
        return store;
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchVectorsAsync(string query, int topK = 5)
    {
        var embeddingClient = GetEmbeddingClient("local-embedding");
        var embedding = await embeddingClient.GenerateEmbeddingAsync(query);
        
        var vectorStore = GetVectorStore("sqlite");
        var collection = vectorStore.GetCollection<string, VectorRecord>("documents");
        
        var results = await collection.VectorizedSearchAsync(
            embedding.Vector.ToArray(),
            new VectorSearchOptions { Top = topK }
        );

        return await results.Results.ToListAsync();
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var cached) && cached is T t)
                return t;

            var result = await factory();
            _cache[key] = result;
            
            if (expiration.HasValue)
            {
                _ = Task.Delay(expiration.Value).ContinueWith(_ =>
                {
                    _cache.TryRemove(key, out _);
                });
            }

            return result;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public AI10Metrics GetMetrics() => _metrics;

    public void Dispose()
    {
        foreach (var session in _onnxSessions.Values)
        {
            session?.Dispose();
        }
        _onnxSessions.Clear();
        
        _cacheLock?.Dispose();
    }
}

/// <summary>
/// ONNX生成结果
/// </summary>
public class OgaResult
{
    public string Content { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public string Model { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// 向量搜索结果
/// </summary>
public class VectorSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// .NET 10 AI 性能指标
/// </summary>
public class AI10Metrics
{
    public long TotalAIRequests { get; set; }
    public long TotalEmbeddingRequests { get; set; }
    public long TotalOnnxRequests { get; set; }
    public long TotalVectorSearchRequests { get; set; }
    public double AverageResponseTime { get; set; }
    public Dictionary<string, double> ProviderLatency { get; set; } = new();
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 向量记录
/// </summary>
public class VectorRecord
{
    [VectorStoreRecordKey]
    public string Id { get; set; } = string.Empty;
    
    [VectorStoreRecordData]
    public string Content { get; set; } = string.Empty;
    
    [VectorStoreRecordVector(Dimensions: 1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }
    
    [VectorStoreRecordData]
    public Dictionary<string, object> Metadata { get; set; } = new();
}
