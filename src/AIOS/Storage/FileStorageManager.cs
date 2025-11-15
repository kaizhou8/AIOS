using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO.Compression;

namespace AIOS.Storage;

/// <summary>
/// 文件存储管理器实现
/// </summary>
public class FileStorageManager : IStorageManager
{
    private readonly ILogger<FileStorageManager> _logger;
    private readonly string _basePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _initialized;

    public FileStorageManager(ILogger<FileStorageManager> logger, string? basePath = null)
    {
        _logger = logger;
        _basePath = basePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage");
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing File Storage Manager...");
        
        // 确保存储目录存在
        Directory.CreateDirectory(_basePath);
        Directory.CreateDirectory(Path.Combine(_basePath, "persistent"));
        Directory.CreateDirectory(Path.Combine(_basePath, "temporary"));
        Directory.CreateDirectory(Path.Combine(_basePath, "cache"));
        
        _initialized = true;
        _logger.LogInformation("File Storage Manager initialized at {Path}", _basePath);
    }

    /// <inheritdoc/>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Shutting down File Storage Manager...");
        
        // 清理临时文件
        await CleanupTemporaryFilesAsync(cancellationToken);
        
        _logger.LogInformation("File Storage Manager shut down successfully");
    }

    /// <inheritdoc/>
    public async Task StoreAsync<T>(string key, T value, StorageOptions? options = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        options ??= new StorageOptions();
        
        var filePath = GetFilePath(key, options);
        var directory = Path.GetDirectoryName(filePath);
        
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            var data = new StorageData<T>
            {
                Key = key,
                Value = value,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = options.Expiration.HasValue ? DateTimeOffset.UtcNow.Add(options.Expiration.Value) : null,
                Category = options.Category,
                Type = options.Type
            };

            var json = JsonSerializer.Serialize(data, _jsonOptions);
            
            if (options.Compression != CompressionType.None)
            {
                await WriteCompressedAsync(filePath, json, options.Compression, cancellationToken);
            }
            else
            {
                await File.WriteAllTextAsync(filePath, json, cancellationToken);
            }
            
            _logger.LogDebug("Stored data with key {Key} at {Path}", key, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store data with key {Key}", key);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<T?> RetrieveAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        // 尝试所有可能的存储类型
        foreach (StorageType type in Enum.GetValues<StorageType>())
        {
            var filePath = GetFilePath(key, new StorageOptions { Type = type });
            
            if (!File.Exists(filePath)) continue;

            try
            {
                string json;
                
                // 尝试解压
                if (filePath.EndsWith(".gz"))
                {
                    json = await ReadCompressedAsync(filePath, CompressionType.Gzip, cancellationToken);
                }
                else if (filePath.EndsWith(".br"))
                {
                    json = await ReadCompressedAsync(filePath, CompressionType.Brotli, cancellationToken);
                }
                else
                {
                    json = await File.ReadAllTextAsync(filePath, cancellationToken);
                }

                var data = JsonSerializer.Deserialize<StorageData<T>>(json, _jsonOptions);
                
                if (data == null) continue;

                // 检查是否过期
                if (data.ExpiresAt.HasValue && DateTimeOffset.UtcNow > data.ExpiresAt.Value)
                {
                    await DeleteAsync(key, cancellationToken);
                    continue;
                }

                _logger.LogDebug("Retrieved data with key {Key} from {Path}", key, filePath);
                return data.Value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve data with key {Key} from {Path}", key, filePath);
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        bool deleted = false;
        
        foreach (StorageType type in Enum.GetValues<StorageType>())
        {
            var filePath = GetFilePath(key, new StorageOptions { Type = type });
            
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    deleted = true;
                    _logger.LogDebug("Deleted data with key {Key} at {Path}", key, filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete data with key {Key} at {Path}", key, filePath);
                }
            }
        }

        return deleted;
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        foreach (StorageType type in Enum.GetValues<StorageType>())
        {
            var filePath = GetFilePath(key, new StorageOptions { Type = type });
            if (File.Exists(filePath))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListKeysAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        var keys = new List<string>();
        
        foreach (StorageType type in Enum.GetValues<StorageType>())
        {
            var directory = Path.Combine(_basePath, type.ToString().ToLower());
            if (!Directory.Exists(directory)) continue;

            var files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(directory, file);
                var key = Path.ChangeExtension(relativePath, null);
                
                if (string.IsNullOrEmpty(prefix) || key.StartsWith(prefix))
                {
                    keys.Add(key);
                }
            }
        }

        return keys.Distinct().ToList();
    }

    /// <inheritdoc/>
    public StorageUsage GetUsage()
    {
        var usage = new StorageUsage();
        var categoryCounts = new Dictionary<string, long>();
        var typeCounts = new Dictionary<string, long>();
        long totalSize = 0;
        long totalItems = 0;

        foreach (StorageType type in Enum.GetValues<StorageType>())
        {
            var directory = Path.Combine(_basePath, type.ToString().ToLower());
            if (!Directory.Exists(directory)) continue;

            var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
            typeCounts[type.ToString()] = files.Length;
            totalItems += files.Length;

            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;

                    // 尝试读取类别信息
                    if (file.EndsWith(".json"))
                    {
                        var json = File.ReadAllText(file);
                        var data = JsonSerializer.Deserialize<StorageData<object>>(json, _jsonOptions);
                        if (data?.Category != null)
                        {
                            categoryCounts[data.Category] = categoryCounts.GetValueOrDefault(data.Category) + 1;
                        }
                    }
                }
                catch
                {
                    // 忽略读取错误
                }
            }
        }

        return new StorageUsage
        {
            TotalItems = totalItems,
            TotalSize = totalSize,
            StorageUsageMB = totalSize / (1024.0 * 1024.0),
            CategoryCounts = categoryCounts,
            TypeCounts = typeCounts
        };
    }

    private string GetFilePath(string key, StorageOptions options)
    {
        var typeDir = options.Type.ToString().ToLower();
        var categoryDir = options.Category ?? "default";
        
        // 确保键是安全的文件路径
        var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        
        var fileName = safeKey + ".json";
        
        if (options.Compression != CompressionType.None)
        {
            fileName += options.Compression switch
            {
                CompressionType.Gzip => ".gz",
                CompressionType.Brotli => ".br",
                CompressionType.Deflate => ".deflate",
                _ => ""
            };
        }

        return Path.Combine(_basePath, typeDir, categoryDir, fileName);
    }

    private async Task WriteCompressedAsync(string filePath, string content, CompressionType compression, CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        
        Stream compressionStream = compression switch
        {
            CompressionType.Gzip => new GZipStream(fileStream, CompressionLevel.Optimal),
            CompressionType.Brotli => new BrotliStream(fileStream, CompressionLevel.Optimal),
            CompressionType.Deflate => new DeflateStream(fileStream, CompressionLevel.Optimal),
            _ => fileStream
        };

        await using var writer = new StreamWriter(compressionStream);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    private async Task<string> ReadCompressedAsync(string filePath, CompressionType compression, CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        
        Stream decompressionStream = compression switch
        {
            CompressionType.Gzip => new GZipStream(fileStream, CompressionMode.Decompress),
            CompressionType.Brotli => new BrotliStream(fileStream, CompressionMode.Decompress),
            CompressionType.Deflate => new DeflateStream(fileStream, CompressionMode.Decompress),
            _ => fileStream
        };

        using var reader = new StreamReader(decompressionStream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private async Task CleanupTemporaryFilesAsync(CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(_basePath, "temporary");
        if (!Directory.Exists(tempDir)) return;

        var files = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc < DateTimeOffset.UtcNow.AddHours(-1))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temporary file {File}", file);
            }
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    private record StorageData<T>
    {
        public required string Key { get; init; }
        public required T Value { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }
        public string? Category { get; init; }
        public StorageType Type { get; init; }
    }
}
