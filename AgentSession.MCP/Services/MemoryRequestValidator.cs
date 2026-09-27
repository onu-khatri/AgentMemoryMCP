using System.Text.Json;
using AgentSession.MCP.Helpers;
using AgentSession.MCP.Interfaces;
using AgentSession.MCP.Options;
using Microsoft.Extensions.Options;

namespace AgentSession.MCP.Services;

public sealed class MemoryRequestValidator(
    IMemoryContentPolicy contentPolicy,
    IOptions<MemoryPolicyOptions> options
)
{
    public void ValidateBatch(int count)
    {
        if (count < 1 || count > options.Value.Limits.MaxMutationBatch)
            throw new ValidationException(
                "Record batch exceeds the configured limit.",
                "capacity_exceeded"
            );
    }

    public void ValidateOptionalBatch(int count)
    {
        if (count < 0 || count > options.Value.Limits.MaxMutationBatch)
            throw new ValidationException(
                "Record batch exceeds the configured limit.",
                "capacity_exceeded"
            );
    }

    public void Validate<T>(T request, bool content = false)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, MemoryJson.Options);
        if (
            bytes.Length
            > (content ? options.Value.Limits.MaxContentBytes : options.Value.Limits.MaxRecordBytes)
        )
            throw new ValidationException(
                "Input exceeds the configured byte limit.",
                "capacity_exceeded"
            );
        try
        {
            using var document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions { MaxDepth = options.Value.Limits.MaxJsonDepth }
            );
        }
        catch (JsonException)
        {
            throw new ValidationException("Input exceeds the configured JSON nesting limit.");
        }
        contentPolicy.Validate(request);
    }
}
