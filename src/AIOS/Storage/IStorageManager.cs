namespace AIOS.Storage;

/// <summary>
/// 存储管理器接口 - 管理AI代理的持久化数据
/// </summary>
public interface IStorageManager
{
    /// <summary>
    /// 存储数据
    /// </summary>
    Task StoreAsync<T>(string key, T value, StorageOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检索数据
    /// </summary>
    Task<T?> RetrieveAsync<T>(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除数据
    /// </summary>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查数据是否存在
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取存储使用情况
    /// </summary>
    StorageUsage GetUsage();
    
    /// <summary>
    /// 列出所有键
    /// </summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string? prefix = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 初始化存储管理器
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 关闭存储管理器
    /// </summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 存储选项
/// </summary>
public record StorageOptions
{
    public string? Category { get; init; }
    public TimeSpan? Expiration { get; init; }
    public StorageType Type { get; init; } = StorageType.Persistent;
    public CompressionType Compression { get; init; } = CompressionType.None;
}

/// <summary>
/// 存储类型
/// </summary>
public enum StorageType
{
    /// <summary>
    /// 持久化存储
    /// </summary>
    Persistent,
    
    /// <summary>
    /// 临时存储
    /// </summary>
    Temporary,
    
    /// <summary>
    /// 缓存存储
    /// </summary>
    Cache
}

/// <summary>
/// 压缩类型
/// </summary>
public enum CompressionType
{
    None,
    Gzip,
    Brotli,
    Deflate
}

/// <summary>
/// 存储使用情况
/// </summary>
public record StorageUsage
{
    public long TotalItems { get; init; }
    public long TotalSize { get; init; }
    public double StorageUsageMB { get; init; }
    public Dictionary<string, long> CategoryCounts { get; init; } = new();
    public Dictionary<string, long> TypeCounts { get; init; } = new();
}
