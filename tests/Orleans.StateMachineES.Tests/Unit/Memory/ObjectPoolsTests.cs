using FluentAssertions;
using Orleans.StateMachineES.Memory;
using Xunit;

namespace Orleans.StateMachineES.Tests.Unit.Memory;

public class ObjectPoolsTests
{
    [Fact]
    public void ObjectPools_StaticInstances_ShouldBeInitialized()
    {
        // Assert
        ObjectPools.ByteArrayPool.Should().NotBeNull();
        ObjectPools.ObjectArrayPool.Should().NotBeNull();
        ObjectPools.StringListPool.Should().NotBeNull();
        ObjectPools.StringObjectDictionaryPool.Should().NotBeNull();
        ObjectPools.CharListPool.Should().NotBeNull();
        ObjectPools.StringHashSetPool.Should().NotBeNull();
    }

    [Fact]
    public void ObjectPool_Constructor_WithNullFactory_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ObjectPool<string>(null!, null, 10));
    }

    [Fact]
    public void ObjectPool_Constructor_WithValidParameters_ShouldInitialize()
    {
        // Act
        var pool = new ObjectPool<List<int>>(() => [], list => list.Clear(), 50);

        // Assert
        pool.Should().NotBeNull();
        pool.Count.Should().Be(0);
    }

    [Fact]
    public void ObjectPool_GetAndReturn_ShouldReuseObjects()
    {
        // Arrange
        var pool = new ObjectPool<List<string>>(() => [], list => list.Clear());
        
        // Act
        var obj1 = pool.Get();
        obj1.Add("test");
        pool.Return(obj1);
        
        var obj2 = pool.Get();

        // Assert
        obj2.Should().BeSameAs(obj1);
        obj2.Should().BeEmpty(); // Reset action should have cleared it
        pool.Count.Should().Be(1); // Count tracks total objects managed by pool
    }

    [Fact]
    public void ObjectPool_GetMultiple_ShouldCreateNewWhenEmpty()
    {
        // Arrange
        var pool = new ObjectPool<List<string>>(() => []);
        
        // Act
        var obj1 = pool.Get();
        var obj2 = pool.Get();

        // Assert
        obj1.Should().NotBeSameAs(obj2);
        obj1.Should().NotBeNull();
        obj2.Should().NotBeNull();
    }

    [Fact]
    public void ObjectPool_ReturnNull_ShouldBeIgnored()
    {
        // Arrange
        var pool = new ObjectPool<List<string>>(() => []);
        
        // Act
        pool.Return(null!);

        // Assert
        pool.Count.Should().Be(0);
    }

    [Fact]
    public void ObjectPool_ExceedMaxSize_ShouldNotAddToPool()
    {
        // Arrange
        var pool = new ObjectPool<List<string>>(() => [], null, maxPoolSize: 2);
        
        // Act
        var obj1 = pool.Get();
        var obj2 = pool.Get();
        var obj3 = pool.Get();
        
        pool.Return(obj1);
        pool.Return(obj2);
        pool.Return(obj3); // This should not be added due to max size

        // Assert
        pool.Count.Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public void ObjectPool_GetDisposable_ShouldReturnPooledObjectWrapper()
    {
        // Arrange
        var pool = new ObjectPool<List<string>>(() => [], list => list.Clear());
        
        // Act & Assert
        using (var pooledObj = pool.GetDisposable())
        {
            pooledObj.Value.Should().NotBeNull();
            pooledObj.Value.Should().BeOfType<List<string>>();
            
            pooledObj.Value.Add("test");
        } // Dispose should return object to pool
        
        // Object should be returned and reset
        var retrieved = pool.Get();
        retrieved.Should().BeEmpty();
    }

    [Fact]
    public void PooledObject_Dispose_ShouldReturnToPool()
    {
        // Arrange
        var pool = new ObjectPool<List<string>>(() => [], list => list.Clear());
        var pooledObj = pool.GetDisposable();
        pooledObj.Value.Add("test");
        
        // Act
        pooledObj.Dispose();
        
        // Assert
        var retrieved = pool.Get();
        retrieved.Should().BeSameAs(pooledObj.Value);
        retrieved.Should().BeEmpty(); // Reset action should have cleared it
    }

    [Fact]
    public void StringListPool_GetAndReturn_ShouldWorkCorrectly()
    {
        // Act
        var list = ObjectPools.StringListPool.Get();
        list.Add("test1");
        list.Add("test2");
        ObjectPools.StringListPool.Return(list);
        
        var retrieved = ObjectPools.StringListPool.Get();

        // Assert
        retrieved.Should().BeSameAs(list);
        retrieved.Should().BeEmpty(); // Should be cleared by reset action
    }

    [Fact]
    public void StringObjectDictionaryPool_GetAndReturn_ShouldWorkCorrectly()
    {
        // Act
        var dict = ObjectPools.StringObjectDictionaryPool.Get();
        dict["key1"] = "value1";
        dict["key2"] = 42;
        ObjectPools.StringObjectDictionaryPool.Return(dict);
        
        var retrieved = ObjectPools.StringObjectDictionaryPool.Get();

        // Assert
        retrieved.Should().BeSameAs(dict);
        retrieved.Should().BeEmpty(); // Should be cleared by reset action
    }

    [Fact]
    public void CharListPool_GetAndReturn_ShouldWorkCorrectly()
    {
        // Act
        var charList = ObjectPools.CharListPool.Get();
        charList.AddRange("hello");
        ObjectPools.CharListPool.Return(charList);
        
        var retrieved = ObjectPools.CharListPool.Get();

        // Assert
        retrieved.Should().BeSameAs(charList);
        retrieved.Should().BeEmpty(); // Should be cleared by reset action
    }

    [Fact]
    public void StringHashSetPool_GetAndReturn_ShouldWorkCorrectly()
    {
        // Act
        var hashSet = ObjectPools.StringHashSetPool.Get();
        hashSet.Add("item1");
        hashSet.Add("item2");
        ObjectPools.StringHashSetPool.Return(hashSet);
        
        var retrieved = ObjectPools.StringHashSetPool.Get();

        // Assert
        retrieved.Should().BeSameAs(hashSet);
        retrieved.Should().BeEmpty(); // Should be cleared by reset action
    }

    [Fact]
    public void ByteArrayPool_ShouldBeSharedInstance()
    {
        // Act
        var bytes = ObjectPools.ByteArrayPool.Rent(1024);
        
        // Assert
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterOrEqualTo(1024);
        
        // Cleanup
        ObjectPools.ByteArrayPool.Return(bytes);
    }

    [Fact]
    public void ObjectArrayPool_ShouldBeSharedInstance()
    {
        // Act
        var objects = ObjectPools.ObjectArrayPool.Rent(10);
        
        // Assert
        objects.Should().NotBeNull();
        objects.Length.Should().BeGreaterOrEqualTo(10);
        
        // Cleanup
        ObjectPools.ObjectArrayPool.Return(objects);
    }

    private enum TestState { Idle, Active }
    private enum TestTrigger { Start, Stop }

    [Fact]
    public void StateTransitionEventPool_Get_ShouldReturnValidInstance()
    {
        // Act
        var evt = StateTransitionEventPool<TestState, TestTrigger>.Get();

        // Assert
        evt.Should().NotBeNull();
        
        // Cleanup
        StateTransitionEventPool<TestState, TestTrigger>.Return(evt);
    }

    [Fact]
    public void StateTransitionEventPool_Create_ShouldReturnConfiguredEvent()
    {
        // Arrange
        var fromState = TestState.Idle;
        var toState = TestState.Active;
        var trigger = TestTrigger.Start;
        var timestamp = DateTime.UtcNow;
        var correlationId = "test-correlation-id";
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var evt = StateTransitionEventPool<TestState, TestTrigger>.Create(
            fromState, toState, trigger, timestamp, correlationId, 
            metadata: metadata);

        // Assert
        evt.Should().NotBeNull();
        evt.FromState.Should().Be(fromState);
        evt.ToState.Should().Be(toState);
        evt.Trigger.Should().Be(trigger);
        evt.Timestamp.Should().Be(timestamp);
        evt.CorrelationId.Should().Be(correlationId);
        evt.Metadata.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void StateTransitionEventPool_GetAndReturn_ShouldReuse()
    {
        // Act
        var evt1 = StateTransitionEventPool<TestState, TestTrigger>.Get();
        StateTransitionEventPool<TestState, TestTrigger>.Return(evt1);
        var evt2 = StateTransitionEventPool<TestState, TestTrigger>.Get();

        // Assert
        evt2.Should().BeSameAs(evt1);
        
        // Cleanup
        StateTransitionEventPool<TestState, TestTrigger>.Return(evt2);
    }

    [Fact]
    public void PoolStatistics_GetStatistics_ShouldReturnCurrentStats()
    {
        // Arrange - Add some items to pools
        var list = ObjectPools.StringListPool.Get();
        var dict = ObjectPools.StringObjectDictionaryPool.Get();
        ObjectPools.StringListPool.Return(list);
        ObjectPools.StringObjectDictionaryPool.Return(dict);

        // Act
        var stats = PoolStatistics.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        stats.StringListPoolCount.Should().BeGreaterOrEqualTo(0);
        stats.StringObjectDictionaryPoolCount.Should().BeGreaterOrEqualTo(0);
        stats.CharListPoolCount.Should().BeGreaterOrEqualTo(0);
        stats.StringHashSetPoolCount.Should().BeGreaterOrEqualTo(0);
        stats.TotalPooledObjects.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void PoolStats_TotalPooledObjects_ShouldSumAllPools()
    {
        // Arrange
        var stats = new PoolStats
        {
            StringListPoolCount = 5,
            StringObjectDictionaryPoolCount = 3,
            CharListPoolCount = 2,
            StringHashSetPoolCount = 1
        };

        // Act
        var total = stats.TotalPooledObjects;

        // Assert
        total.Should().Be(11); // 5 + 3 + 2 + 1
    }

    [Fact]
    public async Task ObjectPool_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var pool = new ObjectPool<List<int>>(() => [], list => list.Clear());
        var tasks = new List<System.Threading.Tasks.Task>();
        var exceptions = new List<Exception>();

        // Act - Simulate concurrent access
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var obj = pool.Get();
                        obj.Add(j);
                        pool.Return(obj);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await System.Threading.Tasks.Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty();
    }
}