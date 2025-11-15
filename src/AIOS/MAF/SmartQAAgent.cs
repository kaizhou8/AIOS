using AIOS.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace AIOS.MAF;

/// <summary>
/// 智能问答代理 - 基于.NET 10 AI功能
/// </summary>
public class SmartQAAgent : BaseAgent
{
    private readonly IAI10Runtime _ai10Runtime;
    private readonly ILogger<SmartQAAgent> _logger;
    private readonly string _vectorStoreName = "qa_knowledge";

    public SmartQAAgent(
        string id,
        string name,
        IAI10Runtime ai10Runtime,
        ILogger<SmartQAAgent> logger) 
        : base(id, name, "基于向量搜索和.NET 10 AI的智能问答代理")
    {
        _ai10Runtime = ai10Runtime;
        _logger = logger;
        
        // 添加AI能力
        AddCapability(new AICapability
        {
            Name = "智能问答",
            Description = "基于向量搜索和LLM的智能问答",
            Parameters = new Dictionary<string, object>
            {
                ["max_tokens"] = 2000,
                ["temperature"] = 0.7,
                ["top_k"] = 5
            }
        });
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // 初始化知识库
        await InitializeKnowledgeBase();
        
        _logger.LogInformation("🧠 Smart QA Agent initialized with .NET 10 AI");
    }

