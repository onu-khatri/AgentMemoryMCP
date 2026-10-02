using AgentSession.MCP.Helpers;
using OnuObservability.Model;
using OnuObservability.Mcp;

namespace AgentSession.MCP.Observability;

internal sealed class AgentMemoryMcpTelemetryOutcomeClassifier : IMcpTelemetryOutcomeClassifier
{
    public McpTelemetryClassification Classify(
        Exception exception,
        CancellationToken callerCancellation = default) => exception switch
        {
            McpClassifiedException classified => classified.Classification,
            ValidationException validation => Classify(validation),
            OperationCanceledException when callerCancellation.IsCancellationRequested =>
                new(McpTelemetrySchema.ClientCancelledOutcome, new ErrorCode("client_cancelled")),
            OperationCanceledException =>
                new(CommonOutcomes.DeadlineExceeded, new ErrorCode("deadline_exceeded")),
            TimeoutException =>
                new(CommonOutcomes.DeadlineExceeded, new ErrorCode("lock_timeout")),
            HttpRequestException =>
                new(CommonOutcomes.DependencyError, new ErrorCode("dependency_http_error")),
            InvalidDataException =>
                new(McpTelemetrySchema.ToolErrorOutcome, new ErrorCode("storage_invalid")),
            ModelContextProtocol.McpProtocolException =>
                new(McpTelemetrySchema.ProtocolErrorOutcome, new ErrorCode("protocol_error")),
            ModelContextProtocol.McpException =>
                new(McpTelemetrySchema.ToolErrorOutcome, new ErrorCode("mcp_error")),
            IOException =>
                new(McpTelemetrySchema.ToolErrorOutcome, new ErrorCode("storage_error")),
            _ when exception.GetType().Namespace?.StartsWith("Grpc", StringComparison.Ordinal) == true =>
                new(CommonOutcomes.DependencyError, new ErrorCode("dependency_grpc_error")),
            _ => new(CommonOutcomes.InternalError, new ErrorCode("internal_error")),
        };

    public static McpTelemetryClassification Classify(ValidationException exception)
    {
        var code = TelemetrySchema.NormalizeErrorCode(exception.Code);
        return code switch
        {
            "approval_required" => new(
                CommonOutcomes.AuthorizationDenied,
                new ErrorCode(code),
                DenialReason: McpDenialReason.ApprovalRequired),
            "policy_denied" => new(
                CommonOutcomes.AuthorizationDenied,
                new ErrorCode(code),
                DenialReason: McpDenialReason.PolicyDenied),
            "capacity_exceeded" => new(
                CommonOutcomes.ValidationError,
                new ErrorCode(code),
                McpValidationReason.OutOfRange),
            "storage_invalid" => new(
                McpTelemetrySchema.ToolErrorOutcome,
                new ErrorCode(code)),
            TelemetrySchema.Other => new(
                CommonOutcomes.ValidationError,
                new ErrorCode(TelemetrySchema.Other),
                McpValidationReason.Other),
            _ => new(
                CommonOutcomes.ValidationError,
                new ErrorCode(code),
                McpValidationReason.DomainRule),
        };
    }
}
