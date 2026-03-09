# Phase 27: Built-in Modules - Research

**Researched:** 2026-03-02
**Domain:** .NET 8 / Blazor Server / OpenAnima module system — implementing built-in processing modules
**Confidence:** HIGH (all findings are based on direct codebase analysis)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### 固定文本模块 (FixedTextModule)
- **模板系统**：支持 `{{variable}}` 语法的模板插值
- **变量来源**：
  - 静态变量：在配置面板中定义 key-value 对（如 `name=Alice`）
  - 动态变量：通过输入端口接收（端口名即变量名，如输入端口 `input1` 对应 `{{input1}}`）
- **触发机制**：事件驱动——每次输入端口收到新数据时触发输出；无输入时可被 WiringEngine 直接执行输出静态内容
- **配置 UI**：复用 EditorConfigSidebar 的 key-value 表单 + 添加 textarea 编辑模板内容
- **端口**：动态输入端口（可选）+ 单个 Text 输出端口

#### TextJoin 模块（合并 Concat 和 Merge）
- **功能**：将多个文本输入拼接为一个输出
- **输入端口**：动态数量的 Text 输入端口（用户可在配置中添加/删除）
- **分隔符**：可配置分隔符字符串（默认为空），在配置面板中设置
- **输出端口**：单个 Text 输出端口
- **说明**：BUILTIN-03 (concat) 和 BUILTIN-05 (merge) 功能重叠，合并为一个模块

#### TextSplit 模块
- **功能**：按分隔符拆分文本
- **分隔符类型**：字符串分隔符（如逗号、换行符等），在配置面板中设置
- **输入端口**：单个 Text 输入端口
- **输出方式**：单个 Text 输出端口，输出 JSON 数组字符串（如 `["part1", "part2", "part3"]`）
- **说明**：由于端口系统是静态定义的，采用 JSON 数组输出避免动态端口问题

#### 条件分支模块 (ConditionalBranchModule)
- **条件表达式语法**：支持表达式语法，用户在配置面板中填写表达式字符串
  - 引用输入数据：使用 `input` 关键字（如 `input.contains("hello")`、`input.length > 10`、`input == "yes"`）
  - 支持的操作：字符串方法（contains、startsWith、endsWith）、比较运算符（==、!=、>、<、>=、<=）、逻辑运算符（&&、||、!）、属性访问（length）
- **输入端口**：单个 Text 输入端口
- **输出端口**：两个 Text 输出端口（`true` 和 `false`）
- **输出数据**：透传输入数据到匹配的分支，不匹配的分支不触发
- **执行逻辑**：表达式求值为 true 时触发 `true` 端口，否则触发 `false` 端口

#### LLM 模块配置扩展
- **配置字段**（BUILTIN-07/08/09）：
  - `apiUrl`：LLM API 端点 URL（文本输入）
  - `apiKey`：API 密钥（文本输入，界面使用 password 类型遮罩显示）
  - `modelName`：模型名称（文本输入，如 `gpt-4`、`claude-3-opus`）
- **存储方式**：明文存储在 JSON 配置文件中（本地应用可接受）
- **安全措施**：
  - 配置面板中 API Key 输入框使用 `type="password"` 遮罩显示
  - 日志输出时脱敏处理（不输出完整 key）
- **运行时行为**：配置的 API URL/Key/Model 覆盖全局 LLMOptions，实现每个 Anima 独立配置 LLM
- **现有代码**：LLMModule 已存在，需要扩展配置读取逻辑和 EditorConfigSidebar 支持

#### Heartbeat 模块可选性 (BUILTIN-10)
- **需求**：Heartbeat 模块是可选的，不是 Anima 运行的必需模块
- **实现**：HeartbeatModule 已存在，确保它不在默认配置中自动添加，用户可从模块面板手动添加

### Claude's Discretion
- 模块的具体实现细节（如表达式解析器的实现、错误处理策略）
- 配置面板的具体布局和样式细节
- 日志输出格式和级别
- 单元测试和集成测试的设计

