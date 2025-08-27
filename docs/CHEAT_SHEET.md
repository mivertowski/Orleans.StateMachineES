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

### 5. Source-Generated State Machines (Roslyn Generator)

#### YAML Specification (`SmartLight.statemachine.yaml`)
```yaml
name: SmartLight
namespace: SmartHome.Devices
states: [Off, On, Dimmed, ColorMode, NightMode]
triggers: [TurnOn, TurnOff, Dim, SetColor, ActivateNightMode]
initialState: Off
transitions:
  - { from: Off, to: On, trigger: TurnOn }
  - { from: On, to: Dimmed, trigger: Dim }
  - { from: On, to: ColorMode, trigger: SetColor }
```

#### JSON Specification (`Thermostat.statemachine.json`)
```json
{
  "name": "Thermostat",
  "namespace": "SmartHome.Climate",
  "states": ["Idle", "Heating", "Cooling", "Auto"],
  "triggers": ["Heat", "Cool", "AutoMode", "Stop"],
  "transitions": [
    { "from": "Idle", "to": "Heating", "trigger": "Heat" },
    { "from": "Heating", "to": "Idle", "trigger": "Stop" }
  ]
}
```

#### Generated Code Usage
```csharp
// Auto-generated interfaces and implementations
ISmartLightGrain light = grainFactory.GetGrain<ISmartLightGrain>("living-room");
IThermostatGrain thermostat = grainFactory.GetGrain<IThermostatGrain>("main");

// Strongly-typed methods
await light.FireTurnOnAsync();
await light.FireDimAsync();
bool isOn = await light.IsOnAsync();

// Generated extension methods
SmartLightState.Off.IsTerminal();
SmartLightTrigger.TurnOn.GetDescription();
```

#### Project Configuration
```xml
<ItemGroup>
  <AdditionalFiles Include="**\*.statemachine.yaml" />
  <AdditionalFiles Include="**\*.statemachine.json" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="Orleans.StateMachineES.Generators" />
</ItemGroup>
```

### 6. Orthogonal Regions (Parallel State Machines)

```csharp
public class SmartHomeSystemGrain : OrthogonalStateMachineGrain<SmartHomeState, SmartHomeTrigger>
{
    protected override void ConfigureOrthogonalRegions()
    {
        // Define independent regions
        DefineOrthogonalRegion("Security", SmartHomeState.SecurityDisarmed, machine =>
        {
            machine.Configure(SmartHomeState.SecurityDisarmed)
                .Permit(SmartHomeTrigger.ArmHome, SmartHomeState.SecurityArmedHome)
                .Permit(SmartHomeTrigger.ArmAway, SmartHomeState.SecurityArmedAway);
                
            machine.Configure(SmartHomeState.SecurityAlarm)
                .OnEntry(() => _logger.LogWarning("ALARM TRIGGERED!"));
        });
        
        DefineOrthogonalRegion("Climate", SmartHomeState.ClimateOff, machine =>
        {
            machine.Configure(SmartHomeState.ClimateOff)
                .Permit(SmartHomeTrigger.StartHeating, SmartHomeState.ClimateHeating)
                .Permit(SmartHomeTrigger.StartCooling, SmartHomeState.ClimateCooling);
        });
        
        DefineOrthogonalRegion("Energy", SmartHomeState.EnergyNormal, machine =>
        {
            machine.Configure(SmartHomeState.EnergyNormal)
                .Permit(SmartHomeTrigger.EnterPeakDemand, SmartHomeState.EnergyPeakDemand)
                .Permit(SmartHomeTrigger.EnableSaving, SmartHomeState.EnergySaving);
        });
        
        // Map triggers to regions
        MapTriggerToRegions(SmartHomeTrigger.VacationMode, "Security", "Climate", "Energy");
    }
    
    // Cross-region synchronization
    protected override async Task OnRegionStateChangedAsync(
        string regionName, SmartHomeState prev, SmartHomeState next, SmartHomeTrigger trigger)
    {
        if (regionName == "Presence" && next == SmartHomeState.PresenceAway)
        {
            // Auto-adjust when leaving home
            await FireInRegionAsync("Security", SmartHomeTrigger.ArmAway);
            await FireInRegionAsync("Climate", SmartHomeTrigger.SetEco);
            await FireInRegionAsync("Energy", SmartHomeTrigger.EnableSaving);
        }
    }
    
    // Usage
    public async Task ActivateVacationModeAsync()
    {
        await FireInRegionAsync("Presence", SmartHomeTrigger.StartVacation);
        await FireInRegionAsync("Security", SmartHomeTrigger.ArmAway);
        await FireInRegionAsync("Climate", SmartHomeTrigger.SetEco);
        await FireInRegionAsync("Energy", SmartHomeTrigger.EnableSaving);
    }
}

// Client usage
var smartHome = grainFactory.GetGrain<ISmartHomeSystemGrain>("my-home");
await smartHome.FireInRegionAsync("Security", SmartHomeTrigger.ArmHome);
await smartHome.FireInRegionAsync("Climate", SmartHomeTrigger.StartHeating);

var status = await smartHome.GetStateSummary();
Console.WriteLine($"Security: {status.RegionStates["Security"]}");
Console.WriteLine($"Climate: {status.RegionStates["Climate"]}");
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

🚀 **PERFORMANCE BREAKTHROUGH: Event sourcing is 30.4% FASTER than regular state machines!**
- Event-sourced: **5,923 transitions/sec** (0.17ms latency)  
- Regular: 4,123 transitions/sec (0.24ms latency)

```csharp
protected override void ConfigureEventSourcing(EventSourcingOptions options)
{   
    options.AutoConfirmEvents = true;      // Essential for optimal performance
    
    // Performance optimizations
    options.EnableSnapshots = true;       // Enable periodic snapshots
    options.SnapshotInterval = 100;       // Events between snapshots
    options.EnableIdempotency = true;     // Deduplicate triggers
    options.MaxDedupeKeysInMemory = 1000; // LRU cache size
    
    // Optional stream publishing
    options.PublishToStream = true;       // Publish to Orleans Streams
    options.StreamProvider = "SMS";       // Stream provider name
    options.StreamNamespace = "Events";   // Stream namespace
}
```

⚠️ **CRITICAL:** `AutoConfirmEvents = true` is **essential** for:
- Maximum performance
- Proper state recovery after grain deactivation
- Reliable event persistence

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

### 5. Distributed Sagas

```csharp
using Orleans.StateMachineES.Sagas;

