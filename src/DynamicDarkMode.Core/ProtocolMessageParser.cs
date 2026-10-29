using System.Text.Json;

namespace DynamicDarkMode.Core;

/// <summary>Protocol v1 message kinds accepted by the strict JSON parser.</summary>
public enum ProtocolMessageType
{
    PairRequest,
    PairResult,
    AuthRequest,
    AuthResult,
    AppearanceSet,
    Acknowledgement
}

/// <summary>
/// A validated protocol v1 message. Only fields applicable to the message kind are populated.
/// Secret contains the Base64URL representation; callers must never log it.
/// </summary>
public sealed record ProtocolMessage(
    ProtocolMessageType Type,
    ThemeAppearance? Appearance = null,
    string? Status = null,
    string? Code = null,
    string? Secret = null);

public enum ProtocolValidationError
{
    None,
    PayloadTooLarge,
    InvalidJson,
    NonObject,
    DuplicateProperty,
    InvalidSchema
}

/// <summary>
/// Validates the complete UTF-8 JSON payload against the exact protocol v1 schemas.
/// This parser deliberately does not perform transport, authentication, or state changes.
/// </summary>
public static class ProtocolMessageParser
{
    public const int MaximumPayloadBytes = 4096;
    private const string ProtocolIdentifier = "dynamic-dark-mode-sync";
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static bool TryParse(
        ReadOnlyMemory<byte> utf8Payload,
        out ProtocolMessage? message,
        out ProtocolValidationError error)
    {
        message = null;
        error = ProtocolValidationError.None;

        if (utf8Payload.Length > MaximumPayloadBytes)
        {
            error = ProtocolValidationError.PayloadTooLarge;
            return false;
        }

        // JSON exchanged by this protocol is UTF-8 without a byte-order mark.
        if (utf8Payload.Span.StartsWith(Utf8Bom))
        {
            error = ProtocolValidationError.InvalidJson;
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Payload);
        }
        catch (JsonException)
        {
            error = ProtocolValidationError.InvalidJson;
            return false;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind is not JsonValueKind.Object)
            {
                error = ProtocolValidationError.NonObject;
                return false;
            }

            if (ContainsDuplicateProperty(root))
            {
                error = ProtocolValidationError.DuplicateProperty;
                return false;
            }

            if (HasCommonHeader(root) is false)
            {
                error = ProtocolValidationError.InvalidSchema;
                return false;
            }

            if (TryGetString(root, "type", out string type) is false)
            {
                error = ProtocolValidationError.InvalidSchema;
                return false;
            }

            bool valid = type switch
            {
                "pair.request" => TryParseSecretRequest(root, "pair.request", ProtocolMessageType.PairRequest, out message),
                "auth.request" => TryParseSecretRequest(root, "auth.request", ProtocolMessageType.AuthRequest, out message),
                "pair.result" => TryParsePairResult(root, out message),
                "auth.result" => TryParseAuthResult(root, out message),
                "appearance.set" => TryParseAppearanceSet(root, out message),
                "ack" => TryParseAcknowledgement(root, out message),
                _ => false
            };

            if (valid is false)
            {
                message = null;
                error = ProtocolValidationError.InvalidSchema;
                return false;
            }

