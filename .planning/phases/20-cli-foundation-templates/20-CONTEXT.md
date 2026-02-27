# Phase 20: CLI Foundation & Templates - Context

**Gathered:** 2026-02-28
**Status:** Ready for planning

<domain>
## Phase Boundary

创建一个 .NET CLI 工具 (`oani`)，让开发者能够：
1. 安装为 .NET 全局工具
2. 运行 `oani new MyModule` 创建可编译的模块项目
3. 通过参数定制模板选项
4. 工具遵循标准 CLI 约定（帮助、退出码、stdout/stderr 分离）

打包、验证、运行时集成属于 Phase 21。

</domain>

<decisions>
## Implementation Decisions

### CLI 输出风格
- 静默优先：默认简洁输出，`--verbose` 查看详情
- 成功输出：简短确认消息（如 "Created MyModule/"）
- 进度提示：文字步骤（如 "Creating project..."、"Copying templates..."）
- 错误格式：单行简洁，适合终端阅读
- 帮助文档：简短描述 + 参数列表，简洁实用

### 模板定制方式
- 纯参数模式：命令行参数指定所有选项，适合 CI/CD 和脚本
- 参数风格：长短结合（如 `-t/--type`、`-n/--name`）
- 必填选项：仅模块名（作为位置参数）
- 默认值策略：不指定时生成最小模块
- 参数校验：友好提示，列出所有有效值

### 生成的项目结构
- 最小项目：只生成必需文件，开发者按需添加
- 文件结构：单文件（所有代码在一个 .cs 文件中）
- 命名空间：仅项目名（简单明了）
- 端口配置：无默认端口（开发者另外添加）
- 模块元数据：自动生成（名称、版本 1.0.0、占位作者），开发者后编辑

### 错误处理策略
- 汇总所有错误：收集所有问题一次性显示，开发者可以全部修复
- 错误类型：处理系统错误（文件、权限）、参数错误（无效值）、模板错误（缺失、格式）
- 退出码：简单二值（0=成功，非0=失败）
- 输出流：标准分离（错误→stderr，成功→stdout）
- 建议提示：关键错误提供解决建议

### Claude's Discretion
- 具体的参数名称设计（如 `--module-type` vs `--type`）
- 模板文件的存放位置和格式
- 日志输出的具体格式
- 多语言支持的实现方式

</decisions>

<specifics>
## Specific Ideas

- 参考标准 .NET CLI 工具的风格（如 `dotnet new`、`dotnet build`）
- 模块项目应该能直接 `dotnet build` 编译通过
- 生成的代码需要正确实现 `IModule` 和 `IModuleMetadata` 接口

</specifics>

<deferred>
## Deferred Ideas

- 模块打包和验证 — Phase 21
- 模块发布到仓库 — 未来阶段
- 交互式模板定制 — 可作为未来增强

</deferred>

---

*Phase: 20-cli-foundation-templates*
*Context gathered: 2026-02-28*