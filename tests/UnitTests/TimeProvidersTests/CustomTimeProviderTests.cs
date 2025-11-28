namespace UnitTests.TimeProvidersTests;

public class CustomTimeProviderTests
{
    [Fact]
    public void GetUtcNow_ShouldReturnCurrentUtcTime_WhenNoFuncIsProvided()
    {
        // Arrange
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: null, localTimeZone: null);

        // Act
        var actualTime = timeProvider.GetUtcNow();
        var expectedTime = DateTimeOffset.UtcNow;

        // Assert
        (actualTime - expectedTime).ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetUtcNow_FromDefaultInstance_ShouldReturnCurrentUtcTime_WhenNoFuncIsProvided()
    {
        // Arrange
        var timeProvider = PragmaStack.Core.TimeProviders.CustomTimeProvider.DefaultInstance;

        // Act
        var actualTime = timeProvider.GetUtcNow();
        var expectedTime = DateTimeOffset.UtcNow;

        // Assert
        (actualTime - expectedTime).ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetUtcNow_ShouldReturnCustomTime_WhenFuncIsProvided()
    {
        // Arrange
        var expectedTime = new DateTimeOffset(
            year: 2023,
            month: 1,
            day: 1,
            hour: 12,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero
        );
        Func<TimeZoneInfo?, DateTimeOffset> customUtcNowFunc = (tz) => expectedTime;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: customUtcNowFunc, localTimeZone: null);

        // Act
        var actualTime = timeProvider.GetUtcNow();

        // Assert
        expectedTime.ShouldBe(actualTime);
    }

    [Fact]
    public void LocalTimeZone_ShouldReturnProvidedTimeZone_WhenCustomTimeZoneIsGiven()
    {
        // Arrange
        var customTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            utcNowFunc: null,
            localTimeZone: customTimeZone
        );

        // Act
        var actualTimeZone = timeProvider.LocalTimeZone;

        // Assert
        actualTimeZone.ShouldBe(customTimeZone);
    }

    [Fact]
    public void LocalTimeZone_ShouldReturnUtc_WhenNoCustomTimeZoneIsGiven()
    {
        // Arrange
        var utcNowFunc = (TimeZoneInfo? tz) => DateTimeOffset.UtcNow;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(utcNowFunc: utcNowFunc, localTimeZone: null);

        // Act
        var actualTimeZone = timeProvider.LocalTimeZone;

        // Assert
        actualTimeZone.ShouldBe(TimeZoneInfo.Utc);
    }

    [Fact]
    public void GetLocalNow_ShouldConvertUtcToLocalTimeZone()
    {
        // Arrange
        var customTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var expectedUtcTime = new DateTimeOffset(
            year: 2023,
            month: 1,
            day: 1,
            hour: 20,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero
        );
        Func<TimeZoneInfo?, DateTimeOffset> customUtcNowFunc = (tz) => expectedUtcTime;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            customUtcNowFunc,
            localTimeZone: customTimeZone
        );

        // Act
        var actualLocalTime = timeProvider.GetLocalNow();

        // Assert
        var expectedLocalTime = TimeZoneInfo.ConvertTime(expectedUtcTime, customTimeZone);
        actualLocalTime.ShouldBe(expectedLocalTime);
    }
}
