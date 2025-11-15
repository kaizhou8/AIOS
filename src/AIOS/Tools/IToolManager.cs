namespace AIOS.Tools;

/// <summary>
/// 工具管理器接口 - 管理AI代理可用的工具
/// </summary>
public interface IToolManager
{
    /// <summary>
    /// 注册工具
    /// </summary>
    Task RegisterAsync(ITool tool, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 注销工具
    /// </summary>
    Task UnregisterAsync(string toolName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取工具
    /// </summary>
    Task<ITool?> GetAsync(string toolName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取所有工具
    /// </summary>
    Task<IReadOnlyList<ITool>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行工具
    /// </summary>
    Task<ToolResult> ExecuteAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 初始化工具管理器
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 关闭工具管理器
    /// </summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 工具接口
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 工具描述
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// 工具参数定义
    /// </summary>
    IReadOnlyList<ToolParameter> Parameters { get; }
    
    /// <summary>
    /// 执行工具
    /// </summary>
    Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// 工具参数
/// </summary>
public record ToolParameter
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Description { get; init; }
    public bool Required { get; init; } = true;
    public object? DefaultValue { get; init; }
    public IReadOnlyList<string>? AllowedValues { get; init; }
}

/// <summary>
/// 工具执行结果
/// </summary>
public record ToolResult
{
    public required bool Success { get; init; }
    public required object? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
