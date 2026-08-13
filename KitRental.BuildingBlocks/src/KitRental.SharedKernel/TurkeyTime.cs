namespace KitRental.SharedKernel;

public static class TurkeyTime
{
    public const string TimeZoneId = "Turkey Standard Time";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static DateTimeOffset Now() => Convert(DateTimeOffset.UtcNow);

    public static DateTimeOffset Convert(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Zone);

    public static DateOnly Today() => DateOnly.FromDateTime(Now().DateTime);

    public static DateOnly DateOf(DateTimeOffset value) =>
        DateOnly.FromDateTime(Convert(value).DateTime);
}

public static class TimeProviderTurkeyExtensions
{
    public static DateTimeOffset GetTurkeyNow(this TimeProvider timeProvider) =>
        TurkeyTime.Convert(timeProvider.GetUtcNow());

    public static DateOnly GetTurkeyToday(this TimeProvider timeProvider) =>
        TurkeyTime.Today();
}
