using FluentAssertions;
using Orleans.StateMachineES.EventSourcing.Evolution;

namespace Orleans.StateMachineES.Tests.Unit.Evolution;

public class EventUpcastRegistryTests
{
    // Test event versions
    [EventVersion(1)]
    public class OrderCreatedV1
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    [EventVersion(2, typeof(OrderCreatedV1))]
    public class OrderCreatedV2
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime CreatedAt { get; set; }
    }

    [EventVersion(3, typeof(OrderCreatedV2))]
    public class OrderCreatedV3
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime CreatedAt { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public bool IsPriority { get; set; }
    }

    [Fact]
    public void Register_LambdaUpcast_ShouldSucceed()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions { AutoRegisterUpcasters = false });

        // Act
        registry.Register<OrderCreatedV1, OrderCreatedV2>((v1, ctx) => new OrderCreatedV2
        {
            OrderId = v1.OrderId,
            Amount = v1.Amount,
            Currency = "USD",
            CreatedAt = ctx.MigrationTimestamp
        });

        // Assert
        registry.CanUpcast(typeof(OrderCreatedV1), typeof(OrderCreatedV2)).Should().BeTrue();
    }

    [Fact]
    public void TryUpcast_SingleStep_ShouldSucceed()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions { AutoRegisterUpcasters = false });
        registry.Register<OrderCreatedV1, OrderCreatedV2>((v1, ctx) => new OrderCreatedV2
        {
            OrderId = v1.OrderId,
            Amount = v1.Amount,
            Currency = "EUR",
            CreatedAt = DateTime.UtcNow
        });

        var oldEvent = new OrderCreatedV1 { OrderId = "ORD-123", Amount = 99.99m };

        // Act
        var result = registry.TryUpcast(oldEvent, typeof(OrderCreatedV2));

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OrderCreatedV2>();

        var v2 = (OrderCreatedV2)result!;
        v2.OrderId.Should().Be("ORD-123");
        v2.Amount.Should().Be(99.99m);
        v2.Currency.Should().Be("EUR");
    }

    [Fact]
    public void TryUpcast_MultipleSteps_ShouldChainCorrectly()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions { AutoRegisterUpcasters = false });

        // Register V1 -> V2
        registry.Register<OrderCreatedV1, OrderCreatedV2>((v1, ctx) => new OrderCreatedV2
        {
            OrderId = v1.OrderId,
            Amount = v1.Amount,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow
        });

        // Register V2 -> V3
        registry.Register<OrderCreatedV2, OrderCreatedV3>((v2, ctx) => new OrderCreatedV3
        {
            OrderId = v2.OrderId,
            Amount = v2.Amount,
            Currency = v2.Currency,
            CreatedAt = v2.CreatedAt,
            CustomerId = "UNKNOWN",
            IsPriority = false
        });

        var oldEvent = new OrderCreatedV1 { OrderId = "ORD-456", Amount = 150.00m };

        // Act - Upcast directly from V1 to V3
        var result = registry.TryUpcast(oldEvent, typeof(OrderCreatedV3));

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<OrderCreatedV3>();

        var v3 = (OrderCreatedV3)result!;
        v3.OrderId.Should().Be("ORD-456");
        v3.Amount.Should().Be(150.00m);
        v3.Currency.Should().Be("USD");
        v3.CustomerId.Should().Be("UNKNOWN");
        v3.IsPriority.Should().BeFalse();
    }

    [Fact]
    public void TryUpcast_NoPath_ShouldReturnNull()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions
        {
            AutoRegisterUpcasters = false,
            ThrowOnMissingUpcast = false
        });

        var oldEvent = new OrderCreatedV1 { OrderId = "ORD-789", Amount = 50.00m };

        // Act
        var result = registry.TryUpcast(oldEvent, typeof(OrderCreatedV3));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryUpcast_NoPathWithThrow_ShouldThrow()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions
        {
            AutoRegisterUpcasters = false,
            ThrowOnMissingUpcast = true
        });

        var oldEvent = new OrderCreatedV1 { OrderId = "ORD-789", Amount = 50.00m };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(
            () => registry.TryUpcast(oldEvent, typeof(OrderCreatedV3)));
    }

    [Fact]
    public void TryUpcast_SameType_ShouldReturnOriginal()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions { AutoRegisterUpcasters = false });
        var originalEvent = new OrderCreatedV1 { OrderId = "ORD-111", Amount = 25.00m };

        // Act
        var result = registry.TryUpcast(originalEvent, typeof(OrderCreatedV1));

        // Assert
        result.Should().BeSameAs(originalEvent);
    }

    [Fact]
    public void RegisterAutoUpcast_ShouldCopyMatchingProperties()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions { AutoRegisterUpcasters = false });
        registry.RegisterAutoUpcast<OrderCreatedV1, OrderCreatedV2>();

        var oldEvent = new OrderCreatedV1 { OrderId = "ORD-AUTO", Amount = 75.50m };

        // Act
        var result = registry.TryUpcast(oldEvent, typeof(OrderCreatedV2));

        // Assert
        result.Should().NotBeNull();
        var v2 = (OrderCreatedV2)result!;
        v2.OrderId.Should().Be("ORD-AUTO");
        v2.Amount.Should().Be(75.50m);
        v2.Currency.Should().Be("USD"); // Default value
    }

    [Fact]
    public void GetEventVersion_WithAttribute_ShouldReturnCorrectVersion()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions { AutoRegisterUpcasters = false });

        // Act & Assert
        registry.GetEventVersion(typeof(OrderCreatedV1)).Should().Be(1);
        registry.GetEventVersion(typeof(OrderCreatedV2)).Should().Be(2);
        registry.GetEventVersion(typeof(OrderCreatedV3)).Should().Be(3);
    }

    [Fact]
    public void GetEventVersion_WithoutAttribute_ShouldReturnOne()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions { AutoRegisterUpcasters = false });

        // Act & Assert - Using a type without the attribute
        registry.GetEventVersion(typeof(string)).Should().Be(1);
    }

    [Fact]
    public void CanUpcast_WithPath_ShouldReturnTrue()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions { AutoRegisterUpcasters = false });
        registry.Register<OrderCreatedV1, OrderCreatedV2>((v1, ctx) => new OrderCreatedV2
        {
            OrderId = v1.OrderId,
            Amount = v1.Amount
        });

        // Assert
        registry.CanUpcast(typeof(OrderCreatedV1), typeof(OrderCreatedV2)).Should().BeTrue();
        registry.CanUpcast(typeof(OrderCreatedV1), typeof(OrderCreatedV3)).Should().BeFalse();
    }

    [Fact]
    public void UpcastToLatest_ShouldFindLatestVersion()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions { AutoRegisterUpcasters = false });

        registry.Register<OrderCreatedV1, OrderCreatedV2>((v1, ctx) => new OrderCreatedV2
        {
            OrderId = v1.OrderId,
            Amount = v1.Amount,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow
        });

        registry.Register<OrderCreatedV2, OrderCreatedV3>((v2, ctx) => new OrderCreatedV3
        {
            OrderId = v2.OrderId,
            Amount = v2.Amount,
            Currency = v2.Currency,
            CreatedAt = v2.CreatedAt,
            CustomerId = "AUTO",
            IsPriority = true
        });

        var v1Event = new OrderCreatedV1 { OrderId = "LATEST-TEST", Amount = 200.00m };

        // Act
        var result = registry.UpcastToLatest(v1Event);

        // Assert
        result.Should().BeOfType<OrderCreatedV3>();
        var v3 = (OrderCreatedV3)result;
        v3.OrderId.Should().Be("LATEST-TEST");
        v3.IsPriority.Should().BeTrue();
    }

    [Fact]
    public void ClearCache_ShouldResetCache()
    {
        // Arrange
        var registry = new EventUpcastRegistry(new EventEvolutionOptions
        {
            AutoRegisterUpcasters = false,
            CacheUpcastedEvents = true
        });

        registry.Register<OrderCreatedV1, OrderCreatedV2>((v1, ctx) => new OrderCreatedV2
        {
            OrderId = v1.OrderId,
            Amount = v1.Amount
        });

        var v1Event = new OrderCreatedV1 { OrderId = "CACHE-TEST", Amount = 100.00m };

        // Populate cache
        registry.TryUpcast(v1Event, typeof(OrderCreatedV2));

        // Act
        registry.ClearCache();

        // Assert - Cache should be empty (no direct way to check, but operation should succeed)
        var result = registry.TryUpcast(v1Event, typeof(OrderCreatedV2));
        result.Should().NotBeNull();
    }

    [Fact]
    public void Callbacks_ShouldBeInvoked()
    {
        // Arrange
        var upcastedCalled = false;
        Type? upcastedFromType = null;
        Type? upcastedToType = null;

        var options = new EventEvolutionOptions
        {
            AutoRegisterUpcasters = false,
            OnEventUpcasted = args =>
            {
                upcastedCalled = true;
                upcastedFromType = args.OriginalType;
                upcastedToType = args.TargetType;
            }
        };

        var registry = new EventUpcastRegistry(options);
        registry.Register<OrderCreatedV1, OrderCreatedV2>((v1, ctx) => new OrderCreatedV2
        {
            OrderId = v1.OrderId,
            Amount = v1.Amount
        });

        var v1Event = new OrderCreatedV1 { OrderId = "CALLBACK-TEST", Amount = 50.00m };

        // Act
        registry.TryUpcast(v1Event, typeof(OrderCreatedV2));

        // Assert
        upcastedCalled.Should().BeTrue();
        upcastedFromType.Should().Be(typeof(OrderCreatedV1));
        upcastedToType.Should().Be(typeof(OrderCreatedV2));
    }

    [Fact]
    public void EventMigrationContext_ShouldProvideCorrectInfo()
    {
        // Arrange
        EventMigrationContext? capturedContext = null;

        var registry = new EventUpcastRegistry(new EventEvolutionOptions { AutoRegisterUpcasters = false });
        registry.Register<OrderCreatedV1, OrderCreatedV2>((v1, ctx) =>
        {
            capturedContext = ctx;
            return new OrderCreatedV2
            {
                OrderId = v1.OrderId,
                Amount = v1.Amount,
                CreatedAt = ctx.MigrationTimestamp
            };
        });

        var v1Event = new OrderCreatedV1 { OrderId = "CTX-TEST", Amount = 100.00m };

        // Act
        registry.TryUpcast(v1Event, typeof(OrderCreatedV2));

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.SourceVersion.Should().Be(1);
        capturedContext.TargetVersion.Should().Be(2);
        capturedContext.MigrationTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

public class EventVersionAttributeTests
{
    [Fact]
    public void Constructor_WithVersion_ShouldSetProperties()
    {
        // Act
        var attr = new EventVersionAttribute(5);

        // Assert
        attr.Version.Should().Be(5);
        attr.PreviousVersionType.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithPreviousType_ShouldSetProperties()
    {
        // Act
        var attr = new EventVersionAttribute(2, typeof(string));

        // Assert
        attr.Version.Should().Be(2);
        attr.PreviousVersionType.Should().Be(typeof(string));
    }

    [Fact]
    public void Constructor_WithInvalidVersion_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventVersionAttribute(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventVersionAttribute(-1));
    }

    [Fact]
    public void ChangeDescription_ShouldBeSettable()
    {
        // Arrange
        var attr = new EventVersionAttribute(3)
        {
            ChangeDescription = "Added new fields"
        };

        // Assert
        attr.ChangeDescription.Should().Be("Added new fields");
    }
}
