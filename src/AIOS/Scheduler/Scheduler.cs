using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AIOS.Scheduler;

/// <summary>
/// 调度器实现 - 基于Channel的异步任务调度
/// </summary>
public class Scheduler : IScheduler
{
    private readonly ILogger<Scheduler> _logger;
    private readonly Channel<ScheduledTask> _taskQueue;
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _maxConcurrency;
    private Task? _processorTask;
    private bool _initialized;

    public Scheduler(ILogger<Scheduler> logger, int maxConcurrency = 10)
    {
        _logger = logger;
        _maxConcurrency = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        
        // 创建无界通道
        _taskQueue = Channel.CreateUnbounded<ScheduledTask>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing Scheduler...");
        
        // 启动任务处理器
        _processorTask = Task.Run(ProcessTasksAsync, cancellationToken);
        
        _initialized = true;
        _logger.LogInformation("Scheduler initialized successfully");
    }

    /// <inheritdoc/>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Shutting down Scheduler...");
        
        _cts.Cancel();
        
        if (_processorTask != null)
        {
            await _processorTask;
        }
        
        _cts.Dispose();
        _maxConcurrency.Dispose();
        
        _logger.LogInformation("Scheduler shut down successfully");
    }

    /// <inheritdoc/>
    public async Task<string> ScheduleAsync(ScheduleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        var taskId = Guid.NewGuid().ToString();
        var scheduledTask = new ScheduledTask
        {
            TaskId = taskId,
            AgentId = request.AgentId,
            Task = request.Task,
            Parameters = request.Parameters,
            Priority = request.Priority,
            Timeout = request.Timeout,
            CallbackUrl = request.CallbackUrl,
            State = TaskState.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _tasks[taskId] = scheduledTask;
        
        await _taskQueue.Writer.WriteAsync(scheduledTask, cancellationToken);
        
        _logger.LogInformation(
            "Scheduled task {TaskId} for agent {AgentId} with priority {Priority}",
            taskId, request.AgentId, request.Priority);

        return taskId;
    }

    /// <inheritdoc/>
    public async Task<bool> CancelAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (_tasks.TryGetValue(taskId, out var task))
        {
            if (task.State == TaskState.Pending || task.State == TaskState.Running)
            {
                task.State = TaskState.Cancelled;
                task.CompletedAt = DateTimeOffset.UtcNow;
                _logger.LogInformation("Cancelled task {TaskId}", taskId);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<TaskStatus> GetStatusAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (_tasks.TryGetValue(taskId, out var task))
        {
            return new TaskStatus
            {
                TaskId = task.TaskId,
                AgentId = task.AgentId,
                State = task.State,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                Result = task.Result,
                Error = task.Error
            };
        }

        throw new KeyNotFoundException($"Task {taskId} not found");
    }

    /// <inheritdoc/>
    public int GetActiveAgentCount()
    {
        return _tasks.Values.Count(t => t.State == TaskState.Running);
    }

    private async Task ProcessTasksAsync()
    {
        await foreach (var task in _taskQueue.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                await _maxConcurrency.WaitAsync(_cts.Token);
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteTaskAsync(task);
                    }
                    finally
                    {
                        _maxConcurrency.Release();
                    }
                }, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing task {TaskId}", task.TaskId);
            }
        }
    }

    private async Task ExecuteTaskAsync(ScheduledTask task)
    {
        task.State = TaskState.Running;
        task.StartedAt = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("Starting task {TaskId} for agent {AgentId}", task.TaskId, task.AgentId);

            // 设置超时
            using var timeoutCts = new CancellationTokenSource(task.Timeout ?? TimeSpan.FromMinutes(5));
            
            // 这里应该调用实际的代理执行逻辑
            var result = await ExecuteAgentTaskAsync(task, timeoutCts.Token);
            
            task.Result = result;
            task.State = TaskState.Completed;
            task.CompletedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation("Completed task {TaskId} in {Duration}ms",
                task.TaskId, task.Duration?.TotalMilliseconds);

            // 如果有回调URL，发送结果
            if (!string.IsNullOrEmpty(task.CallbackUrl))
            {
                await SendCallbackAsync(task);
            }
        }
        catch (OperationCanceledException)
        {
            task.State = TaskState.Timeout;
            task.Error = "Task timed out";
            task.CompletedAt = DateTimeOffset.UtcNow;
            _logger.LogWarning("Task {TaskId} timed out", task.TaskId);
        }
        catch (Exception ex)
        {
            task.State = TaskState.Failed;
            task.Error = ex.Message;
            task.CompletedAt = DateTimeOffset.UtcNow;
            _logger.LogError(ex, "Task {TaskId} failed", task.TaskId);
        }
    }

    private async Task<string> ExecuteAgentTaskAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        // 模拟代理任务执行
        await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(1, 5)), cancellationToken);
        
        return $"Task '{task.Task}' completed successfully by agent {task.AgentId}";
    }

    private async Task SendCallbackAsync(ScheduledTask task)
    {
        try
        {
            using var httpClient = new HttpClient();
            var callbackData = new
            {
                taskId = task.TaskId,
                agentId = task.AgentId,
                result = task.Result,
                error = task.Error,
                duration = task.Duration?.TotalMilliseconds
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(callbackData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            await httpClient.PostAsync(task.CallbackUrl, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send callback for task {TaskId}", task.TaskId);
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    private record ScheduledTask
    {
        public required string TaskId { get; init; }
        public required string AgentId { get; init; }
        public required string Task { get; init; }
        public Dictionary<string, object>? Parameters { get; init; }
        public TaskPriority Priority { get; init; }
        public TimeSpan? Timeout { get; init; }
        public string? CallbackUrl { get; init; }
        public TaskState State { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? Result { get; set; }
        public string? Error { get; set; }
    }
}
