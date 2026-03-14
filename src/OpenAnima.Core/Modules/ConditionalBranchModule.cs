using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Modules;

/// <summary>
/// Conditional branch module that evaluates an expression against the input text
/// and routes it to either the "true" or "false" output port.
///
/// Supported expression syntax (using "input" keyword to reference the incoming value):
///   - input.contains("text")
///   - input.startsWith("text")
///   - input.endsWith("text")
///   - input.length > N, input.length &lt; N, input.length >= N, input.length &lt;= N,
///     input.length == N, input.length != N
///   - input == "text", input != "text"
///   - Logical: expr1 &amp;&amp; expr2, expr1 || expr2, !expr
///   - Grouping: (expr)
/// </summary>
[InputPort("input", PortType.Text)]
[OutputPort("true", PortType.Text)]
[OutputPort("false", PortType.Text)]
public class ConditionalBranchModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly IAnimaModuleConfigService _configService;
    private readonly IAnimaContext _animaContext;
    private readonly ILogger<ConditionalBranchModule> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;
    private readonly SemaphoreSlim _executionGuard = new SemaphoreSlim(1, 1);

    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "ConditionalBranchModule", "1.0.0", "Routes input to true/false branch based on expression");

    public ConditionalBranchModule(
        IEventBus eventBus,
        IAnimaModuleConfigService configService,
        IAnimaContext animaContext,
        ILogger<ConditionalBranchModule> logger)
    {
        _eventBus = eventBus;
        _configService = configService;
        _animaContext = animaContext;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sub = _eventBus.Subscribe<string>(
            $"{Metadata.Name}.port.input",
            async (evt, ct) =>
            {
                var input = evt.Payload;
                await ExecuteInternalAsync(input, ct);
            });
        _subscriptions.Add(sub);
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken ct = default) => Task.CompletedTask;

    private async Task ExecuteInternalAsync(string input, CancellationToken ct)
    {
        if (!_executionGuard.Wait(0)) return;

        try
        {
            if (input == null) return;

            _state = ModuleExecutionState.Running;
            _lastError = null;

            var animaId = _animaContext.ActiveAnimaId;
            var expression = string.Empty;

            if (animaId != null)
            {
                var config = _configService.GetConfig(animaId, Metadata.Name);
                expression = config.TryGetValue("expression", out var expr) ? expr : string.Empty;
            }

            bool result;
            if (string.IsNullOrWhiteSpace(expression))
            {
                // Safe default: empty expression routes to false
                result = false;
            }
            else
            {
                try
                {
                    result = EvaluateExpression(expression.Trim(), input);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogWarning(ex, "ConditionalBranchModule: failed to parse expression '{Expression}', routing to false", expression);
                    _state = ModuleExecutionState.Error;
                    _lastError = ex;
                    result = false;
                }
            }

            var outputPort = result ? "true" : "false";
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.{outputPort}",
                SourceModuleId = Metadata.Name,
                Payload = input
            }, ct);

            if (_state != ModuleExecutionState.Error)
                _state = ModuleExecutionState.Completed;

            _logger.LogDebug("ConditionalBranchModule executed, routed to '{Port}'", outputPort);
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "ConditionalBranchModule execution failed");
            throw;
        }
        finally
        {
            _executionGuard.Release();
        }
    }

    /// <summary>
    /// Evaluates a boolean expression string against the provided input value.
    /// Supports: ||, &amp;&amp;, !, parentheses, input.contains/startsWith/endsWith,
    /// input.length comparisons, input == / != string literals.
    /// </summary>
    private bool EvaluateExpression(string expression, string inputValue)
    {
        expression = expression.Trim();

        // Handle parenthesized group: (expr)
        if (expression.StartsWith("(") && FindMatchingParen(expression, 0) == expression.Length - 1)
            return EvaluateExpression(expression[1..^1], inputValue);

        // Handle || (lowest precedence)
        var orIndex = FindTopLevelOperator(expression, "||");
        if (orIndex >= 0)
        {
            var left = expression[..orIndex].Trim();
            var right = expression[(orIndex + 2)..].Trim();
            return EvaluateExpression(left, inputValue) || EvaluateExpression(right, inputValue);
        }

        // Handle && (higher than ||)
        var andIndex = FindTopLevelOperator(expression, "&&");
        if (andIndex >= 0)
        {
            var left = expression[..andIndex].Trim();
            var right = expression[(andIndex + 2)..].Trim();
            return EvaluateExpression(left, inputValue) && EvaluateExpression(right, inputValue);
        }

        // Handle ! prefix (negation)
        if (expression.StartsWith("!"))
            return !EvaluateExpression(expression[1..].Trim(), inputValue);

        // Handle input.length comparisons: input.length > N
        if (expression.StartsWith("input.length"))
        {
            var rest = expression["input.length".Length..].Trim();
            return EvaluateLengthComparison(rest, inputValue.Length);
        }

        // Handle input.contains("...")
        if (expression.StartsWith("input.contains("))
        {
            var arg = ExtractStringArgument(expression, "input.contains(");
            return inputValue.Contains(arg, StringComparison.Ordinal);
        }

        // Handle input.startsWith("...")
        if (expression.StartsWith("input.startsWith("))
        {
            var arg = ExtractStringArgument(expression, "input.startsWith(");
            return inputValue.StartsWith(arg, StringComparison.Ordinal);
        }

        // Handle input.endsWith("...")
        if (expression.StartsWith("input.endsWith("))
        {
            var arg = ExtractStringArgument(expression, "input.endsWith(");
            return inputValue.EndsWith(arg, StringComparison.Ordinal);
        }

        // Handle input == "..." or input != "..."
        if (expression.StartsWith("input =="))
        {
            var literal = ExtractStringLiteral(expression["input ==".Length..].Trim());
            return inputValue == literal;
        }

        if (expression.StartsWith("input !="))
        {
            var literal = ExtractStringLiteral(expression["input !=".Length..].Trim());
            return inputValue != literal;
        }

        throw new ArgumentException($"Unrecognized expression: '{expression}'");
    }

    /// <summary>Finds top-level (not nested in parentheses) occurrence of operator in expression.</summary>
    private static int FindTopLevelOperator(string expression, string op)
    {
        var depth = 0;
        var inString = false;
        for (var i = 0; i < expression.Length - op.Length + 1; i++)
        {
            var c = expression[i];
            if (c == '"' && (i == 0 || expression[i - 1] != '\\'))
                inString = !inString;
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0 && expression[i..].StartsWith(op))
                return i;
        }
        return -1;
    }

    /// <summary>Finds the index of the closing parenthesis matching the opening at startIndex.</summary>
    private static int FindMatchingParen(string expression, int startIndex)
    {
        var depth = 0;
        for (var i = startIndex; i < expression.Length; i++)
        {
            if (expression[i] == '(') depth++;
            else if (expression[i] == ')') depth--;
            if (depth == 0) return i;
        }
        return -1;
    }

    /// <summary>Evaluates a length comparison like "> 10", "&lt;= 100", "== 0".</summary>
    private static bool EvaluateLengthComparison(string operatorAndValue, int length)
    {
        var parts = operatorAndValue.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var n))
            throw new ArgumentException($"Invalid length comparison: '{operatorAndValue}'");

        return parts[0] switch
        {
            ">" => length > n,
            "<" => length < n,
            ">=" => length >= n,
            "<=" => length <= n,
            "==" => length == n,
            "!=" => length != n,
            _ => throw new ArgumentException($"Unknown length comparison operator: '{parts[0]}'")
        };
    }

    /// <summary>Extracts a string argument from a method call like input.contains("hello").</summary>
    private static string ExtractStringArgument(string expression, string prefix)
    {
        var afterPrefix = expression[prefix.Length..];
        // Find closing paren from end
        var closeParen = afterPrefix.LastIndexOf(')');
        var inner = closeParen >= 0 ? afterPrefix[..closeParen] : afterPrefix;
        return ExtractStringLiteral(inner.Trim());
    }

    /// <summary>Extracts the string value from a quoted literal like "hello" or 'hello'.</summary>
    private static string ExtractStringLiteral(string value)
    {
        value = value.Trim();
        if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
            (value.StartsWith("'") && value.EndsWith("'")))
        {
            return value[1..^1];
        }
        throw new ArgumentException($"Expected a quoted string literal, got: '{value}'");
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
