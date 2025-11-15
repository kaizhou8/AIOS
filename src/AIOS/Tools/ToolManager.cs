using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AIOS.Tools;

/// <summary>
/// 工具管理器实现
/// </summary>
public class ToolManager : IToolManager
{
    private readonly ILogger<ToolManager> _logger;
    private readonly ConcurrentDictionary<string, ITool> _tools = new();
    private bool _initialized;

    public ToolManager(ILogger<ToolManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing Tool Manager...");
        
        // 注册内置工具
        await RegisterBuiltInToolsAsync(cancellationToken);
        
        _initialized = true;
        _logger.LogInformation("Tool Manager initialized with {Count} tools", _tools.Count);
    }

    /// <inheritdoc/>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Shutting down Tool Manager...");
        _tools.Clear();
        _initialized = false;
        _logger.LogInformation("Tool Manager shut down successfully");
    }

    /// <inheritdoc/>
    public async Task RegisterAsync(ITool tool, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        _tools[tool.Name] = tool;
        _logger.LogInformation("Registered tool: {ToolName}", tool.Name);
    }

    /// <inheritdoc/>
    public async Task UnregisterAsync(string toolName, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (_tools.TryRemove(toolName, out _))
        {
            _logger.LogInformation("Unregistered tool: {ToolName}", toolName);
        }
    }

    /// <inheritdoc/>
    public async Task<ITool?> GetAsync(string toolName, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        _tools.TryGetValue(toolName, out var tool);
        return tool;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ITool>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        return _tools.Values.ToList();
    }

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return new ToolResult
            {
                Success = false,
                ErrorMessage = $"Tool '{toolName}' not found"
            };
        }

        var startTime = DateTimeOffset.UtcNow;
        
        try
        {
            _logger.LogInformation("Executing tool {ToolName} with parameters: {Parameters}", toolName, string.Join(", ", parameters.Keys));
            
            var result = await tool.ExecuteAsync(parameters, cancellationToken);
            
            var duration = DateTimeOffset.UtcNow - startTime;
            
            _logger.LogInformation("Tool {ToolName} executed successfully in {Duration}ms", toolName, duration.TotalMilliseconds);
            
            return result with { Duration = duration };
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            
            _logger.LogError(ex, "Tool {ToolName} execution failed", toolName);
            
            return new ToolResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = duration
            };
        }
    }

    private async Task RegisterBuiltInToolsAsync(CancellationToken cancellationToken)
    {
        var tools = new List<ITool>
        {
            new CalculatorTool(),
            new FileSystemTool(),
            new WebSearchTool(),
            new DateTimeTool(),
            new MemoryTool()
        };

        foreach (var tool in tools)
        {
            await RegisterAsync(tool, cancellationToken);
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }
}