            return true;
        }
    }

    private static bool HasCommonHeader(JsonElement root) =>
        TryGetString(root, "protocol", out string protocol)
            && protocol == ProtocolIdentifier &&
        root.TryGetProperty("version", out JsonElement version)
            && version.ValueKind == JsonValueKind.Number &&
        version.TryGetInt32(out int versionNumber)
            && versionNumber == 1;

    private static bool TryParseSecretRequest(
        JsonElement root,
        string type,
        ProtocolMessageType messageType,
        out ProtocolMessage? message)
    {
        message = null;
        if (HasExactFields(root, "protocol", "version", "type", "secret") is false
            || HasType(root, type) is false ||
            TryGetString(root, "secret", out string secret) is false
                || IsValidSecret(secret) is false)
        {
            return false;
        }

        message = new ProtocolMessage(Type: messageType, Secret: secret);
        return true;
    }

    private static bool TryParsePairResult(JsonElement root, out ProtocolMessage? message)
    {
        message = null;
        if (TryGetString(root, "status", out string status) is false)
        {
            return false;
        }

        if (status == "paired"

            && HasExactFields(root, "protocol", "version", "type", "status") &&
            HasType(root, "pair.result"))
        {
            message = new ProtocolMessage(Type: ProtocolMessageType.PairResult, Status: status);
            return true;
        }

        if (status == "error"

            && HasExactFields(root, "protocol", "version", "type", "status", "code") &&
            HasType(root, "pair.result")
                && TryGetString(root, "code", out string code) &&
            code == "pairing_failed")
        {
            message = new ProtocolMessage(Type: ProtocolMessageType.PairResult, Status: status, Code: code);
            return true;
        }

        return false;
    }

    private static bool TryParseAuthResult(JsonElement root, out ProtocolMessage? message)
    {
        message = null;
        if (TryGetString(root, "status", out string status) is false
            || HasType(root, "auth.result") is false)
        {
            return false;
        }

        if (status == "authenticated"

            && HasExactFields(root, "protocol", "version", "type", "status"))
        {
            message = new ProtocolMessage(Type: ProtocolMessageType.AuthResult, Status: status);
            return true;
        }

        if (status == "error"

            && HasExactFields(root, "protocol", "version", "type", "status", "code") &&
            TryGetString(root, "code", out var code)
                && code == "authentication_failed")
        {
            message = new ProtocolMessage(Type: ProtocolMessageType.AuthResult, Status: status, Code: code);
            return true;
        }

        return false;
    }

    private static bool TryParseAppearanceSet(JsonElement root, out ProtocolMessage? message)
    {
        message = null;
        if (HasExactFields(root, "protocol", "version", "type", "appearance") is false
            || HasType(root, "appearance.set") is false ||
            TryGetString(root, "appearance", out string appearance) is false)
        {
            return false;
        }

        ThemeAppearance parsedAppearance = appearance switch
        {
            "light" => ThemeAppearance.Light,
            "dark" => ThemeAppearance.Dark,
            _ => ThemeAppearance.Unknown
        };

        if (parsedAppearance == ThemeAppearance.Unknown)
        {
            return false;
        }

        message = new ProtocolMessage(Type: ProtocolMessageType.AppearanceSet, Appearance: parsedAppearance);
        return true;
    }

    private static bool TryParseAcknowledgement(JsonElement root, out ProtocolMessage? message)
    {
        message = null;
        if (TryGetString(root, "status", out string status) is false
            || HasType(root, "ack") is false)
        {
            return false;
        }

        if (status == "applied"

            && HasExactFields(root, "protocol", "version", "type", "status", "appearance") &&
            TryGetString(root, "appearance", out var appearance))
        {
            ThemeAppearance parsedAppearance = appearance switch
            {
                "light" => ThemeAppearance.Light,
                "dark" => ThemeAppearance.Dark,
                _ => ThemeAppearance.Unknown
            };

            if (parsedAppearance == ThemeAppearance.Unknown)
            {
                return false;
            }

            message = new ProtocolMessage(
                Type: ProtocolMessageType.Acknowledgement,
                Appearance: parsedAppearance,
                Status: status);
            return true;
        }

        if (status == "error"

            && HasExactFields(root, "protocol", "version", "type", "status", "code") &&
            TryGetString(root, "code", out var code)
                && code is "apply_failed" or "temporarily_unavailable")
        {
            message = new ProtocolMessage(Type: ProtocolMessageType.Acknowledgement, Status: status, Code: code);
            return true;
        }

        return false;
    }

    private static bool HasType(JsonElement root, string expected) =>
        TryGetString(root, "type", out string actual)
            && actual == expected;

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (root.TryGetProperty(propertyName, out JsonElement property) is false
            || property.ValueKind is not JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool HasExactFields(JsonElement root, params string[] expectedFields)
    {
        HashSet<string> expected = new HashSet<string>(expectedFields, StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (expected.Remove(property.Name) is false)
            {
                return false;
            }
        }

        return expected.Count == 0;
    }

    private static bool IsValidSecret(string secret)
    {
        // 32 bytes encode to exactly 43 unpadded Base64URL characters.
        if (secret.Length is not 43
            || secret.Any(character => IsBase64UrlCharacter(character) is false))
        {
            return false;
        }

        string base64 = secret.Replace('-', '+').Replace('_', '/') + "=";
        try
        {
            byte[] decoded = Convert.FromBase64String(base64);
            string canonical = Convert.ToBase64String(decoded)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return decoded.Length == 32
                && string.Equals(canonical, secret, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsBase64UrlCharacter(char character) =>
        character is >= 'A' and <= 'Z' or
            >= 'a' and <= 'z' or
            >= '0' and <= '9' or '-' or '_';

    private static bool ContainsDuplicateProperty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (names.Add(property.Name) is false
                    || ContainsDuplicateProperty(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsDuplicateProperty(item))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
