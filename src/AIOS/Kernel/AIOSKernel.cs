using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using AIOS.Runtime;
using AIOS.Memory;
using AIOS.Storage;
using AIOS.Scheduler;
using AIOS.Tools;

namespace AIOS.Kernel;

/// <summary>
/// AIOS核心内核 - AI Agent Operating System Kernel
/// 负责管理LLM资源、内存、存储、工具和调度
/// </summary>
public interface IAIOSKernel
{
    /// <summary>
    /// 获取运行时引擎
    /// </summary>
    ILLMRuntime Runtime { get; }
    
    /// <summary>
    /// 获取内存管理器
    /// </summary>
    IMemoryManager Memory { get; }
    
    /// <summary>
    /// 获取存储管理器
    /// </summary>
    IStorageManager Storage { get; }
    
    /// <summary>
    /// 获取工具管理器
    /// </summary>
    IToolManager Tools { get; }
    
    /// <summary>
    /// 获取调度器
    /// </summary>
    IScheduler Scheduler { get; }
    
    /// <summary>
    /// 启动内核
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 停止内核
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取系统状态
    /// </summary>
    KernelStatus GetStatus();
}

/// <summary>
/// AIOS内核状态
/// </summary>
public record KernelStatus
{
    public bool IsRunning { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public TimeSpan Uptime => DateTimeOffset.UtcNow - StartTime;
    public Dictionary<string, object> Metrics { get; init; } = new();
}

/// <summary>
/// AIOS内核实现
/// </summary>
public class AIOSKernel : IAIOSKernel, IDisposable
{
    private readonly ILogger<AIOSKernel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _isRunning;
    private DateTimeOffset _startTime;

    public ILLMRuntime Runtime { get; }
    public IMemoryManager Memory { get; }
    public IStorageManager Storage { get; }
    public IToolManager Tools { get; }
    public IScheduler Scheduler { get; }

    public AIOSKernel(
        ILogger<AIOSKernel> logger,
        ILLMRuntime runtime,
        IMemoryManager memory,
        IStorageManager storage,
        IToolManager tools,
        IScheduler scheduler)
    {
        _logger = logger;
        Runtime = runtime;
        Memory = memory;
        Storage = storage;
        Tools = tools;
        Scheduler = scheduler;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("AIOS Kernel is already running");
            return;
        }

        _logger.LogInformation("Starting AIOS Kernel...");
        
        try
        {
            // 初始化各个子系统
            await Memory.InitializeAsync(cancellationToken);
            await Storage.InitializeAsync(cancellationToken);
            await Tools.InitializeAsync(cancellationToken);
            await Scheduler.InitializeAsync(cancellationToken);
            await Runtime.InitializeAsync(cancellationToken);

            _isRunning = true;
            _startTime = DateTimeOffset.UtcNow;
            
            _logger.LogInformation("AIOS Kernel started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start AIOS Kernel");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("AIOS Kernel is not running");
            return;
        }

        _logger.LogInformation("Stopping AIOS Kernel...");
        
        try
        {
            // 优雅关闭各个子系统
            await Runtime.ShutdownAsync(cancellationToken);
            await Scheduler.ShutdownAsync(cancellationToken);
            await Tools.ShutdownAsync(cancellationToken);
            await Storage.ShutdownAsync(cancellationToken);
            await Memory.ShutdownAsync(cancellationToken);

            _isRunning = false;
            _logger.LogInformation("AIOS Kernel stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AIOS Kernel shutdown");
            throw;
        }
    }

    /// <inheritdoc/>
    public KernelStatus GetStatus()
    {
        return new KernelStatus
        {
            IsRunning = _isRunning,
            StartTime = _startTime,
            Metrics = new Dictionary<string, object>
            {
                ["memory_usage"] = Memory.GetUsage(),
                ["storage_usage"] = Storage.GetUsage(),
                ["active_agents"] = Scheduler.GetActiveAgentCount(),
                ["total_requests"] = Runtime.GetTotalRequests()
            }
        };
    }

    public void Dispose()
    {
        if (_isRunning)
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }
}