### Deferred Ideas (OUT OF SCOPE)
无 — 讨论保持在阶段范围内
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| BUILTIN-01 | Fixed text module outputs configurable text content | FixedTextModule: new module following IModuleExecutor pattern; reads template + static vars from AnimaModuleConfigService; publishes to EventBus output port |
| BUILTIN-02 | User can edit fixed text content in detail panel | EditorConfigSidebar needs `textarea` field type support for template editing; module config stored as key-value (key="template", value="...") |
| BUILTIN-03 | Text concat module concatenates two text inputs | Satisfied by TextJoinModule (merged with BUILTIN-05); fixed two input ports "input1" and "input2" plus separator config |
| BUILTIN-04 | Text split module splits text by delimiter | TextSplitModule: single input, single output publishing JSON array string; delimiter from AnimaModuleConfigService |
| BUILTIN-05 | Text merge module merges multiple inputs into one output | Satisfied by TextJoinModule (merged with BUILTIN-03); dynamically supports N inputs via fixed port declarations for typical use |
| BUILTIN-06 | Conditional branch module routes based on condition expression | ConditionalBranchModule: custom expression evaluator (no external library required given limited operator set); two output ports "true" and "false" |
| BUILTIN-07 | LLM module allows configuration of API URL in detail panel | Extend LLMModule.ExecuteAsync to call AnimaModuleConfigService; EditorConfigSidebar reads config and passes to LLMService |
| BUILTIN-08 | LLM module allows configuration of API key in detail panel | EditorConfigSidebar needs `password` field type support; LLMModule reads "apiKey" config key |
| BUILTIN-09 | LLM module allows configuration of model name in detail panel | LLMModule reads "modelName" config key; create per-Anima ChatClient instead of global singleton |
| BUILTIN-10 | Heartbeat module is optional (not required for Anima to run) | Remove HeartbeatModule from WiringInitializationService.ModuleTypes auto-init list; ensure it stays registered in PortRegistry so it appears in palette and can be added manually |
</phase_requirements>

## Summary

Phase 27 implements a rich set of built-in modules (FixedText, TextJoin, TextSplit, ConditionalBranch) and extends the existing LLM module with per-Anima configuration. All modules follow the well-established `IModuleExecutor` pattern: attribute-declared ports, EventBus subscription for inputs, EventBus publish for outputs, and config read from `IAnimaModuleConfigService`. The codebase provides exact templates to copy.

The two most architecturally involved tasks are: (1) the LLM per-Anima config override — the global `ChatClient` singleton is currently constructed from `LLMOptions` at startup, so LLMModule must create a local `ChatClient` from per-Anima config if those config keys are present; and (2) `EditorConfigSidebar` currently renders all config fields as plain `<input type="text">`, so it must gain the ability to render `textarea` (for FixedText template editing) and `password` (for LLM API Key) field types. These are the only two non-trivial integration points; everything else is new module files.

**Primary recommendation:** Implement the four new module classes first, register them in WiringInitializationService, then extend EditorConfigSidebar for textarea/password, then wire LLMModule per-Anima config. Each concern is independently testable via the existing module test patterns.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| OpenAnima.Contracts | project | IModuleExecutor, port attributes, IEventBus, ModuleEvent | Already used by all modules |
| OpenAnima.Core.Services.IAnimaModuleConfigService | project | Per-Anima module config persistence | Used by EditorConfigSidebar and all future modules |
| System.Text.Json | .NET 8 built-in | JSON serialization for TextSplit output and config | Already used throughout codebase |
| OpenAI (Azure SDK) | project reference | ChatClient creation for per-Anima LLM override | Already used in LLMService and Program.cs |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.Logging | .NET 8 built-in | Module execution logging | All modules inject ILogger |
| System.ClientModel | .NET 8 SDK | ApiKeyCredential for ChatClient construction | LLMModule per-Anima override |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom expression parser for ConditionalBranch | NCalc, DynamicLinq | External libs add dependency; the defined operator set is small enough for a ~100-line hand-rolled parser |
| JSON array string for TextSplit output | Dynamic ports | Dynamic ports require PortRegistry changes and schema changes; JSON string output is safe with existing PortType.Text |

