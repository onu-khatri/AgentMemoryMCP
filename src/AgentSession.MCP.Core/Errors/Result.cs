namespace AgentSession.MCP.Core.Errors;

public sealed class Result<T>
{
    private readonly T? value;
    private readonly PlatformError? error;

    private Result(T value)
    {
        this.value = value;
        IsSuccess = true;
    }

    private Result(PlatformError error) => this.error = error;

    public bool IsSuccess { get; }

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed result has no value.");

    public PlatformError Error => !IsSuccess
        ? error!
        : throw new InvalidOperationException("A successful result has no error.");

    public static Result<T> Success(T value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        return new(value);
    }

    public static Result<T> Failure(PlatformError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }
}
