namespace AIOS.Memory;

/// <summary>
/// 内存管理器接口 - 管理AI代理的上下文和记忆
/// </summary>
public interface IMemoryManager
{
    /// <summary>
    /// 存储记忆
    /// </summary>
    Task StoreAsync(string key, object value, MemoryOptions? options = null);
    
    /// <summary>
    /// 检索记忆
    /// </summary>
    Task<T?> RetrieveAsync<T>(string key);
    
    /// <summary>
    /// 搜索相关记忆
    /// </summary>
    Task<IReadOnlyList<MemoryResult>> SearchAsync(string query, int limit = 10);
    
    /// <summary>
    /// 删除记忆
    /// </summary>
    Task DeleteAsync(string key);
    
    /// <summary>
    /// 获取内存使用情况
    /// </summary>
    MemoryUsage GetUsage();
    
    /// <summary>
    /// 初始化内存管理器
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 关闭内存管理器
    /// </summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 内存选项
/// </summary>
public record MemoryOptions
{
    public TimeSpan? Expiration { get; init; }
    public string? Category { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// 内存搜索结果
/// </summary>
public record MemoryResult
{
    public required string Key { get; init; }
    public required object Value { get; init; }
    public required double Relevance { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public MemoryOptions? Options { get; init; }
}

/// <summary>
/// 内存使用情况
/// </summary>
public record MemoryUsage
{
    public long TotalItems { get; init; }
    public long TotalSize { get; init; }
    public double MemoryUsageMB { get; init; }
    public Dictionary<string, long> CategoryCounts { get; init; } = new();
}