**Installation:** No new NuGet packages required. All needed libraries are already present.

## Architecture Patterns

### Recommended Project Structure
```
src/OpenAnima.Core/
├── Modules/
│   ├── FixedTextModule.cs          (new)
│   ├── TextJoinModule.cs           (new)
│   ├── TextSplitModule.cs          (new)
│   ├── ConditionalBranchModule.cs  (new)
│   ├── LLMModule.cs                (extend existing)
│   ├── HeartbeatModule.cs          (no change)
│   └── ...
├── Components/Shared/
│   └── EditorConfigSidebar.razor   (extend: textarea + password field types)
└── Hosting/
    └── WiringInitializationService.cs  (extend: add new modules, remove Heartbeat from auto-init)
```

### Pattern 1: Standard IModuleExecutor Implementation
**What:** Every built-in module follows: attribute ports → constructor injection → InitializeAsync subscribes to input port events → EventBus handler calls ExecuteAsync or processes inline → ExecuteAsync publishes output.
**When to use:** All four new modules.

```csharp
// Source: LLMModule.cs and ChatOutputModule.cs in codebase
[InputPort("input", PortType.Text)]
[OutputPort("output", PortType.Text)]
public class TextSplitModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IAnimaContext _animaContext;
    private readonly ILogger<TextSplitModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();
    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private string? _pendingInput;

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "TextSplitModule", "1.0.0", "Splits text by delimiter into JSON array");

    public TextSplitModule(IEventBus eventBus, IAnimaModuleConfigService configService,
        IAnimaContext animaContext, ILogger<TextSplitModule> logger)
    { /* assign fields */ }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.input",
            async (evt, token) =>
            {
                _pendingInput = evt.Payload;
                await ExecuteAsync(token);
            });
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (_pendingInput == null) return;
        _state = ModuleExecutionState.Running;
        _lastError = null;
        try
        {
            var animaId = _animaContext.ActiveAnimaId ?? "";
            var config = _configService.GetConfig(animaId, Metadata.Name);
            var delimiter = config.TryGetValue("delimiter", out var d) ? d : ",";
            var parts = _pendingInput.Split(delimiter);
            var json = System.Text.Json.JsonSerializer.Serialize(parts);

            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.output",
                SourceModuleId = Metadata.Name,
                Payload = json
            }, ct);
            _state = ModuleExecutionState.Completed;
        }
        catch (Exception ex) { _state = ModuleExecutionState.Error; _lastError = ex; throw; }
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
```

### Pattern 2: Module Registration in WiringInitializationService
**What:** `WiringInitializationService` holds a static array `ModuleTypes` that drives both port discovery and module initialization. New modules must be added to this array, and Heartbeat must be moved out of auto-initialization while keeping port registration (so it appears in the ModulePalette).
**When to use:** Each new module class added.

```csharp
// Source: WiringInitializationService.cs in codebase
// Current:
private static readonly Type[] ModuleTypes =
{
    typeof(LLMModule), typeof(ChatInputModule),
    typeof(ChatOutputModule), typeof(HeartbeatModule)
};

// Required change — split into two arrays:
private static readonly Type[] PortRegistrationTypes =
{
    typeof(LLMModule), typeof(ChatInputModule), typeof(ChatOutputModule),
    typeof(HeartbeatModule), // Keep for ModulePalette visibility
    typeof(FixedTextModule), typeof(TextJoinModule),
    typeof(TextSplitModule), typeof(ConditionalBranchModule)
};

private static readonly Type[] AutoInitModuleTypes =
{
    typeof(LLMModule), typeof(ChatInputModule), typeof(ChatOutputModule),
    // HeartbeatModule intentionally excluded — BUILTIN-10
    typeof(FixedTextModule), typeof(TextJoinModule),
    typeof(TextSplitModule), typeof(ConditionalBranchModule)
};
```

