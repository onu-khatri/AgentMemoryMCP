using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSession.MCP.Contracts;
using AgentSession.MCP.Services;

namespace AgentSession.MCP.Tests;

public sealed class MemoryContractTests
{
    [Fact]
    public void MemoryDtosAreStrictTypedAndExposeFiniteSchemaBounds()
    {
        var recall = MemoryJson.SchemaFor<RecallMemoryRequest>();
        var reindex = MemoryJson.SchemaFor<ReindexMemoryRequest>();
        var prepare = MemoryJson.SchemaFor<PrepareCompactionRequest>();
        Assert.Contains("\"maximum\": 100", recall.ToJsonString(MemoryJson.Options));
        Assert.Contains("\"maximum\": 100", reindex.ToJsonString(MemoryJson.Options));
        Assert.Contains("\"minItems\": 2", prepare.ToJsonString(MemoryJson.Options));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ReindexMemoryRequest>(
                """
                {"operationId":"reindex","scope":"file_system","maxItems":10,"unknown":true}
                """,
                MemoryJson.Options
            )
        );
    }

    [Fact]
    public void ExportedMemoryOperationSchemasCoverEveryToolContract()
    {
        var contracts = new JsonObject
        {
            ["rememberRequest"] = MemoryJson.SchemaFor<RememberMemoryRequest>(),
            ["getRequest"] = MemoryJson.SchemaFor<GetMemoryRequest>(),
            ["updateRequest"] = MemoryJson.SchemaFor<UpdateMemoryRequest>(),
            ["recallRequest"] = MemoryJson.SchemaFor<RecallMemoryRequest>(),
            ["reviewListRequest"] = MemoryJson.SchemaFor<ReviewListRequest>(),
            ["reviewRequest"] = MemoryJson.SchemaFor<ReviewMemoryRequest>(),
            ["prepareCompactionRequest"] = MemoryJson.SchemaFor<PrepareCompactionRequest>(),
            ["commitCompactionRequest"] = MemoryJson.SchemaFor<CommitCompactionRequest>(),
            ["compactRequest"] = MemoryJson.SchemaFor<CompactMemoryRequest>(),
            ["deleteRequest"] = MemoryJson.SchemaFor<DeleteMemoryRequest>(),
            ["archiveRequest"] = MemoryJson.SchemaFor<ArchiveMemoryRequest>(),
            ["recordEventRequest"] = MemoryJson.SchemaFor<RecordMemoryEventRequest>(),
            ["recordOutcomeRequest"] = MemoryJson.SchemaFor<RecordOutcomeRequest>(),
            ["promoteRequest"] = MemoryJson.SchemaFor<PromoteMemoryRequest>(),
            ["lifecycleRequest"] = MemoryJson.SchemaFor<LongTermLifecycleRequest>(),
            ["reindexRequest"] = MemoryJson.SchemaFor<ReindexMemoryRequest>(),
            ["statusResult"] = MemoryJson.SchemaFor<MemoryStatusResult>(),
            ["cleanupResult"] = MemoryJson.SchemaFor<CleanupMemoryResult>(),
            ["compactionResult"] = MemoryJson.SchemaFor<CompactionResult>(),
        };
        var root = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["schemaVersion"] = 1,
            ["contracts"] = contracts,
        };
        Assert.Equal(19, contracts.Count);
        var output = Path.Combine(AppContext.BaseDirectory, "schemas");
        Directory.CreateDirectory(output);
        File.WriteAllText(
            Path.Combine(output, "memory-operations-v1.schema.json"),
            root.ToJsonString(MemoryJson.Options)
        );
    }
}
