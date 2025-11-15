using Microsoft.SemanticKernel;

namespace AIOS.Runtime;

/// <summary>
/// LLM运行时接口 - 管理大型语言模型的执行
/// </summary>
public interface ILLMRuntime
{
    /// <summary>
    /// 获取可用的LLM提供者列表
    /// </summary>
    Task<IReadOnlyList<string>> GetProvidersAsync();
    
    /// <summary>
    /// 执行文本生成
    /// </summary>
    Task<string> GenerateTextAsync(
        string provider,
        string prompt,
        LLMOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行聊天完成
    /// </summary>
    Task<ChatResponse> ChatAsync(
        string provider,
        IReadOnlyList<ChatMessage> messages,
        LLMOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取模型信息
    /// </summary>
    Task<ModelInfo> GetModelInfoAsync(string provider);
    
    /// <summary>
    /// 初始化运行时
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 关闭运行时
    /// </summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取总请求数
    /// </summary>
    long GetTotalRequests();
}

/// <summary>
/// LLM配置选项
/// </summary>
public record LLMOptions
{
    public string? Model { get; init; }
    public double Temperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 1000;
    public double TopP { get; init; } = 1.0;
    public Dictionary<string, object>? AdditionalParameters { get; init; }
}

/// <summary>
/// 聊天消息
/// </summary>
public record ChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public string? Name { get; init; }
}

/// <summary>
/// 聊天响应
/// </summary>
public record ChatResponse
{
    public required string Content { get; init; }
    public required string Model { get; init; }
    public required int TokenUsage { get; init; }
    public required TimeSpan Duration { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// 模型信息
/// </summary>
public record ModelInfo
{
    public required string Name { get; init; }
    public required string Provider { get; init; }
    public required string Version { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public Dictionary<string, object>? Capabilities { get; init; }
}