### Pattern 3: DI Registration for New Modules
**What:** New modules are registered as singletons in `WiringServiceExtensions.AddWiringServices()`, matching the existing pattern for LLMModule, ChatInputModule, etc.
**When to use:** Each new module class added.

```csharp
// Source: WiringServiceExtensions.cs in codebase
services.AddSingleton<FixedTextModule>();
services.AddSingleton<TextJoinModule>();
services.AddSingleton<TextSplitModule>();
services.AddSingleton<ConditionalBranchModule>();
// Note: HeartbeatModule already registered as singleton — no change needed
```

### Pattern 4: EditorConfigSidebar Field Type Extension
**What:** The sidebar currently renders all config keys as `<input type="text">`. A module-specific field metadata mechanism is needed to support `textarea` (FixedText template) and `password` (LLM API key). The simplest approach: introduce a naming convention where keys with a `__type` suffix hint (e.g., `template` → if key == "template" render textarea, `apiKey` → if key == "apiKey" render password input). Alternatively, register field metadata per module.
**When to use:** When adding FixedTextModule (textarea for "template") and extending LLMModule config UI (password for "apiKey").

The recommended approach is **key-name-based rendering rules** in EditorConfigSidebar — no new infrastructure required:

```razor
@* In EditorConfigSidebar.razor config form loop *@
@foreach (var kvp in _currentConfig)
{
    <div class="config-field">
        <label>@kvp.Key</label>
        @if (kvp.Key == "template")
        {
            <textarea @oninput="@(e => HandleConfigChanged(kvp.Key, e.Value?.ToString() ?? string.Empty))">@kvp.Value</textarea>
        }
        else if (kvp.Key == "apiKey")
        {
            <input type="password"
                   value="@kvp.Value"
                   @oninput="@(e => HandleConfigChanged(kvp.Key, e.Value?.ToString() ?? string.Empty))" />
        }
        else
        {
            <input type="text"
                   value="@kvp.Value"
                   @oninput="@(e => HandleConfigChanged(kvp.Key, e.Value?.ToString() ?? string.Empty))" />
        }
    </div>
}
```

### Pattern 5: Template Interpolation for FixedTextModule
**What:** `{{variable}}` substitution using `string.Replace()` in a loop — no external library needed given the simple syntax.
**When to use:** FixedTextModule.ExecuteAsync.

```csharp
// Source: Phase 20 decisions (string.Replace() for template substitution)
private string InterpolateTemplate(string template, Dictionary<string, string> variables)
{
    var result = template;
    foreach (var (key, value) in variables)
        result = result.Replace($"{{{{{key}}}}}", value);
    return result;
}
```

### Pattern 6: ConditionalBranch Expression Evaluator
**What:** A simple recursive-descent or regex-based parser for the defined subset: string methods (`contains`, `startsWith`, `endsWith`), comparison operators (`==`, `!=`, `>`, `<`, `>=`, `<=`), logical operators (`&&`, `||`, `!`), and `length` property. The `input` keyword refers to the received string payload.
**When to use:** ConditionalBranchModule.ExecuteAsync.

Design: Parse expression string into an AST or evaluate directly with a tokenizer. The operator set is small enough for a ~80-line evaluator. No NuGet package needed.

