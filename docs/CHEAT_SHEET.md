# Orleans.StateMachineES Developer Cheat Sheet

## Quick Start Guide

### 1. Basic State Machine Grain

```csharp
public enum OrderState { Pending, Processing, Shipped, Delivered }
public enum OrderTrigger { Process, Ship, Deliver, Cancel }

[StorageProvider(ProviderName = "Default")]
public class OrderGrain : StateMachineGrain<OrderState, OrderTrigger>, IOrderGrain
{
    protected override StateMachine<OrderState, OrderTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<OrderState, OrderTrigger>(OrderState.Pending);
        
        machine.Configure(OrderState.Pending)
            .Permit(OrderTrigger.Process, OrderState.Processing)
            .Permit(OrderTrigger.Cancel, OrderState.Cancelled);
            
        machine.Configure(OrderState.Processing)
            .Permit(OrderTrigger.Ship, OrderState.Shipped);
            
        return machine;
    }
    
    public Task ProcessAsync() => FireAsync(OrderTrigger.Process);
    public Task ShipAsync() => FireAsync(OrderTrigger.Ship);
}
```

### 2. Event Sourcing Enabled

```csharp
[LogConsistencyProvider(ProviderName = "LogStorage")]
[StorageProvider(ProviderName = "Default")]
public class OrderGrain : EventSourcedStateMachineGrain<OrderState, OrderTrigger, OrderGrainState>, IOrderGrain
{
    protected override void ConfigureEventSourcing(EventSourcingOptions options)
    {
        options.AutoConfirmEvents = true;
        options.EnableSnapshots = true;
        options.SnapshotInterval = 100;
        options.EnableIdempotency = true;
    }
}

[GenerateSerializer]
public class OrderGrainState : EventSourcedStateMachineState<OrderState>
{
    [Id(0)] public string? CustomerId { get; set; }
    [Id(1)] public decimal Amount { get; set; }
}
```

### 3. Timer-Enabled States

```csharp
public class OrderGrain : TimerEnabledStateMachineGrain<OrderState, OrderTrigger, OrderGrainState>
{
    protected override void ConfigureTimeouts()
    {
        // Auto-cancel orders after 24 hours
        RegisterStateTimeout(OrderState.Pending,
            ConfigureTimeout(OrderState.Pending)
                .After(TimeSpan.FromHours(24))
                .TransitionTo(OrderTrigger.Cancel)
                .UseReminder()
                .WithName("OrderExpiry")
                .Build());
                
        // Process orders within 2 hours
        RegisterStateTimeout(OrderState.Processing,
            ConfigureTimeout(OrderState.Processing)
                .After(TimeSpan.FromHours(2))
                .TransitionTo(OrderTrigger.Ship)
                .UseTimer()
                .WithName("ProcessingTimeout")
                .Build());
    }
}
```

### 4. Hierarchical States

```csharp
public enum DeviceState { Offline, Online, Idle, Active, Processing, Monitoring }
public enum DeviceTrigger { PowerOn, PowerOff, StartProcessing, StartMonitoring, Stop }

public class DeviceGrain : HierarchicalStateMachineGrain<DeviceState, DeviceTrigger, DeviceGrainState>
{
    protected override StateMachine<DeviceState, DeviceTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<DeviceState, DeviceTrigger>(DeviceState.Offline);
        
        // Configure parent states
        machine.Configure(DeviceState.Online)
            .Permit(DeviceTrigger.PowerOff, DeviceState.Offline);
            
        // Configure substates
        machine.Configure(DeviceState.Idle)
            .SubstateOf(DeviceState.Online)
            .Permit(DeviceTrigger.StartProcessing, DeviceState.Processing);
            
        machine.Configure(DeviceState.Processing)
            .SubstateOf(DeviceState.Active)
            .Permit(DeviceTrigger.Stop, DeviceState.Idle);
            
        return machine;
    }
    
    protected override void ConfigureHierarchy()
    {
        DefineSubstate(DeviceState.Idle, DeviceState.Online);
        DefineSubstate(DeviceState.Active, DeviceState.Online);
        DefineSubstate(DeviceState.Processing, DeviceState.Active);
    }
    
    // Query hierarchy
    public async Task<bool> IsOnlineAsync() 
        => await IsInStateOrSubstateAsync(DeviceState.Online);
    
    public async Task<IReadOnlyList<DeviceState>> GetCurrentPathAsync() 
        => await GetCurrentStatePathAsync();
}
```

