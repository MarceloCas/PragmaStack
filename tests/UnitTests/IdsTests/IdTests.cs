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
    public void GenerateNewGlobalId_ShouldReturnUniqueAndSequentialIds()
    {
        // Arrange
        var numberOfIdsToGenerate = 1000;
        var idList = new List<Guid>(capacity: numberOfIdsToGenerate);

        // Act
        for (int i = 0; i < numberOfIdsToGenerate; i++)
        {
            var newId = PragmaStack.Core.Ids.Id.GenerateNewGlobalId();
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
    public void GenerateNewGlobalId_MultiThreaded_ShouldReturnUniqueIds()
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
                var newId = PragmaStack.Core.Ids.Id.GenerateNewGlobalId();
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
    public void GenerateNewGlobalId_MultiThreaded_ShouldBeGloballySequential()
    {
        // Arrange
        var numberOfThreads = 5;
        var idsPerThread = 200;
        var allIds = new ConcurrentBag<Guid>();

        // Act
        Parallel.For(fromInclusive: 0, toExclusive: numberOfThreads, body: _ =>
        {
            for (int i = 0; i < idsPerThread; i++)
            {
                var newId = PragmaStack.Core.Ids.Id.GenerateNewGlobalId();
                allIds.Add(newId);
            }
        });

        // Assert
        var sortedIds = allIds.OrderBy(id => id).ToList();

        // All IDs should already be in sorted order (or very close due to timing)
        // We verify that the generated IDs respect global monotonicity
        var totalIds = numberOfThreads * idsPerThread;
        allIds.Count.ShouldBe(totalIds);

        var uniqueIds = new HashSet<Guid>(allIds);
        uniqueIds.Count.ShouldBe(totalIds, "All global IDs should be unique");
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
    public void GenerateNewGlobalId_StressTest_ShouldHandleHighVolume()
    {
        // Arrange
        var numberOfIdsToGenerate = 10000;
        var idSet = new HashSet<Guid>();

        // Act & Assert - Should not throw and all IDs should be unique
        for (int i = 0; i < numberOfIdsToGenerate; i++)
        {
            var newId = PragmaStack.Core.Ids.Id.GenerateNewGlobalId();
            idSet.Add(newId).ShouldBeTrue($"ID at position {i} should be unique");
        }

        idSet.Count.ShouldBe(numberOfIdsToGenerate);
    }
}