```csharp
// Pseudo-implementation sketch
private bool EvaluateExpression(string expression, string inputValue)
{
    var expr = expression.Trim();

    // Handle logical operators (lowest precedence)
    if (TrySplitOnOperator(expr, "||", out var left, out var right))
        return EvaluateExpression(left, inputValue) || EvaluateExpression(right, inputValue);
    if (TrySplitOnOperator(expr, "&&", out left, out right))
        return EvaluateExpression(left, inputValue) && EvaluateExpression(right, inputValue);
    if (expr.StartsWith("!"))
        return !EvaluateExpression(expr[1..].Trim(), inputValue);

    // Handle method calls: input.contains("x"), input.startsWith("x"), input.endsWith("x")
    if (expr.StartsWith("input.contains("))
        return inputValue.Contains(ExtractStringArg(expr));
    if (expr.StartsWith("input.startsWith("))
        return inputValue.StartsWith(ExtractStringArg(expr));
    if (expr.StartsWith("input.endsWith("))
        return inputValue.EndsWith(ExtractStringArg(expr));

    // Handle comparisons: input == "x", input != "x", input.length > 10
    // ...parse left, op, right then compare

    throw new ArgumentException($"Cannot evaluate expression: {expr}");
}
```

### Pattern 7: LLM Per-Anima Config Override
**What:** `LLMModule.ExecuteAsync` must check `IAnimaModuleConfigService` for per-Anima `apiUrl`/`apiKey`/`modelName`. If all three are present, create a new `ChatClient` with those parameters instead of using the injected global `ILLMService`. If absent, fall back to global `ILLMService`.
**When to use:** LLMModule extension.

The `ChatClient` construction pattern is already in `Program.cs`:
```csharp
// Source: Program.cs
var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) };
var client = new ChatClient(
    model: options.Model,
    credential: new ApiKeyCredential(options.ApiKey),
    options: clientOptions);
```

LLMModule needs `IAnimaModuleConfigService` and `IAnimaContext` added to its constructor, and the execute method should create a transient `LLMService` (or call ChatClient directly) when per-Anima config is present.

### Anti-Patterns to Avoid
- **Registering HeartbeatModule in AutoInitModuleTypes**: This would violate BUILTIN-10. HeartbeatModule must remain in port registration (so it appears in ModulePalette) but NOT in the auto-initialization list.
- **Dynamic port counts at runtime**: The port system uses C# attributes for static declaration and `PortDiscovery` scans at startup. Dynamically adding/removing ports is not supported. TextJoin must declare fixed ports at class definition time (e.g., `input1`, `input2`, `input3` as reasonable fixed set, or just two for concat use case).
- **Sharing module state across Animas**: Modules are currently singletons (ANIMA-08 known limitation). FixedTextModule, TextJoinModule, etc. must derive their config from `IAnimaContext.ActiveAnimaId` at execution time, not cache it at initialization.
- **Creating ChatClient in DI singleton scope**: When LLMModule creates a per-Anima ChatClient, it must do so per-execution (or cache by animaId), not store it as a singleton field — multiple Animas could call it with different credentials.
- **Logging API keys**: When LLMModule logs config, it must mask the `apiKey` field (per locked decisions). Use `apiKey[..4] + "***"` or similar.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization for TextSplit output | Custom array formatter | `System.Text.Json.JsonSerializer.Serialize(parts)` | Already used throughout codebase; handles edge cases |
| Config persistence for module settings | File I/O in modules | `IAnimaModuleConfigService.GetConfig()` + `SetConfigAsync()` | Already exists, handles locking, per-Anima scoping, JSON persistence |
| Template string interpolation | Regex engine | Simple `string.Replace($"{{{{{key}}}}}", value)` loop | The syntax is simple and fixed; regex is overkill |
| ChatClient creation | Custom HTTP client | `OpenAI.ChatClient` from existing `OpenAI` NuGet reference | Already used in LLMService; same constructor pattern in Program.cs |

**Key insight:** The codebase is extremely well-factored. All cross-cutting concerns (config, EventBus, port registration, logging) are already solved. This phase is mostly "follow the existing module pattern four more times" plus two targeted extensions.

## Common Pitfalls