public class InvoiceProcessingSaga : SagaOrchestratorGrain<InvoiceData>, IInvoiceProcessingSagaGrain
{
    protected override void ConfigureSagaSteps()
    {
        AddStep(new PostInvoiceStep())
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithRetry(3)
            .WithMetadata("Description", "Posts invoice to accounting system");

        AddStep(new CreateJournalEntryStep())
            .WithTimeout(TimeSpan.FromSeconds(45))
            .WithRetry(2)
            .WithMetadata("Description", "Creates journal entries");

        AddStep(new RunControlCheckStep())
            .WithTimeout(TimeSpan.FromSeconds(60))
            .WithRetry(1)
            .WithMetadata("Description", "Runs compliance control checks");
    }

    protected override string GenerateBusinessTransactionId(InvoiceData sagaData)
    {
        return $"INV-TXN-{sagaData.InvoiceId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }
}

// Example saga step implementation
public class PostInvoiceStep : ISagaStep<InvoiceData>
{
    public string StepName => "PostInvoice";
    public TimeSpan Timeout => TimeSpan.FromSeconds(30);
    public bool CanRetry => true;
    public int MaxRetryAttempts => 3;

    public async Task<SagaStepResult> ExecuteAsync(InvoiceData sagaData, SagaContext context)
    {
        try
        {
            var invoiceGrain = GrainFactory.GetGrain<IInvoiceGrain>(sagaData.InvoiceId);
            var result = await invoiceGrain.PostAsync(sagaData, context.CorrelationId);
            
            return SagaStepResult.Success(result);
        }
        catch (BusinessRuleException ex)
        {
            return SagaStepResult.BusinessFailure(ex.Message);
        }
        catch (Exception ex)
        {
            return SagaStepResult.TechnicalFailure(ex.Message, ex);
        }
    }

