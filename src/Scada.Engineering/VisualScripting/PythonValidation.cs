namespace Scada.Engineering.VisualScripting;

public enum PythonDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public readonly record struct PythonSourcePosition
{
    public PythonSourcePosition(int line, int column)
    {
        if (line <= 0)
            throw new ArgumentOutOfRangeException(nameof(line), "Line is 1-based and must be positive.");
        if (column <= 0)
            throw new ArgumentOutOfRangeException(nameof(column), "Column is 1-based and must be positive.");

        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }
}

public readonly record struct PythonSourceSpan
{
    public PythonSourceSpan(PythonSourcePosition start, PythonSourcePosition end)
    {
        if (end.Line < start.Line ||
            (end.Line == start.Line && end.Column < start.Column))
        {
            throw new ArgumentException("Diagnostic end position cannot precede its start position.");
        }

        Start = start;
        End = end;
    }

    public PythonSourcePosition Start { get; }

    public PythonSourcePosition End { get; }

    public static PythonSourceSpan Point(int line, int column) =>
        new(new PythonSourcePosition(line, column), new PythonSourcePosition(line, column));
}

public sealed record PythonValidationDiagnostic(
    string Code,
    PythonDiagnosticSeverity Severity,
    string Message,
    PythonSourceSpan Span);

public sealed class PythonValidationResult
{
    public PythonValidationResult(IReadOnlyCollection<PythonValidationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public IReadOnlyCollection<PythonValidationDiagnostic> Diagnostics { get; }

    public bool IsValid => Diagnostics.All(diagnostic => diagnostic.Severity != PythonDiagnosticSeverity.Error);
}

/// <summary>
/// Engine-level syntax validation contract. The selected Python engine is expected to provide real
/// compile/parse diagnostics through this interface when the scripting runtime is integrated.
/// </summary>
public interface IPythonEngineValidator
{
    ValueTask<PythonValidationResult> ValidateAsync(
        PythonScriptDefinition script,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Deterministic editor preflight for obviously prohibited imports/calls and a few structural mistakes.
/// This is developer feedback only; runtime sandbox enforcement must not depend on source scanning.
/// </summary>
public sealed class PythonPreflightValidator
{
    private static readonly HashSet<string> DeniedImportRoots =
        new(StringComparer.Ordinal)
        {
            "os",
            "sys",
            "subprocess",
            "socket",
            "pathlib",
            "requests",
            "urllib",
            "http",
            "ftplib",
            "sqlite3",
            "ctypes",
            "multiprocessing",
            "importlib"
        };

    private static readonly string[] DeniedCallNames =
    {
        "open",
        "exec",
        "eval",
        "compile",
        "__import__"
    };

    public PythonValidationResult Validate(PythonScriptDefinition script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var diagnostics = new List<PythonValidationDiagnostic>();
        if (string.IsNullOrWhiteSpace(script.Source))
        {
            diagnostics.Add(new(
                "PY_SOURCE_REQUIRED",
                PythonDiagnosticSeverity.Error,
                "Python source cannot be empty.",
                PythonSourceSpan.Point(1, 1)));
            return new(diagnostics);
        }

        var lines = NormalizeLines(script.Source);
        ValidateImports(lines, diagnostics);
        ValidateDeniedCalls(lines, diagnostics);
        ValidateDelimiters(lines, diagnostics);
        ValidateEntryPoints(script, diagnostics);

        return new(diagnostics);
    }

    private static string[] NormalizeLines(string source) =>
        source.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static void ValidateImports(
        IReadOnlyList<string> lines,
        ICollection<PythonValidationDiagnostic> diagnostics)
    {
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var masked = MaskStringsAndComments(lines[lineIndex]);
            var trimmed = masked.TrimStart();
            var indent = masked.Length - trimmed.Length;

            if (trimmed.StartsWith("import ", StringComparison.Ordinal))
            {
                var importText = trimmed["import ".Length..];
                foreach (var part in importText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var token = part.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                    AddDeniedImportIfNeeded(lines[lineIndex], lineIndex, indent, token, diagnostics);
                }
            }
            else if (trimmed.StartsWith("from ", StringComparison.Ordinal))
            {
                var remainder = trimmed["from ".Length..];
                var separator = remainder.IndexOf(' ');
                var token = separator >= 0 ? remainder[..separator] : remainder;
                AddDeniedImportIfNeeded(lines[lineIndex], lineIndex, indent, token, diagnostics);
            }
        }
    }

    private static void AddDeniedImportIfNeeded(
        string originalLine,
        int zeroBasedLineIndex,
        int indent,
        string moduleToken,
        ICollection<PythonValidationDiagnostic> diagnostics)
    {
        var root = moduleToken.Trim().Split('.', 2)[0];
        if (!DeniedImportRoots.Contains(root))
            return;

        var columnIndex = originalLine.IndexOf(moduleToken, indent, StringComparison.Ordinal);
        var column = columnIndex >= 0 ? columnIndex + 1 : indent + 1;

        diagnostics.Add(new(
            "PY_SANDBOX_IMPORT_DENIED",
            PythonDiagnosticSeverity.Error,
            $"Import '{root}' is outside the EliteSCADA scripting sandbox.",
            new(
                new PythonSourcePosition(zeroBasedLineIndex + 1, column),
                new PythonSourcePosition(zeroBasedLineIndex + 1, column + Math.Max(moduleToken.Length - 1, 0)))));
    }

    private static void ValidateDeniedCalls(
        IReadOnlyList<string> lines,
        ICollection<PythonValidationDiagnostic> diagnostics)
    {
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var masked = MaskStringsAndComments(lines[lineIndex]);

            foreach (var callName in DeniedCallNames)
            {
                var searchIndex = 0;
                while (searchIndex < masked.Length)
                {
                    var found = masked.IndexOf(callName, searchIndex, StringComparison.Ordinal);
                    if (found < 0)
                        break;

                    var beforeValid = found == 0 || !IsIdentifierCharacter(masked[found - 1]);
                    var afterName = found + callName.Length;
                    var afterValid = afterName >= masked.Length || !IsIdentifierCharacter(masked[afterName]);

                    var cursor = afterName;
                    while (cursor < masked.Length && char.IsWhiteSpace(masked[cursor]))
                        cursor++;

                    if (beforeValid && afterValid && cursor < masked.Length && masked[cursor] == '(')
                    {
                        diagnostics.Add(new(
                            "PY_SANDBOX_CALL_DENIED",
                            PythonDiagnosticSeverity.Error,
                            $"Call '{callName}' is outside the EliteSCADA scripting sandbox.",
                            new(
                                new PythonSourcePosition(lineIndex + 1, found + 1),
                                new PythonSourcePosition(lineIndex + 1, found + callName.Length))));
                    }

                    searchIndex = found + callName.Length;
                }
            }
        }
    }

