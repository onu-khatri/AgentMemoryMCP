namespace AgentSession.MCP.Core.Policies;

public readonly record struct BoundedInt
{
    public BoundedInt(int minimum, int maximum)
    {
        if (minimum < 0)
            throw new ArgumentOutOfRangeException(nameof(minimum));
        if (maximum <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        if (minimum > maximum)
            throw new ArgumentException("Minimum cannot exceed maximum.", nameof(minimum));
        Minimum = minimum;
        Maximum = maximum;
    }

    public int Minimum { get; }
    public int Maximum { get; }
    public bool Contains(int value) => value >= Minimum && value <= Maximum;
}

public sealed record ExecutionBounds
{
    public ExecutionBounds(
        BoundedInt inputCharacters,
        BoundedInt collectionItems,
        BoundedInt pageSize,
        BoundedInt responseItems,
        TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        InputCharacters = inputCharacters;
        CollectionItems = collectionItems;
        PageSize = pageSize;
        ResponseItems = responseItems;
        Timeout = timeout;
    }

    public BoundedInt InputCharacters { get; }
    public BoundedInt CollectionItems { get; }
    public BoundedInt PageSize { get; }
    public BoundedInt ResponseItems { get; }
    public TimeSpan Timeout { get; }
}