    public async Task<CompensationResult> CompensateAsync(
        InvoiceData sagaData, 
        SagaStepResult? stepResult, 
        SagaContext context)
    {
        try
        {
            var invoiceGrain = GrainFactory.GetGrain<IInvoiceGrain>(sagaData.InvoiceId);
            await invoiceGrain.CancelAsync(context.CorrelationId);
            
            return CompensationResult.Success();
        }
        catch (Exception ex)
        {
            return CompensationResult.Failure($"Failed to compensate invoice: {ex.Message}", ex);
        }
    }
}
```

#### Usage Examples

```csharp
// Execute saga
var sagaGrain = grainFactory.GetGrain<IInvoiceProcessingSagaGrain>("saga-123");
var correlationId = Guid.NewGuid().ToString("N");

var invoiceData = new InvoiceData
{
    InvoiceId = "INV-001",
    CustomerId = "CUST-123",
    Amount = 1500.00m
};

var result = await sagaGrain.ExecuteAsync(invoiceData, correlationId);

if (result.IsSuccess)
{
    Console.WriteLine("Saga completed successfully");
}
else if (result.IsCompensated)
{
    Console.WriteLine("Saga failed but was compensated successfully");
}

// Check saga status
var status = await sagaGrain.GetStatusAsync();
Console.WriteLine($"Saga status: {status.Status}");
Console.WriteLine($"Current step: {status.CurrentStepName}");
Console.WriteLine($"Progress: {status.CurrentStepIndex + 1}/{status.TotalSteps}");

// Get detailed execution history
var history = await sagaGrain.GetHistoryAsync();
foreach (var step in history.StepExecutions)
{
    Console.WriteLine($"Step {step.StepName}: {step.IsSuccess} ({step.Duration.TotalMilliseconds}ms)");
}
```

#### Saga Features

- **Orchestration Pattern**: Central coordinator manages business process flow
- **Automatic Compensation**: Failed steps trigger rollback of completed steps in reverse order
- **Retry Logic**: Configurable retry attempts with exponential backoff for technical failures
- **Correlation Tracking**: Full correlation ID propagation across all distributed operations
- **Event Sourcing Integration**: Complete audit trail of saga execution and compensation
- **Timeout Handling**: Per-step timeouts with graceful failure handling
- **Hierarchical State Management**: Extends hierarchical state machine capabilities
- **Business vs Technical Errors**: Different handling strategies for different error types

### 6. State Machine Versioning

```csharp
using Orleans.StateMachineES.Versioning;

public class VersionedOrderGrain : 
    VersionedStateMachineGrain<OrderState, OrderTrigger, VersionedOrderState>,
    IVersionedOrderGrain
{
    protected override async Task RegisterBuiltInVersionsAsync()
    {
        if (DefinitionRegistry != null)
        {
            // Register version 1.0.0
            await DefinitionRegistry.RegisterDefinitionAsync<OrderState, OrderTrigger>(
                GetType().Name,
                new StateMachineVersion(1, 0, 0),
                () => BuildOrderWorkflowV1(),
                new StateMachineDefinitionMetadata
                {
                    Description = "Initial order workflow",
                    Features = { "Basic order processing" }
                });

            // Register version 1.1.0 (backward compatible)
            await DefinitionRegistry.RegisterDefinitionAsync<OrderState, OrderTrigger>(
                GetType().Name,
                new StateMachineVersion(1, 1, 0),
                () => BuildOrderWorkflowV11(),
                new StateMachineDefinitionMetadata
                {
                    Description = "Enhanced order workflow",
                    Features = { "Enhanced validation", "Better error handling" }
                });

            // Register version 2.0.0 (breaking changes)
            await DefinitionRegistry.RegisterDefinitionAsync<OrderState, OrderTrigger>(
                GetType().Name,
                new StateMachineVersion(2, 0, 0),
                () => BuildOrderWorkflowV2(),
                new StateMachineDefinitionMetadata
                {
                    Description = "Major refactor",
                    Features = { "New approval workflow", "Multi-step processing" },
                    BreakingChanges = { "Added approval states", "Changed validation rules" }
                });
        }
    }

    protected override Task<StateMachine<OrderState, OrderTrigger>?> BuildVersionedStateMachineAsync(
        StateMachineVersion version)
    {
        return Task.FromResult(version switch
        {
            { Major: 1, Minor: 0, Patch: 0 } => BuildOrderWorkflowV1(),
            { Major: 1, Minor: 1, Patch: 0 } => BuildOrderWorkflowV11(),
            { Major: 2, Minor: 0, Patch: 0 } => BuildOrderWorkflowV2(),
            _ => (StateMachine<OrderState, OrderTrigger>?)null
        });
    }
}
```

#### Usage Examples

```csharp
// Check current version
var grain = grainFactory.GetGrain<IVersionedOrderGrain>("order-123");
var version = await grain.GetVersionAsync();
Console.WriteLine($"Current version: {version}");

// Get version compatibility info
var compatibility = await grain.GetVersionCompatibilityAsync();
Console.WriteLine($"Available versions: {string.Join(", ", compatibility.AvailableVersions)}");

// Upgrade to new version
var upgradeResult = await grain.UpgradeToVersionAsync(
    new StateMachineVersion(1, 1, 0), 
    MigrationStrategy.Automatic);

if (upgradeResult.IsSuccess)
{
    Console.WriteLine($"Upgraded to {upgradeResult.NewVersion} in {upgradeResult.UpgradeDuration.TotalMilliseconds}ms");
}

// Shadow evaluation - test without committing
var shadowResult = await grain.RunShadowEvaluationAsync(
    new StateMachineVersion(2, 0, 0), 
    OrderTrigger.Submit);

if (shadowResult.WouldSucceed)
{
    Console.WriteLine($"Shadow: {shadowResult.CurrentState} -> {shadowResult.PredictedState}");
    // Safe to upgrade
}

// Blue-green deployment
var blueGreenResult = await grain.UpgradeToVersionAsync(
    new StateMachineVersion(2, 0, 0), 
    MigrationStrategy.BlueGreen);

// Custom migration with hooks
var customResult = await grain.UpgradeToVersionAsync(
    new StateMachineVersion(2, 0, 0), 
    MigrationStrategy.Custom);
```

#### Migration Hooks

```csharp
public class CustomMigrationHook : IMigrationHook
{
    public string HookName => "CustomDataMigration";
    public int Priority => 50;

