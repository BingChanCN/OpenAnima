# OpenAnima

OpenAnima 是一个面向“数字生命 / 主动助手”的本地优先（local-first）平台。  
它的核心目标是：让 Agent 可以主动思考、主动行动，同时保持模块连接的可控与可验证。

## 为什么是 OpenAnima

- 主动性：不是只会“被问才答”的机器人，而是能持续运行的 Agent Runtime
- 可控性：模块之间用强类型契约连接，避免不可预测的“黑盒拼接”
- 可扩展性：插件化模块架构，支持按需装配能力
- 工程化：运行时、事件总线、心跳调度、热加载均可独立演进

## 项目状态

- `v1.0` 已完成：Core Runtime Foundation
- `v1.1` 开发中：WebUI Runtime Dashboard（Blazor Server + SignalR）

当前仓库以 `v1.0` 可运行核心为主，`v1.1` 正在推进。

## 已具备能力（Today）

- C# 模块隔离加载（`AssemblyLoadContext`）
- 强类型模块契约（`IModule` / `IModuleInput<T>` / `IModuleOutput<T>`）
- 线程安全模块注册中心
- 事件总线（发布订阅 + 请求响应）
- 100ms 心跳循环（模块 Tick 调度）
- 模块目录监听与热发现

## 公开路线图（Roadmap Snapshot）

- `v1.1`：Web 监控与控制面板（实时模块状态、心跳指标、控制操作）
- `v1.2+`：分层思考循环、LLM 集成增强、可视化编排编辑器、持久化能力

## 快速开始

环境要求：
- `.NET SDK 8.0+`

1. 发布 Runtime

```bash
dotnet publish src/OpenAnima.Core/OpenAnima.Core.csproj -c Debug -o dist/OpenAnima.Core
```

2. 发布示例模块到 Runtime 的 `modules` 目录

```bash
dotnet publish samples/SampleModule/SampleModule.csproj -c Debug -o dist/OpenAnima.Core/modules/SampleModule
cp samples/SampleModule/module.json dist/OpenAnima.Core/modules/SampleModule/module.json
```

Windows:

```powershell
Copy-Item samples/SampleModule/module.json dist/OpenAnima.Core/modules/SampleModule/module.json
```

3. 启动

```bash
dotnet dist/OpenAnima.Core/OpenAnima.Core.dll
```

## 模块开发（最小约定）

- 实现 `OpenAnima.Contracts.IModule`
- 提供公开无参构造函数
- 模块目录包含 `module.json` 与入口 DLL

`module.json` 示例：

```json
{
  "name": "SampleModule",
  "version": "1.0.0",
  "description": "A sample module for testing the plugin system",
  "entryAssembly": "SampleModule.dll"
}
```

## 仓库结构

```text
src/OpenAnima.Contracts   # 契约层
src/OpenAnima.Core        # Runtime 核心
samples/SampleModule      # 示例模块源码
modules/SampleModule      # 示例模块产物
.planning                 # 项目规划与路线图
```

## 适合谁

- 想做“可持续运行”的 Agent 系统的开发者
- 需要模块化、可验证、可演进架构的团队
- 关注本地优先与可控智能系统的产品探索者

## 文档入口

- 项目总览：`.planning/PROJECT.md`
- 当前需求：`.planning/REQUIREMENTS.md`
- 路线图：`.planning/ROADMAP.md`

## 贡献

欢迎通过 Issue / PR 参与。  
在提交前，建议先阅读 `.planning` 下的需求与路线图，确保变更与当前里程碑一致。
