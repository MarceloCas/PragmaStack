using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.IdsTests;

public class IdTests
{
    [Fact]
    public void GenerateNewId_ShouldReturnUniqueAndSequentialIds()
    {
        // Arrange
        var numberOfIdsToGenerate = 1000;
        var idList = new List<Guid>(capacity: numberOfIdsToGenerate);

        // Act
        for (int i = 0; i < numberOfIdsToGenerate; i++)
        {
            var newId = PragmaStack.Core.Ids.Id.GenerateNewId();
            idList.Add(newId);
        }

        // Assert
        var idSet = new HashSet<Guid>(idList);
        idSet.Count.ShouldBe(numberOfIdsToGenerate);

        for (int i = 0; i < idList.Count - 1; i++)
        {
            var currentId = idList[i];
            var nextId = idList[i + 1];

            var comparisonResult = currentId.CompareTo(nextId);
            comparisonResult.ShouldBeLessThan(0);
        }
    }

    [Fact]
    public void GenerateNewId_MultiThreaded_ShouldReturnUniqueIds()
    {
        // Arrange
        var numberOfThreads = 10;
        var idsPerThread = 1000;
        var allIds = new ConcurrentBag<Guid>();

        // Act
        Parallel.For(fromInclusive: 0, toExclusive: numberOfThreads, body: _ =>
        {
            for (int i = 0; i < idsPerThread; i++)
            {
                var newId = PragmaStack.Core.Ids.Id.GenerateNewId();
                allIds.Add(newId);
            }
        });

        // Assert
        var totalIds = numberOfThreads * idsPerThread;
        allIds.Count.ShouldBe(totalIds);

        var uniqueIds = new HashSet<Guid>(allIds);
        uniqueIds.Count.ShouldBe(totalIds, "All IDs should be unique");
    }

    [Fact]
    public void GenerateNewId_MultiThreaded_ShouldBeSequentialPerThread()
    {
        // Arrange
        var numberOfThreads = 5;
        var idsPerThread = 100;
        var threadResults = new ConcurrentBag<List<Guid>>();

        // Act
        Parallel.For(0, numberOfThreads, _ =>
        {
            var threadIds = new List<Guid>(capacity: idsPerThread);
            for (int i = 0; i < idsPerThread; i++)
            {
                var newId = PragmaStack.Core.Ids.Id.GenerateNewId();
                threadIds.Add(newId);
            }
            threadResults.Add(threadIds);
        });

        // Assert - Each thread's IDs should be sequential
        foreach (var threadIds in threadResults)
        {
            for (int i = 0; i < threadIds.Count - 1; i++)
            {
                var currentId = threadIds[i];
                var nextId = threadIds[i + 1];

                var comparisonResult = currentId.CompareTo(nextId);
                comparisonResult.ShouldBeLessThan(0, $"IDs within same thread should be sequential: {currentId} < {nextId}");
            }
        }
    }

    [Fact]
    public void GenerateNewId_StressTest_ShouldHandleHighVolume()
    {
        // Arrange
        var numberOfIdsToGenerate = 10000;
        var idSet = new HashSet<Guid>();

        // Act & Assert - Should not throw and all IDs should be unique
        for (int i = 0; i < numberOfIdsToGenerate; i++)
        {
            var newId = PragmaStack.Core.Ids.Id.GenerateNewId();
            idSet.Add(newId).ShouldBeTrue($"ID at position {i} should be unique");
        }

        idSet.Count.ShouldBe(numberOfIdsToGenerate);
    }