## Core Interfaces & Methods

### IStateMachineGrain<TState, TTrigger>
```csharp
// State queries
Task<TState> GetStateAsync();
Task<bool> IsInStateAsync(TState state);
Task<IEnumerable<TTrigger>> GetPermittedTriggersAsync();

// Trigger execution
Task FireAsync(TTrigger trigger);
Task FireAsync<TArg0>(TTrigger trigger, TArg0 arg0);
Task FireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1);

// Guard validation
Task<bool> CanFireAsync(TTrigger trigger);
Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync(TTrigger trigger);

// Lifecycle
Task ActivateAsync();
Task DeactivateAsync();
```

### Timer Configuration
```csharp
protected TimeoutConfiguration<TState, TTrigger> ConfigureTimeout(TState state)
{
    return new TimeoutConfiguration<TState, TTrigger>(state)
        .After(TimeSpan.FromMinutes(5))           // Duration
        .TransitionTo(OrderTrigger.Timeout)       // Target trigger
        .UseTimer()                               // or .UseReminder()
        .WithName("MyTimeout")                    // Optional name
        .WithMetadata(new { reason = "expired" }) // Optional metadata
        .Build();
}
```

### Event Sourcing Options
```csharp
protected override void ConfigureEventSourcing(EventSourcingOptions options)
{
    options.AutoConfirmEvents = true;      // Auto-confirm after transitions
    options.EnableSnapshots = true;       // Enable periodic snapshots
    options.SnapshotInterval = 100;       // Events between snapshots
    options.EnableIdempotency = true;     // Deduplicate triggers
    options.MaxDedupeKeysInMemory = 1000; // LRU cache size
    options.PublishToStream = true;       // Publish to Orleans Streams
    options.StreamProvider = "SMS";       // Stream provider name
    options.StreamNamespace = "Events";   // Stream namespace
}
```

### Hierarchical State Queries
```csharp
// Parent-child relationships
Task<TState?> GetParentStateAsync(TState state);
Task<IReadOnlyList<TState>> GetSubstatesAsync(TState parentState);
Task<IReadOnlyList<TState>> GetAncestorStatesAsync(TState state);
Task<IReadOnlyList<TState>> GetDescendantStatesAsync(TState parentState);

// Hierarchy navigation
Task<bool> IsInStateOrSubstateAsync(TState state);
Task<IReadOnlyList<TState>> GetCurrentStatePathAsync();
Task<TState?> GetActiveSubstateAsync(TState parentState);
Task<HierarchicalStateInfo<TState>> GetHierarchicalInfoAsync();
```

## Common Patterns

### Guards & Conditional Transitions
```csharp
machine.Configure(OrderState.Processing)
    .PermitIf(OrderTrigger.Ship, OrderState.Shipped, 
        () => HasInventory() && IsPaymentConfirmed())
    .OnEntry(() => Logger.LogInformation("Started processing order"));
```

### Parameterized Triggers
```csharp
public async Task ProcessWithPriorityAsync(int priority)
{
    await FireAsync(OrderTrigger.ProcessWithPriority, priority);
}

// In BuildStateMachine:
var priorityTrigger = machine.SetTriggerParameters<int>(OrderTrigger.ProcessWithPriority);
machine.Configure(OrderState.Pending)
    .Permit(priorityTrigger, OrderState.Processing);
```

### Custom State Classes
```csharp
[GenerateSerializer]
public class OrderGrainState : EventSourcedStateMachineState<OrderState>
{
    [Id(0)] public string CustomerId { get; set; } = "";
    [Id(1)] public List<OrderItem> Items { get; set; } = new();
    [Id(2)] public decimal Total { get; set; }
    [Id(3)] public DateTime OrderDate { get; set; }
}
```