    private static void ValidateDelimiters(
        IReadOnlyList<string> lines,
        ICollection<PythonValidationDiagnostic> diagnostics)
    {
        var stack = new Stack<(char Delimiter, int Line, int Column)>();

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var masked = MaskStringsAndComments(lines[lineIndex]);
            for (var charIndex = 0; charIndex < masked.Length; charIndex++)
            {
                var current = masked[charIndex];
                if (current is '(' or '[' or '{')
                {
                    stack.Push((current, lineIndex + 1, charIndex + 1));
                    continue;
                }

                if (current is not (')' or ']' or '}'))
                    continue;

                if (stack.Count == 0)
                {
                    diagnostics.Add(new(
                        "PY_DELIMITER_UNEXPECTED",
                        PythonDiagnosticSeverity.Error,
                        $"Unexpected closing delimiter '{current}'.",
                        PythonSourceSpan.Point(lineIndex + 1, charIndex + 1)));
                    continue;
                }

                var opening = stack.Pop();
                if (!Matches(opening.Delimiter, current))
                {
                    diagnostics.Add(new(
                        "PY_DELIMITER_MISMATCH",
                        PythonDiagnosticSeverity.Error,
                        $"Delimiter '{opening.Delimiter}' does not match '{current}'.",
                        PythonSourceSpan.Point(lineIndex + 1, charIndex + 1)));
                }
            }
        }

        while (stack.Count > 0)
        {
            var opening = stack.Pop();
            diagnostics.Add(new(
                "PY_DELIMITER_UNCLOSED",
                PythonDiagnosticSeverity.Error,
                $"Delimiter '{opening.Delimiter}' is not closed.",
                PythonSourceSpan.Point(opening.Line, opening.Column)));
        }
    }

    private static void ValidateEntryPoints(
        PythonScriptDefinition script,
        ICollection<PythonValidationDiagnostic> diagnostics)
    {
        foreach (var entryPoint in script.EntryPoints)
        {
            if (IsPythonIdentifier(entryPoint.HandlerName))
                continue;

            diagnostics.Add(new(
                "PY_ENTRYPOINT_IDENTIFIER_INVALID",
                PythonDiagnosticSeverity.Error,
                $"Handler '{entryPoint.HandlerName}' is not a valid Python identifier.",
                PythonSourceSpan.Point(1, 1)));
        }

        if (script.Scope == PythonScriptScope.Server &&
            script.EntryPoints.Any(entry => entry.EventKind is
                PythonScriptEventKind.ObjectInteraction or
                PythonScriptEventKind.ClientMemoryChanged or
                PythonScriptEventKind.FrameTick))
        {
            diagnostics.Add(new(
                "PY_SCOPE_EVENT_INVALID",
                PythonDiagnosticSeverity.Error,
                "Server Scripts cannot subscribe to client visual or Client Memory events.",
                PythonSourceSpan.Point(1, 1)));
        }

        if (script.Scope == PythonScriptScope.ClientVisual &&
            script.EntryPoints.Any(entry => entry.EventKind == PythonScriptEventKind.ServerRuntimeEvent))
        {
            diagnostics.Add(new(
                "PY_SCOPE_EVENT_INVALID",
                PythonDiagnosticSeverity.Error,
                "Client Visual Scripts cannot subscribe to server runtime events.",
                PythonSourceSpan.Point(1, 1)));
        }
    }

    private static string MaskStringsAndComments(string line)
    {
        var chars = line.ToCharArray();
        char quote = '\0';
        var escaped = false;

        for (var index = 0; index < chars.Length; index++)
        {
            var current = chars[index];

            if (quote != '\0')
            {
                if (escaped)
                {
                    chars[index] = ' ';
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    chars[index] = ' ';
                    escaped = true;
                    continue;
                }

                if (current == quote)
                {
                    chars[index] = ' ';
                    quote = '\0';
                    continue;
                }

                chars[index] = ' ';
                continue;
            }

            if (current == '#')
            {
                for (var rest = index; rest < chars.Length; rest++)
                    chars[rest] = ' ';
                break;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                chars[index] = ' ';
            }
        }

        return new string(chars);
    }

    private static bool IsIdentifierCharacter(char value) =>
        char.IsLetterOrDigit(value) || value == '_';

    private static bool IsPythonIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!(char.IsLetter(value[0]) || value[0] == '_'))
            return false;

        return value.Skip(1).All(IsIdentifierCharacter);
    }

    private static bool Matches(char opening, char closing) =>
        (opening == '(' && closing == ')') ||
        (opening == '[' && closing == ']') ||
        (opening == '{' && closing == '}');
}
