using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTests.RegistryVersionTests;

public class RegistryVersionTests
{
    #region [ Basic Generation Tests ]

    [Fact]
    public void GenerateNewVersion_ShouldReturnNonZeroValue()
    {
        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();

        // Assert
        version.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GenerateNewVersion_ShouldReturnSequentialAndUniqueVersions()
    {
        // Arrange
        var numberOfVersionsToGenerate = 1000;
        var versionList = new List<PragmaStack.Core.RegistryVersions.RegistryVersion>(capacity: numberOfVersionsToGenerate);

        // Act
        for (int i = 0; i < numberOfVersionsToGenerate; i++)
        {
            var newVersion = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
            versionList.Add(newVersion);
        }

        // Assert - All versions should be unique
        var versionSet = new HashSet<long>(versionList.Select(v => v.Value));
        versionSet.Count.ShouldBe(numberOfVersionsToGenerate);

        // Assert - Versions should be strictly increasing
        for (int i = 0; i < versionList.Count - 1; i++)
        {
            var currentVersion = versionList[i];
            var nextVersion = versionList[i + 1];

            var comparisonResult = currentVersion.CompareTo(nextVersion);
            comparisonResult.ShouldBeLessThan(0, $"Version {i} should be less than version {i + 1}");
        }
    }

    [Fact]
    public void GenerateNewVersion_StressTest_ShouldHandleHighVolume()
    {
        // Arrange
        var numberOfVersionsToGenerate = 10000;
        var versionSet = new HashSet<long>();

        // Act & Assert - Should not throw and all versions should be unique
        for (int i = 0; i < numberOfVersionsToGenerate; i++)
        {
            var newVersion = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
            versionSet.Add(newVersion.Value).ShouldBeTrue($"Version at position {i} should be unique");
        }

        versionSet.Count.ShouldBe(numberOfVersionsToGenerate);
    }

    #endregion [ Basic Generation Tests ]

    #region [ Multi-Threaded Tests ]

    [Fact]
    public void GenerateNewVersion_MultiThreaded_ShouldGenerateVersionsSuccessfully()
    {
        // Arrange
        var numberOfThreads = 10;
        var versionsPerThread = 1000;
        var allVersions = new ConcurrentBag<PragmaStack.Core.RegistryVersions.RegistryVersion>();

        // Act
        Parallel.For(fromInclusive: 0, toExclusive: numberOfThreads, body: _ =>
        {
            for (int i = 0; i < versionsPerThread; i++)
            {
                var newVersion = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
                allVersions.Add(newVersion);
            }
        });

        // Assert
        var totalVersions = numberOfThreads * versionsPerThread;
        allVersions.Count.ShouldBe(totalVersions, "All versions should be generated");

        // Note: RegistryVersion uses ThreadStatic for per-thread monotonicity. Different threads
        // may generate overlapping values since each thread maintains independent state.
        // This is by design - RegistryVersion is optimized for single-threaded sequential use
        // within an entity/aggregate, not for global uniqueness across threads.
        // The key guarantee is per-thread monotonicity, tested in GenerateNewVersion_MultiThreaded_ShouldBeSequentialPerThread.

        // All versions should be valid (non-zero positive timestamps)
        allVersions.All(v => v.Value > 0).ShouldBeTrue("All versions should have valid positive timestamps");
    }

    [Fact]
    public void GenerateNewVersion_MultiThreaded_ShouldBeSequentialPerThread()
    {
        // Arrange
        var numberOfThreads = 5;
        var versionsPerThread = 100;
        var threadResults = new ConcurrentBag<List<PragmaStack.Core.RegistryVersions.RegistryVersion>>();

        // Act
        Parallel.For(0, numberOfThreads, _ =>
        {
            var threadVersions = new List<PragmaStack.Core.RegistryVersions.RegistryVersion>(capacity: versionsPerThread);
            for (int i = 0; i < versionsPerThread; i++)
            {
                var newVersion = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
                threadVersions.Add(newVersion);
            }
            threadResults.Add(threadVersions);
        });

        // Assert - Each thread's versions should be sequential
        foreach (var threadVersions in threadResults)
        {
            for (int i = 0; i < threadVersions.Count - 1; i++)
            {
                var currentVersion = threadVersions[i];
                var nextVersion = threadVersions[i + 1];

                var comparisonResult = currentVersion.CompareTo(nextVersion);
                comparisonResult.ShouldBeLessThan(0, $"Versions within same thread should be sequential: {currentVersion} < {nextVersion}");
            }
        }
    }

    [Fact]
    public void GenerateNewVersion_MultiThreaded_HighThreadCount_ShouldMaintainPerThreadMonotonicity()
    {
        // Arrange
        var numberOfThreads = Environment.ProcessorCount * 2;
        var versionsPerThread = 100;
        var threadResults = new ConcurrentBag<List<PragmaStack.Core.RegistryVersions.RegistryVersion>>();

        // Act
        Parallel.For(0, numberOfThreads, _ =>
        {
            var threadVersions = new List<PragmaStack.Core.RegistryVersions.RegistryVersion>(capacity: versionsPerThread);
            for (int i = 0; i < versionsPerThread; i++)
            {
                var newVersion = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
                threadVersions.Add(newVersion);
            }
            threadResults.Add(threadVersions);
        });

        // Assert - Per-thread monotonicity (this is the key guarantee of RegistryVersion)
        // Each thread should maintain strictly increasing sequence regardless of other threads
        foreach (var threadVersions in threadResults)
        {
            for (int i = 0; i < threadVersions.Count - 1; i++)
            {
                (threadVersions[i] < threadVersions[i + 1]).ShouldBeTrue(
                    $"Versions within same thread should be strictly monotonic: {threadVersions[i]} < {threadVersions[i + 1]}"
                );
            }
        }

        // All threads should have completed successfully
        threadResults.Count.ShouldBe(numberOfThreads);
    }

    #endregion [ Multi-Threaded Tests ]

    #region [ TimeProvider Injection Tests ]

    [Fact]
    public void GenerateNewVersion_WithFixedTimeProvider_ShouldGenerateDeterministicVersions()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            utcNowFunc: _ => fixedTime,
            localTimeZone: null
        );