    public override async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        try
        {
            _logger.LogInformation("🔄 Processing question: {Question}", context.Task);
            
            // 1. 向量搜索相关知识
            var relevantDocs = await SearchRelevantKnowledgeAsync(context.Task);
            
            // 2. 构建增强提示
            var enhancedPrompt = BuildEnhancedPrompt(context.Task, relevantDocs);
            
            // 3. 使用.NET 10 AI生成回答
            var chatClient = _ai10Runtime.GetChatClient("openai-gpt4o");
            var response = await chatClient.CompleteAsync(enhancedPrompt);
            
            // 4. 记录使用统计
            var metrics = _ai10Runtime.GetMetrics();
            
            return new AgentResult
            {
                Success = true,
                Data = response.Message.Content,
                Metadata = new Dictionary<string, object>
                {
                    ["relevant_docs"] = relevantDocs.Count,
                    ["confidence"] = CalculateConfidence(relevantDocs),
                    ["provider"] = "openai-gpt4o",
                    ["tokens_used"] = response.Usage?.TotalTokens ?? 0,
                    ["response_time"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing question: {Question}", context.Task);
            return new AgentResult
            {
                Success = false,
                Error = ex.Message,
                Data = "抱歉，处理问题时出现错误。"
            };
        }
    }

    private async Task<IReadOnlyList<SearchResult>> SearchRelevantKnowledgeAsync(string query)
    {
        try
        {
            var results = await _ai10Runtime.SearchVectorsAsync(query, topK: 5);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to search vectors, using fallback");
            return new List<SearchResult>();
        }
    }

    private string BuildEnhancedPrompt(string query, IReadOnlyList<SearchResult> relevantDocs)
    {
        if (!relevantDocs.Any())
        {
            return $"""
                请回答以下问题：
                问题：{query}
                
                请提供详细、准确的回答。
                """;
        }

        var context = string.Join("\n\n", relevantDocs.Select((doc, i) => 
            $"[知识{i+1}] {doc.Content} (相关性: {doc.Score:F2})"));

        return $"""
            基于以下知识回答用户问题：
            
            {context}
            
            用户问题：{query}
            
            要求：
            1. 基于提供的知识回答
            2. 如果知识不足，请明确说明
            3. 提供具体的例子或步骤
            4. 保持回答简洁明了
            """;
    }

    private double CalculateConfidence(IReadOnlyList<SearchResult> docs)
    {
        if (!docs.Any()) return 0.0;
        
        var avgScore = docs.Average(d => d.Score);
        return Math.Min(avgScore * 100, 95.0); // 转换为百分比
    }

    private async Task InitializeKnowledgeBase()
    {
        try
        {
            // 添加示例知识
            var sampleKnowledge = new[]
            {
                "C#性能优化技巧：1) 使用Span<T>避免内存分配 2) 使用ValueTask减少异步开销 3) 使用MemoryCache缓存频繁访问的数据",
                "AIOS架构优势：三层架构设计，支持1000+并发代理，毫秒级响应时间，企业级可靠性",
                ".NET 10 AI特性：统一AI接口、向量搜索、机器学习、ONNX推理、智能缓存",
                "向量搜索原理：将文本转换为高维向量，通过余弦相似度计算相关性，支持语义搜索",
                "代理调度策略：基于负载均衡、优先级队列、智能路由的混合调度算法"
            };

            foreach (var knowledge in sampleKnowledge)
            {
                await _ai10Runtime.GetOrCreateAsync($"knowledge_{Guid.NewGuid()}", async () =>
                {
                    await _ai10Runtime.GetEmbeddingClient("local-embedding");
                    return knowledge;
                });
            }

            _logger.LogInformation("📚 Knowledge base initialized with {Count} entries", sampleKnowledge.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to initialize knowledge base");
        }
    }

    /// <summary>
    /// 添加知识到向量存储
    /// </summary>
    public async Task<string> AddKnowledgeAsync(string content, Dictionary<string, object> metadata)
    {
        try
        {
            var embeddingClient = _ai10Runtime.GetEmbeddingClient("local-embedding");
            var embedding = await embeddingClient.GenerateEmbeddingAsync(content);
            
            var recordId = Guid.NewGuid().ToString();
            
            // 存储到向量数据库
            var vectorStore = _ai10Runtime.GetVectorStore(_vectorStoreName);
            var collection = vectorStore.GetCollection<string, VectorRecord>("knowledge");
            
            var record = new VectorRecord
            {
                Id = recordId,
                Content = content,
                Embedding = embedding.Vector,
                Metadata = metadata
            };

            await collection.UpsertAsync(record);
            
            _logger.LogInformation("📥 Added knowledge: {ContentPreview}...", content[..50]);
            return recordId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to add knowledge");
            throw;
        }
    }

    /// <summary>
    /// 批量添加知识
    /// </summary>
    public async Task<IReadOnlyList<string>> AddKnowledgeBatchAsync(IReadOnlyList<(string content, Dictionary<string, object> metadata)> items)
    {
        var results = new List<string>();
        
        foreach (var (content, metadata) in items)
        {
            var id = await AddKnowledgeAsync(content, metadata);
            results.Add(id);
        }
        
        return results;
    }

    /// <summary>
    /// 获取代理统计信息
    /// </summary>
    public override async Task<AgentStatus> GetStatusAsync()
    {
        var baseStatus = await base.GetStatusAsync();
        
        try
        {
            var vectorStore = _ai10Runtime.GetVectorStore(_vectorStoreName);
            var collection = vectorStore.GetCollection<string, VectorRecord>("knowledge");
            var knowledgeCount = await collection.CountAsync();
            
            baseStatus.Metrics["knowledge_count"] = knowledgeCount;
            baseStatus.Metrics["last_search_time"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to get knowledge count");
        }
        
        return baseStatus;
    }

    /// <summary>
    /// 清理知识库
    /// </summary>
    public async Task CleanKnowledgeAsync()
    {
        try
        {
            var vectorStore = _ai10Runtime.GetVectorStore(_vectorStoreName);
            var collection = vectorStore.GetCollection<string, VectorRecord>("knowledge");
            await collection.DeleteCollectionAsync();
            
            _logger.LogInformation("🧹 Knowledge base cleaned");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to clean knowledge base");
        }
    }
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
    
    [VectorStoreRecordVector(Dimensions: 768)]
    public ReadOnlyMemory<float> Embedding { get; set; }
    
    [VectorStoreRecordData]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 搜索结果
/// </summary>
public class SearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
