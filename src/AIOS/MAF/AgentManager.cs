using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AIOS.MAF;

/// <summary>
/// 代理管理器 - 管理所有AI代理的生命周期和协调
/// </summary>
public interface IAgentManager
{
    /// <summary>
    /// 注册代理
    /// </summary>
    Task RegisterAsync(IAgent agent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 注销代理
    /// </summary>
    Task UnregisterAsync(string agentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取代理
    /// </summary>
    Task<IAgent?> GetAsync(string agentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取所有代理
    /// </summary>
    Task<IReadOnlyList<IAgent>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 按能力查找代理
    /// </summary>
    Task<IReadOnlyList<IAgent>> FindByCapabilityAsync(string capabilityName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 调度代理执行任务
    /// </summary>
    Task<AgentResult> DispatchAsync(
        string agentId, 
        AgentContext context, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 智能代理选择
    /// </summary>
    Task<IAgent?> SelectAgentAsync(
        AgentContext context, 
        AgentSelectionCriteria? criteria = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取系统状态
    /// </summary>
    AgentManagerStatus GetStatus();
    
    /// <summary>
    /// 初始化代理管理器
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 关闭代理管理器
    /// </summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 代理选择标准
/// </summary>
public record AgentSelectionCriteria
{
    /// <summary>
    /// 所需能力列表
    /// </summary>
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = new List<string>();
    
    /// <summary>
    /// 优先级要求
    /// </summary>
    public AgentPriority? MinPriority { get; init; }
    
    /// <summary>
    /// 最大负载
    /// </summary>
    public int? MaxActiveTasks { get; init; }
    
    /// <summary>
    /// 代理标签
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
    
    /// <summary>
    /// 性能要求
    /// </summary>
    public PerformanceRequirements? Performance { get; init; }
}

/// <summary>
/// 性能要求
/// </summary>
public record PerformanceRequirements
{
    /// <summary>
    /// 最大响应时间
    /// </summary>
    public TimeSpan? MaxResponseTime { get; init; }
    
    /// <summary>
    /// 最小成功率
    /// </summary>
    public double? MinSuccessRate { get; init; }
    
    /// <summary>
    /// 最大错误率
    /// </summary>
    public double? MaxErrorRate { get; init; }
}

/// <summary>
/// 代理管理器状态
/// </summary>
public record AgentManagerStatus
{
    /// <summary>
    /// 总代理数
    /// </summary>
    public int TotalAgents { get; init; }
    
    /// <summary>
    /// 活跃代理数
    /// </summary>
    public int ActiveAgents { get; init; }
    
    /// <summary>
    /// 按状态统计
    /// </summary>
    public Dictionary<AgentState, int> AgentsByState { get; init; } = new();
    
    /// <summary>
    /// 总处理任务数
    /// </summary>
    public long TotalProcessedTasks { get; init; }
    
    /// <summary>
    /// 总错误数
    /// </summary>
    public long TotalErrors { get; init; }
    
    /// <summary>
    /// 系统负载
    /// </summary>
    public double SystemLoad { get; init; }
}

/// <summary>
/// 代理管理器实现
/// </summary>
public class AgentManager : IAgentManager
{
    private readonly ILogger<AgentManager> _logger;
    private readonly ConcurrentDictionary<string, IAgent> _agents = new();
    private readonly ConcurrentDictionary<string, AgentMetrics> _metrics = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public AgentManager(ILogger<AgentManager> logger)
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
            _logger.LogInformation("Initializing Agent Manager...");
            
            // 注册内置代理
            await RegisterBuiltInAgentsAsync(cancellationToken);
            
            _initialized = true;
            _logger.LogInformation("Agent Manager initialized with {Count} agents", _agents.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Shutting down Agent Manager...");
        
        var shutdownTasks = _agents.Values.Select(agent => agent.StopAsync(cancellationToken));
        await Task.WhenAll(shutdownTasks);
        
        _agents.Clear();
        _metrics.Clear();
        
        _logger.LogInformation("Agent Manager shut down successfully");
    }

    /// <inheritdoc/>
    public async Task RegisterAsync(IAgent agent, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (_agents.TryAdd(agent.Id, agent))
        {
            _metrics[agent.Id] = new AgentMetrics();
            await agent.InitializeAsync(cancellationToken);
            _logger.LogInformation("Registered agent {AgentName} ({AgentId})", agent.Name, agent.Id);
        }
        else
        {
            _logger.LogWarning("Agent {AgentId} is already registered", agent.Id);
        }
    }

    /// <inheritdoc/>
    public async Task UnregisterAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (_agents.TryRemove(agentId, out var agent))
        {
            await agent.StopAsync(cancellationToken);
            _metrics.TryRemove(agentId, out _);
            _logger.LogInformation("Unregistered agent {AgentId}", agentId);
        }
    }

    /// <inheritdoc/>
    public async Task<IAgent?> GetAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();
        return _agents.TryGetValue(agentId, out var agent) ? agent : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IAgent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();
        return _agents.Values.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IAgent>> FindByCapabilityAsync(string capabilityName, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        return _agents.Values
            .Where(agent => agent.Capabilities.Any(c => c.Name.Equals(capabilityName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<AgentResult> DispatchAsync(
        string agentId, 
        AgentContext context, 
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (!_agents.TryGetValue(agentId, out var agent))
        {
            return new AgentResult
            {
                Success = false,
                ErrorMessage = $"Agent {agentId} not found"
            };
        }

        var metrics = _metrics.GetOrAdd(agentId, _ => new AgentMetrics());
        metrics.IncrementTasks();

        try
        {
            var result = await agent.ExecuteAsync(context, cancellationToken);
            
            if (result.Success)
            {
                metrics.IncrementSuccess();
            }
            else
            {
                metrics.IncrementError();
            }
            
            return result;
        }
        catch (Exception ex)
        {
            metrics.IncrementError();
            return new AgentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<IAgent?> SelectAgentAsync(
        AgentContext context, 
        AgentSelectionCriteria? criteria = null, 
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        criteria ??= new AgentSelectionCriteria();
        
        var candidates = _agents.Values.AsEnumerable();
        
        // 按能力过滤
        if (criteria.RequiredCapabilities.Any())
        {
            candidates = candidates.Where(agent =>
                criteria.RequiredCapabilities.All(required =>
                    agent.Capabilities.Any(capability =>
                        capability.Name.Equals(required, StringComparison.OrdinalIgnoreCase))));
        }
        
        // 按标签过滤
        if (criteria.Tags.Any())
        {
            candidates = candidates.Where(agent =>
                criteria.Tags.All(tag =>
                    agent.Metadata.ContainsKey(tag.Key) &&
                    agent.Metadata[tag.Key]?.ToString() == tag.Value));
        }
        
        // 按性能过滤
        if (criteria.Performance != null)
        {
            candidates = candidates.Where(agent =>
            {
                if (!_metrics.TryGetValue(agent.Id, out var metrics)) return true;
                
                var successRate = metrics.SuccessRate;
                var errorRate = metrics.ErrorRate;
                
                return (!criteria.Performance.MinSuccessRate.HasValue || successRate >= criteria.Performance.MinSuccessRate.Value) &&
                       (!criteria.Performance.MaxErrorRate.HasValue || errorRate <= criteria.Performance.MaxErrorRate.Value);
            });
        }
        
        // 按负载过滤
        if (criteria.MaxActiveTasks.HasValue)
        {
            candidates = candidates.Where(agent =>
                !_metrics.TryGetValue(agent.Id, out var metrics) ||
                metrics.ActiveTasks < criteria.MaxActiveTasks.Value);
        }
        
        // 按优先级排序
        var selected = candidates
            .OrderByDescending(agent => context.Priority)
            .ThenBy(agent => _metrics.GetValueOrDefault(agent.Id, new AgentMetrics()).ActiveTasks)
            .FirstOrDefault();
        
        if (selected != null)
        {
            _logger.LogDebug("Selected agent {AgentName} for task: {Task}", selected.Name, context.Task);
        }
        else
        {
            _logger.LogWarning("No suitable agent found for task: {Task}", context.Task);
        }
        
        return selected;
    }

    /// <inheritdoc/>
    public AgentManagerStatus GetStatus()
    {
        var agents = _agents.Values.ToList();
        var totalTasks = _metrics.Values.Sum(m => m.TotalTasks);
        var totalErrors = _metrics.Values.Sum(m => m.TotalErrors);
        
        var agentsByState = agents.GroupBy(a => a.State)
            .ToDictionary(g => g.Key, g => g.Count());
        
        var activeAgents = agents.Count(a => a.State == AgentState.Running);
        var systemLoad = activeAgents > 0 ? (double)activeAgents / agents.Count : 0;
        
        return new AgentManagerStatus
        {
            TotalAgents = agents.Count,
            ActiveAgents = activeAgents,
            AgentsByState = agentsByState,
            TotalProcessedTasks = totalTasks,
            TotalErrors = totalErrors,
            SystemLoad = systemLoad
        };
    }

    private async Task RegisterBuiltInAgentsAsync(CancellationToken cancellationToken)
    {
        // 这里可以注册内置代理
        // 例如：
        // var chatAgent = new ChatAgent(loggerFactory.CreateLogger<ChatAgent>());
        // await RegisterAsync(chatAgent, cancellationToken);
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    private record AgentMetrics
    {
        public long TotalTasks { get; private set; }
        public long SuccessfulTasks { get; private set; }
        public long FailedTasks { get; private set; }
        public int ActiveTasks { get; private set; }
        
        public double SuccessRate => TotalTasks > 0 ? (double)SuccessfulTasks / TotalTasks : 1.0;
        public double ErrorRate => TotalTasks > 0 ? (double)FailedTasks / TotalTasks : 0.0;
        
        public void IncrementTasks() => TotalTasks++;
        public void IncrementSuccess() => SuccessfulTasks++;
        public void IncrementError() => FailedTasks++;
        public void IncrementActive() => ActiveTasks++;
        public void DecrementActive() => ActiveTasks--;
    }
}
