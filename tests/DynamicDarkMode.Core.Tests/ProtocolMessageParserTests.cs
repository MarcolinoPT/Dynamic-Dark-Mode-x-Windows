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
        var message = Parse($$"""{"{{Header}},"type":"appearance.set","appearance":"{{appearance}}"}""");

        message.Type.ShouldBe(ProtocolMessageType.AppearanceSet);
        message.Appearance.ShouldBe(expected);
    }

    [Fact]
    public void ParsesValidPairAndAuthenticationRequests()
    {
        Parse($$"""{"{{Header}},"type":"pair.request","secret":"{{TestSecret}}"}""")
            .Type.ShouldBe(ProtocolMessageType.PairRequest);

        Parse($$"""{"{{Header}},"type":"auth.request","secret":"{{TestSecret}}"}""")
            .Type.ShouldBe(ProtocolMessageType.AuthRequest);
    }

    [Fact]
    public void ParsesValidAcknowledgementAndErrorResults()
    {
        var applied = Parse($$"""{"{{Header}},"type":"ack","status":"applied","appearance":"dark"}""");
        applied.Type.ShouldBe(ProtocolMessageType.Acknowledgement);
        applied.Status.ShouldBe("applied");
        applied.Appearance.ShouldBe(ThemeAppearance.Dark);

        Parse($$"""{"{{Header}},"type":"ack","status":"error","code":"temporarily_unavailable"}""")
            .Code.ShouldBe("temporarily_unavailable");

        Parse($$"""{"{{Header}},"type":"pair.result","status":"paired"}""")
            .Status.ShouldBe("paired");

        Parse($$"""{"{{Header}},"type":"auth.result","status":"authenticated"}""")
            .Status.ShouldBe("authenticated");
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
        var success = ProtocolMessageParser.TryParse(Encoding.UTF8.GetBytes(json), out var message, out var error);

        success.ShouldBeFalse();
        message.ShouldBeNull();
        error.ShouldBe(expectedError);
    }

    [Fact]
    public void RejectsUnsupportedVersionAndAppearance()
    {
        Reject($$"""{"protocol":"dynamic-dark-mode-sync","version":2,"type":"appearance.set","appearance":"dark"}""");
        Reject($$"""{"{{Header}},"type":"appearance.set","appearance":"auto"}""");
    }

    [Fact]
    public void RejectsMalformedOrWrongLengthPairingSecrets()
    {
        Reject($$"""{"{{Header}},"type":"pair.request","secret":"not-a-secret"}""");
        Reject($$"""{"{{Header}},"type":"auth.request","secret":"{{TestSecret}}="}""");
    }

    [Fact]
    public void RejectsPayloadOverLimit()
    {
        var oversized = Encoding.UTF8.GetBytes(new string(' ', ProtocolMessageParser.MaximumPayloadBytes + 1));

        var success = ProtocolMessageParser.TryParse(oversized, out var message, out var error);

        success.ShouldBeFalse();
        message.ShouldBeNull();
        error.ShouldBe(ProtocolValidationError.PayloadTooLarge);
    }

    [Fact]
    public void RejectsInvalidUtf8()
    {
        byte[] invalidUtf8 = [0x7B, 0x22, 0x78, 0x22, 0x3A, 0xFF, 0x7D];

        var success = ProtocolMessageParser.TryParse(invalidUtf8, out var message, out var error);

        success.ShouldBeFalse();
        message.ShouldBeNull();
        error.ShouldBe(ProtocolValidationError.InvalidJson);
    }

    private static ProtocolMessage Parse(string json)
    {
        var success = ProtocolMessageParser.TryParse(Encoding.UTF8.GetBytes(json), out var message, out var error);
        success.ShouldBeTrue($"Expected valid protocol message, got {error}.");
        message.ShouldNotBeNull();
        return message!;
    }

    private static void Reject(string json)
    {
        var success = ProtocolMessageParser.TryParse(Encoding.UTF8.GetBytes(json), out var message, out _);
        success.ShouldBeFalse();
        message.ShouldBeNull();
    }
}
