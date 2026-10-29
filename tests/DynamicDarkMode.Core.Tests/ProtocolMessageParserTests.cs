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
    public void ParsesValidAppearanceRequest(string appearance, ThemeAppearance expected)
    {
        // Arrange
        var json = $"{{{Header},\"type\":\"appearance.set\",\"appearance\":\"{appearance}\"}}";
        var payload = Encoding.UTF8.GetBytes(json);

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeTrue($"Expected a valid message, got {error}.");
        message.ShouldNotBeNull();
        message.ShouldSatisfyAllConditions(
            () => message.Type.ShouldBe(ProtocolMessageType.AppearanceSet),
            () => message.Appearance.ShouldBe(expected),
            () => message.Status.ShouldBeNull(),
            () => message.Code.ShouldBeNull());
    }

    [Fact]
    public void ParsesValidPairingRequest()
    {
        // Arrange
        var json = $"{{{Header},\"type\":\"pair.request\",\"secret\":\"{TestSecret}\"}}";
        var payload = Encoding.UTF8.GetBytes(json);

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeTrue($"Expected a valid message, got {error}.");
        message.ShouldNotBeNull();
        message.ShouldSatisfyAllConditions(
            () => message.Type.ShouldBe(ProtocolMessageType.PairRequest),
            () => message.Secret.ShouldBe(TestSecret),
            () => message.Appearance.ShouldBeNull());
    }

    [Fact]
    public void ParsesValidAuthenticationRequest()
    {
        // Arrange
        var json = $"{{{Header},\"type\":\"auth.request\",\"secret\":\"{TestSecret}\"}}";
        var payload = Encoding.UTF8.GetBytes(json);

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeTrue($"Expected a valid message, got {error}.");
        message.ShouldNotBeNull();
        message.ShouldSatisfyAllConditions(
            () => message.Type.ShouldBe(ProtocolMessageType.AuthRequest),
            () => message.Secret.ShouldBe(TestSecret),
            () => message.Appearance.ShouldBeNull());
    }

    [Fact]
    public void ParsesValidAppliedAcknowledgement()
    {
        // Arrange
        var json = $"{{{Header},\"type\":\"ack\",\"status\":\"applied\",\"appearance\":\"dark\"}}";
        var payload = Encoding.UTF8.GetBytes(json);

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeTrue($"Expected a valid message, got {error}.");
        message.ShouldNotBeNull();
        message.ShouldSatisfyAllConditions(
            () => message.Type.ShouldBe(ProtocolMessageType.Acknowledgement),
            () => message.Status.ShouldBe("applied"),
            () => message.Appearance.ShouldBe(ThemeAppearance.Dark),
            () => message.Code.ShouldBeNull());
    }

    [Theory]
    [InlineData("ack", "error", "temporarily_unavailable", ProtocolMessageType.Acknowledgement)]
    [InlineData("pair.result", "paired", null, ProtocolMessageType.PairResult)]
    [InlineData("auth.result", "authenticated", null, ProtocolMessageType.AuthResult)]
    public void ParsesValidStatusResults(
        string type,
        string status,
        string? code,
        ProtocolMessageType expectedType)
    {
        // Arrange
        var optionalCode = code is null ? string.Empty : $",\"code\":\"{code}\"";
        var json = $"{{{Header},\"type\":\"{type}\",\"status\":\"{status}\"{optionalCode}}}";
        var payload = Encoding.UTF8.GetBytes(json);

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeTrue($"Expected a valid message, got {error}.");
        message.ShouldNotBeNull();
        message.ShouldSatisfyAllConditions(
            () => message.Type.ShouldBe(expectedType),
            () => message.Status.ShouldBe(status),
            () => message.Code.ShouldBe(code));
    }

    [Theory]
    [InlineData("[]", ProtocolValidationError.NonObject)]
    [InlineData("null", ProtocolValidationError.NonObject)]
    [InlineData("{", ProtocolValidationError.InvalidJson)]
    [InlineData("{\"protocol\":\"dynamic-dark-mode-sync\",\"protocol\":\"dynamic-dark-mode-sync\",\"version\":1,\"type\":\"appearance.set\",\"appearance\":\"dark\"}", ProtocolValidationError.DuplicateProperty)]
    [InlineData("{\"protocol\":\"dynamic-dark-mode-sync\",\"version\":true,\"type\":\"appearance.set\",\"appearance\":\"dark\"}", ProtocolValidationError.InvalidSchema)]
    [InlineData("{\"protocol\":\"dynamic-dark-mode-sync\",\"version\":1,\"type\":\"appearance.set\",\"appearance\":\"dark\",\"extra\":true}", ProtocolValidationError.InvalidSchema)]
    [InlineData("{\"protocol\":\"dynamic-dark-mode-sync\",\"version\":1,\"type\":\"appearance.set\",\"appearance\":\"dark\",\"nested\":{\"x\":1,\"x\":2}}", ProtocolValidationError.DuplicateProperty)]
    public void RejectsInvalidMessages(string json, ProtocolValidationError expectedError)
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes(json);

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeFalse();
        message.ShouldBeNull();
        error.ShouldBe(expectedError);
    }

    [Fact]
    public void RejectsUnsupportedProtocolVersion()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes(
            """{"protocol":"dynamic-dark-mode-sync","version":2,"type":"appearance.set","appearance":"dark"}""");

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeFalse();
        message.ShouldBeNull();
        error.ShouldBe(ProtocolValidationError.InvalidSchema);
    }

    [Fact]
    public void RejectsUnsupportedAppearance()
    {
        // Arrange
        var json = $"{{{Header},\"type\":\"appearance.set\",\"appearance\":\"auto\"}}";
        var payload = Encoding.UTF8.GetBytes(json);

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeFalse();
        message.ShouldBeNull();
        error.ShouldBe(ProtocolValidationError.InvalidSchema);
    }

    [Theory]
    [InlineData("not-a-secret")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB")]
    public void RejectsMalformedOrNonCanonicalPairingSecrets(string secret)
    {
        // Arrange
        var json = $"{{{Header},\"type\":\"pair.request\",\"secret\":\"{secret}\"}}";
        var payload = Encoding.UTF8.GetBytes(json);

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeFalse();
        message.ShouldBeNull();
        error.ShouldBe(ProtocolValidationError.InvalidSchema);
    }

    [Fact]
    public void RejectsPayloadOverLimit()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes(new string(' ', ProtocolMessageParser.MaximumPayloadBytes + 1));

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeFalse();
        message.ShouldBeNull();
        error.ShouldBe(ProtocolValidationError.PayloadTooLarge);
    }

    [Fact]
    public void RejectsInvalidUtf8()
    {
        // Arrange
        byte[] payload = [0x7B, 0x22, 0x78, 0x22, 0x3A, 0xFF, 0x7D];

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeFalse();
        message.ShouldBeNull();
        error.ShouldBe(ProtocolValidationError.InvalidJson);
    }

    [Fact]
    public void RejectsUtf8ByteOrderMark()
    {
        // Arrange
        var json = $"{{{Header},\"type\":\"appearance.set\",\"appearance\":\"dark\"}}";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        byte[] payload = [0xEF, 0xBB, 0xBF, .. jsonBytes];

        // Act
        var success = ProtocolMessageParser.TryParse(payload, out var message, out var error);

        // Assert
        success.ShouldBeFalse();
        message.ShouldBeNull();
        error.ShouldBe(ProtocolValidationError.InvalidJson);
    }
}
