using System.Security.Cryptography;
using System.Text;

namespace AgentSession.MCP.Core.Storage;

public static class ContentHash
{
    public static string Compute(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public static string Compute(byte[] content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));
}
