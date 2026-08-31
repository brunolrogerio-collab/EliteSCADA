namespace Scada.Drivers.AllenBradley;

public static class LogixPortableAddress
{
    public const string Prefix = "logix:v1";

    public static string Format(
        LogixSymbolReference reference,
        LogixExternalAccess access = LogixExternalAccess.Unknown,
        bool constant = false)
    {
        ArgumentNullException.ThrowIfNull(reference);
        reference.Validate();
        var parts = new List<string>
        {
            Prefix,
            $"scope={Escape(reference.Scope == LogixTagScope.Controller ? "controller" : "program")}",
            $"symbol={Escape(reference.SymbolPath)}",
            $"native={Escape(reference.NativeType.ToString().ToLowerInvariant())}",
            $"access={Escape(FormatAccess(access))}",
            $"constant={(constant ? "true" : "false")}" 
        };
        if (reference.Scope == LogixTagScope.Program)
            parts.Insert(2, $"program={Escape(reference.ProgramName!)}");
        return string.Join(';', parts);
    }

    public static bool TryParse(
        string? portableAddress,
        out LogixSymbolReference? reference,
        out LogixExternalAccess access,
        out bool constant,
        out string? error)
    {
        reference = null;
        access = LogixExternalAccess.Unknown;
        constant = false;
        error = null;

        if (string.IsNullOrWhiteSpace(portableAddress))
        {
            error = "Logix portable address is required.";
            return false;
        }

        var parts = portableAddress.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Logix portable address must start with '{Prefix}'.";
            return false;
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts.Skip(1))
        {
            var equals = part.IndexOf('=');
            if (equals <= 0)
            {
                error = $"Invalid Logix portable-address field '{part}'.";
                return false;
            }
            var key = part[..equals].Trim();
            if (!TryUnescape(part[(equals + 1)..].Trim(), out var value))
            {
                error = $"Invalid escaped value for Logix field '{key}'.";
                return false;
            }
            if (!fields.TryAdd(key, value!))
            {
                error = $"Duplicate Logix portable-address field '{key}'.";
                return false;
            }
        }

        if (!fields.TryGetValue("scope", out var scopeText) ||
            !fields.TryGetValue("symbol", out var symbol) ||
            !fields.TryGetValue("native", out var nativeText))
        {
            error = "Logix portable address requires scope, symbol and native fields.";
            return false;
        }

        LogixTagScope scope;
        if (scopeText.Equals("controller", StringComparison.OrdinalIgnoreCase)) scope = LogixTagScope.Controller;
        else if (scopeText.Equals("program", StringComparison.OrdinalIgnoreCase)) scope = LogixTagScope.Program;
        else
        {
            error = $"Unsupported Logix scope '{scopeText}'.";
            return false;
        }

        if (!Enum.TryParse<LogixNativeType>(nativeText, true, out var nativeType))
        {
            error = $"Unsupported Logix native type '{nativeText}'.";
            return false;
        }

        string? programName = null;
        if (scope == LogixTagScope.Program)
        {
            if (!fields.TryGetValue("program", out programName) || string.IsNullOrWhiteSpace(programName))
            {
                error = "Program-scoped Logix address requires a program field.";
                return false;
            }
        }
        else if (fields.ContainsKey("program"))
        {
            error = "Controller-scoped Logix address cannot contain a program field.";
            return false;
        }

        if (fields.TryGetValue("access", out var accessText) && !TryParseAccess(accessText, out access))
        {
            error = $"Unsupported Logix External Access value '{accessText}'.";
            return false;
        }

        if (fields.TryGetValue("constant", out var constantText) &&
            !bool.TryParse(constantText, out constant))
        {
            error = $"Invalid Logix constant value '{constantText}'.";
            return false;
        }

        try
        {
            reference = new LogixSymbolReference(scope, symbol, nativeType, programName);
            reference.Validate();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            error = ex.Message;
            reference = null;
            return false;
        }
    }

    private static string FormatAccess(LogixExternalAccess access) => access switch
    {
        LogixExternalAccess.Unknown => "unknown",
        LogixExternalAccess.ReadWrite => "readwrite",
        LogixExternalAccess.ReadOnly => "readonly",
        LogixExternalAccess.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(access))
    };

    private static bool TryParseAccess(string value, out LogixExternalAccess access)
    {
        access = value.Trim().ToLowerInvariant() switch
        {
            "unknown" => LogixExternalAccess.Unknown,
            "readwrite" or "read/write" => LogixExternalAccess.ReadWrite,
            "readonly" or "read-only" => LogixExternalAccess.ReadOnly,
            "none" => LogixExternalAccess.None,
            _ => (LogixExternalAccess)(-1)
        };
        return Enum.IsDefined(access);
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static bool TryUnescape(string value, out string? result)
    {
        try
        {
            result = Uri.UnescapeDataString(value);
            return true;
        }
        catch (UriFormatException)
        {
            result = null;
            return false;
        }
    }
}