### Pitfall 1: TextJoin Dynamic Port Count Mismatch
**What goes wrong:** The decisions say TextJoin has "dynamic number of Text input ports (user can add/delete in config)." However, the port system uses static `[InputPort]` attributes — there is no mechanism for runtime port addition.
**Why it happens:** Port discovery runs at startup by scanning C# attributes. `PortRegistry.RegisterPorts()` can be called programmatically, but the editor's canvas and wiring connection system expects ports to be fixed.
**How to avoid:** Declare a fixed set of input ports for the typical use cases. For BUILTIN-03 (concat two inputs) and BUILTIN-05 (merge multiple), a reasonable approach is declaring 2-4 fixed input ports (e.g., `input1`, `input2`, `input3`, `input4`) that are all optional — the module waits until it has received at least one value and then joins whichever ports have been received. This satisfies both requirements within the static port system.
**Warning signs:** Any attempt to call `portRegistry.RegisterPorts()` from within a module's `InitializeAsync` with different port counts based on config.

### Pitfall 2: IAnimaContext.ActiveAnimaId Used in InitializeAsync
**What goes wrong:** Modules subscribe to EventBus in `InitializeAsync`, which runs at app startup before any Anima is active. If a module caches `ActiveAnimaId` at subscribe time, it will always use the startup Anima's config.
**Why it happens:** Modules are singletons. Config lookup must happen at execution time, not initialization time.
**How to avoid:** Always call `_animaContext.ActiveAnimaId` in `ExecuteAsync`, not in `InitializeAsync`. This is the existing pattern in LLMModule (though LLMModule doesn't currently use per-Anima config). See how EditorConfigSidebar subscribes to `ActiveAnimaChanged` to reload config — modules don't need this because they look up config at execution time.
**Warning signs:** `var animaId = _animaContext.ActiveAnimaId;` in `InitializeAsync`.

### Pitfall 3: LLM Per-Anima Config Validation Order
**What goes wrong:** If any one of `apiUrl`/`apiKey`/`modelName` is missing from per-Anima config, falling back silently to global config is correct — but half-configured state (e.g., per-Anima URL but global key) is ambiguous.
**Why it happens:** Partial config is common during first-time setup when user fills fields one by one.
**How to avoid:** Define a clear fallback rule: use per-Anima config only if ALL three fields (`apiUrl`, `apiKey`, `modelName`) are non-empty; otherwise fall back entirely to global `ILLMService`. Log at DEBUG when falling back.
**Warning signs:** Using per-Anima URL with global API key — this would silently fail with 401.

### Pitfall 4: EditorConfigSidebar Validation Breaks for Empty Template Fields
**What goes wrong:** The current `HandleConfigChanged` validation treats any empty value as a validation error. A FixedText module's template field may legitimately be edited to be blank (user is clearing it). A `type="textarea"` template field shouldn't block auto-save while user is typing.
**Why it happens:** The `if (string.IsNullOrWhiteSpace(newValue)) { _validationErrors[key] = "empty"; }` check applies to ALL fields.
**How to avoid:** Allow empty values for the "template" config key — it is a valid state (outputs empty string). Consider making the validation rule per-key rather than global, or simply skipping validation for "template".
**Warning signs:** User cannot clear a template field without a red validation error appearing.

### Pitfall 5: ConditionalBranch Expression Errors at Runtime
**What goes wrong:** User enters a malformed expression (unclosed quotes, unsupported operator). Module enters Error state and the error propagates through WiringEngine.
**Why it happens:** Expression evaluation with user-provided strings is inherently unsafe.
**How to avoid:** Wrap expression evaluation in a try/catch. On parse/eval error, log the error, set `_state = ModuleExecutionState.Error`, and publish to the `false` branch as a safe fallback (or publish to neither and just stop). Document in config panel that invalid expressions default to `false`.
**Warning signs:** Unhandled `FormatException` or `ArgumentException` from expression parser escaping into WiringEngine's catch block.

### Pitfall 6: HeartbeatModule Disappears from ModulePalette
**What goes wrong:** If HeartbeatModule is removed from `ModuleTypes` entirely (both port registration and initialization), it will no longer appear in the ModulePalette — users won't be able to add it manually.
**Why it happens:** `ModulePalette.LoadAvailableModules()` reads from `_portRegistry.GetAllPorts()`, which is populated by `WiringInitializationService.RegisterModulePorts()`. If HeartbeatModule is not in the port registration list, it won't appear.
**How to avoid:** Split `ModuleTypes` into two arrays: one for port registration (includes HeartbeatModule), one for auto-initialization (excludes HeartbeatModule). Both arrays are iterated in `StartAsync`.
**Warning signs:** HeartbeatModule missing from editor ModulePalette after the change.

## Code Examples

Verified patterns from codebase analysis:

### EventBus Subscription (from ChatOutputModule.cs)
```csharp
public Task InitializeAsync(CancellationToken cancellationToken = default)
{
    var sub = _eventBus.Subscribe<string>(
        $"{Metadata.Name}.port.displayText",
        (evt, ct) =>
        {
            // Process evt.Payload here
            return Task.CompletedTask;
        });
    _subscriptions.Add(sub);
    return Task.CompletedTask;
}
```

### EventBus Publish (from LLMModule.cs)
```csharp
await _eventBus.PublishAsync(new ModuleEvent<string>
{
    EventName = $"{Metadata.Name}.port.response",
    SourceModuleId = Metadata.Name,
    Payload = result.Content
}, ct);
```

### Config Read (from AnimaModuleConfigService.cs)
```csharp
// In ExecuteAsync — always read at execution time, not initialization time
var animaId = _animaContext.ActiveAnimaId ?? "";
var config = _configService.GetConfig(animaId, Metadata.Name);
var delimiter = config.TryGetValue("delimiter", out var d) ? d : ",";
```

### ChatClient Construction for Per-Anima LLM (from Program.cs)
```csharp
var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(apiUrl) };
var chatClient = new ChatClient(
    model: modelName,
    credential: new ApiKeyCredential(apiKey),
    options: clientOptions);
// Then use: await chatClient.CompleteChatAsync(messages, cancellationToken: ct)
```

### Port Attribute Declaration (from HeartbeatModule.cs / LLMModule.cs)
```csharp
[InputPort("prompt", PortType.Text)]
[OutputPort("response", PortType.Text)]
public class LLMModule : IModuleExecutor { ... }
```

### Module Registration in DI (from WiringServiceExtensions.cs)
```csharp
services.AddSingleton<TextSplitModule>();
services.AddSingleton<TextJoinModule>();
services.AddSingleton<FixedTextModule>();
services.AddSingleton<ConditionalBranchModule>();
```

### Adding to Port Registration in WiringInitializationService (from WiringInitializationService.cs)
```csharp
private static readonly Type[] PortRegistrationTypes =
{
    typeof(LLMModule), typeof(ChatInputModule), typeof(ChatOutputModule),
    typeof(HeartbeatModule),           // keep for ModulePalette
    typeof(FixedTextModule),           // new
    typeof(TextJoinModule),            // new
    typeof(TextSplitModule),           // new
    typeof(ConditionalBranchModule),   // new
};

private static readonly Type[] AutoInitModuleTypes =
{
    typeof(LLMModule), typeof(ChatInputModule), typeof(ChatOutputModule),
    // HeartbeatModule removed — BUILTIN-10
    typeof(FixedTextModule),
    typeof(TextJoinModule),
    typeof(TextSplitModule),
    typeof(ConditionalBranchModule),
};
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Global singleton ChatClient + global LLMOptions | Per-Anima config override in LLMModule + fallback to global | Phase 27 (this phase) | Each Anima can use a different LLM provider |
| HeartbeatModule auto-initialized at startup | HeartbeatModule optional — user adds from ModulePalette | Phase 27 (this phase) | Anima can run without heartbeat loop |
| EditorConfigSidebar renders all config as text inputs | EditorConfigSidebar renders textarea for "template", password for "apiKey" | Phase 27 (this phase) | Better UX for secret fields and multi-line content |

**Deprecated/outdated:**
- `ModuleTypes` static array in WiringInitializationService: Will be split into two arrays (`PortRegistrationTypes` and `AutoInitModuleTypes`) in this phase.

## Open Questions

1. **TextJoin port count decision**
   - What we know: Port system is static; `[InputPort]` attributes cannot vary at runtime. User decisions say "dynamic number of inputs (user can add/delete in config)."
   - What's unclear: How to reconcile dynamic config intention with static port system. The architectural constraint (static ports) is firm.
   - Recommendation: Declare a fixed set of 2-3 input ports (`input1`, `input2`, `input3`). The module waits to collect all connected ports' data before joining, using a per-execution buffer. This satisfies BUILTIN-03 (2 inputs) and BUILTIN-05 (merge multiple) within the static system. The "dynamic" aspect of the user decision appears to be a UX aspiration — the technical implementation must use fixed ports.

2. **LLMModule per-Anima ChatClient thread safety**
   - What we know: LLMModule is a singleton. Multiple Animas could trigger it concurrently with different per-Anima configs.
   - What's unclear: Whether creating a new `ChatClient` per execution is acceptable performance-wise, or whether per-Anima caching is needed.
   - Recommendation: Create `ChatClient` per-execution when per-Anima config is detected. `ChatClient` from the OpenAI SDK is lightweight to construct. Cache with `Dictionary<string, ChatClient>` keyed by animaId only if performance profiling shows an issue — not needed initially.

3. **FixedTextModule with no inputs vs. with inputs**
   - What we know: Trigger mechanism is "event-driven when input arrives; without input, WiringEngine can directly execute to output static content."
   - What's unclear: How WiringEngine "directly executes" a source module. Looking at `WiringEngine.ExecuteModuleAsync`, it publishes `{moduleName}.execute` on the EventBus. But no existing module subscribes to `.execute` — they are either tick-driven (HeartbeatModule) or input-port-driven.
   - Recommendation: FixedTextModule subscribes to `{Metadata.Name}.execute` in `InitializeAsync` in addition to input port subscriptions. When the execute event fires and no inputs are pending, it outputs the static template content. This aligns with how WiringEngine orchestrates modules.

## Sources

### Primary (HIGH confidence)
- Direct codebase analysis — `LLMModule.cs`, `HeartbeatModule.cs`, `ChatOutputModule.cs`, `ChatInputModule.cs` — module implementation patterns
- Direct codebase analysis — `WiringInitializationService.cs` — port registration and auto-init flow
- Direct codebase analysis — `EditorConfigSidebar.razor` — current config UI and extension points
- Direct codebase analysis — `WiringServiceExtensions.cs`, `AnimaServiceExtensions.cs` — DI registration patterns
- Direct codebase analysis — `AnimaModuleConfigService.cs`, `IAnimaModuleConfigService.cs` — config API
- Direct codebase analysis — `Program.cs` — ChatClient construction, global LLM service wiring
- Direct codebase analysis — `WiringEngine.cs` — module execution flow, execute event pattern
- Direct codebase analysis — `EventBus.cs`, `PortRegistry.cs`, `PortDiscovery.cs` — event and port infrastructure
- Direct codebase analysis — `AnimaRuntime.cs`, `HeartbeatLoop.cs` — per-Anima runtime structure
- Direct codebase analysis — `SharedResources.en-US.resx` — i18n pattern for new UI keys

### Secondary (MEDIUM confidence)
- `STATE.md` key decisions — "Port types fixed to Text and Trigger (not extensible by design)", "Zero new dependencies: Use .NET 8.0 built-ins" — confirms no new packages needed
- `27-CONTEXT.md` decisions — all implementation choices (locked by user discussion)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — verified from existing project files; no external research needed
- Architecture: HIGH — all patterns are direct copies or minimal extensions of existing, working code
- Pitfalls: HIGH — identified from architectural constraints (static ports), existing code analysis (singleton scope, validation logic), and locked design decisions

**Research date:** 2026-03-02
**Valid until:** 2026-04-02 (stable codebase, no external dependencies changing)