    [Fact]
    public void GenerateNewId_WithFixedTimeProvider_ShouldGenerateDeterministicIds()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            utcNowFunc: _ => fixedTime,
            localTimeZone: null
        );

        // Act
        var id1 = PragmaStack.Core.Ids.Id.GenerateNewId(timeProvider);
        var id2 = PragmaStack.Core.Ids.Id.GenerateNewId(timeProvider);
        var id3 = PragmaStack.Core.Ids.Id.GenerateNewId(timeProvider);

        // Assert
        // All IDs should be unique (different counters)
        id1.ShouldNotBe(id2);
        id2.ShouldNotBe(id3);
        id1.ShouldNotBe(id3);

        // All IDs should be sequential (monotonic)
        (id1 < id2).ShouldBeTrue("id1 should be less than id2");
        (id2 < id3).ShouldBeTrue("id2 should be less than id3");
    }

    [Fact]
    public void GenerateNewId_WithAdvancingTimeProvider_ShouldResetCounter()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var currentTime = baseTime;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            utcNowFunc: _ => currentTime,
            localTimeZone: null
        );

        // Act - Generate IDs in same millisecond
        var id1 = PragmaStack.Core.Ids.Id.GenerateNewId(timeProvider);
        var id2 = PragmaStack.Core.Ids.Id.GenerateNewId(timeProvider);

        // Advance time by 1 millisecond
        currentTime = baseTime.AddMilliseconds(1);

        // Generate more IDs in new millisecond
        var id3 = PragmaStack.Core.Ids.Id.GenerateNewId(timeProvider);
        var id4 = PragmaStack.Core.Ids.Id.GenerateNewId(timeProvider);

        // Assert
        // All IDs should be unique
        var allIds = new[] { id1, id2, id3, id4 };
        var uniqueIds = new HashSet<Guid>(allIds.Select(id => id.Value));
        uniqueIds.Count.ShouldBe(4);

        // All IDs should be monotonically increasing
        (id1 < id2).ShouldBeTrue("id1 < id2");
        (id2 < id3).ShouldBeTrue("id2 < id3 (counter reset with new timestamp)");
        (id3 < id4).ShouldBeTrue("id3 < id4");
    }

    [Fact]
    public void GenerateNewId_WithTimeProviderClockDrift_ShouldMaintainMonotonicity()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var currentTime = baseTime;
        var timeProvider = new PragmaStack.Core.TimeProviders.CustomTimeProvider(
            utcNowFunc: _ => currentTime,
            localTimeZone: null
        );

        // Act - Generate IDs at base time
        var id1 = PragmaStack.Core.Ids.Id.GenerateNewId(timeProvider);

        // Simulate clock drift backward (time goes backwards)
        currentTime = baseTime.AddMilliseconds(-5);
        var id2 = PragmaStack.Core.Ids.Id.GenerateNewId(timeProvider);

        // Assert
        // Even with clock drift, IDs should remain monotonic
        (id1 < id2).ShouldBeTrue("IDs should remain monotonic even when clock goes backward");
    }

    [Fact]
    public void GenerateNewId_WithSystemTimeProvider_ShouldWork()
    {
        // Act
        var id1 = PragmaStack.Core.Ids.Id.GenerateNewId(TimeProvider.System);
        var id2 = PragmaStack.Core.Ids.Id.GenerateNewId(TimeProvider.System);

        // Assert
        id1.ShouldNotBe(id2);
        id1.Value.ShouldNotBe(Guid.Empty);
        id2.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void GenerateNewId_WithSameDateTimeOffset_ShouldIncrementCounter()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var id1 = PragmaStack.Core.Ids.Id.GenerateNewId(timestamp);
        var id2 = PragmaStack.Core.Ids.Id.GenerateNewId(timestamp);
        var id3 = PragmaStack.Core.Ids.Id.GenerateNewId(timestamp);

        // Assert
        id1.ShouldNotBe(id2);
        id2.ShouldNotBe(id3);

        // Should be monotonically increasing
        (id1 < id2).ShouldBeTrue();
        (id2 < id3).ShouldBeTrue();
    }

    [Fact]
    public void GenerateNewId_WithAdvancingDateTimeOffset_ShouldGenerateSequentialIds()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var id1 = PragmaStack.Core.Ids.Id.GenerateNewId(baseTime);
        var id2 = PragmaStack.Core.Ids.Id.GenerateNewId(baseTime.AddMilliseconds(1));
        var id3 = PragmaStack.Core.Ids.Id.GenerateNewId(baseTime.AddMilliseconds(2));

        // Assert
        (id1 < id2).ShouldBeTrue();
        (id2 < id3).ShouldBeTrue();
    }

    [Fact]
    public void GenerateNewId_WithBackwardDateTimeOffset_ShouldMaintainMonotonicity()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var id1 = PragmaStack.Core.Ids.Id.GenerateNewId(baseTime);
        var id2 = PragmaStack.Core.Ids.Id.GenerateNewId(baseTime.AddMilliseconds(-10)); // Clock goes backward

        // Assert
        // Should maintain monotonicity even when timestamp goes backward
        (id1 < id2).ShouldBeTrue("IDs should remain monotonic despite backward timestamp");
    }

    [Fact]
    public void GenerateNewId_WithDateTimeOffsetMinValue_ShouldNotThrow()
    {
        // Act & Assert
        var id = PragmaStack.Core.Ids.Id.GenerateNewId(DateTimeOffset.MinValue);
        id.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void GenerateNewId_WithDateTimeOffsetMaxValue_ShouldNotThrow()
    {
        // Act & Assert
        var id = PragmaStack.Core.Ids.Id.GenerateNewId(DateTimeOffset.MaxValue);
        id.Value.ShouldNotBe(Guid.Empty);
    }
}
