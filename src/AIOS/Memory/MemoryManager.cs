using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AIOS.Memory;

/// <summary>
/// 内存管理器实现 - 基于内存的临时存储
/// </summary>
public class MemoryManager : IMemoryManager
{
    private readonly ILogger<MemoryManager> _logger;
    private readonly ConcurrentDictionary<string, MemoryEntry> _memory = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public MemoryManager(ILogger<MemoryManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Initializing Memory Manager...");
            _initialized = true;
            _logger.LogInformation("Memory Manager initialized successfully");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Shutting down Memory Manager...");
            _memory.Clear();
            _initialized = false;
            _logger.LogInformation("Memory Manager shut down successfully");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StoreAsync(string key, object value, MemoryOptions? options = null)
    {
        await EnsureInitializedAsync();

        var entry = new MemoryEntry
        {
            Key = key,
            Value = value,
            Timestamp = DateTimeOffset.UtcNow,
            Options = options
        };

        _memory[key] = entry;
        
        _logger.LogDebug("Stored memory entry with key {Key}", key);
    }

    /// <inheritdoc/>
    public async Task<T?> RetrieveAsync<T>(string key)
    {
        await EnsureInitializedAsync();

        if (_memory.TryGetValue(key, out var entry))
        {
            // 检查是否过期
            if (entry.Options?.Expiration.HasValue == true &&
                DateTimeOffset.UtcNow - entry.Timestamp > entry.Options.Expiration.Value)
            {
                _memory.TryRemove(key, out _);
                _logger.LogDebug("Expired memory entry removed: {Key}", key);
                return default;
            }

            try
            {
                return (T?)entry.Value;
            }
            catch (InvalidCastException)
            {
                _logger.LogWarning("Type mismatch for memory entry: {Key}", key);
                return default;
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MemoryResult>> SearchAsync(string query, int limit = 10)
    {
        await EnsureInitializedAsync();

        var results = _memory.Values
            .Where(entry => entry.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           (entry.Value?.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(entry => CalculateRelevance(entry, query))
            .Take(limit)
            .Select(entry => new MemoryResult
            {
                Key = entry.Key,
                Value = entry.Value,
                Relevance = CalculateRelevance(entry, query),
                Timestamp = entry.Timestamp,
                Options = entry.Options
            })
            .ToList();

        return results;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key)
    {
        await EnsureInitializedAsync();

        _memory.TryRemove(key, out _);
        _logger.LogDebug("Deleted memory entry: {Key}", key);
    }

    /// <inheritdoc/>
    public MemoryUsage GetUsage()
    {
        var totalSize = _memory.Sum(kvp => 
            (kvp.Value.Value?.ToString()?.Length ?? 0) * sizeof(char));

        var categoryCounts = _memory
            .GroupBy(kvp => kvp.Value.Options?.Category ?? "default")
            .ToDictionary(g => g.Key, g => (long)g.Count());

        return new MemoryUsage
        {
            TotalItems = _memory.Count,
            TotalSize = totalSize,
            MemoryUsageMB = totalSize / (1024.0 * 1024.0),
            CategoryCounts = categoryCounts
        };
    }

    private double CalculateRelevance(MemoryEntry entry, string query)
    {
        var relevance = 0.0;
        
        // 基于时间衰减
        var age = DateTimeOffset.UtcNow - entry.Timestamp;
        var timeDecay = Math.Exp(-age.TotalHours / 24.0); // 24小时衰减
        
        // 基于关键词匹配
        var keyMatch = entry.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        var valueMatch = entry.Value?.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ? 0.8 : 0.0;
        
        relevance = (keyMatch + valueMatch) * timeDecay;
        
        return relevance;
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    private record MemoryEntry
    {
        public required string Key { get; init; }
        public required object Value { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
        public MemoryOptions? Options { get; init; }
    }
}
