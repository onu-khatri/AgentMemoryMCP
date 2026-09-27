using System.Text.Json;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Services;

namespace AgentSession.MCP.Tests;

public sealed class MemoryContentPolicyTests
{
    [Theory]
    [InlineData("Authorization: Bearer synthetic-token-value")]
    [InlineData("Server=localhost;User Id=test;Password=synthetic-value;")]
    [InlineData("-----BEGIN PRIVATE KEY-----synthetic")]
    [InlineData("api_key=synthetic-value")]
    public void RejectsSecretTextWithoutEchoingIt(string text)
    {
        var exception = Assert.Throws<ValidationException>(() => new MemoryContentPolicy().Validate(new { evidence = new[] { new { notes = text } } }));
        Assert.DoesNotContain(text, exception.Message);
    }

    [Fact]
    public void DecodedJsonSecretsAreRejectedAtAnyDepth()
    {
        using var doc = JsonDocument.Parse("{\"evidence\":[{\"apiKey\":\"synthetic-value\"}]}");
        Assert.Throws<ValidationException>(() => new MemoryContentPolicy().Validate(doc.RootElement));
        Assert.Throws<ValidationException>(() => new MemoryContentPolicy().Validate(new { content = doc.RootElement.GetRawText() }));
    }

    [Fact]
    public void OrdinarySecurityDiscussionAndClaimTokensAreAllowed()
        => new MemoryContentPolicy().Validate(new
        {
            content = "Use secure password storage. Test authentication before deployment.",
            claimToken = "lease-generation-3", operationId = "operation-one"
        });

    [Fact]
    public void RejectsUrlAndBase64EncodedSecretMaterial()
    {
        const string secret = "password=synthetic-secret-value";
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secret));
        var url = Uri.EscapeDataString(secret);
        var policy = new MemoryContentPolicy();
        var encoded = Assert.Throws<ValidationException>(() => policy.Validate(new { evidence = base64 }));
        Assert.Equal("content_policy_rejected", encoded.Code);
        Assert.DoesNotContain(secret, encoded.Message);
        Assert.Throws<ValidationException>(() => policy.Validate(new { evidence = url }));
    }
}
