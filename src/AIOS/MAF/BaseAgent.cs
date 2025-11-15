using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace AIOS.MAF;

/// <summary>
/// 基础代理实现 - 提供MAF代理的标准基类
/// </summary>
public abstract class BaseAgent : IAgent
{
    protected readonly ILogger _logger;
    private readonly List<IAgentEventHandler> _eventHandlers = new();
    private readonly List<IAgentCapability> _capabilities = new();
    private readonly Dictionary<string, object> _metadata = new();
    private AgentState _state = AgentState.Uninitialized;
    private DateTimeOffset? _startTime;
    private long _processedTasks = 0;
    private long _errorCount = 0;

    protected BaseAgent(ILogger logger, string id, string name, string description)
    {
        _logger = logger;
        Id = id;
        Name = name;
        Description = description;
    }

    /// <inheritdoc/>
    public string Id { get; }
    
    /// <inheritdoc/>
    public string Name { get; }
    
    /// <inheritdoc/>
    public string Description { get; }
    
    /// <inheritdoc/>
    public AgentState State => _state;
    
    /// <inheritdoc/>
    public IReadOnlyList<IAgentCapability> Capabilities => _capabilities.AsReadOnly();
    
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Metadata => _metadata.AsReadOnly();

    /// <summary>
    /// 添加能力
    /// </summary>
    protected void AddCapability(IAgentCapability capability)
    {
        _capabilities.Add(capability);
        _logger.LogDebug("Added capability {CapabilityName} to agent {AgentName}", capability.Name, Name);
    }

    /// <summary>
    /// 添加元数据
    /// </summary>
    protected void AddMetadata(string key, object value)
    {
        _metadata[key] = value;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_state != AgentState.Uninitialized)
        {
            _logger.LogWarning("Agent {AgentName} is already initialized", Name);
            return;
        }

        await SetStateAsync(AgentState.Initializing, cancellationToken);
        
        try
        {
            _logger.LogInformation("Initializing agent {AgentName}...", Name);
            await OnInitializeAsync(cancellationToken);
            await SetStateAsync(AgentState.Ready, cancellationToken);
            _logger.LogInformation("Agent {AgentName} initialized successfully", Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize agent {AgentName}", Name);
            await SetStateAsync(AgentState.Error, cancellationToken);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        if (_state != AgentState.Ready && _state != AgentState.Running)
        {
            throw new InvalidOperationException($"Agent {Name} is not ready to execute tasks");
        }

        await SetStateAsync(AgentState.Running, cancellationToken);
        _startTime ??= DateTimeOffset.UtcNow;

        await NotifyTaskStartedAsync(context, cancellationToken);

        var startTime = DateTimeOffset.UtcNow;
        
        try
        {
            _logger.LogInformation("Agent {AgentName} starting task: {Task}", Name, context.Task);
            
            var result = await OnExecuteAsync(context, cancellationToken);
            
            Interlocked.Increment(ref _processedTasks);
            
            var duration = DateTimeOffset.UtcNow - startTime;
            result = result with { Duration = duration };
            
            await NotifyTaskCompletedAsync(context, result, cancellationToken);
            
            _logger.LogInformation("Agent {AgentName} completed task in {Duration}ms", Name, duration.TotalMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _errorCount);
            
            var duration = DateTimeOffset.UtcNow - startTime;
            var result = new AgentResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = duration
            };
            
            await NotifyErrorAsync(ex, cancellationToken);
            
            _logger.LogError(ex, "Agent {AgentName} task failed", Name);
            
            return result;
        }
        finally
        {
            if (_state == AgentState.Running)
            {
                await SetStateAsync(AgentState.Ready, cancellationToken);
            }
        }
    }

    /// <inheritdoc/>
    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        if (_state == AgentState.Running)
        {
            await SetStateAsync(AgentState.Paused, cancellationToken);
            await OnPauseAsync(cancellationToken);
            _logger.LogInformation("Agent {AgentName} paused", Name);
        }
    }

    /// <inheritdoc/>
    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (_state == AgentState.Paused)
        {
            await OnResumeAsync(cancellationToken);
            await SetStateAsync(AgentState.Ready, cancellationToken);
            _logger.LogInformation("Agent {AgentName} resumed", Name);
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_state is AgentState.Running or AgentState.Ready or AgentState.Paused)
        {
            await SetStateAsync(AgentState.Stopped, cancellationToken);
            await OnStopAsync(cancellationToken);
            _logger.LogInformation("Agent {AgentName} stopped", Name);
        }
    }

    /// <inheritdoc/>
    public AgentStatus GetStatus()
    {
        return new AgentStatus
        {
            State = _state,
            StartTime = _startTime,
            ProcessedTasks = _processedTasks,
            ErrorCount = _errorCount,
            ResourceUsage = new Dictionary<string, object>
            {
                ["capabilities"] = _capabilities.Count,
                ["metadata"] = _metadata.Count,
                ["event_handlers"] = _eventHandlers.Count
            }
        };
    }

    /// <inheritdoc/>
    public void AddEventHandler(IAgentEventHandler handler)
    {
        if (!_eventHandlers.Contains(handler))
        {
            _eventHandlers.Add(handler);
        }
    }

    /// <inheritdoc/>
    public void RemoveEventHandler(IAgentEventHandler handler)
    {
        _eventHandlers.Remove(handler);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_state != AgentState.Disposed)
        {
            _ = StopAsync();
            _state = AgentState.Disposed;
            _logger.LogInformation("Agent {AgentName} disposed", Name);
        }
    }

    /// <summary>
    /// 设置代理状态
    /// </summary>
    protected async Task SetStateAsync(AgentState newState, CancellationToken cancellationToken = default)
    {
        var oldState = _state;
        _state = newState;
        
        await NotifyStateChangedAsync(oldState, newState, cancellationToken);
    }

    /// <summary>
    /// 初始化钩子方法
    /// </summary>
    protected abstract Task OnInitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行任务的钩子方法
    /// </summary>
    protected abstract Task<AgentResult> OnExecuteAsync(AgentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// 暂停钩子方法
    /// </summary>
    protected virtual Task OnPauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// 恢复钩子方法
    /// </summary>
    protected virtual Task OnResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// 停止钩子方法
    /// </summary>
    protected virtual Task OnStopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private async Task NotifyStateChangedAsync(AgentState oldState, AgentState newState, CancellationToken cancellationToken)
    {
        foreach (var handler in _eventHandlers)
        {
            try
            {
                await handler.OnStateChangedAsync(this, oldState, newState, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event handler failed for state change in agent {AgentName}", Name);
            }
        }
    }

    private async Task NotifyTaskStartedAsync(AgentContext context, CancellationToken cancellationToken)
    {
        foreach (var handler in _eventHandlers)
        {
            try
            {
                await handler.OnTaskStartedAsync(this, context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event handler failed for task start in agent {AgentName}", Name);
            }
        }
    }

    private async Task NotifyTaskCompletedAsync(AgentContext context, AgentResult result, CancellationToken cancellationToken)
    {
        foreach (var handler in _eventHandlers)
        {
            try
            {
                await handler.OnTaskCompletedAsync(this, context, result, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event handler failed for task completion in agent {AgentName}", Name);
            }
        }
    }

    private async Task NotifyErrorAsync(Exception error, CancellationToken cancellationToken)
    {
        foreach (var handler in _eventHandlers)
        {
            try
            {
                await handler.OnErrorAsync(this, error, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event handler failed for error in agent {AgentName}", Name);
            }
        }
    }
}
