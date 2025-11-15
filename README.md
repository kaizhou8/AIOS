# AIOS-CSharp

**AI Agent Operating System - C# Implementation**

**AI代理操作系统 - C#实现**

## 🎯 项目概述

AIOS-CSharp 是 AIOS（AI Agent Operating System）的完整 C# 实现，提供了 AI 代理的运行时环境、资源管理和调度系统。

## ✨ 核心特性

### 🧠 AI运行时引擎
- **多LLM支持**: OpenAI, Ollama, HuggingFace
- **负载均衡**: 智能路由和故障转移
- **动态加载**: 运行时模型切换

### 💾 内存管理
- **上下文记忆**: 会话历史管理
- **智能缓存**: 基于相关性的记忆检索
- **过期策略**: 时间衰减和容量控制

### 💿 存储系统
- **多级存储**: 持久化、临时、缓存
- **压缩支持**: Gzip, Brotli, Deflate
- **分类管理**: 按类型和类别组织

### ⚡ 任务调度
- **优先级队列**: 支持多级优先级
- **并发控制**: 可配置并发限制
- **超时管理**: 自动任务超时处理
- **回调机制**: 异步结果通知

### 🔧 工具系统
- **内置工具**: 计算器、文件系统、Web搜索等
- **扩展机制**: 支持自定义工具注册
- **参数验证**: 类型安全和参数检查

## 🚀 快速开始

### 1. 安装依赖

```bash
# 安装 .NET 8.0 SDK
# 下载地址: https://dotnet.microsoft.com/download/dotnet/8.0

# 克隆项目
git clone https://github.com/your-org/aios-csharp.git
cd aios-csharp
```

### 2. 配置环境变量

```bash
# Linux/MacOS
export OPENAI_API_KEY="your-openai-api-key"
export HUGGINGFACE_API_TOKEN="your-huggingface-token"

# Windows
set OPENAI_API_KEY=your-openai-api-key
set HUGGINGFACE_API_TOKEN=your-huggingface-token
```

### 3. 运行示例

```bash
# 运行演示
dotnet run

# 运行测试
dotnet test

# 发布应用
dotnet publish -c Release
```

## 📋 项目结构

```
AIOS-CSharp/
├── src/
│   └── AIOS/
│       ├── Kernel/           # AIOS核心内核
│       ├── Runtime/          # LLM运行时引擎
│       ├── Memory/           # 内存管理系统
│       ├── Storage/          # 存储管理系统
│       ├── Scheduler/        # 任务调度器
│       └── Tools/            # 工具管理器
├── tests/                    # 单元测试
├── docs/                     # 文档
└── examples/                 # 示例代码
```

## 🛠️ 技术栈

- **运行时**: .NET 8.0
- **AI集成**: Microsoft Semantic Kernel
- **日志**: Serilog
- **配置**: Microsoft.Extensions.Configuration
- **依赖注入**: Microsoft.Extensions.DependencyInjection
- **并发**: System.Threading.Channels
- **序列化**: System.Text.Json

## 📊 性能指标

- **启动时间**: < 1秒
- **内存占用**: < 50MB
- **并发任务**: 支持100+并发
- **响应延迟**: < 100ms

## 🔧 配置选项

### appsettings.json

```json
{
  "AIOS": {
    "Kernel": {
      "MaxConcurrency": 10,
      "DefaultTimeout": "00:05:00"
    },
    "LLM": {
      "Providers": {
        "OpenAI": {
          "ApiKey": "${OPENAI_API_KEY}",
          "Model": "gpt-3.5-turbo"
        }
      }
    }
  }
}
```

## 🎯 使用示例

### 基本使用

```csharp
using AIOS;

// 创建AIOS实例
var kernel = services.GetRequiredService<IAIOSKernel>();
await kernel.StartAsync();

// 调度任务
var scheduler = services.GetRequiredService<IScheduler>();
var taskId = await scheduler.ScheduleAsync(new ScheduleRequest
{
    AgentId = "my_agent",
    Task = "analyze_data",
    Priority = TaskPriority.High
});

// 使用工具
var tools = services.GetRequiredService<IToolManager>();
var result = await tools.ExecuteAsync("calculator", new Dictionary<string, object>
{
    ["operation"] = "add",
    ["a"] = 10,
    ["b"] = 20
});
```

### 自定义工具

```csharp
public class MyTool : ITool
{
    public string Name => "my_tool";
    public string Description => "My custom tool";
    
    public IReadOnlyList<ToolParameter> Parameters => new[]
    {
        new ToolParameter { Name = "input", Type = "string", Description = "Input text", Required = true }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        var input = parameters["input"].ToString();
        return Task.FromResult(new ToolResult
        {
            Success = true,
            Data = $"Processed: {input}"
        });
    }
}
```

## 📈 路线图

### 短期目标 (v1.1)
- [ ] Web API接口
- [ ] 实时监控仪表板
- [ ] 插件系统
- [ ] 性能优化

### 中期目标 (v1.2)
- [ ] 分布式部署支持
- [ ] 联邦学习
- [ ] 安全增强
- [ ] 多租户支持

### 长期目标 (v2.0)
- [ ] 边缘计算支持
- [ ] 实时协作
- [ ] AI模型市场
- [ ] 企业级特性

## 🤝 贡献指南

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'Add amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 创建 Pull Request

## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

## 🆘 支持

- 📖 [文档](docs/)
- 🐛 [问题报告](https://github.com/your-org/aios-csharp/issues)
- 💬 [讨论](https://github.com/your-org/aios-csharp/discussions)

---

**AIOS-CSharp** - 为AI代理提供企业级运行环境

**Built with ❤️ by the AIOS Team**
