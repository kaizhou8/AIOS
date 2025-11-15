using Microsoft.Extensions.DependencyInjection;
using AIOS.Kernel;
using AIOS.Runtime;
using AIOS.Memory;
using AIOS.Storage;
using AIOS.Scheduler;
using AIOS.Tools;
using AIOS.MAF;

namespace AIOS.Extensions;

/// <summary>
/// 服务集合扩展 - 简化AIOS和MAF的依赖注入配置
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加完整的AIOS服务
    /// </summary>
    public static IServiceCollection AddAIOS(this IServiceCollection services)
    {
        // 核心服务
        services.AddSingleton<IAIOSKernel, AIOSKernel>();
        services.AddSingleton<ILLMRuntime, LLMRuntime>();
        services.AddSingleton<IMemoryManager, MemoryManager>();
        services.AddSingleton<IStorageManager, FileStorageManager>();
        services.AddSingleton<IScheduler, Scheduler>();
        services.AddSingleton<IToolManager, ToolManager>();
        
        // MAF服务
        services.AddSingleton<IAgentManager, AgentManager>();
        
        return services;
    }
    
    /// <summary>
    /// 添加LLM代理
    /// </summary>
    public static IServiceCollection AddLLMAgent<TAgent>(this IServiceCollection services, 
        string id, string name, string description) where TAgent : LLMAgent
    {
        services.AddTransient<TAgent>(provider => 
            ActivatorUtilities.CreateInstance<TAgent>(provider, id, name, provider.GetRequiredService<ILLMRuntime>()));
        
        return services;
    }
    
    /// <summary>
    /// 添加自定义代理
    /// </summary>
    public static IServiceCollection AddAgent<TAgent>(this IServiceCollection services, 
        string id, string name, string description) where TAgent : BaseAgent
    {
        services.AddTransient<TAgent>(provider => 
            ActivatorUtilities.CreateInstance<TAgent>(provider, id, name, description));
        
        return services;
    }
    
    /// <summary>
    /// 配置AIOS选项
    /// </summary>
    public static IServiceCollection ConfigureAIOS(this IServiceCollection services, Action<AIOSOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}

/// <summary>
/// AIOS配置选项
/// </summary>
public class AIOSOptions
{
    /// <summary>
    /// 内核配置
    /// </summary>
    public KernelOptions Kernel { get; set; } = new();
    
    /// <summary>
    /// LLM配置
    /// </summary>
    public LLMOptions LLM { get; set; } = new();
    
    /// <summary>
    /// 存储配置
    /// </summary>
    public StorageOptions Storage { get; set; } = new();
    
    /// <summary>
    /// 内存配置
    /// </summary>
    public MemoryOptions Memory { get; set; } = new();
    
    /// <summary>
    /// 调度器配置
    /// </summary>
    public SchedulerOptions Scheduler { get; set; } = new();
    
    /// <summary>
    /// 代理配置
    /// </summary>
    public AgentOptions Agents { get; set; } = new();
}

/// <summary>
/// 内核配置
/// </summary>
public class KernelOptions
{
    public int MaxConcurrency { get; set; } = 10;
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// LLM配置
/// </summary>
public class LLMOptions
{
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
}

/// <summary>
/// 提供商配置
/// </summary>
public class ProviderConfig
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-3.5-turbo";
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// 存储配置
/// </summary>
public class StorageOptions
{
    public string BasePath { get; set; } = "./storage";
    public string MaxFileSize { get; set; } = "100MB";
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// 内存配置
/// </summary>
public class MemoryOptions
{
    public int MaxEntries { get; set; } = 10000;
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// 调度器配置
/// </summary>
public class SchedulerOptions
{
    public int MaxConcurrency { get; set; } = 10;
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// 代理配置
/// </summary>
public class AgentOptions
{
    public bool AutoRegisterBuiltIn { get; set; } = true;
    public Dictionary<string, AgentConfig> CustomAgents { get; set; } = new();
}

/// <summary>
/// 代理配置
/// </summary>
public class AgentConfig
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
