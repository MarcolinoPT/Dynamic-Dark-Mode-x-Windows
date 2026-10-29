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
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = ProtocolValidationError.NonObject;
                return false;
            }

            if (ContainsDuplicateProperty(root))
            {
                error = ProtocolValidationError.DuplicateProperty;
                return false;
            }

            if (!HasCommonHeader(root))
            {
                error = ProtocolValidationError.InvalidSchema;
                return false;
            }

            if (!TryGetString(root, "type", out var type))
            {
                error = ProtocolValidationError.InvalidSchema;
                return false;
            }

            var valid = type switch
            {
                "pair.request" => TryParseSecretRequest(root, "pair.request", ProtocolMessageType.PairRequest, out message),
                "auth.request" => TryParseSecretRequest(root, "auth.request", ProtocolMessageType.AuthRequest, out message),
                "pair.result" => TryParsePairResult(root, out message),
                "auth.result" => TryParseAuthResult(root, out message),
                "appearance.set" => TryParseAppearanceSet(root, out message),
                "ack" => TryParseAcknowledgement(root, out message),
                _ => false
            };

            if (!valid)
            {
                message = null;
                error = ProtocolValidationError.InvalidSchema;
                return false;
            }

            return true;
        }
    }

    private static bool HasCommonHeader(JsonElement root) =>
        TryGetString(root, "protocol", out var protocol) &&
        protocol == ProtocolIdentifier &&
        root.TryGetProperty("version", out var version) &&
        version.ValueKind == JsonValueKind.Number &&
        version.TryGetInt32(out var versionNumber) &&
        versionNumber == 1;

    private static bool TryParseSecretRequest(
        JsonElement root,
        string type,
        ProtocolMessageType messageType,
        out ProtocolMessage? message)
    {
        message = null;
        if (!HasExactFields(root, "protocol", "version", "type", "secret") ||
            !HasType(root, type) ||
            !TryGetString(root, "secret", out var secret) ||
            !IsValidSecret(secret))
        {
            return false;
        }

        message = new ProtocolMessage(messageType, Secret: secret);
        return true;
    }

    private static bool TryParsePairResult(JsonElement root, out ProtocolMessage? message)
    {
        message = null;
        if (!TryGetString(root, "status", out var status))
        {
            return false;
        }

        if (status == "paired" &&
            HasExactFields(root, "protocol", "version", "type", "status") &&
            HasType(root, "pair.result"))
        {
            message = new ProtocolMessage(ProtocolMessageType.PairResult, Status: status);
            return true;
        }

        if (status == "error" &&
            HasExactFields(root, "protocol", "version", "type", "status", "code") &&
            HasType(root, "pair.result") &&
            TryGetString(root, "code", out var code) &&
            code == "pairing_failed")
        {
            message = new ProtocolMessage(ProtocolMessageType.PairResult, Status: status, Code: code);
            return true;
        }

        return false;
    }

    private static bool TryParseAuthResult(JsonElement root, out ProtocolMessage? message)
    {
        message = null;
        if (!TryGetString(root, "status", out var status) || !HasType(root, "auth.result"))
        {
            return false;
        }

        if (status == "authenticated" &&
            HasExactFields(root, "protocol", "version", "type", "status"))
        {
            message = new ProtocolMessage(ProtocolMessageType.AuthResult, Status: status);
            return true;
        }

        if (status == "error" &&
            HasExactFields(root, "protocol", "version", "type", "status", "code") &&
            TryGetString(root, "code", out var code) &&
            code == "authentication_failed")
        {
            message = new ProtocolMessage(ProtocolMessageType.AuthResult, Status: status, Code: code);
            return true;
        }

        return false;
    }

    private static bool TryParseAppearanceSet(JsonElement root, out ProtocolMessage? message)
    {
        message = null;
        if (!HasExactFields(root, "protocol", "version", "type", "appearance") ||
            !HasType(root, "appearance.set") ||
            !TryGetString(root, "appearance", out var appearance))
        {
            return false;
        }

        var parsedAppearance = appearance switch
        {
            "light" => ThemeAppearance.Light,
            "dark" => ThemeAppearance.Dark,
            _ => ThemeAppearance.Unknown
        };

        if (parsedAppearance == ThemeAppearance.Unknown)
        {
            return false;
        }

        message = new ProtocolMessage(ProtocolMessageType.AppearanceSet, Appearance: parsedAppearance);
        return true;
    }

    private static bool TryParseAcknowledgement(JsonElement root, out ProtocolMessage? message)
    {
        message = null;
        if (!TryGetString(root, "status", out var status) || !HasType(root, "ack"))
        {
            return false;
        }

        if (status == "applied" &&
            HasExactFields(root, "protocol", "version", "type", "status", "appearance") &&
            TryGetString(root, "appearance", out var appearance))
        {
            var parsedAppearance = appearance switch
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
                ProtocolMessageType.Acknowledgement,
                Appearance: parsedAppearance,
                Status: status);
            return true;
        }

        if (status == "error" &&
            HasExactFields(root, "protocol", "version", "type", "status", "code") &&
            TryGetString(root, "code", out var code) &&
            code is "apply_failed" or "temporarily_unavailable")
        {
            message = new ProtocolMessage(ProtocolMessageType.Acknowledgement, Status: status, Code: code);
            return true;
        }

        return false;
    }

    private static bool HasType(JsonElement root, string expected) =>
        TryGetString(root, "type", out var actual) && actual == expected;

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool HasExactFields(JsonElement root, params string[] expectedFields)
    {
        var expected = new HashSet<string>(expectedFields, StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!expected.Remove(property.Name))
            {
                return false;
            }
        }

        return expected.Count == 0;
    }

    private static bool IsValidSecret(string secret)
    {
        // 32 bytes encode to exactly 43 unpadded Base64URL characters.
        if (secret.Length != 43 ||
            secret.Any(character =>
                !(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
        {
            return false;
        }

        var base64 = secret.Replace('-', '+').Replace('_', '/') + "=";
        try
        {
            return Convert.FromBase64String(base64).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool ContainsDuplicateProperty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || ContainsDuplicateProperty(property.Value))
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