### Stream Integration
```csharp
// Configure in silo
siloBuilder.AddStreams(StreamConfigurator.StreamProvider)
    .AddMemoryStreams("SMS");

// In grain
protected override void ConfigureEventSourcing(EventSourcingOptions options)
{
    options.PublishToStream = true;
    options.StreamProvider = "SMS";
    options.StreamNamespace = "OrderEvents";
}
```

## Testing Patterns

### Basic Testing
```csharp
[Fact]
public async Task Should_Process_Order_Successfully()
{
    var grain = _cluster.Client.GetGrain<IOrderGrain>("order-123");
    
    await grain.ProcessAsync();
    
    var state = await grain.GetStateAsync();
    state.Should().Be(OrderState.Processing);
}
```

### Timer Testing
```csharp
[Fact]
public async Task Should_Timeout_After_Configured_Duration()
{
    var grain = _cluster.Client.GetGrain<IOrderGrain>("order-timeout");
    
    await grain.ProcessAsync();
    await Task.Delay(TimeSpan.FromSeconds(3)); // Wait for timeout
    
    var state = await grain.GetStateAsync();
    state.Should().Be(OrderState.Cancelled);
}
```

### Hierarchical Testing
```csharp
[Fact]
public async Task Should_Navigate_Hierarchy_Correctly()
{
    var grain = _cluster.Client.GetGrain<IDeviceGrain>("device-123");
    
    await grain.PowerOnAsync();
    await grain.StartProcessingAsync();
    
    (await grain.IsInStateOrSubstateAsync(DeviceState.Online)).Should().BeTrue();
    (await grain.IsInStateAsync(DeviceState.Processing)).Should().BeTrue();
    
    var path = await grain.GetCurrentStatePathAsync();
    path.Should().ContainInOrder(DeviceState.Online, DeviceState.Active, DeviceState.Processing);
}
```

## Best Practices

### 1. State Machine Design
- Keep states focused and meaningful
- Use hierarchical states for related behaviors
- Design for testability with clear state transitions

### 2. Performance
- Use timers for short durations (< 5 minutes)
- Use reminders for long durations (> 5 minutes)
- Enable snapshots for high-frequency state machines
- Configure appropriate dedupe key limits

### 3. Error Handling
```csharp
try
{
    await FireAsync(OrderTrigger.Process);
}
catch (InvalidStateTransitionException ex)
{
    Logger.LogWarning("Invalid transition: {Message}", ex.Message);
    // Handle invalid transition gracefully
}
```

### 4. Orleans Configuration
```csharp
// In Program.cs
siloBuilder
    .AddMemoryGrainStorage("Default")
    .AddLogStorageBasedLogConsistencyProvider("LogStorage")
    .AddMemoryGrainStorage("PubSubStore")
    .AddStreams("SMS")
    .AddMemoryStreams("SMS");
```

### 5. Monitoring & Observability
```csharp
protected override async Task RecordTransitionEvent(/*...*/)
{
    // Add custom telemetry
    using var activity = ActivitySource.StartActivity("StateMachine.Transition");
    activity?.SetTag("grain.id", GetPrimaryKeyString());
    activity?.SetTag("from.state", fromState.ToString());
    activity?.SetTag("to.state", toState.ToString());
    
    await base.RecordTransitionEvent(/*...*/);
}
```

## Troubleshooting

### Common Issues
1. **InvalidStateTransitionException**: Check permitted triggers with `GetPermittedTriggersAsync()`
2. **Timer not firing**: Verify reminder/timer registration and Orleans configuration
3. **Events not persisting**: Check log consistency provider configuration
4. **Hierarchy not working**: Ensure both `SubstateOf()` and `DefineSubstate()` are called

### Debug Commands
```csharp
// Check current state and permitted triggers
var state = await grain.GetStateAsync();
var triggers = await grain.GetPermittedTriggersAsync();
var info = await grain.GetInfoAsync();

// For hierarchical grains
var hierarchy = await grain.GetHierarchicalInfoAsync();
var path = await grain.GetCurrentStatePathAsync();
```

This cheat sheet covers all major features of Orleans.StateMachineES. For detailed examples, see the test projects and documentation.