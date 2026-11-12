namespace PRN232.Application.Policies;

// These database fields already represent UTC. Preserve that contract even when
// a relational provider materializes DateTime with Kind=Unspecified (e.g. SQLite).
public static class UtcTimestamp
{
    public static DateTime ForResponse(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    public static DateTime? ForResponse(DateTime? value) => value.HasValue ? ForResponse(value.Value) : null;
}
