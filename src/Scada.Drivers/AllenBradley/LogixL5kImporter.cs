using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.AllenBradley;

/// <summary>
/// Conservative parser for full-project Logix L5K ASCII exports. It extracts
/// controller/program TAG declarations into transient Engineering candidates.
/// Runtime values, force data and logic are intentionally ignored.
/// </summary>
public static class LogixL5kImporter
{
    public const int DefaultMaximumSourceChars = 32 * 1024 * 1024;
    public const int DefaultMaximumTagCount = 250_000;
    public const int DefaultMaximumStatementChars = 1024 * 1024;

    public static async IAsyncEnumerable<DriverImportCandidate> ImportAsync(
        DriverImportRequest request,
        Stream content,
        int maximumSourceChars = DefaultMaximumSourceChars,
        int maximumTagCount = DefaultMaximumTagCount,
        int maximumStatementChars = DefaultMaximumStatementChars,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);
        if (maximumSourceChars <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSourceChars));
        if (maximumTagCount <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTagCount));
        if (maximumStatementChars <= 0) throw new ArgumentOutOfRangeException(nameof(maximumStatementChars));

        string source;
        using (var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true))
        {
            source = await reader.ReadToEndAsync(cancellationToken);
        }

        if (source.Length > maximumSourceChars)
        {
            yield return ErrorCandidate(
                request.SourceName,
                "LOGIX_L5K_SOURCE_TOO_LARGE",
                $"L5K source contains {source.Length.ToString(CultureInfo.InvariantCulture)} characters; the configured limit is {maximumSourceChars.ToString(CultureInfo.InvariantCulture)}.");
            yield break;
        }

        IReadOnlyList<RawTag> tags;
        try
        {
            tags = ParseRawTags(source, maximumTagCount, maximumStatementChars, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            yield return ErrorCandidate(
                request.SourceName,
                "LOGIX_L5K_PARSE_FAILED",
                Sanitize(ex.Message));
            yield break;
        }

        var lookup = tags
            .Where(static tag => tag.AliasFor is null)
            .GroupBy(static tag => ScopeKey(tag.Scope, tag.ProgramName, tag.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = CreateCandidate(request.SourceName, tag, lookup);
            if (!seen.Add(candidate.StableIdentity)) continue;
            yield return candidate;
        }
    }

    internal static IReadOnlyList<RawTag> ParseRawTags(
        string source,
        int maximumTagCount = DefaultMaximumTagCount,
        int maximumStatementChars = DefaultMaximumStatementChars,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tags = new List<RawTag>();
        string? programName = null;
        var inTagBlock = false;
        var statement = new StringBuilder();
        var lineNumber = 0;

        using var reader = new StringReader(source);
        string? rawLine;
        while ((rawLine = reader.ReadLine()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            var line = StripLineComment(rawLine).Trim();
            if (line.Length == 0) continue;

            if (!inTagBlock)
            {
                if (TryParseProgramStart(line, out var parsedProgram))
                {
                    programName = parsedProgram;
                    continue;
                }
                if (StartsWithKeyword(line, "END_PROGRAM"))
                {
                    programName = null;
                    continue;
                }
                if (StartsWithKeyword(line, "TAG"))
                {
                    inTagBlock = true;
                    var remainder = line.Length == 3 ? string.Empty : line[3..].TrimStart();
                    if (remainder.Length > 0)
                        AppendAndExtract(remainder, lineNumber, programName, tags, statement, maximumTagCount, maximumStatementChars);
                }
                continue;
            }

            if (statement.Length == 0 && StartsWithKeyword(line, "END_TAG"))
            {
                inTagBlock = false;
                continue;
            }

            AppendAndExtract(line, lineNumber, programName, tags, statement, maximumTagCount, maximumStatementChars);
        }

        if (statement.Length > 0)
            throw new InvalidDataException("L5K TAG block ended with an unterminated declaration.");
        if (inTagBlock)
            throw new InvalidDataException("L5K source ended before END_TAG.");
        return tags;
    }

    private static void AppendAndExtract(
        string line,
        int lineNumber,
        string? programName,
        ICollection<RawTag> tags,
        StringBuilder statement,
        int maximumTagCount,
        int maximumStatementChars)
    {
        if (statement.Length > 0) statement.Append(' ');
        statement.Append(line);
        if (statement.Length > maximumStatementChars)
            throw new InvalidDataException($"L5K TAG declaration near line {lineNumber} exceeds the configured statement length limit.");

        while (TryTakeStatement(statement, out var declaration))
        {
            if (string.IsNullOrWhiteSpace(declaration)) continue;
            if (tags.Count >= maximumTagCount)
                throw new InvalidDataException($"L5K TAG count exceeds the configured limit of {maximumTagCount.ToString(CultureInfo.InvariantCulture)}.");
            tags.Add(ParseTagDeclaration(declaration, programName, lineNumber));
        }
    }

    private static RawTag ParseTagDeclaration(string declaration, string? programName, int lineNumber)
    {
        var text = declaration.Trim();
        var scope = string.IsNullOrWhiteSpace(programName) ? LogixTagScope.Controller : LogixTagScope.Program;

        var aliasMarker = FindKeywordOutsideQuotes(text, " OF ");
        var colon = FindDelimiterOutsideQuotes(text, " : ");
        if (aliasMarker > 0 && (colon < 0 || aliasMarker < colon))
        {
            var name = text[..aliasMarker].Trim();
            var remainder = text[(aliasMarker + 4)..].Trim();
            var attributesStart = FindTopLevelChar(remainder, '(');
            var aliasFor = attributesStart >= 0 ? remainder[..attributesStart].Trim() : remainder;
            var attributes = attributesStart >= 0 ? ParseAttributes(ExtractParenthesized(remainder, attributesStart)) : EmptyAttributes();
            ValidateTagToken(name, lineNumber);
            if (string.IsNullOrWhiteSpace(aliasFor))
                throw new InvalidDataException($"L5K alias TAG '{name}' near line {lineNumber} has no alias target.");

            return new RawTag(
                scope,
                programName,
                name,
                null,
                null,
                ParseExternalAccess(GetAttribute(attributes, "ExternalAccess")),
                ParseYesNo(GetAttribute(attributes, "Constant")),
                aliasFor,
                IsSafety(attributes),
                lineNumber);
        }

        if (colon <= 0)
            throw new InvalidDataException($"Unsupported or malformed L5K TAG declaration near line {lineNumber}: '{Abbreviate(text)}'.");

        var tagName = text[..colon].Trim();
        ValidateTagToken(tagName, lineNumber);
        var afterColon = text[(colon + 3)..].Trim();
        var assignment = FindTopLevelAssignment(afterColon);
        var header = assignment >= 0 ? afterColon[..assignment].Trim() : afterColon;

        var attributesStartIndex = FindTopLevelChar(header, '(');
        var typeAndDimensions = attributesStartIndex >= 0 ? header[..attributesStartIndex].Trim() : header;
        var attributesText = attributesStartIndex >= 0 ? ExtractParenthesized(header, attributesStartIndex) : null;
        var attrs = ParseAttributes(attributesText);

        var openBracket = typeAndDimensions.IndexOf('[', StringComparison.Ordinal);
        string typeName;
        string? dimensions = null;
        if (openBracket >= 0)
        {
            if (!typeAndDimensions.EndsWith(']'))
                throw new InvalidDataException($"L5K TAG '{tagName}' near line {lineNumber} has malformed array dimensions.");
            typeName = typeAndDimensions[..openBracket].Trim();
            dimensions = typeAndDimensions[(openBracket + 1)..^1].Trim();
            if (dimensions.Length == 0 || dimensions.Any(static ch => ch != ',' && !char.IsDigit(ch)))
                throw new InvalidDataException($"L5K TAG '{tagName}' near line {lineNumber} has invalid array dimensions '{dimensions}'.");
        }
        else
        {
            typeName = typeAndDimensions.Trim();
        }

        if (string.IsNullOrWhiteSpace(typeName) || typeName.Any(char.IsWhiteSpace))
            throw new InvalidDataException($"L5K TAG '{tagName}' near line {lineNumber} has an invalid datatype token '{typeName}'.");

        return new RawTag(
            scope,
            programName,
            tagName,
            typeName,
            dimensions,
            ParseExternalAccess(GetAttribute(attrs, "ExternalAccess")),
            ParseYesNo(GetAttribute(attrs, "Constant")),
            null,
            IsSafety(attrs),
            lineNumber);
    }

    private static DriverImportCandidate CreateCandidate(
        string sourceName,
        RawTag tag,
        IReadOnlyDictionary<string, RawTag> lookup)
    {
        var issues = new List<DriverEngineeringIssue>();
        var effective = tag;
        string? aliasFor = tag.AliasFor;
        if (aliasFor is not null)
        {
            if (TryResolveSimpleAlias(tag, aliasFor, lookup, out var resolved))
            {
                effective = tag with
                {
                    NativeTypeName = resolved.NativeTypeName,
                    Dimensions = resolved.Dimensions,
                    ExternalAccess = resolved.ExternalAccess,
                    Constant = resolved.Constant,
                    Safety = resolved.Safety
                };
                issues.Add(new DriverEngineeringIssue(
                    "LOGIX_L5K_ALIAS_RESOLVED",
                    DriverEngineeringIssueSeverity.Information,
                    $"L5K alias '{tag.Name}' resolves to '{aliasFor}' for type/access evidence; the persisted symbolic identity remains the alias name."));
            }
            else
            {
                issues.Add(new DriverEngineeringIssue(
                    "LOGIX_L5K_ALIAS_REQUIRES_RESOLUTION",
                    DriverEngineeringIssueSeverity.Warning,
                    $"L5K alias '{tag.Name}' targets '{aliasFor}'. The first-cut importer cannot prove the target's effective type/access semantics, so this candidate remains fail-closed."));
                return UnsupportedCandidate(sourceName, tag, tag.NativeTypeName ?? "alias", issues, aliasFor);
            }
        }

        if (!TryMapNativeType(effective.NativeTypeName, out var nativeType))
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_L5K_TYPE_UNSUPPORTED",
                DriverEngineeringIssueSeverity.Warning,
                $"L5K TAG '{tag.Name}' uses unsupported/structured data type '{effective.NativeTypeName ?? "<missing>"}'."));
            return UnsupportedCandidate(sourceName, tag, effective.NativeTypeName, issues, aliasFor);
        }

        LogixSymbolReference reference;
        try
        {
            reference = new LogixSymbolReference(tag.Scope, tag.Name, nativeType, tag.ProgramName);
            reference.Validate();
        }
        catch (ArgumentException ex)
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_L5K_SYMBOL_INVALID",
                DriverEngineeringIssueSeverity.Warning,
                Sanitize(ex.Message)));
            return UnsupportedCandidate(sourceName, tag, effective.NativeTypeName, issues, aliasFor);
        }

        var isArray = !string.IsNullOrWhiteSpace(effective.Dimensions);
        if (isArray)
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_ARRAY_BINDING_REQUIRES_ELEMENT",
                DriverEngineeringIssueSeverity.Warning,
                $"L5K TAG '{tag.Name}' is an array ({effective.Dimensions}). It is preserved as a candidate but requires explicit element/member selection before Runtime binding."));
        }
        if (effective.Safety)
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_SAFETY_TAG_UNSUPPORTED",
                DriverEngineeringIssueSeverity.Warning,
                $"L5K TAG '{tag.Name}' is safety-class data. The first-cut SCADA driver does not claim safety TAG access."));
        }
        if (!LogixValueCodec.IsFirstCutRuntimeReadable(nativeType))
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_TYPE_RUNTIME_UNSUPPORTED",
                DriverEngineeringIssueSeverity.Warning,
                $"L5K TAG '{tag.Name}' uses native type '{nativeType}', which is preserved by Engineering but not enabled in the first-cut Runtime."));
        }
        if (nativeType == LogixNativeType.Bool && effective.ExternalAccess == LogixExternalAccess.ReadWrite)
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_BOOL_DIRECT_WRITE_DEFERRED",
                DriverEngineeringIssueSeverity.Information,
                "Direct BOOL writes remain disabled until packed BOOL/type-position metadata is proven. Integer physical-bit bindings use coordinated read-modify-write instead."));
        }

        var readable = !isArray &&
                       !effective.Safety &&
                       effective.ExternalAccess != LogixExternalAccess.None &&
                       LogixValueCodec.IsFirstCutRuntimeReadable(nativeType);
        var writable = readable &&
                       effective.ExternalAccess == LogixExternalAccess.ReadWrite &&
                       !effective.Constant &&
                       LogixValueCodec.IsFirstCutRuntimeWritable(nativeType);

        return new DriverImportCandidate(
            $"l5k:{reference.StableIdentity}",
            reference.StableIdentity,
            tag.Name,
            LogixPortableAddress.Format(reference, effective.ExternalAccess, effective.Constant),
            readable,
            writable,
            TryCanonicalType(nativeType),
            Metadata: CreateMetadata(sourceName, tag, effective, aliasFor),
            Issues: issues.Count == 0 ? null : issues);
    }

    private static DriverImportCandidate UnsupportedCandidate(
        string sourceName,
        RawTag tag,
        string? nativeType,
        IReadOnlyCollection<DriverEngineeringIssue> issues,
        string? aliasFor = null)
    {
        var stableIdentity = tag.Scope == LogixTagScope.Controller
            ? $"controller:{tag.Name}"
            : $"program:{tag.ProgramName}:{tag.Name}";
        return new DriverImportCandidate(
            $"l5k:{stableIdentity}",
            stableIdentity,
            tag.Name,
            BuildUnsupportedPortableAddress(tag, nativeType),
            false,
            false,
            Metadata: CreateMetadata(sourceName, tag, tag, aliasFor),
            Issues: issues);
    }

    private static IReadOnlyDictionary<string, string> CreateMetadata(
        string sourceName,
        RawTag original,
        RawTag effective,
        string? aliasFor)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["source"] = sourceName,
            ["sourceFormat"] = "L5K",
            ["scope"] = original.Scope.ToString(),
            ["nativeType"] = effective.NativeTypeName ?? string.Empty,
            ["externalAccess"] = effective.ExternalAccess.ToString(),
            ["constant"] = effective.Constant.ToString(CultureInfo.InvariantCulture),
            ["safety"] = effective.Safety.ToString(CultureInfo.InvariantCulture),
            ["sourceLine"] = original.LineNumber.ToString(CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(original.ProgramName)) metadata["programName"] = original.ProgramName;
        if (!string.IsNullOrWhiteSpace(effective.Dimensions)) metadata["dimensions"] = effective.Dimensions;
        if (!string.IsNullOrWhiteSpace(aliasFor)) metadata["aliasFor"] = aliasFor;
        return metadata;
    }

    private static bool TryResolveSimpleAlias(
        RawTag alias,
        string aliasFor,
        IReadOnlyDictionary<string, RawTag> lookup,
        out RawTag resolved)
    {
        resolved = null!;
        var target = aliasFor.Trim();
        if (target.Length == 0 || target.IndexOfAny(['.', '[', ']']) >= 0)
            return false;

        if (lookup.TryGetValue(ScopeKey(alias.Scope, alias.ProgramName, target), out resolved))
            return true;
        if (alias.Scope == LogixTagScope.Program && lookup.TryGetValue(ScopeKey(LogixTagScope.Controller, null, target), out resolved))
            return true;
        return false;
    }

    private static Dictionary<string, string> ParseAttributes(string? text)
    {
        var result = EmptyAttributes();
        if (string.IsNullOrWhiteSpace(text)) return result;
        foreach (var piece in SplitTopLevel(text, ','))
        {
            var assignment = piece.IndexOf(":=", StringComparison.Ordinal);
            if (assignment <= 0) continue;
            var key = piece[..assignment].Trim();
            var value = piece[(assignment + 2)..].Trim().Trim('"');
            if (key.Length > 0) result[key] = value;
        }
        return result;
    }

    private static Dictionary<string, string> EmptyAttributes() => new(StringComparer.OrdinalIgnoreCase);

    private static string? GetAttribute(IReadOnlyDictionary<string, string> attributes, string key) =>
        attributes.TryGetValue(key, out var value) ? value : null;

    private static bool IsSafety(IReadOnlyDictionary<string, string> attributes) =>
        string.Equals(GetAttribute(attributes, "Class"), "Safety", StringComparison.OrdinalIgnoreCase);

    private static LogixExternalAccess ParseExternalAccess(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "read/write" or "readwrite" => LogixExternalAccess.ReadWrite,
        "read only" or "read-only" or "readonly" => LogixExternalAccess.ReadOnly,
        "none" => LogixExternalAccess.None,
        _ => LogixExternalAccess.Unknown
    };

    private static bool ParseYesNo(string? value) => value?.Trim().ToLowerInvariant() is "yes" or "true" or "1";

    private static bool TryMapNativeType(string? value, out LogixNativeType nativeType)
    {
        nativeType = value?.Trim().ToUpperInvariant() switch
        {
            "BOOL" => LogixNativeType.Bool,
            "SINT" => LogixNativeType.Sint,
            "INT" => LogixNativeType.Int,
            "DINT" => LogixNativeType.Dint,
            "LINT" => LogixNativeType.Lint,
            "REAL" => LogixNativeType.Real,
            "LREAL" => LogixNativeType.Lreal,
            "STRING" => LogixNativeType.String,
            _ => (LogixNativeType)(-1)
        };
        return Enum.IsDefined(nativeType);
    }

    private static TagDataType? TryCanonicalType(LogixNativeType nativeType) =>
        LogixValueCodec.TryGetCanonicalDataType(nativeType, out var dataType) ? dataType : null;

    private static string BuildUnsupportedPortableAddress(RawTag tag, string? nativeType)
    {
        var parts = new List<string>
        {
            LogixPortableAddress.Prefix,
            $"scope={(tag.Scope == LogixTagScope.Controller ? "controller" : "program")}",
            $"symbol={Uri.EscapeDataString(tag.Name)}",
            $"native={Uri.EscapeDataString(nativeType ?? "unsupported")}",
            "supported=false"
        };
        if (tag.Scope == LogixTagScope.Program && !string.IsNullOrWhiteSpace(tag.ProgramName))
            parts.Insert(2, $"program={Uri.EscapeDataString(tag.ProgramName)}");
        return string.Join(';', parts);
    }

    private static DriverImportCandidate ErrorCandidate(string sourceName, string code, string message) =>
        new(
            "l5k:document-error",
            "l5k:document-error",
            sourceName,
            "invalid:l5k",
            false,
            false,
            Issues:
            [
                new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, message)
            ]);

    private static bool TryParseProgramStart(string line, out string? programName)
    {
        programName = null;
        if (!StartsWithKeyword(line, "PROGRAM")) return false;
        var remainder = line[7..].TrimStart();
        if (remainder.Length == 0) throw new InvalidDataException("L5K PROGRAM declaration is missing a program name.");
        var end = 0;
        while (end < remainder.Length && !char.IsWhiteSpace(remainder[end]) && remainder[end] != '(') end++;
        programName = remainder[..end].Trim();
        if (programName.Length == 0) throw new InvalidDataException("L5K PROGRAM declaration is missing a program name.");
        return true;
    }

    private static bool StartsWithKeyword(string value, string keyword) =>
        value.Equals(keyword, StringComparison.OrdinalIgnoreCase) ||
        (value.Length > keyword.Length && value.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) && char.IsWhiteSpace(value[keyword.Length]));

    private static bool TryTakeStatement(StringBuilder buffer, out string statement)
    {
        var inQuote = false;
        for (var index = 0; index < buffer.Length; index++)
        {
            var ch = buffer[index];
            if (ch == '"' && !IsDollarEscaped(buffer, index)) inQuote = !inQuote;
            if (ch == ';' && !inQuote)
            {
                statement = buffer.ToString(0, index);
                buffer.Remove(0, index + 1);
                while (buffer.Length > 0 && char.IsWhiteSpace(buffer[0])) buffer.Remove(0, 1);
                return true;
            }
        }
        statement = string.Empty;
        return false;
    }

    private static int FindTopLevelAssignment(string text)
    {
        var inQuote = false;
        var depth = 0;
        for (var index = 0; index < text.Length - 1; index++)
        {
            var ch = text[index];
            if (ch == '"' && !IsDollarEscaped(text, index)) inQuote = !inQuote;
            if (inQuote) continue;
            if (ch == '(') depth++;
            else if (ch == ')' && depth > 0) depth--;
            else if (depth == 0 && ch == ':' && text[index + 1] == '=') return index;
        }
        return -1;
    }

    private static int FindTopLevelChar(string text, char sought)
    {
        var inQuote = false;
        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (ch == '"' && !IsDollarEscaped(text, index)) inQuote = !inQuote;
            if (!inQuote && ch == sought) return index;
        }
        return -1;
    }

    private static int FindDelimiterOutsideQuotes(string text, string delimiter)
    {
        var inQuote = false;
        for (var index = 0; index <= text.Length - delimiter.Length; index++)
        {
            if (text[index] == '"' && !IsDollarEscaped(text, index)) inQuote = !inQuote;
            if (!inQuote && text.AsSpan(index).StartsWith(delimiter, StringComparison.Ordinal)) return index;
        }
        return -1;
    }

    private static int FindKeywordOutsideQuotes(string text, string keyword) => FindDelimiterOutsideQuotes(text, keyword);

    private static string ExtractParenthesized(string text, int start)
    {
        var inQuote = false;
        var depth = 0;
        for (var index = start; index < text.Length; index++)
        {
            var ch = text[index];
            if (ch == '"' && !IsDollarEscaped(text, index)) inQuote = !inQuote;
            if (inQuote) continue;
            if (ch == '(') depth++;
            else if (ch == ')')
            {
                depth--;
                if (depth == 0) return text[(start + 1)..index];
            }
        }
        throw new InvalidDataException("L5K TAG attribute list is missing a closing parenthesis.");
    }

    private static IEnumerable<string> SplitTopLevel(string text, char separator)
    {
        var inQuote = false;
        var depth = 0;
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (ch == '"' && !IsDollarEscaped(text, index)) inQuote = !inQuote;
            if (inQuote) continue;
            if (ch == '(' || ch == '[') depth++;
            else if ((ch == ')' || ch == ']') && depth > 0) depth--;
            else if (ch == separator && depth == 0)
            {
                yield return text[start..index].Trim();
                start = index + 1;
            }
        }
        yield return text[start..].Trim();
    }

    private static string StripLineComment(string line)
    {
        var inQuote = false;
        for (var index = 0; index < line.Length - 1; index++)
        {
            if (line[index] == '"' && !IsDollarEscaped(line, index)) inQuote = !inQuote;
            if (!inQuote && line[index] == '/' && line[index + 1] == '/') return line[..index];
        }
        return line;
    }

    private static bool IsDollarEscaped(StringBuilder text, int quoteIndex) => quoteIndex > 0 && text[quoteIndex - 1] == '$';
    private static bool IsDollarEscaped(string text, int quoteIndex) => quoteIndex > 0 && text[quoteIndex - 1] == '$';

    private static void ValidateTagToken(string name, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException($"L5K TAG near line {lineNumber} has an empty name.");
        if (name.Any(char.IsWhiteSpace))
            throw new InvalidDataException($"L5K TAG name '{name}' near line {lineNumber} contains whitespace.");
    }

    private static string ScopeKey(LogixTagScope scope, string? programName, string name) =>
        scope == LogixTagScope.Controller ? $"C|{name}" : $"P|{programName}|{name}";

    private static string Abbreviate(string text) => text.Length <= 160 ? text : text[..160] + "...";

    private static string Sanitize(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }

    internal sealed record RawTag(
        LogixTagScope Scope,
        string? ProgramName,
        string Name,
        string? NativeTypeName,
        string? Dimensions,
        LogixExternalAccess ExternalAccess,
        bool Constant,
        string? AliasFor,
        bool Safety,
        int LineNumber);
}
