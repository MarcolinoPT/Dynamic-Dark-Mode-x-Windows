using System.Text;
using DynamicDarkMode.Core;
using Shouldly;
using Xunit;

namespace DynamicDarkMode.Core.Tests;

public sealed class ProtocolMessageParserTests
{
    private const string Header = "\"protocol\":\"dynamic-dark-mode-sync\",\"version\":1";
    private const string TestSecret = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Theory]
    [InlineData("light", ThemeAppearance.Light)]
    [InlineData("dark", ThemeAppearance.Dark)]
    public void ParsesValidAppearanceRequest(
        string appearance,
        ThemeAppearance expectedAppearance)
    {
        // Arrange
        string json = $"{{{Header},\"type\":\"appearance.set\",\"appearance\":\"{appearance}\"}}";
        byte[] payload = Encoding.UTF8.GetBytes(json);

        // Act
        ProtocolMessage message = ParseValidMessage(payload);

        // Assert
        message.ShouldSatisfyAllConditions(
            () => message.Type.ShouldBe(ProtocolMessageType.AppearanceSet),
            () => message.Appearance.ShouldBe(expectedAppearance),
            () => message.Status.ShouldBeNull(),
            () => message.Code.ShouldBeNull(),
            () => message.Secret.ShouldBeNull());
    }

    [Fact]
    public void ParsesValidPairingRequest()
    {
        // Arrange
        string json = $"{{{Header},\"type\":\"pair.request\",\"secret\":\"{TestSecret}\"}}";
        byte[] payload = Encoding.UTF8.GetBytes(json);

        // Act
        ProtocolMessage message = ParseValidMessage(payload);

        // Assert
        message.ShouldSatisfyAllConditions(
            () => message.Type.ShouldBe(ProtocolMessageType.PairRequest),
            () => message.Secret.ShouldBe(TestSecret),
            () => message.Appearance.ShouldBeNull(),
            () => message.Status.ShouldBeNull(),
            () => message.Code.ShouldBeNull());
    }

    [Fact]
    public void ParsesValidAuthenticationRequest()
    {
        // Arrange
        string json = $"{{{Header},\"type\":\"auth.request\",\"secret\":\"{TestSecret}\"}}";
        byte[] payload = Encoding.UTF8.GetBytes(json);

        // Act
        ProtocolMessage message = ParseValidMessage(payload);

        // Assert
        message.ShouldSatisfyAllConditions(
            () => message.Type.ShouldBe(ProtocolMessageType.AuthRequest),
            () => message.Secret.ShouldBe(TestSecret),
            () => message.Appearance.ShouldBeNull(),
            () => message.Status.ShouldBeNull(),
            () => message.Code.ShouldBeNull());
    }

    [Fact]
    public void ParsesValidAppliedAcknowledgement()
    {
        // Arrange
        string json = $"{{{Header},\"type\":\"ack\",\"status\":\"applied\",\"appearance\":\"dark\"}}";
        byte[] payload = Encoding.UTF8.GetBytes(json);

        // Act
        ProtocolMessage message = ParseValidMessage(payload);

        // Assert
        message.ShouldSatisfyAllConditions(
            () => message.Type.ShouldBe(ProtocolMessageType.Acknowledgement),
            () => message.Status.ShouldBe("applied"),
            () => message.Appearance.ShouldBe(ThemeAppearance.Dark),
            () => message.Code.ShouldBeNull(),
            () => message.Secret.ShouldBeNull());
    }

    [Theory]
    [InlineData("ack", "error", "temporarily_unavailable", ProtocolMessageType.Acknowledgement)]
    [InlineData("ack", "error", "apply_failed", ProtocolMessageType.Acknowledgement)]
    [InlineData("pair.result", "paired", null, ProtocolMessageType.PairResult)]
    [InlineData("pair.result", "error", "pairing_failed", ProtocolMessageType.PairResult)]
    [InlineData("auth.result", "authenticated", null, ProtocolMessageType.AuthResult)]
    [InlineData("auth.result", "error", "authentication_failed", ProtocolMessageType.AuthResult)]
    public void ParsesValidStatusResults(
        string type,
        string status,
        string? code,
        ProtocolMessageType expectedType)
    {
        // Arrange
        string optionalCode = code is null ? string.Empty : $",\"code\":\"{code}\"";
        string json = $"{{{Header},\"type\":\"{type}\",\"status\":\"{status}\"{optionalCode}}}";
        byte[] payload = Encoding.UTF8.GetBytes(json);

        // Act
        ProtocolMessage message = ParseValidMessage(payload);

        // Assert
        message.ShouldSatisfyAllConditions(
            () => message.Type.ShouldBe(expectedType),
            () => message.Status.ShouldBe(status),
            () => message.Code.ShouldBe(code),
            () => message.Appearance.ShouldBeNull(),
            () => message.Secret.ShouldBeNull());
    }

    [Theory]
    [InlineData("[]", ProtocolValidationError.NonObject)]
    [InlineData("null", ProtocolValidationError.NonObject)]
    [InlineData("{", ProtocolValidationError.InvalidJson)]
    [InlineData("{\"protocol\":\"dynamic-dark-mode-sync\",\"protocol\":\"dynamic-dark-mode-sync\",\"version\":1,\"type\":\"appearance.set\",\"appearance\":\"dark\"}", ProtocolValidationError.DuplicateProperty)]
    [InlineData("{\"protocol\":\"dynamic-dark-mode-sync\",\"version\":true,\"type\":\"appearance.set\",\"appearance\":\"dark\"}", ProtocolValidationError.InvalidSchema)]
    [InlineData("{\"protocol\":\"dynamic-dark-mode-sync\",\"version\":1,\"type\":\"appearance.set\",\"appearance\":\"dark\",\"extra\":true}", ProtocolValidationError.InvalidSchema)]
    [InlineData("{\"protocol\":\"dynamic-dark-mode-sync\",\"version\":1,\"type\":\"appearance.set\",\"appearance\":\"dark\",\"nested\":{\"x\":1,\"x\":2}}", ProtocolValidationError.DuplicateProperty)]
    public void RejectsInvalidMessages(
        string json,
        ProtocolValidationError expectedError)
    {
        // Arrange
        byte[] payload = Encoding.UTF8.GetBytes(json);

        // Act
        bool success = ProtocolMessageParser.TryParse(
            utf8Payload: payload,
            message: out ProtocolMessage? message,
            error: out ProtocolValidationError error);

        // Assert
        (success, message, error).ShouldSatisfyAllConditions(
            result => result.success.ShouldBeFalse(),
            result => result.message.ShouldBeNull(),
            result => result.error.ShouldBe(expectedError));
    }

    [Fact]
    public void RejectsUnsupportedProtocolVersion()
    {
        // Arrange
        byte[] payload = Encoding.UTF8.GetBytes(
            """{"protocol":"dynamic-dark-mode-sync","version":2,"type":"appearance.set","appearance":"dark"}""");

        // Act
        bool success = ProtocolMessageParser.TryParse(
            utf8Payload: payload,
            message: out ProtocolMessage? message,
            error: out ProtocolValidationError error);

        // Assert
        (success, message, error).ShouldSatisfyAllConditions(
            result => result.success.ShouldBeFalse(),
            result => result.message.ShouldBeNull(),
            result => result.error.ShouldBe(ProtocolValidationError.InvalidSchema));
    }

    [Fact]
    public void RejectsUnsupportedAppearance()
    {
        // Arrange
        string json = $"{{{Header},\"type\":\"appearance.set\",\"appearance\":\"auto\"}}";
        byte[] payload = Encoding.UTF8.GetBytes(json);

        // Act
        bool success = ProtocolMessageParser.TryParse(
            utf8Payload: payload,
            message: out ProtocolMessage? message,
            error: out ProtocolValidationError error);

        // Assert
        (success, message, error).ShouldSatisfyAllConditions(
            result => result.success.ShouldBeFalse(),
            result => result.message.ShouldBeNull(),
            result => result.error.ShouldBe(ProtocolValidationError.InvalidSchema));
    }

    [Theory]
    [InlineData("not-a-secret")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB")]
    public void RejectsMalformedOrNonCanonicalPairingSecrets(string secret)
    {
        // Arrange
        string json = $"{{{Header},\"type\":\"pair.request\",\"secret\":\"{secret}\"}}";
        byte[] payload = Encoding.UTF8.GetBytes(json);

        // Act
        bool success = ProtocolMessageParser.TryParse(
            utf8Payload: payload,
            message: out ProtocolMessage? message,
            error: out ProtocolValidationError error);

        // Assert
        (success, message, error).ShouldSatisfyAllConditions(
            result => result.success.ShouldBeFalse(),
            result => result.message.ShouldBeNull(),
            result => result.error.ShouldBe(ProtocolValidationError.InvalidSchema));
    }

    [Fact]
    public void RejectsPayloadOverLimit()
    {
        // Arrange
        byte[] payload = Encoding.UTF8.GetBytes(
            new string(' ', ProtocolMessageParser.MaximumPayloadBytes + 1));

        // Act
        bool success = ProtocolMessageParser.TryParse(
            utf8Payload: payload,
            message: out ProtocolMessage? message,
            error: out ProtocolValidationError error);

        // Assert
        (success, message, error).ShouldSatisfyAllConditions(
            result => result.success.ShouldBeFalse(),
            result => result.message.ShouldBeNull(),
            result => result.error.ShouldBe(ProtocolValidationError.PayloadTooLarge));
    }

    [Fact]
    public void RejectsInvalidUtf8()
    {
        // Arrange
        byte[] payload = [0x7B, 0x22, 0x78, 0x22, 0x3A, 0xFF, 0x7D];

        // Act
        bool success = ProtocolMessageParser.TryParse(
            utf8Payload: payload,
            message: out ProtocolMessage? message,
            error: out ProtocolValidationError error);

        // Assert
        (success, message, error).ShouldSatisfyAllConditions(
            result => result.success.ShouldBeFalse(),
            result => result.message.ShouldBeNull(),
            result => result.error.ShouldBe(ProtocolValidationError.InvalidJson));
    }

    [Fact]
    public void RejectsUtf8ByteOrderMark()
    {
        // Arrange
        string json = $"{{{Header},\"type\":\"appearance.set\",\"appearance\":\"dark\"}}";
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        byte[] payload = [0xEF, 0xBB, 0xBF, .. jsonBytes];

        // Act
        bool success = ProtocolMessageParser.TryParse(
            utf8Payload: payload,
            message: out ProtocolMessage? message,
            error: out ProtocolValidationError error);

        // Assert
        (success, message, error).ShouldSatisfyAllConditions(
            result => result.success.ShouldBeFalse(),
            result => result.message.ShouldBeNull(),
            result => result.error.ShouldBe(ProtocolValidationError.InvalidJson));
    }

    private static ProtocolMessage ParseValidMessage(byte[] payload)
    {
        bool success = ProtocolMessageParser.TryParse(
            utf8Payload: payload,
            message: out ProtocolMessage? message,
            error: out ProtocolValidationError error);

        success.ShouldBeTrue($"Expected a valid message, got {error}.");
        message.ShouldNotBeNull();
        return message;
    }
}
