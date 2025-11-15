namespace AIOS.Scheduler;

/// <summary>
/// 调度器接口 - 管理AI代理的执行调度
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// 调度一个任务
    /// </summary>
    Task<string> ScheduleAsync(ScheduleRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 取消调度任务
    /// </summary>
    Task<bool> CancelAsync(string taskId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取任务状态
    /// </summary>
    Task<TaskStatus> GetStatusAsync(string taskId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取活跃代理数量
    /// </summary>
    int GetActiveAgentCount();
    
    /// <summary>
    /// 初始化调度器
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 关闭调度器
    /// </summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 调度请求
/// </summary>
public record ScheduleRequest
{
    public required string AgentId { get; init; }
    public required string Task { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
    public TaskPriority Priority { get; init; } = TaskPriority.Normal;
    public TimeSpan? Timeout { get; init; }
    public string? CallbackUrl { get; init; }
}

/// <summary>
/// 任务优先级
/// </summary>
public enum TaskPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// 任务状态
/// </summary>
public record TaskStatus
{
    public required string TaskId { get; init; }
    public required string AgentId { get; init; }
    public required TaskState State { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? Result { get; init; }
    public string? Error { get; init; }
    public TimeSpan? Duration => CompletedAt - StartedAt;
}

/// <summary>
/// 任务状态枚举
/// </summary>
public enum TaskState
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Timeout
}
