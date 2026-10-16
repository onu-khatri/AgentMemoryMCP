namespace AgentSession.MCP.Core.Policies;

public enum RetryEligibility
{
    Never,
    SafeRead,
    IdempotentWrite,
}

public enum IdempotencyRequirement
{
    NotApplicable,
    Supported,
    Required,
}

public sealed record RetryAndIdempotencyPolicy(
    RetryEligibility Eligibility,
    IdempotencyRequirement Idempotency)
{
    public bool CanRetry(bool transientFailure, bool cancelled, string? idempotencyKey) =>
        transientFailure && !cancelled && Eligibility switch
        {
            RetryEligibility.SafeRead => true,
            RetryEligibility.IdempotentWrite =>
                Idempotency != IdempotencyRequirement.Required
                || !string.IsNullOrWhiteSpace(idempotencyKey),
            _ => false,
        };
}