    public async Task<bool> BeforeMigrationAsync(MigrationContext context)
    {
        // Pre-migration validation and data transformation
        if (context.ToVersion.Major > context.FromVersion.Major)
        {
            // Handle breaking changes
            var data = context.GetStateValue<OrderData>("OrderData");
            if (data != null)
            {
                var transformed = TransformForNewVersion(data);
                context.SetStateValue("OrderData", transformed);
            }
        }
        return true;
    }

    public async Task AfterMigrationAsync(MigrationContext context)
    {
        // Post-migration verification
        Console.WriteLine($"Migration completed for {context.GrainId}");
    }

    public async Task OnMigrationRollbackAsync(MigrationContext context, Exception error)
    {
        // Rollback custom changes
        Console.WriteLine($"Rolling back migration: {error.Message}");
    }
}
```

#### Version Compatibility Checking

```csharp
var checker = serviceProvider.GetRequiredService<IVersionCompatibilityChecker>();

// Check upgrade compatibility
var result = await checker.CheckCompatibilityAsync(
    "OrderGrain",
    new StateMachineVersion(1, 0, 0),
    new StateMachineVersion(2, 0, 0));

if (!result.IsCompatible)
{
    foreach (var change in result.BreakingChanges)
    {
        Console.WriteLine($"Breaking change: {change.Description}");
        Console.WriteLine($"Impact: {change.Impact}, Mitigation: {change.Mitigation}");
    }
}

// Get upgrade recommendations
var recommendations = await checker.GetUpgradeRecommendationsAsync(
    "OrderGrain", 
    new StateMachineVersion(1, 0, 0));

foreach (var rec in recommendations)
{
    Console.WriteLine($"Upgrade to {rec.ToVersion}: {rec.RecommendationType}");
    Console.WriteLine($"Risk: {rec.RiskLevel}, Effort: {rec.EstimatedEffort}");
}
```

#### Versioning Features

- **Semantic Versioning**: Full major.minor.patch version support with pre-release and build metadata
- **Backward Compatibility**: Automatic compatibility checking for minor version upgrades
- **Breaking Change Detection**: Identifies and documents breaking changes in major versions
- **Migration Strategies**: Automatic, custom, blue-green, and dry-run migration options
- **Shadow Evaluation**: Test new versions without affecting live state
- **Migration Hooks**: Extensible system for custom migration logic with priorities
- **Rollback Support**: Automatic state backup and rollback on migration failure
- **Deployment Validation**: Check compatibility with existing deployed versions
- **Audit Trail**: Complete history of version upgrades and migrations

## Best Practices

### 1. State Machine Design
- Keep states focused and meaningful
- Use hierarchical states for related behaviors
- Design for testability with clear state transitions
- Enable nullable reference types (`<Nullable>enable</Nullable>`) for better null safety

### 2. Performance
- Use timers for short durations (< 5 minutes)
- Use reminders for long durations (> 5 minutes)
- Enable snapshots for high-frequency state machines
- Configure appropriate dedupe key limits
- Optimize async methods by removing unnecessary `async` keywords where no await is needed

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

### 5. Build Configuration
```xml
<!-- Recommended project settings for Orleans.StateMachineES -->
<PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
</PropertyGroup>

<!-- Package references with verified compatibility -->
<ItemGroup>
    <PackageReference Include="Orleans.StateMachineES" Version="1.0.0" />
    <PackageReference Include="Microsoft.Orleans.Sdk" Version="9.1.2" />
    <!-- For testing -->
    <PackageReference Include="xunit" Version="2.8.0" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
</ItemGroup>
```

### 6. Monitoring & Observability
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

## Complete Example Applications

The `examples/` directory contains four applications:

1. **ECommerceWorkflow** - Order processing with event sourcing, timers, and monitoring
2. **DocumentApproval** - Hierarchical states with saga orchestration
3. **MonitoringDashboard** - Health checks, metrics, and visualization
4. **SmartHome** - Source generator and orthogonal regions demonstration

### SmartHome Example Highlights

The SmartHome example demonstrates the newest features:
- State machines generated from YAML/JSON specifications
- Orthogonal regions with 4 independent subsystems (Security, Climate, Energy, Presence)
- Cross-region synchronization and reactions
- Integration between generated device grains and orthogonal system grain

See [examples/README.md](../examples/README.md) for complete documentation and usage instructions.

This cheat sheet covers all major features of Orleans.StateMachineES. For detailed examples, see the test projects and documentation.