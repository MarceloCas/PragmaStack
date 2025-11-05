namespace PragmaStack.Core.TimeProviders;

public class CustomTimeProvider
    : TimeProvider
{
    // Fields
    private readonly Func<TimeZoneInfo?, DateTimeOffset>? _utcNowFunc;

    // Properties
    public static CustomTimeProvider DefaultInstance { get; } = new(utcNowFunc: null, localTimeZone: null);
    public override TimeZoneInfo LocalTimeZone { get; }

    // Constructors
    public CustomTimeProvider(
        Func<TimeZoneInfo?, DateTimeOffset>? utcNowFunc,
        TimeZoneInfo? localTimeZone
    )
    {
        _utcNowFunc = utcNowFunc;

        LocalTimeZone = localTimeZone ?? TimeZoneInfo.Utc;
    }

    // Public Methods
    public override DateTimeOffset GetUtcNow()
    {
        if (_utcNowFunc != null)
            return _utcNowFunc(LocalTimeZone);

        return DateTimeOffset.UtcNow;
    }
}
