# Session implementation evidence

Windows, .NET 10, 2026-09-26. This file records the earlier session-foundation milestone. Final integrated session, learning, packaging and acceptance results are in [verification.md](verification.md).

`dotnet restore --disable-parallel` succeeded using the approved user NuGet configuration/cache. A restore file-creation collision was resolved by disabling parallel restore. The MCP SDK is pinned to 2.2.0; YamlDotNet and its unused persistence route were removed.

`dotnet test --no-restore` rebuilt and passed 96 tests, 0 failures, 0 skipped at the session milestone. The original 32 legacy tests were removed along with their obsolete implementations; the total therefore is not comparable to the earlier 114-test mixed legacy/foundation suite. Subsequent checks are recorded in tasks.md.

| Outcome | Executed evidence |
| --- | --- |
| SC1 restart-safe resume | `StructuredContextSurvivesRestartAndLegacyPayloadDoesNotWrite`: saved objective, current plan, detailed plan data and next actions survive a real server process restart. |
| SC2 shared contributions | `TwoAgentsShareCompletedOutputsAndRepositoriesStayIsolated`, artifact batch conflict/retry tests, interrupted batch replay: contributors see committed outputs/evidence; stale writes fail. |
| SC3 claims and takeover | Two-process competing claim and duplicate-delegation tests; `ExpiredClaimCanBeTakenOverAfterServerRestart`; stale task and linked-artifact owner rejection. |
| SC4 explicit checkpoint/catch-up | `CursorAndCheckpointCatchUpSurviveRestartWithConcurrentContribution`, controlled-time expiry/invalid-cursor tests and >1 MiB snapshot pagination test. Reads do not acknowledge; later contributions remain visible. |
| SC5 semantic-service independence | The above tests launch the production Program entry point with no embedding/vector clients or jobs registered. They require no Ollama/Qdrant connection. This does not verify degradation of the future integrated semantic service. |
| SC6 contract transition | Protocol discovery rejects generic legacy writers; obsolete freeform payload rejection; central repository isolation. No historical reader/importer exists in the active implementation. |

Protocol tests execute newline-delimited stdio JSON-RPC against the production server assembly with a framework-dependent test launch. They validate unique discovery, typed input/output schemas, stdout framing, actual structured calls and cancellation with protocol versions 2025-06-18, 2025-11-25 and 2026-07-28. The new protocol test supplies per-request protocol/client metadata; the old versions use initialize. This is not a published-package smoke test or Unix-runner result.

SDK migration references reviewed: [official releases](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.2.0), [protocol migration and stdio discovery/fallback](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/stateless/stateless.html), and [tool parameter/response/cancellation contract](https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/v2.2.0/src/ModelContextProtocol.Core/Server/McpServerToolAttribute.cs).

The final integrated rerun supersedes this milestone's open-gate statement. See `verification.md` for AC1-AC20, real Ollama/Qdrant, archive, packaging and final SC1-SC6 evidence, including the remaining hosted-CI boundary.