        // Act
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timeProvider);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timeProvider);
        var v3 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timeProvider);

        // Assert - All versions should be unique (different due to monotonicity protection)
        v1.ShouldNotBe(v2);
        v2.ShouldNotBe(v3);
        v1.ShouldNotBe(v3);

        // All versions should be sequential (monotonic)
        (v1 < v2).ShouldBeTrue("v1 should be less than v2");
        (v2 < v3).ShouldBeTrue("v2 should be less than v3");

        // First version should be >= fixed time ticks (may be higher due to ThreadStatic from previous tests)
        v1.Value.ShouldBeGreaterThanOrEqualTo(fixedTime.UtcTicks);

        // Subsequent versions should increment by 1 tick from previous
        v2.Value.ShouldBe(v1.Value + 1);
        v3.Value.ShouldBe(v2.Value + 1);
    }

    [Fact]
    public void GenerateNewVersion_WithAdvancingTimeProvider_ShouldResetToNewTimestamp()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var currentTime = baseTime;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            utcNowFunc: _ => currentTime,
            localTimeZone: null
        );

        // Act - Generate versions in same millisecond
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timeProvider);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timeProvider);

        // Advance time by 1 millisecond
        currentTime = baseTime.AddMilliseconds(1);

        // Generate more versions in new millisecond
        var v3 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timeProvider);
        var v4 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timeProvider);

        // Assert - All versions should be unique
        var allVersions = new[] { v1, v2, v3, v4 };
        var uniqueVersions = new HashSet<long>(allVersions.Select(v => v.Value));
        uniqueVersions.Count.ShouldBe(4);

        // All versions should be monotonically increasing
        (v1 < v2).ShouldBeTrue("v1 < v2");
        (v2 < v3).ShouldBeTrue("v2 < v3 (timestamp advanced)");
        (v3 < v4).ShouldBeTrue("v3 < v4");

        // v3 should use the new timestamp
        v3.Value.ShouldBeGreaterThanOrEqualTo(baseTime.AddMilliseconds(1).UtcTicks);
    }

    [Fact]
    public void GenerateNewVersion_WithTimeProviderClockDrift_ShouldMaintainMonotonicity()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var currentTime = baseTime;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            utcNowFunc: _ => currentTime,
            localTimeZone: null
        );

        // Act - Generate version at base time
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timeProvider);

        // Simulate clock drift backward (time goes backwards)
        currentTime = baseTime.AddMilliseconds(-5);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timeProvider);

        // Assert - Even with clock drift, versions should remain monotonic
        (v1 < v2).ShouldBeTrue("Versions should remain monotonic even when clock goes backward");
        v2.Value.ShouldBe(v1.Value + 1, "v2 should be exactly 1 tick more than v1");
    }

    [Fact]
    public void GenerateNewVersion_WithSystemTimeProvider_ShouldWork()
    {
        // Act
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(TimeProvider.System);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(TimeProvider.System);

        // Assert
        v1.ShouldNotBe(v2);
        v1.Value.ShouldBeGreaterThan(0);
        v2.Value.ShouldBeGreaterThan(0);
        (v1 < v2).ShouldBeTrue();
    }

    #endregion [ TimeProvider Injection Tests ]

    #region [ DateTimeOffset Overload Tests ]

    [Fact]
    public void GenerateNewVersion_WithSameDateTimeOffset_ShouldIncrementByOneTick()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timestamp);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timestamp);
        var v3 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timestamp);

        // Assert
        v1.ShouldNotBe(v2);
        v2.ShouldNotBe(v3);

        // Should be monotonically increasing by exactly 1 tick each time
        v1.Value.ShouldBeGreaterThanOrEqualTo(timestamp.UtcTicks);
        v2.Value.ShouldBe(v1.Value + 1);
        v3.Value.ShouldBe(v2.Value + 1);

        // Should be monotonically increasing
        (v1 < v2).ShouldBeTrue();
        (v2 < v3).ShouldBeTrue();
    }

    [Fact]
    public void GenerateNewVersion_WithAdvancingDateTimeOffset_ShouldGenerateSequentialVersions()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime.AddMilliseconds(1));
        var v3 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime.AddMilliseconds(2));

        // Assert
        (v1 < v2).ShouldBeTrue();
        (v2 < v3).ShouldBeTrue();

        // If clock advances, should use new timestamp
        v1.Value.ShouldBeGreaterThanOrEqualTo(baseTime.UtcTicks);
        v2.Value.ShouldBeGreaterThan(v1.Value);
        v3.Value.ShouldBeGreaterThan(v2.Value);
    }

    [Fact]
    public void GenerateNewVersion_WithBackwardDateTimeOffset_ShouldMaintainMonotonicity()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime.AddMilliseconds(-10)); // Clock goes backward

        // Assert - Should maintain monotonicity even when timestamp goes backward
        (v1 < v2).ShouldBeTrue("Versions should remain monotonic despite backward timestamp");
        v2.Value.ShouldBe(v1.Value + 1, "v2 should increment by exactly 1 tick from v1");
    }

    [Fact]
    public void GenerateNewVersion_WithDateTimeOffsetMinValue_ShouldNotThrow()
    {
        // Act & Assert
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(DateTimeOffset.MinValue);
        version.Value.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GenerateNewVersion_WithDateTimeOffsetMaxValue_ShouldNotThrow()
    {
        // Act & Assert
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(DateTimeOffset.MaxValue);
        version.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GenerateNewVersion_RapidGeneration_SameDateTimeOffset_ShouldIncrementSequentially()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act - Generate 5 versions with the same timestamp
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timestamp);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timestamp);
        var v3 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timestamp);
        var v4 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timestamp);
        var v5 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(timestamp);

        // Assert - Each should increment by exactly 1 tick from previous
        v1.Value.ShouldBeGreaterThanOrEqualTo(timestamp.UtcTicks);
        v2.Value.ShouldBe(v1.Value + 1);
        v3.Value.ShouldBe(v2.Value + 1);
        v4.Value.ShouldBe(v3.Value + 1);
        v5.Value.ShouldBe(v4.Value + 1);

        // Assert - All are strictly increasing
        (v1 < v2).ShouldBeTrue();
        (v2 < v3).ShouldBeTrue();
        (v3 < v4).ShouldBeTrue();
        (v4 < v5).ShouldBeTrue();
    }

    [Fact]
    public void GenerateNewVersion_ClockAdvances_ShouldUseNewTimestamp()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime.AddMilliseconds(100));

        // Assert
        (v1 < v2).ShouldBeTrue();
        v2.Value.ShouldBeGreaterThanOrEqualTo(baseTime.AddMilliseconds(100).UtcTicks);
    }

    #endregion [ DateTimeOffset Overload Tests ]

    #region [ Factory Method Tests ]

    [Fact]
    public void FromLong_WithPositiveValue_ShouldCreateVersion()
    {
        // Arrange
        long value = 637123456789012345L;

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Assert
        version.Value.ShouldBe(value);
    }

    [Fact]
    public void FromLong_WithZeroValue_ShouldCreateVersion()
    {
        // Arrange
        long value = 0L;

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Assert
        version.Value.ShouldBe(value);
    }

    [Fact]
    public void FromLong_WithMaxValue_ShouldCreateVersion()
    {
        // Arrange
        long value = long.MaxValue;

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Assert
        version.Value.ShouldBe(value);
    }

    [Fact]
    public void FromLong_WithMinValue_ShouldCreateVersion()
    {
        // Arrange
        long value = long.MinValue;

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Assert
        version.Value.ShouldBe(value);
    }

    [Fact]
    public void FromDateTimeOffset_WithValidDate_ShouldCreateVersion()
    {
        // Arrange
        var dateTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromDateTimeOffset(dateTime);

        // Assert
        version.Value.ShouldBe(dateTime.UtcTicks);
    }

    [Fact]
    public void FromDateTimeOffset_WithMinValue_ShouldCreateVersion()
    {
        // Arrange
        var dateTime = DateTimeOffset.MinValue;

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromDateTimeOffset(dateTime);

        // Assert
        version.Value.ShouldBe(dateTime.UtcTicks);
    }

    [Fact]
    public void FromDateTimeOffset_WithMaxValue_ShouldCreateVersion()
    {
        // Arrange
        var dateTime = DateTimeOffset.MaxValue;

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromDateTimeOffset(dateTime);

        // Assert
        version.Value.ShouldBe(dateTime.UtcTicks);
    }

    [Fact]
    public void FromDateTimeOffset_ShouldPreserveTicksExactly()
    {
        // Arrange
        var dateTime = new DateTimeOffset(2025, 6, 15, 14, 22, 33, 123, TimeSpan.FromHours(-3));

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromDateTimeOffset(dateTime);

        // Assert
        version.Value.ShouldBe(dateTime.UtcTicks);
    }

    #endregion [ Factory Method Tests ]

    #region [ Property Tests ]

    [Fact]
    public void Value_Property_ShouldReturnSetValue()
    {
        // Arrange
        long expectedValue = 637123456789012345L;
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(expectedValue);

        // Act
        var actualValue = version.Value;

        // Assert
        actualValue.ShouldBe(expectedValue);
    }

    [Fact]
    public void AsDateTimeOffset_ShouldReturnValidDateTimeOffset()
    {
        // Arrange
        var originalDateTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromDateTimeOffset(originalDateTime);

        // Act
        var convertedDateTime = version.AsDateTimeOffset;

        // Assert
        convertedDateTime.UtcTicks.ShouldBe(originalDateTime.UtcTicks);
    }

    [Fact]
    public void AsDateTimeOffset_ShouldHaveUTCOffset()
    {
        // Arrange
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();

        // Act
        var dateTime = version.AsDateTimeOffset;

        // Assert
        dateTime.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void AsDateTimeOffset_ShouldPreserveTicks()
    {
        // Arrange
        long ticks = 637123456789012345L;
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(ticks);

        // Act
        var dateTime = version.AsDateTimeOffset;

        // Assert
        dateTime.UtcTicks.ShouldBe(ticks);
    }

    [Fact]
    public void AsDateTimeOffset_RoundTrip_WithFromDateTimeOffset_ShouldPreserveValue()
    {
        // Arrange
        var original = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromDateTimeOffset(original);
        var restored = version.AsDateTimeOffset;

        // Assert
        restored.UtcTicks.ShouldBe(original.UtcTicks);
        restored.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void AsDateTimeOffset_WithMinValue_ShouldNotThrow()
    {
        // Arrange
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromDateTimeOffset(DateTimeOffset.MinValue);

        // Act & Assert
        var dateTime = version.AsDateTimeOffset;
        dateTime.UtcTicks.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void AsDateTimeOffset_WithMaxValue_ShouldNotThrow()
    {
        // Arrange
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromDateTimeOffset(DateTimeOffset.MaxValue);

        // Act & Assert
        var dateTime = version.AsDateTimeOffset;
        dateTime.UtcTicks.ShouldBeGreaterThan(0);
    }

    #endregion [ Property Tests ]

    #region [ Equality and Comparison Tests ]

    [Fact]
    public void Equals_WithSameVersion_ShouldReturnTrue()
    {
        // Arrange
        long value = 637123456789012345L;
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);
        object v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act & Assert
        v1.Equals(v2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentVersion_ShouldReturnFalse()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
        object v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();

        // Act & Assert
        v1.Equals(v2).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNonVersionObject_ShouldReturnFalse()
    {
        // Arrange
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
        object notAVersion = "not a RegistryVersion";

        // Act & Assert
        version.Equals(notAVersion).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        // Arrange
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();

        // Act & Assert
        version.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_IEquatable_WithSameValue_ShouldReturnTrue()
    {
        // Arrange
        long value = 637123456789012345L;
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act & Assert
        v1.Equals(v2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_IEquatable_WithDifferentValue_ShouldReturnFalse()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);

        // Act & Assert
        v1.Equals(v2).ShouldBeFalse();
    }

    [Fact]
    public void CompareTo_WithLesserVersion_ShouldReturnNegative()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        result.ShouldBeLessThan(0);
    }

    [Fact]
    public void CompareTo_WithEqualVersion_ShouldReturnZero()
    {
        // Arrange
        long value = 637123456789012345L;
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void CompareTo_WithGreaterVersion_ShouldReturnPositive()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        result.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_InSorting_ShouldOrderCorrectly()
    {
        // Arrange
        var versions = new[]
        {
            PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(500),
            PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100),
            PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(300),
            PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200),
            PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(400)
        };

        // Act
        Array.Sort(versions);

        // Assert
        versions[0].Value.ShouldBe(100);
        versions[1].Value.ShouldBe(200);
        versions[2].Value.ShouldBe(300);
        versions[3].Value.ShouldBe(400);
        versions[4].Value.ShouldBe(500);
    }

    #endregion [ Equality and Comparison Tests ]

    #region [ Operator Tests ]

    [Fact]
    public void ImplicitConversion_ToLong_ShouldReturnValue()
    {
        // Arrange
        long expectedValue = 637123456789012345L;
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(expectedValue);

        // Act
        long actualValue = version;

        // Assert
        actualValue.ShouldBe(expectedValue);
    }

    [Fact]
    public void ImplicitConversion_FromLong_ShouldCreateVersion()
    {
        // Arrange
        long value = 637123456789012345L;

        // Act
        PragmaStack.Core.RegistryVersions.RegistryVersion version = value;

        // Assert
        version.Value.ShouldBe(value);
    }

    [Fact]
    public void ImplicitConversion_ChainConversion_ShouldWork()
    {
        // Arrange
        long original = 637123456789012345L;

        // Act
        PragmaStack.Core.RegistryVersions.RegistryVersion version = original;
        long restored = version;

        // Assert
        restored.ShouldBe(original);
    }

    [Fact]
    public void EqualityOperator_WithEqualVersions_ShouldReturnTrue()
    {
        // Arrange
        long value = 637123456789012345L;
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act & Assert
        (v1 == v2).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_WithDifferentVersions_ShouldReturnFalse()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);

        // Act & Assert
        (v1 == v2).ShouldBeFalse();
    }

    [Fact]
    public void InequalityOperator_WithDifferentVersions_ShouldReturnTrue()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();

        // Act & Assert
        (v1 != v2).ShouldBeTrue();
    }

    [Fact]
    public void InequalityOperator_WithEqualVersions_ShouldReturnFalse()
    {
        // Arrange
        long value = 637123456789012345L;
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act & Assert
        (v1 != v2).ShouldBeFalse();
    }

    [Fact]
    public void LessThanOperator_WithLesserVersion_ShouldReturnTrue()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);

        // Act & Assert
        (v1 < v2).ShouldBeTrue();
    }

    [Fact]
    public void LessThanOperator_WithGreaterVersion_ShouldReturnFalse()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);

        // Act & Assert
        (v1 < v2).ShouldBeFalse();
    }

    [Fact]
    public void LessThanOperator_WithEqualVersion_ShouldReturnFalse()
    {
        // Arrange
        long value = 637123456789012345L;
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act & Assert
        (v1 < v2).ShouldBeFalse();
    }

    [Fact]
    public void GreaterThanOperator_WithGreaterVersion_ShouldReturnTrue()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);

        // Act & Assert
        (v1 > v2).ShouldBeTrue();
    }

    [Fact]
    public void GreaterThanOperator_WithLesserVersion_ShouldReturnFalse()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);

        // Act & Assert
        (v1 > v2).ShouldBeFalse();
    }

    [Fact]
    public void GreaterThanOperator_WithEqualVersion_ShouldReturnFalse()
    {
        // Arrange
        long value = 637123456789012345L;
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act & Assert
        (v1 > v2).ShouldBeFalse();
    }

    [Fact]
    public void LessThanOrEqualOperator_WithLesserVersion_ShouldReturnTrue()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);

        // Act & Assert
        (v1 <= v2).ShouldBeTrue();
    }

    [Fact]
    public void LessThanOrEqualOperator_WithEqualVersion_ShouldReturnTrue()
    {
        // Arrange
        long value = 637123456789012345L;
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act & Assert
        (v1 <= v2).ShouldBeTrue();
    }

    [Fact]
    public void LessThanOrEqualOperator_WithGreaterVersion_ShouldReturnFalse()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);

        // Act & Assert
        (v1 <= v2).ShouldBeFalse();
    }

    [Fact]
    public void GreaterThanOrEqualOperator_WithGreaterVersion_ShouldReturnTrue()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);

        // Act & Assert
        (v1 >= v2).ShouldBeTrue();
    }

    [Fact]
    public void GreaterThanOrEqualOperator_WithEqualVersion_ShouldReturnTrue()
    {
        // Arrange
        long value = 637123456789012345L;
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act & Assert
        (v1 >= v2).ShouldBeTrue();
    }

    [Fact]
    public void GreaterThanOrEqualOperator_WithLesserVersion_ShouldReturnFalse()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);

        // Act & Assert
        (v1 >= v2).ShouldBeFalse();
    }

    #endregion [ Operator Tests ]

    #region [ Hash Code Tests ]

    [Fact]
    public void GetHashCode_ForEqualVersions_ShouldReturnSameHashCode()
    {
        // Arrange
        long value = 637123456789012345L;
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act
        var hashCode1 = v1.GetHashCode();
        var hashCode2 = v2.GetHashCode();

        // Assert
        hashCode1.ShouldBe(hashCode2);
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistentAcrossMultipleCalls()
    {
        // Arrange
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();

        // Act
        var hashCode1 = version.GetHashCode();
        var hashCode2 = version.GetHashCode();
        var hashCode3 = version.GetHashCode();

        // Assert
        hashCode1.ShouldBe(hashCode2);
        hashCode2.ShouldBe(hashCode3);
    }

    [Fact]
    public void RegistryVersion_InHashSet_ShouldWork()
    {
        // Arrange
        var hashSet = new HashSet<PragmaStack.Core.RegistryVersions.RegistryVersion>();
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
        var v3 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(v1.Value); // Duplicate of v1

        // Act
        hashSet.Add(v1);
        hashSet.Add(v2);
        hashSet.Add(v3); // Should not be added (duplicate)

        // Assert
        hashSet.Count.ShouldBe(2);
        hashSet.Contains(v1).ShouldBeTrue();
        hashSet.Contains(v2).ShouldBeTrue();
        hashSet.Contains(v3).ShouldBeTrue(); // v3 equals v1
    }

    [Fact]
    public void RegistryVersion_AsDictionaryKey_ShouldWork()
    {
        // Arrange
        var dictionary = new Dictionary<PragmaStack.Core.RegistryVersions.RegistryVersion, string>();
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();

        // Act
        dictionary[v1] = "Version 1";
        dictionary[v2] = "Version 2";

        // Assert
        dictionary.Count.ShouldBe(2);
        dictionary[v1].ShouldBe("Version 1");
        dictionary[v2].ShouldBe("Version 2");
    }

    [Fact]
    public void RegistryVersion_WithLinqDistinct_ShouldWork()
    {
        // Arrange
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(200);
        var v3 = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(100); // Duplicate of v1

        var versions = new[] { v1, v2, v3, v1, v2 };

        // Act
        var distinctVersions = versions.Distinct().ToList();

        // Assert
        distinctVersions.Count.ShouldBe(2);
    }

    #endregion [ Hash Code Tests ]

    #region [ Clock Drift Tests ]

    [Fact]
    public void GenerateNewVersion_ClockDriftBackwardOneTick_ShouldIncrementByOneTick()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime.AddTicks(-1));

        // Assert
        v2.Value.ShouldBe(v1.Value + 1);
    }

    [Fact]
    public void GenerateNewVersion_ClockDriftBackwardMultipleTicks_ShouldStillIncrementByOneTick()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(baseTime.AddTicks(-1000));

        // Assert
        v2.Value.ShouldBe(v1.Value + 1, "Should increment by exactly 1 tick, not 1000");
    }

    #endregion [ Clock Drift Tests ]

    #region [ High-Volume Stress Tests ]

    [Fact]
    public void GenerateNewVersion_ExtremeHighVolume_100kVersions_ShouldMaintainMonotonicity()
    {
        // This test generates a very large number of versions to verify the system handles
        // extreme volume scenarios and maintains monotonicity under stress.

        // Arrange & Act - Generate 100K versions
        var versionSet = new HashSet<long>();
        var lastVersion = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();

        for (int i = 0; i < 100_000; i++)
        {
            var newVersion = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();

            // Assert - All versions should be unique
            versionSet.Add(newVersion.Value).ShouldBeTrue($"Version at iteration {i} should be unique");

            // Assert - Versions should be monotonic
            (lastVersion < newVersion).ShouldBeTrue($"Versions should be monotonic at iteration {i}");

            lastVersion = newVersion;
        }

        versionSet.Count.ShouldBe(100_000);
    }

    #endregion [ High-Volume Stress Tests ]

    #region [ ToString Tests ]

    [Fact]
    public void ToString_ShouldReturnValueAsString()
    {
        // Arrange
        long value = 637123456789012345L;
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(value);

        // Act
        var stringValue = version.ToString();

        // Assert
        stringValue.ShouldBe(value.ToString());
    }

    [Fact]
    public void ToString_ShouldBeConsistent()
    {
        // Arrange
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion();

        // Act
        var str1 = version.ToString();
        var str2 = version.ToString();

        // Assert
        str1.ShouldBe(str2);
    }

    #endregion [ ToString Tests ]

    #region [ Round-Trip Tests ]

    [Fact]
    public void RoundTrip_FromLong_ImplicitConversion_ShouldPreserveValue()
    {
        // Arrange
        long original = 637123456789012345L;

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.FromLong(original);
        long restored = version; // Implicit conversion

        // Assert
        restored.ShouldBe(original);
    }

    [Fact]
    public void RoundTrip_ImplicitConversion_Both_ShouldPreserveValue()
    {
        // Arrange
        long original = 637123456789012345L;

        // Act
        PragmaStack.Core.RegistryVersions.RegistryVersion version = original; // Implicit from long
        long restored = version; // Implicit to long

        // Assert
        restored.ShouldBe(original);
    }

    #endregion [ Round-Trip Tests ]

    #region [ Edge Cases Tests ]

    [Fact]
    public void GenerateNewVersion_WithEpochTime_ShouldWork()
    {
        // Arrange
        var unixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(unixEpoch);

        // Assert
        version.Value.ShouldBeGreaterThan(0);
        version.Value.ShouldBeGreaterThanOrEqualTo(unixEpoch.UtcTicks);
    }

    [Fact]
    public void GenerateNewVersion_WithFutureDate_ShouldWork()
    {
        // Arrange
        var futureDate = DateTimeOffset.UtcNow.AddYears(100);

        // Act
        var version = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(futureDate);

        // Assert
        version.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GenerateNewVersion_WithDifferentTimeZones_ShouldNormalizeToUTC()
    {
        // Arrange
        var date1 = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.FromHours(5.5));  // +5:30
        var date2 = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.FromHours(-8));   // -8:00
        var date3 = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);            // UTC

        // Act
        var v1 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(date1);
        var v2 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(date2);
        var v3 = PragmaStack.Core.RegistryVersions.RegistryVersion.GenerateNewVersion(date3);

        // Assert - All should use UTC ticks (may be higher due to monotonicity protection)
        v1.Value.ShouldBeGreaterThanOrEqualTo(date1.UtcTicks);
        v2.Value.ShouldBeGreaterThanOrEqualTo(date2.UtcTicks);
        v3.Value.ShouldBeGreaterThanOrEqualTo(date3.UtcTicks);

        // All versions should be monotonic
        (v1 < v2).ShouldBeTrue();
        (v2 < v3).ShouldBeTrue();
    }

    #endregion [ Edge Cases Tests ]
}
