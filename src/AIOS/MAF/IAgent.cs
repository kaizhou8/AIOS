using System.ComponentModel;

namespace AIOS.MAF;

/// <summary>
/// Microsoft Agent Framework 核心接口
/// 定义AI代理的基本能力和生命周期
/// </summary>
public interface IAgent : IDisposable
{
    /// <summary>
    /// 代理唯一标识符
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// 代理名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 代理描述
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// 代理状态
    /// </summary>
    AgentState State { get; }
    
    /// <summary>
    /// 代理能力列表
    /// </summary>
    IReadOnlyList<IAgentCapability> Capabilities { get; }
    
    /// <summary>
    /// 代理元数据
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
    
    /// <summary>
    /// 初始化代理
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行代理任务
    /// </summary>
    Task<AgentResult> ExecuteAsync(
        AgentContext context, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 暂停代理
    /// </summary>
    Task PauseAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 恢复代理
    /// </summary>
    Task ResumeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 停止代理
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取代理状态
    /// </summary>
    AgentStatus GetStatus();
    
    /// <summary>
    /// 添加事件处理器
    /// </summary>
    void AddEventHandler(IAgentEventHandler handler);
    
    /// <summary>
    /// 移除事件处理器
    /// </summary>
    void RemoveEventHandler(IAgentEventHandler handler);
}

/// <summary>
/// 代理状态枚举
/// </summary>
public enum AgentState
{
    /// <summary>
    /// 未初始化
    /// </summary>
    Uninitialized,
    
    /// <summary>
    /// 正在初始化
    /// </summary>
    Initializing,
    
    /// <summary>
    /// 就绪状态
    /// </summary>
    Ready,
    
    /// <summary>
    /// 运行中
    /// </summary>
    Running,
    
    /// <summary>
    /// 暂停状态
    /// </summary>
    Paused,
    
    /// <summary>
    /// 错误状态
    /// </summary>
    Error,
    
    /// <summary>
    /// 已停止
    /// </summary>
    Stopped,
    
    /// <summary>
    /// 已销毁
    /// </summary>
    Disposed
}

/// <summary>
/// 代理能力接口
/// </summary>
public interface IAgentCapability
{
    /// <summary>
    /// 能力名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 能力描述
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// 能力版本
    /// </summary>
    string Version { get; }
    
    /// <summary>
    /// 能力参数定义
    /// </summary>
    IReadOnlyList<CapabilityParameter> Parameters { get; }
    
    /// <summary>
    /// 验证参数
    /// </summary>
    bool ValidateParameters(Dictionary<string, object> parameters);
}

/// <summary>
/// 代理上下文
/// </summary>
public record AgentContext
{
    /// <summary>
    /// 上下文ID
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// 任务描述
    /// </summary>
    public required string Task { get; init; }
    
    /// <summary>
    /// 输入参数
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();
    
    /// <summary>
    /// 历史上下文
    /// </summary>
    public IReadOnlyList<AgentContext> History { get; init; } = new List<AgentContext>();
    
    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
    
    /// <summary>
    /// 超时设置
    /// </summary>
    public TimeSpan? Timeout { get; init; }
    
    /// <summary>
    /// 优先级
    /// </summary>
    public AgentPriority Priority { get; init; } = AgentPriority.Normal;
}

/// <summary>
/// 代理执行结果
/// </summary>
public record AgentResult
{
    /// <summary>
    /// 执行是否成功
    /// </summary>
    public required bool Success { get; init; }
    
    /// <summary>
    /// 结果数据
    /// </summary>
    public object? Data { get; init; }
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// 执行时间
    /// </summary>
    public required TimeSpan Duration { get; init; }
    
    /// <summary>
    /// 使用的token数
    /// </summary>
    public int TokenUsage { get; init; }
    
    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// 代理状态信息
/// </summary>
public record AgentStatus
{
    /// <summary>
    /// 当前状态
    /// </summary>
    public required AgentState State { get; init; }
    
    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }
    
    /// <summary>
    /// 运行时间
    /// </summary>
    public TimeSpan? Uptime => State == AgentState.Running ? DateTimeOffset.UtcNow - StartTime : null;
    
    /// <summary>
    /// 已处理任务数
    /// </summary>
    public long ProcessedTasks { get; init; }
    
    /// <summary>
    /// 错误次数
    /// </summary>
    public long ErrorCount { get; init; }
    
    /// <summary>
    /// 资源使用情况
    /// </summary>
    public Dictionary<string, object> ResourceUsage { get; init; } = new();
}

/// <summary>
/// 代理优先级
/// </summary>
public enum AgentPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// 能力参数定义
/// </summary>
public record CapabilityParameter
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// 参数类型
    /// </summary>
    public required string Type { get; init; }
    
    /// <summary>
    /// 参数描述
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// 是否必需
    /// </summary>
    public bool Required { get; init; } = true;
    
    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; init; }
    
    /// <summary>
    /// 允许的值列表
    /// </summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }
}

/// <summary>
/// 代理事件处理器
/// </summary>
public interface IAgentEventHandler
{
    /// <summary>
    /// 状态变更事件
    /// </summary>
    Task OnStateChangedAsync(IAgent agent, AgentState oldState, AgentState newState, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 任务开始事件
    /// </summary>
    Task OnTaskStartedAsync(IAgent agent, AgentContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 任务完成事件
    /// </summary>
    Task OnTaskCompletedAsync(IAgent agent, AgentContext context, AgentResult result, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 错误事件
    /// </summary>
    Task OnErrorAsync(IAgent agent, Exception error, CancellationToken cancellationToken = default);
}
