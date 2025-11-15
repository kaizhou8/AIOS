using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AIOS.Kernel;
using AIOS.Runtime;
using AIOS.Memory;
using AIOS.Storage;
using AIOS.Scheduler;
using AIOS.Tools;
using AIOS.MAF;
using AIOS.Extensions;

namespace AIOS;

/// <summary>
/// AIOS主程序入口点
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 AIOS - AI Agent Operating System (C#)");
        Console.WriteLine("==========================================\n");

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // 配置AIOS服务
                ConfigureAIOS(services);
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        try
        {
            // 启动AIOS
            await host.StartAsync();
            
            // 运行演示
            await RunDemoAsync(host.Services);
            
            // 等待用户输入退出
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
            
            await host.StopAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 启动失败: {ex.Message}");
        }
    }

    private static void ConfigureAIOS(IServiceCollection services)
    {
        // 添加完整的AIOS服务（包含MAF）
        services.AddAIOS();
        
        // 注册LLM代理
        services.AddTransient<LLMAgent>(provider => 
            ActivatorUtilities.CreateInstance<LLMAgent>(
                provider, 
                "llm_general", 
                "General LLM Agent", 
                provider.GetRequiredService<ILLMRuntime>()));
                
        services.AddTransient<CodeGenerationAgent>(provider => 
            ActivatorUtilities.CreateInstance<CodeGenerationAgent>(
                provider, 
                "code_gen", 
                "Code Generation Agent", 
                provider.GetRequiredService<ILLMRuntime>()));
                
        services.AddTransient<DataAnalysisAgent>(provider => 
            ActivatorUtilities.CreateInstance<DataAnalysisAgent>(
                provider, 
                "data_analysis", 
                "Data Analysis Agent", 
                provider.GetRequiredService<ILLMRuntime>()));
    }

    private static async Task RunDemoAsync(IServiceProvider services)
    {
        var kernel = services.GetRequiredService<IAIOSKernel>();
        var scheduler = services.GetRequiredService<IScheduler>();
        var tools = services.GetRequiredService<IToolManager>();
        var runtime = services.GetRequiredService<ILLMRuntime>();

        Console.WriteLine("📋 启动AIOS内核...");
        await kernel.StartAsync();
        
        Console.WriteLine("✅ AIOS内核已启动\n");

        // 显示系统状态
        var status = kernel.GetStatus();
        Console.WriteLine($"📊 系统状态:");
        Console.WriteLine($"   运行时间: {status.Uptime:hh\:mm\:ss}");
        Console.WriteLine($"   活跃代理: {status.Metrics["active_agents"]}");
        Console.WriteLine($"   总请求数: {runtime.GetTotalRequests()}");
        Console.WriteLine();

        // 演示工具使用
        Console.WriteLine("🔧 演示工具使用:");
        var calcResult = await tools.ExecuteAsync("calculator", new Dictionary<string, object>
        {
            ["operation"] = "add",
            ["a"] = 42,
            ["b"] = 8
        });
        Console.WriteLine($"   计算器结果: {calcResult.Data}");
        
        var fsResult = await tools.ExecuteAsync("filesystem", new Dictionary<string, object>
        {
            ["operation"] = "write",
            ["path"] = "demo.txt",
            ["content"] = "Hello from AIOS!"
        });
        Console.WriteLine($"   文件系统结果: {fsResult.Success}");
        Console.WriteLine();

        // 演示任务调度
        Console.WriteLine("📅 演示任务调度:");
        var taskId = await scheduler.ScheduleAsync(new ScheduleRequest
        {
            AgentId = "demo_agent",
            Task = "analyze_data",
            Parameters = new Dictionary<string, object> { ["data"] = "sample_data" },
            Priority = TaskPriority.Normal
        });
        
        Console.WriteLine($"   已调度任务: {taskId}");
        
        // 等待任务完成
        await Task.Delay(2000);
        
        var taskStatus = await scheduler.GetStatusAsync(taskId);
        Console.WriteLine($"   任务状态: {taskStatus.State}");
        Console.WriteLine($"   任务结果: {taskStatus.Result}");
        Console.WriteLine();

        // 演示内存和存储
        Console.WriteLine("💾 演示内存和存储:");
        var memory = services.GetRequiredService<IMemoryManager>();
        await memory.StoreAsync("demo_key", "Hello AIOS!");
        var memoryValue = await memory.RetrieveAsync<string>("demo_key");
        Console.WriteLine($"   内存存储: {memoryValue}");
        
        var storage = services.GetRequiredService<IStorageManager>();
        await storage.StoreAsync("demo_storage_key", new { message = "Hello from storage!", timestamp = DateTime.UtcNow });
        var storageValue = await storage.RetrieveAsync<dynamic>("demo_storage_key");
        Console.WriteLine($"   存储读取: {storageValue?.message}");
        
        // 演示MAF代理系统
        Console.WriteLine("🤖 演示MAF代理系统:");
        var agentManager = services.GetRequiredService<IAgentManager>();
        await agentManager.InitializeAsync();
        
        // 注册代理
        var generalAgent = services.GetRequiredService<LLMAgent>();
        await agentManager.RegisterAsync(generalAgent);
        
        var codeAgent = services.GetRequiredService<CodeGenerationAgent>();
        await agentManager.RegisterAsync(codeAgent);
        
        // 智能代理选择
        var selectedAgent = await agentManager.SelectAgentAsync(new AgentContext
        {
            Id = "demo_task",
            Task = "Generate a simple C# class for a user model",
            Parameters = new Dictionary<string, object>
            {
                ["language"] = "csharp",
                ["complexity"] = "simple"
            },
            Priority = AgentPriority.Normal
        });
        
        if (selectedAgent != null)
        {
            Console.WriteLine($"   选择的代理: {selectedAgent.Name}");
            
            // 执行代理任务
            var result = await agentManager.DispatchAsync(selectedAgent.Id, new AgentContext
            {
                Id = "code_generation_task",
                Task = "Create a User class with Name and Email properties",
                Parameters = new Dictionary<string, object>
                {
                    ["prompt"] = "Generate a simple C# User class with Name and Email string properties",
                    ["provider"] = "openai"
                }
            });
            
            Console.WriteLine($"   任务结果: {(result.Success ? "成功" : "失败")}");
            if (result.Success)
            {
                Console.WriteLine($"   生成代码: {result.Data}");
            }
        }
        
        // 显示代理管理器状态
        var agentStatus = agentManager.GetStatus();
        Console.WriteLine($"   代理总数: {agentStatus.TotalAgents}");
        Console.WriteLine($"   活跃代理: {agentStatus.ActiveAgents}");
        Console.WriteLine($"   总任务数: {agentStatus.TotalProcessedTasks}");
    }
}
