namespace OrleansPlayground;

[GenerateSerializer]
public sealed record ReminderMetrics(
    [property: Id(0)] string GrainId,
    [property: Id(1)] long Count,
    [property: Id(2)] DateTime? LastUtc,
    [property: Id(3)] double? LastLatenessMs)
{
    public static ReminderMetrics Empty(string id) => new(id, 0, null, null);
}
