# Orleans.StateMachineES

[![NuGet](https://img.shields.io/nuget/v/Orleans.StateMachineES.svg)](https://www.nuget.org/packages/Orleans.StateMachineES/)
[![.NET](https://github.com/mivertowski/Orleans.StateMachineES/actions/workflows/dotnet.yml/badge.svg)](https://github.com/mivertowski/Orleans.StateMachineES/actions/workflows/dotnet.yml)
[![CodeQL](https://github.com/mivertowski/Orleans.StateMachineES/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/mivertowski/Orleans.StateMachineES/actions/workflows/codeql-analysis.yml)
[![License](https://img.shields.io/github/license/mivertowski/Orleans.StateMachineES)](LICENSE)

> **Fork Notice**: This is an enhanced fork of the original [ManagedCode.Orleans.StateMachine](https://github.com/managedcode/Orleans.StateMachine) library by the ManagedCode team.

A powerful integration of the [Stateless](https://github.com/dotnet-state-machine/stateless) state machine library with [Microsoft Orleans](https://github.com/dotnet/orleans), now enhanced with **event sourcing capabilities** and advanced distributed state machine features.

## Fork Intention

This fork extends the original ManagedCode.Orleans.StateMachine library with enterprise-grade event sourcing and advanced distributed state machine features. The goal is to provide a comprehensive solution for building event-driven, distributed state machines in Orleans with full audit trails, replay capabilities, and production-ready features.

### New Features in this Fork

- 📚 **Event Sourcing** - First-class event sourcing with Orleans JournaledGrain
- 🔁 **Event Replay** - Automatic state reconstruction from event history
- 🎯 **Idempotency** - Built-in deduplication with LRU cache
- 📷 **Snapshots** - Configurable snapshot intervals for performance
- 🔄 **Correlation Tracking** - Track related events across distributed systems
- 🌊 **Orleans Streams Integration** - Publish state transitions to streams
- ⏰ **Timers & Reminders** - State-driven timeouts with Orleans timers and reminders
- 🔄 **Repeating Actions** - Support for repeating timers with heartbeat patterns
- 🏗️ **Hierarchical States** - Support for nested states with parent-child relationships  
- 🎭 **Distributed Sagas** - Multi-grain workflows with compensation and correlation tracking
- 🔍 **Distributed Tracing** - OpenTelemetry integration with activity sources and metrics
- 📊 **State Machine Visualization** - Interactive diagrams and analysis tools (DOT, Mermaid, PlantUML, HTML)
- 📈 **Advanced Monitoring** - Real-time metrics, health checks, and monitoring endpoints
- 🔄 **State Machine Versioning** - Seamless versioning with migration support
- 🧩 **State Machine Composition** - Build complex state machines from reusable components
- 🏥 **Health Checks** - ASP.NET Core health check integration with custom providers
- 🎨 **Visualization Batch Service** - Analyze and visualize multiple state machines simultaneously
- 🔧 **Component Inheritance** - Create reusable state machine components (Validation, Retry, Approval)
- 🏗️ **Enterprise-Grade** - Production-ready with comprehensive error handling

### Original Features

- 🎯 **Seamless Orleans Integration** - State machines as Orleans grains with full Orleans lifecycle support
- 🔄 **Async-First API** - All operations are async-compatible for Orleans' single-threaded execution model
- 💪 **Strongly Typed** - Generic state and trigger types with compile-time safety
- 🛡️ **Guard Conditions** - Support for guard clauses with detailed validation feedback
- 📊 **Rich State Inspection** - Query permitted triggers, state info, and transition paths
- 🎭 **Parameterized Triggers** - Pass up to 3 typed arguments with triggers
- 🔍 **Comprehensive Metadata** - Export state machine structure and configuration
- ⚡ **Orleans Context Extensions** - Special async extensions for Orleans grain context

## 📦 Installation

```bash
dotnet add package Orleans.StateMachineES
```

### NuGet Package
```xml
<PackageReference Include="Orleans.StateMachineES" Version="1.0.0" />
```

## Quick Start

### Standard State Machine (Original Functionality)

#### 1. Define Your States and Triggers

```csharp
public enum DoorState
{
    Open,
    Closed,
    Locked
}

public enum DoorTrigger
{
    OpenDoor,
    CloseDoor,
    LockDoor,
    UnlockDoor
}
```

#### 2. Create Your State Machine Grain

```csharp
public interface IDoorGrain : IGrainWithStringKey
{
    Task<DoorState> GetCurrentStateAsync();
    Task OpenAsync();
    Task CloseAsync();
    Task LockAsync(string lockCode);
    Task UnlockAsync(string lockCode);
}

public class DoorGrain : StateMachineGrain<DoorState, DoorTrigger>, IDoorGrain
{
    private string? _lockCode;

    protected override StateMachine<DoorState, DoorTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<DoorState, DoorTrigger>(DoorState.Closed);

        machine.Configure(DoorState.Open)
            .Permit(DoorTrigger.CloseDoor, DoorState.Closed);

        machine.Configure(DoorState.Closed)
            .Permit(DoorTrigger.OpenDoor, DoorState.Open)
            .Permit(DoorTrigger.LockDoor, DoorState.Locked);

        machine.Configure(DoorState.Locked)
            .PermitIf(DoorTrigger.UnlockDoor, DoorState.Closed, 
                () => !string.IsNullOrEmpty(_lockCode))
            .OnEntryFrom(DoorTrigger.LockDoor, (string code) => _lockCode = code);

        return machine;
    }

    public Task<DoorState> GetCurrentStateAsync() => GetStateAsync();
    
    public Task OpenAsync() => FireAsync(DoorTrigger.OpenDoor);
    
    public Task CloseAsync() => FireAsync(DoorTrigger.CloseDoor);
    
    public Task LockAsync(string lockCode) => FireAsync(DoorTrigger.LockDoor, lockCode);
    
    public Task UnlockAsync(string lockCode)
    {
        if (lockCode == _lockCode)
        {
            _lockCode = null;
            return FireAsync(DoorTrigger.UnlockDoor);
        }
        throw new InvalidOperationException("Invalid lock code");
    }
}
```

#### 3. Use Your State Machine Grain

```csharp
// In your Orleans client or another grain
var doorGrain = grainFactory.GetGrain<IDoorGrain>("front-door");

// Check current state
var state = await doorGrain.GetCurrentStateAsync();
Console.WriteLine($"Door is {state}");

// Open the door
await doorGrain.OpenAsync();

// Close and lock with a code
await doorGrain.CloseAsync();
await doorGrain.LockAsync("secret-code");

// Try to unlock
await doorGrain.UnlockAsync("secret-code");
```

### Event-Sourced State Machine (New in this Fork)

#### 1. Create an Event-Sourced State Machine Grain

```csharp
using Orleans.StateMachineES.EventSourcing;

public class EventSourcedDoorGrain : EventSourcedStateMachineGrain<DoorState, DoorTrigger, DoorGrainState>, IDoorGrain
{
    private string? _lockCode;

    protected override StateMachine<DoorState, DoorTrigger> BuildStateMachine()
    {
        // Same configuration as before
        var machine = new StateMachine<DoorState, DoorTrigger>(DoorState.Closed);
        // ... configure states ...
        return machine;
    }

    protected override void ConfigureEventSourcing(EventSourcingOptions options)
    {
        options.AutoConfirmEvents = true;
        options.EnableIdempotency = true;
        options.EnableSnapshots = true;
        options.SnapshotInterval = 100;
        options.PublishToStream = true;
        options.StreamProvider = "SMS";
    }

    // Implementation methods...
}
```

### Timer-Enabled State Machine (Phase 3)

#### 1. Create a State Machine with Timers and Reminders

```csharp
using Orleans.StateMachineES.Timers;

public enum ProcessingState { Idle, Processing, Monitoring, Completed, TimedOut, Failed }
public enum ProcessingTrigger { Start, Complete, Timeout, Heartbeat, Cancel }

public class TimerProcessingGrain : TimerEnabledStateMachineGrain<ProcessingState, ProcessingTrigger, ProcessingGrainState>, IProcessingGrain
{
    protected override StateMachine<ProcessingState, ProcessingTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<ProcessingState, ProcessingTrigger>(ProcessingState.Idle);

        machine.Configure(ProcessingState.Processing)
            .Permit(ProcessingTrigger.Complete, ProcessingState.Completed)
            .Permit(ProcessingTrigger.Timeout, ProcessingState.TimedOut);

        machine.Configure(ProcessingState.Monitoring)
            .Permit(ProcessingTrigger.Cancel, ProcessingState.Idle)
            .Ignore(ProcessingTrigger.Heartbeat); // For repeating heartbeats

        return machine;
    }

    protected override void ConfigureTimeouts()
    {
        // Short timeout with Orleans Timer
        RegisterStateTimeout(ProcessingState.Processing,
            ConfigureTimeout(ProcessingState.Processing)
                .After(TimeSpan.FromSeconds(30))
                .TransitionTo(ProcessingTrigger.Timeout)
                .UseTimer()
                .WithName("ProcessingTimeout")
                .Build());

        // Repeating heartbeat timer
        RegisterStateTimeout(ProcessingState.Monitoring,
            ConfigureTimeout(ProcessingState.Monitoring)
                .After(TimeSpan.FromSeconds(10))
                .TransitionTo(ProcessingTrigger.Heartbeat)
                .UseTimer()
                .Repeat()
                .WithName("MonitoringHeartbeat")
                .Build());

        // Long-running timeout with Orleans Reminder (durable)
        RegisterStateTimeout(ProcessingState.LongRunning,
            ConfigureTimeout(ProcessingState.LongRunning)
                .After(TimeSpan.FromHours(2))
                .TransitionTo(ProcessingTrigger.Timeout)
                .UseDurableReminder() // Survives grain deactivation
                .WithName("LongRunningTimeout")
                .Build());
    }
}
```

#### 2. Configure Orleans Silo for Timers and Event Sourcing

```csharp
siloBuilder
    .AddLogStorageBasedLogConsistencyProvider()
    .AddStateStorageBasedLogConsistencyProvider()
    .AddMemoryGrainStorage("EventStore")
    .AddMemoryStreams("SMS")
    .UseInMemoryReminderService(); // For durable reminders
```

#### 3. Timer Features

- **Automatic Management** - Timers start/stop automatically on state transitions
- **Orleans Timers** - Fast, in-memory timers for short durations (< 5 minutes)
- **Orleans Reminders** - Durable, persistent reminders for long durations (> 5 minutes)
- **Repeating Timers** - Support for periodic actions like heartbeats
- **Fluent Configuration** - Intuitive API for timeout setup
- **Event Sourcing Integration** - Timer events are recorded in event history

#### 4. Benefits of Event Sourcing

- **Complete Audit Trail** - Every state transition is recorded as an event
- **Time Travel** - Replay events to reconstruct state at any point
- **Debugging** - See exactly what happened and when
- **Event Streaming** - Publish transitions to Orleans Streams for real-time processing
- **Idempotency** - Automatic deduplication of duplicate commands

### Hierarchical State Machine (Phase 4)

#### 1. Create a State Machine with Nested States

```csharp
using Orleans.StateMachineES.Hierarchical;

public enum DeviceState { Offline, Online, Idle, Active, Processing, Monitoring }
public enum DeviceTrigger { PowerOn, PowerOff, StartProcessing, StartMonitoring, Stop, Timeout }

public class DeviceControllerGrain : HierarchicalStateMachineGrain<DeviceState, DeviceTrigger, DeviceGrainState>, IDeviceGrain
{
    protected override StateMachine<DeviceState, DeviceTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<DeviceState, DeviceTrigger>(DeviceState.Offline);

        // Root level states
        machine.Configure(DeviceState.Offline)
            .Permit(DeviceTrigger.PowerOn, DeviceState.Idle);

        // Online is a parent state
        machine.Configure(DeviceState.Online)
            .Permit(DeviceTrigger.PowerOff, DeviceState.Offline);

        // Idle is a substate of Online
        machine.Configure(DeviceState.Idle)
            .SubstateOf(DeviceState.Online)
            .Permit(DeviceTrigger.StartProcessing, DeviceState.Processing);

        // Active is a substate of Online, parent to Processing and Monitoring
        machine.Configure(DeviceState.Active)
            .SubstateOf(DeviceState.Online)
            .Permit(DeviceTrigger.Stop, DeviceState.Idle);

        // Processing is a substate of Active
        machine.Configure(DeviceState.Processing)
            .SubstateOf(DeviceState.Active)
            .Permit(DeviceTrigger.StartMonitoring, DeviceState.Monitoring)
            .Permit(DeviceTrigger.Stop, DeviceState.Idle);

        return machine;
    }

    protected override void ConfigureHierarchy()
    {
        // Define explicit hierarchical relationships for queries
        DefineSubstate(DeviceState.Idle, DeviceState.Online);
        DefineSubstate(DeviceState.Active, DeviceState.Online);
        DefineSubstate(DeviceState.Processing, DeviceState.Active);
        DefineSubstate(DeviceState.Monitoring, DeviceState.Active);
    }

    // Interface methods
    public Task PowerOnAsync() => FireAsync(DeviceTrigger.PowerOn);
    public Task StartProcessingAsync() => FireAsync(DeviceTrigger.StartProcessing);
    
    // Hierarchical queries
    public Task<bool> IsOnlineAsync() => IsInStateOrSubstateAsync(DeviceState.Online);
    public Task<IReadOnlyList<DeviceState>> GetCurrentPathAsync() => GetCurrentStatePathAsync();
}
```

#### 2. Hierarchical State Features

- **Parent-Child Relationships** - States can have substates that inherit behaviors
- **State Inheritance** - Being in a substate means you're also in the parent state
- **Hierarchy Queries** - Query ancestors, descendants, and state paths
- **Event Sourcing Integration** - Hierarchical transitions are recorded with full context
- **Timer Inheritance** - Substates can inherit timeout behaviors from parent states

```csharp
// Usage examples
var device = grainFactory.GetGrain<IDeviceGrain>("device-1");

await device.PowerOnAsync();        // -> Idle (substate of Online)
await device.StartProcessingAsync(); // -> Processing (substate of Active, which is substate of Online)

// Hierarchical checks
var isOnline = await device.IsOnlineAsync(); // true (Processing is a descendant of Online)
var currentPath = await device.GetCurrentPathAsync(); // [Online, Active, Processing]

// Query hierarchy
var parent = await device.GetParentStateAsync(DeviceState.Processing); // Active
var ancestors = await device.GetAncestorStatesAsync(DeviceState.Processing); // [Active, Online]
var descendants = await device.GetDescendantStatesAsync(DeviceState.Online); // [Idle, Active, Processing, Monitoring]
```

### State Machine Versioning (Phase 6)

#### 1. Create a Versioned State Machine

```csharp
using Orleans.StateMachineES.Versioning;

public class OrderProcessorGrain : 
    VersionedStateMachineGrain<OrderState, OrderTrigger, OrderProcessorState>,
    IOrderProcessorGrain
{
    protected override async Task RegisterBuiltInVersionsAsync()
    {
        if (DefinitionRegistry != null)
        {
            // Register version 1.0.0 - Initial implementation
            await DefinitionRegistry.RegisterDefinitionAsync<OrderState, OrderTrigger>(
                GetType().Name,
                new StateMachineVersion(1, 0, 0),
                () => BuildOrderProcessorV1(),
                new StateMachineDefinitionMetadata
                {
                    Description = "Initial order processing implementation",
                    Features = { "Basic order workflow", "Payment processing" }
                });

            // Register version 1.1.0 - Enhanced with validation
            await DefinitionRegistry.RegisterDefinitionAsync<OrderState, OrderTrigger>(
                GetType().Name,
                new StateMachineVersion(1, 1, 0),
                () => BuildOrderProcessorV11(),
                new StateMachineDefinitionMetadata
                {
                    Description = "Enhanced with validation and error handling",
                    Features = { "Order validation", "Enhanced error handling", "Retry logic" }
                });

            // Register version 2.0.0 - Major refactor
            await DefinitionRegistry.RegisterDefinitionAsync<OrderState, OrderTrigger>(
                GetType().Name,
                new StateMachineVersion(2, 0, 0),
                () => BuildOrderProcessorV2(),
                new StateMachineDefinitionMetadata
                {
                    Description = "Major refactor with new workflow states",
                    Features = { "Multi-step approval", "Advanced routing", "Batch processing" },
                    BreakingChanges = { "Added approval states", "Changed validation logic" }
                });
        }
    }

    protected override async Task<StateMachine<OrderState, OrderTrigger>?> BuildVersionedStateMachineAsync(
        StateMachineVersion version)
    {
        return version switch
        {
            { Major: 1, Minor: 0, Patch: 0 } => BuildOrderProcessorV1(),
            { Major: 1, Minor: 1, Patch: 0 } => BuildOrderProcessorV11(),
            { Major: 2, Minor: 0, Patch: 0 } => BuildOrderProcessorV2(),
            _ => null
        };
    }

    private StateMachine<OrderState, OrderTrigger> BuildOrderProcessorV1()
    {
        var machine = new StateMachine<OrderState, OrderTrigger>(OrderState.Created);

        machine.Configure(OrderState.Created)
            .Permit(OrderTrigger.Submit, OrderState.Processing);

        machine.Configure(OrderState.Processing)
            .Permit(OrderTrigger.Complete, OrderState.Completed)
            .Permit(OrderTrigger.Reject, OrderState.Rejected);

        return machine;
    }

    private StateMachine<OrderState, OrderTrigger> BuildOrderProcessorV11()
    {
        var machine = BuildOrderProcessorV1();
        
        // Add validation state (backward compatible enhancement)
        machine.Configure(OrderState.Created)
            .Permit(OrderTrigger.Validate, OrderState.Validating);

        machine.Configure(OrderState.Validating)
            .Permit(OrderTrigger.Submit, OrderState.Processing)
            .Permit(OrderTrigger.Reject, OrderState.Rejected);

        return machine;
    }

    private StateMachine<OrderState, OrderTrigger> BuildOrderProcessorV2()
    {
        var machine = new StateMachine<OrderState, OrderTrigger>(OrderState.Created);
        
        // V2.0 - Major refactor with approval workflow
        machine.Configure(OrderState.Created)
            .Permit(OrderTrigger.Submit, OrderState.PendingApproval);

        machine.Configure(OrderState.PendingApproval)
            .Permit(OrderTrigger.Approve, OrderState.Processing)
            .Permit(OrderTrigger.Reject, OrderState.Rejected);

        machine.Configure(OrderState.Processing)
            .Permit(OrderTrigger.Complete, OrderState.Completed);

        return machine;
    }
}
```

#### 2. Configure Versioning Services

```csharp
// In Program.cs
siloBuilder.ConfigureServices(services =>
{
    services.AddSingleton<IStateMachineDefinitionRegistry, StateMachineDefinitionRegistry>();
    services.AddSingleton<IVersionCompatibilityChecker, VersionCompatibilityChecker>();
    
    // Register migration hooks
    services.AddSingleton<IMigrationHook>(provider => 
        new BuiltInMigrationHooks.StateBackupHook());
    services.AddSingleton<IMigrationHook>(provider => 
        new BuiltInMigrationHooks.AuditLoggingHook(
            provider.GetRequiredService<ILogger<BuiltInMigrationHooks.AuditLoggingHook>>()));
});
```

#### 3. Version Management Operations

```csharp
// Check current version
var grain = grainFactory.GetGrain<IOrderProcessorGrain>("order-123");
var currentVersion = await grain.GetVersionAsync();
Console.WriteLine($"Current version: {currentVersion}");

// Get version compatibility information
var compatibility = await grain.GetVersionCompatibilityAsync();
Console.WriteLine($"Available versions: {string.Join(", ", compatibility.AvailableVersions)}");
Console.WriteLine($"Supports automatic upgrade: {compatibility.SupportsAutomaticUpgrade}");

// Upgrade to new version
var targetVersion = new StateMachineVersion(1, 1, 0);
var upgradeResult = await grain.UpgradeToVersionAsync(targetVersion, MigrationStrategy.Automatic);

if (upgradeResult.IsSuccess)
{
    Console.WriteLine($"Successfully upgraded to {upgradeResult.NewVersion} in {upgradeResult.UpgradeDuration.TotalMilliseconds}ms");
    
    foreach (var change in upgradeResult.MigrationSummary!.ChangesApplied)
    {
        Console.WriteLine($"  - {change}");
    }
}
else
{
    Console.WriteLine($"Upgrade failed: {upgradeResult.ErrorMessage}");
}
```

#### 4. Shadow Evaluation

```csharp
// Test new version without committing changes
var shadowVersion = new StateMachineVersion(2, 0, 0);
var shadowResult = await grain.RunShadowEvaluationAsync(shadowVersion, OrderTrigger.Submit);

if (shadowResult.WouldSucceed)
{
    Console.WriteLine($"Shadow evaluation: {shadowResult.CurrentState} -> {shadowResult.PredictedState}");
    Console.WriteLine("Safe to upgrade to new version");
}
else
{
    Console.WriteLine($"Shadow evaluation failed: {shadowResult.ErrorMessage}");
    Console.WriteLine("New version would break current workflow");
}
```

#### 5. Blue-Green Deployment

```csharp
// Deploy new version alongside existing version
var blueGreenResult = await grain.UpgradeToVersionAsync(
    new StateMachineVersion(2, 0, 0), 
    MigrationStrategy.BlueGreen);

if (blueGreenResult.IsSuccess)
{
    Console.WriteLine("New version deployed in blue-green mode");
    
    // Test the new version with shadow evaluation
    var testResult = await grain.RunShadowEvaluationAsync(
        new StateMachineVersion(2, 0, 0), 
        OrderTrigger.Submit);
    
    if (testResult.WouldSucceed)
    {
        // Switch to new version
        await grain.UpgradeToVersionAsync(
            new StateMachineVersion(2, 0, 0), 
            MigrationStrategy.Automatic);
        Console.WriteLine("Successfully switched to new version");
    }
    else
    {
        Console.WriteLine("New version failed validation, staying on current version");
    }
}
```

#### 6. Custom Migration Hooks

```csharp
public class OrderDataMigrationHook : IMigrationHook
{
    public string HookName => "OrderDataMigration";
    public int Priority => 50;

    public async Task<bool> BeforeMigrationAsync(MigrationContext context)
    {
        // Perform custom data migration
        if (context.ToVersion.Major > context.FromVersion.Major)
        {
            // Major version upgrade - transform order data structure
            var orderData = context.GetStateValue<OrderData>("OrderData");
            if (orderData != null)
            {
                var migratedData = TransformOrderDataForV2(orderData);
                context.SetStateValue("OrderData", migratedData);
                
                context.Metadata["OrderDataTransformed"] = true;
            }
        }
        
        return true;
    }

    public async Task AfterMigrationAsync(MigrationContext context)
    {
        // Verify migration success
        if (context.Metadata.ContainsKey("OrderDataTransformed"))
        {
            Console.WriteLine("Order data successfully migrated to new format");
        }
    }

    public async Task OnMigrationRollbackAsync(MigrationContext context, Exception error)
    {
        // Rollback custom changes if needed
        Console.WriteLine($"Rolling back order data migration due to: {error.Message}");
    }

    private OrderData TransformOrderDataForV2(OrderData oldData)
    {
        // Transform data structure for version 2.0
        return new OrderData
        {
            OrderId = oldData.OrderId,
            // New required fields in V2
            ApprovalRequired = oldData.Amount > 1000,
            ApprovalLevel = DetermineApprovalLevel(oldData.Amount)
        };
    }
}
```

#### 7. Version Compatibility Checking

```csharp
// Use version compatibility checker service
var compatibilityChecker = serviceProvider.GetRequiredService<IVersionCompatibilityChecker>();

// Check if upgrade is compatible
var compatibilityResult = await compatibilityChecker.CheckCompatibilityAsync(
    "OrderProcessorGrain",
    new StateMachineVersion(1, 0, 0),
    new StateMachineVersion(2, 0, 0));

if (compatibilityResult.IsCompatible)
{
    Console.WriteLine($"Upgrade is compatible with {compatibilityResult.CompatibilityLevel} compatibility");
}
else
{
    Console.WriteLine("Upgrade requires migration:");
    foreach (var change in compatibilityResult.BreakingChanges)
    {
        Console.WriteLine($"  - {change.Description} (Impact: {change.Impact})");
        Console.WriteLine($"    Mitigation: {change.Mitigation}");
    }
}

// Get upgrade recommendations
var recommendations = await compatibilityChecker.GetUpgradeRecommendationsAsync(
    "OrderProcessorGrain",
    new StateMachineVersion(1, 0, 0));

foreach (var rec in recommendations)
{
    Console.WriteLine($"Upgrade to {rec.ToVersion}: {rec.RecommendationType}");
    Console.WriteLine($"  Risk: {rec.RiskLevel}, Effort: {rec.EstimatedEffort}");
    Console.WriteLine($"  Benefits: {string.Join(", ", rec.Benefits)}");
}
```

#### 8. Versioning Features

- **Semantic Versioning**: Full semantic version support with major.minor.patch format
- **Backward Compatibility**: Minor version upgrades are backward compatible
- **Breaking Change Detection**: Automatic detection of breaking changes in major versions
- **Shadow Evaluation**: Test new versions without affecting live state
- **Migration Strategies**: Automatic, custom, blue-green, and dry-run migration options
- **Migration Hooks**: Extensible hook system for custom migration logic
- **Rollback Support**: Automatic rollback on migration failure
- **Audit Trail**: Complete history of version upgrades and migrations
- **Deployment Validation**: Check deployment compatibility with existing versions

### Distributed Sagas (Phase 5)

#### 1. Create a Multi-Grain Workflow with Compensation

```csharp
using Orleans.StateMachineES.Sagas;

public class InvoiceProcessingSaga : SagaOrchestratorGrain<InvoiceData>, IInvoiceProcessingSagaGrain
{
    protected override void ConfigureSagaSteps()
    {
        // Step 1: Post Invoice to accounting system
        AddStep(new PostInvoiceStep())
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithRetry(3)
            .WithMetadata("Description", "Posts invoice to accounting system");

        // Step 2: Create Journal Entry in general ledger
        AddStep(new CreateJournalEntryStep())
            .WithTimeout(TimeSpan.FromSeconds(45))
            .WithRetry(2)
            .WithMetadata("Description", "Creates journal entries for GL");

        // Step 3: Run Control Checks for compliance
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

// Example saga step with compensation logic
public class PostInvoiceStep : ISagaStep<InvoiceData>
{
    public string StepName => "PostInvoice";
    public TimeSpan Timeout => TimeSpan.FromSeconds(30);
    public bool CanRetry => true;
    public int MaxRetryAttempts => 3;

    public async Task<SagaStepResult> ExecuteAsync(InvoiceData sagaData, SagaContext context)
    {
        var invoiceGrain = GrainFactory.GetGrain<IInvoiceGrain>(sagaData.InvoiceId);
        
        try 
        {
            var result = await invoiceGrain.PostAsync(sagaData, context.CorrelationId);
            return SagaStepResult.Success(result);
        }
        catch (BusinessRuleException ex)
        {
            return SagaStepResult.BusinessFailure(ex.Message); // Triggers immediate compensation
        }
        catch (Exception ex)
        {
            return SagaStepResult.TechnicalFailure(ex.Message, ex); // Triggers retry then compensation
        }
    }

    public async Task<CompensationResult> CompensateAsync(
        InvoiceData sagaData, 
        SagaStepResult? stepResult, 
        SagaContext context)
    {
        var invoiceGrain = GrainFactory.GetGrain<IInvoiceGrain>(sagaData.InvoiceId);
        await invoiceGrain.CancelAsync(context.CorrelationId);
        
        return CompensationResult.Success();
    }
}
```

#### 2. Execute and Monitor Sagas

```csharp
// Execute the multi-grain saga
var sagaGrain = grainFactory.GetGrain<IInvoiceProcessingSagaGrain>("invoice-saga-123");
var correlationId = Guid.NewGuid().ToString("N");

var invoiceData = new InvoiceData
{
    InvoiceId = "INV-12345",
    CustomerId = "CUST-789",
    Amount = 2500.00m
};

var result = await sagaGrain.ExecuteAsync(invoiceData, correlationId);

if (result.IsSuccess)
{
    Console.WriteLine($"Saga completed successfully in {result.Duration.TotalSeconds}s");
}
else if (result.IsCompensated)
{
    Console.WriteLine("Saga failed but was fully compensated");
    Console.WriteLine($"Error: {result.ErrorMessage}");
}

// Monitor saga progress
var status = await sagaGrain.GetStatusAsync();
Console.WriteLine($"Status: {status.Status}");
Console.WriteLine($"Progress: {status.CurrentStepIndex + 1}/{status.TotalSteps}");

// Get detailed execution history for audit
var history = await sagaGrain.GetHistoryAsync();
foreach (var step in history.StepExecutions)
{
    Console.WriteLine($"Step {step.StepName}: {(step.IsSuccess ? "✅" : "❌")} ({step.Duration.TotalMilliseconds}ms)");
    if (step.RetryAttempts > 0)
    {
        Console.WriteLine($"  Retries: {step.RetryAttempts}");
    }
}

// Show compensation history if any
foreach (var compensation in history.CompensationExecutions)
{
    Console.WriteLine($"Compensated {compensation.StepName}: {(compensation.IsSuccess ? "✅" : "❌")}");
}
```

#### 3. Saga Features

- **Orchestration Pattern**: Central saga coordinator manages the entire business process
- **Automatic Compensation**: Failed steps trigger rollback in reverse execution order
- **Retry Logic**: Configurable retry with exponential backoff for technical failures
- **Correlation Tracking**: Full distributed tracing with correlation IDs across all grains
- **Business vs Technical Errors**: Different handling strategies for different error types
- **Event Sourcing Integration**: Complete audit trail of saga execution and compensations
- **Timeout Management**: Per-step timeouts with graceful degradation
- **Status Monitoring**: Real-time saga progress and execution history

#### 4. Error Handling Strategies

```csharp
// Business failures trigger immediate compensation without retry
if (validationFails)
{
    return SagaStepResult.BusinessFailure("Customer credit limit exceeded");
}

// Technical failures trigger retry, then compensation if max attempts reached
if (serviceUnavailable)
{
    return SagaStepResult.TechnicalFailure("Invoice service temporarily unavailable");
}

// Compensation must be idempotent and handle partial rollbacks
public async Task<CompensationResult> CompensateAsync(...)
{
    try
    {
        // Only compensate if the step actually succeeded
        if (stepResult?.IsSuccess == true)
        {
            await UndoInvoicePosting(sagaData.InvoiceId);
        }
        
        return CompensationResult.Success();
    }
    catch (Exception ex)
    {
        return CompensationResult.Failure($"Compensation failed: {ex.Message}", ex);
    }
}
```

### Distributed Tracing with OpenTelemetry

#### 1. Configure OpenTelemetry for State Machine Tracing

```csharp
using Orleans.StateMachineES.Tracing;

// In Program.cs for ASP.NET Core or console applications
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddStateMachineInstrumentation()
        .AddJaegerExporter()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddStateMachineMetrics()
        .AddPrometheusExporter());

// For Orleans Silo
siloBuilder.ConfigureServices(services =>
{
    services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddStateMachineInstrumentation()
            .AddOtlpExporter())
        .WithMetrics(metrics => metrics
            .AddStateMachineMetrics());
});
```

#### 2. Automatic Activity Tracking

State machine operations are automatically traced:

```csharp
public class OrderGrain : EventSourcedStateMachineGrain<OrderState, OrderTrigger, OrderGrainState>
{
    protected override StateMachine<OrderState, OrderTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<OrderState, OrderTrigger>(OrderState.Created);
        
        machine.Configure(OrderState.Created)
            .OnEntry(() => 
            {
                // This action is automatically traced
                Logger.LogInformation("Order created");
            })
            .Permit(OrderTrigger.Submit, OrderState.Processing);
            
        return machine;
    }

    // This method call will create a distributed trace
    public async Task SubmitOrderAsync()
    {
        await FireAsync(OrderTrigger.Submit); // Traced automatically
    }
}
```

#### 3. Custom Tracing with Helper

```csharp
using Orleans.StateMachineES.Tracing;

public class PaymentProcessorGrain : StateMachineGrain<PaymentState, PaymentTrigger>
{
    public async Task ProcessPaymentAsync(PaymentRequest request)
    {
        // Use TracingHelper for custom operations
        var result = await TracingHelper.TraceStateTransition(
            grainType: GetType().Name,
            grainId: this.GetPrimaryKeyString(),
            fromState: StateMachine.State.ToString(),
            trigger: PaymentTrigger.Process.ToString(),
            operation: async () =>
            {
                // Your business logic here
                var paymentResult = await ProcessPaymentLogic(request);
                await FireAsync(PaymentTrigger.Process);
                return paymentResult;
            },
            parameterCount: 1);
            
        // Activity context is automatically enriched with:
        // - Grain type and ID
        // - State transition details
        // - Duration and success status
        // - Custom tags and events
    }
    
    public async Task ProcessSagaStepAsync(string sagaId, string stepName)
    {
        // Trace saga execution with correlation
        await TracingHelper.TraceSagaExecution(
            sagaType: "PaymentSaga",
            sagaId: sagaId,
            stepName: stepName,
            grainId: this.GetPrimaryKeyString(),
            operation: async () =>
            {
                await ExecuteSagaStep();
            });
    }
}
```

#### 4. Metrics Collection

Built-in metrics are automatically collected:

```csharp
// Metrics are automatically exported:
// - statemachine_transitions_total: Counter of state transitions
// - statemachine_transition_duration: Histogram of transition times  
// - statemachine_active_grains: Gauge of currently active grains
// - statemachine_trigger_errors_total: Counter of failed trigger attempts
// - statemachine_saga_executions_total: Counter of saga executions
// - statemachine_saga_duration: Histogram of saga execution times

// View metrics in your monitoring dashboard (Grafana, etc.)
```

#### 5. Distributed Trace Visualization

Traces will show the complete flow across grains:

```
OrderService.SubmitOrder
├── OrderGrain.SubmitOrderAsync (span: 245ms)
│   ├── State Transition: Created → Processing
│   └── Event: OrderSubmittedEvent published
├── PaymentGrain.ProcessPaymentAsync (span: 180ms)  
│   ├── State Transition: Pending → Authorized
│   └── External API call: PaymentProvider.Authorize
├── InventoryGrain.ReserveItemsAsync (span: 95ms)
│   └── State Transition: Available → Reserved  
└── NotificationGrain.SendConfirmationAsync (span: 45ms)
    └── External call: EmailService.SendEmail

Total Duration: 565ms
```

### State Machine Visualization and Analysis

#### 1. Generate Visual Diagrams

```csharp
using Orleans.StateMachineES.Visualization;

public class OrderVisualizationService
{
    public async Task GenerateOrderStateDiagramAsync()
    {
        var grain = grainFactory.GetGrain<IOrderGrain>("order-123");
        
        // Create a comprehensive visualization report
        var report = await StateMachineVisualizer.CreateReportAsync(grain, includeRuntimeInfo: true);
        
        if (report.Success)
        {
            // Export to various formats
            var dotGraph = StateMachineVisualizer.ToDotGraph(stateMachine);
            var analysis = StateMachineVisualizer.AnalyzeStructure(stateMachine);
            
            // Export to multiple formats
            var jsonBytes = await StateMachineVisualizer.ExportAsync(stateMachine, ExportFormat.Json);
            var mermaidBytes = await StateMachineVisualizer.ExportAsync(stateMachine, ExportFormat.Mermaid);
            var plantUmlBytes = await StateMachineVisualizer.ExportAsync(stateMachine, ExportFormat.PlantUml);
            
            await File.WriteAllBytesAsync("order-state-machine.json", jsonBytes);
            await File.WriteAllBytesAsync("order-diagram.mmd", mermaidBytes);
        }
    }
}
```

#### 2. Interactive Web Visualization

```csharp
using Orleans.StateMachineES.Visualization.Web;

public class StateMachineWebController : ControllerBase
{
    [HttpGet("visualize/{grainType}/{grainId}")]
    public async Task<IActionResult> VisualizeGrain(string grainType, string grainId)
    {
        // Generate interactive HTML visualization
        var html = StateMachineWebVisualizer.GenerateRealTimeHtml<OrderState, OrderTrigger>(
            grainType, grainId, new WebVisualizationOptions
            {
                Title = $"Order State Machine: {grainId}",
                ShowStatistics = true,
                VisualizationLibrary = WebVisualizationLibrary.VisJS
            });
            
        return Content(html, "text/html");
    }
    
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        // Create a dashboard showing multiple state machines
        var stateMachines = await GetStateMachineAnalyses();
        
        var dashboardHtml = StateMachineWebVisualizer.GenerateDashboardHtml(
            stateMachines, new DashboardOptions
            {
                Title = "State Machine Operations Dashboard",
                ShowCharts = true,
                ShowMiniVisualizations = true
            });
            
        return Content(dashboardHtml, "text/html");
    }
}
```

#### 3. Batch Analysis and Reporting

```csharp
using Orleans.StateMachineES.Visualization;

public class StateMachineAnalyticsService
{
    public async Task GenerateBatchReportsAsync()
    {
        var batchService = new BatchVisualizationService();
        
        // Analyze multiple grain types
        var result = await batchService.GenerateGrainVisualizationsAsync<OrderGrain, OrderState, OrderTrigger>(
            grainFactory,
            new[] { "order-1", "order-2", "order-3" },
            new BatchVisualizationOptions
            {
                OutputDirectory = "./reports",
                IncludeStatistics = true,
                GenerateComparisons = true,
                ExportFormats = { ExportFormat.Json, ExportFormat.Mermaid, ExportFormat.Dot }
            });
            
        Console.WriteLine($"Generated {result.SuccessfulCount} reports in {result.ProcessingTime.TotalSeconds}s");
        
        // View comparative statistics
        var statistics = result.Statistics;
        Console.WriteLine($"Average complexity: {statistics.AverageComplexity:F2}");
        Console.WriteLine($"State count range: {statistics.StateCountRange.Min}-{statistics.StateCountRange.Max}");
    }
}
```

#### 4. Real-Time Monitoring Dashboard

The web visualization includes:

- **Live State Tracking**: See current state of running grains
- **Transition History**: Visual timeline of state changes
- **Performance Metrics**: Transition times and success rates
- **Interactive Controls**: Trigger state transitions directly from UI
- **Comparative Analysis**: Side-by-side comparison of multiple state machines
- **Export Functions**: Download diagrams and reports in various formats

#### 5. Analysis Features

```csharp
// Analyze state machine complexity
var analysis = StateMachineVisualizer.AnalyzeStructure(stateMachine);

Console.WriteLine($"State Machine Analysis:");
Console.WriteLine($"  States: {analysis.States.Count}");
Console.WriteLine($"  Triggers: {analysis.Triggers.Count}");
Console.WriteLine($"  Complexity Level: {analysis.Metrics.ComplexityLevel}");
Console.WriteLine($"  Cyclomatic Complexity: {analysis.Metrics.CyclomaticComplexity}");
Console.WriteLine($"  Max Depth: {analysis.Metrics.MaxDepth}");
Console.WriteLine($"  Connectivity Index: {analysis.Metrics.ConnectivityIndex:F2}");

// Find potential issues
foreach (var state in analysis.States.Where(s => s.Substates.Count > 5))
{
    Console.WriteLine($"Warning: State {state.Name} has {state.Substates.Count} substates (consider refactoring)");
}

foreach (var trigger in analysis.Triggers.Where(t => t.UsageCount > 10))
{
    Console.WriteLine($"Info: Trigger {trigger.Name} is used {trigger.UsageCount} times across states");
}
```

#### 6. Visualization Output Formats

- **DOT (Graphviz)**: Generate publication-quality diagrams
- **Mermaid**: GitHub-compatible markdown diagrams  
- **PlantUML**: Enterprise documentation standard
- **JSON**: Machine-readable analysis data
- **XML**: Structured metadata export
- **Interactive HTML**: Real-time web dashboards

#### 7. Integration with Monitoring Systems

```csharp
// Export metrics to monitoring platforms
public class MonitoringIntegration
{
    public async Task ExportToPrometheus()
    {
        // State machine metrics are automatically available in Prometheus format
        // Access at http://your-app/metrics
    }
    
    public async Task ExportToAppInsights()
    {
        // Traces and metrics are automatically sent to Application Insights
        // when configured with OpenTelemetry
    }
    
    public async Task ExportToJaeger()
    {
        // Distributed traces are automatically sent to Jaeger
        // View complete request flows across all grains
    }
}
```

## Advanced Features

### Guard Conditions with Detailed Feedback

```csharp
// Check if a trigger can be fired
var canFire = await grain.CanFireAsync(trigger);

// Get detailed information about why a trigger cannot be fired
var (canFire, unmetConditions) = await grain.CanFireWithUnmetGuardsAsync(trigger);
if (!canFire)
{
    foreach (var condition in unmetConditions)
    {
        Console.WriteLine($"Guard not met: {condition}");
    }
}
```

### Parameterized Triggers

```csharp
// Configure triggers with parameters
machine.Configure(States.Processing)
    .OnEntryFrom(Triggers.StartProcessing, (int jobId, string user) => 
    {
        // Handle parameters
        _currentJobId = jobId;
        _currentUser = user;
    });

// Fire trigger with parameters (up to 3 supported)
await grain.FireAsync(Triggers.StartProcessing, jobId, userName);
```

### State Machine Inspection

```csharp
// Get permitted triggers in current state
var triggers = await grain.GetPermittedTriggersAsync();

// Get detailed trigger information
var details = await grain.GetDetailedPermittedTriggersAsync();

// Check if in a specific state
var isProcessing = await grain.IsInStateAsync(States.Processing);

// Get complete state machine structure
var info = await grain.GetInfoAsync();
```

### State-Based Timeouts and Scheduling

Configure automatic timeouts and periodic actions based on state:

```csharp
protected override void ConfigureTimeouts()
{
    // Basic timeout - transition after specified time
    RegisterStateTimeout(States.Processing,
        ConfigureTimeout(States.Processing)
            .After(TimeSpan.FromMinutes(5))
            .TransitionTo(Triggers.Timeout)
            .Build());

    // Repeating heartbeat timer
    RegisterStateTimeout(States.Active,
        ConfigureTimeout(States.Active)
            .After(TimeSpan.FromSeconds(30))
            .TransitionTo(Triggers.Heartbeat)
            .Repeat()
            .WithName("HealthCheck")
            .Build());

    // Long-running durable reminder
    RegisterStateTimeout(States.LongProcess,
        ConfigureTimeout(States.LongProcess)
            .After(TimeSpan.FromHours(24))
            .TransitionTo(Triggers.Expire)
            .UseDurableReminder() // Survives grain restarts
            .WithName("DailyCleanup")
            .Build());
}

// Timer events are automatically recorded in event history
// Timers are automatically managed on state transitions
```

### Orleans Context Extensions

Use special async extensions for Orleans grain context:

```csharp
protected override StateMachine<State, Trigger> BuildStateMachine()
{
    var machine = new StateMachine<State, Trigger>(State.Initial);
    
    machine.Configure(State.Active)
        .OnEntryOrleansContextAsync(async () => 
        {
            // Async operations with grain context
            await WriteStateAsync();
        })
        .OnExitOrleansContextAsync(async () => 
        {
            await LogTransitionAsync();
        })
        .OnActivateOrleansContextAsync(async () => 
        {
            await InitializeAsync();
        });
        
    return machine;
}
```

## Architecture

The library provides a base `StateMachineGrain<TState, TTrigger>` class that:

- Wraps the Stateless `StateMachine` with async-friendly operations
- Integrates with Orleans grain lifecycle
- Provides thread-safe state management through Orleans' single-threaded execution model
- Supports comprehensive state inspection and metadata export

## 🏗️ Project Structure

```
Orleans.StateMachineES/
├── src/
│   └── Orleans.StateMachineES/
│       ├── EventSourcing/          # Event sourcing implementation
│       │   ├── Configuration/      # Event sourcing options
│       │   ├── Events/            # Event definitions
│       │   └── Exceptions/        # Custom exceptions
│       ├── Hierarchical/          # Hierarchical state machines
│       ├── Interfaces/            # Core interfaces
│       ├── Models/                # Data models
│       ├── Sagas/                 # Distributed saga support
│       ├── Timers/                # Timer-based transitions
│       ├── Tracing/               # OpenTelemetry distributed tracing
│       │   ├── StateMachineActivitySource.cs
│       │   ├── StateMachineMetrics.cs
│       │   ├── TracingExtensions.cs
│       │   ├── TracingHelper.cs
│       │   └── TracingSetupExamples.cs
│       ├── Visualization/         # State machine visualization
│       │   ├── Models/           # Visualization data models
│       │   ├── Web/              # Interactive web visualization
│       │   ├── StateMachineVisualizer.cs
│       │   └── BatchVisualizationService.cs
│       ├── Versioning/            # State machine versioning
│       │   ├── StateMachineIntrospector.cs
│       │   ├── ImprovedStateMachineIntrospector.cs
│       │   └── VersionedStateMachineGrain.cs
│       ├── Extensions/            # Extension methods
│       └── StateMachineGrain.cs  # Base grain implementation
├── tests/
│   └── Orleans.StateMachineES.Tests/
│       ├── Cluster/               # Test cluster setup
│       ├── EventSourcing/         # Event sourcing tests
│       ├── Hierarchical/          # Hierarchical tests
│       ├── Sagas/                 # Saga tests
│       ├── Timers/                # Timer tests
│       └── Versioning/            # Versioning tests
├── docs/                          # Documentation
│   ├── CHEAT_SHEET.md
│   ├── IMPLEMENTATION_STRATEGY.md
│   └── MIGRATION_GUIDE.md
└── Orleans.StateMachineES.sln     # Solution file
```

## State Machine Composition & Inheritance

Build complex state machines from reusable components with multiple composition strategies.

### Creating Reusable Components

```csharp
// Create a reusable validation component
public class EmailValidationComponent : ComposableStateMachineBase<ProcessState, ProcessTrigger>
{
    public EmailValidationComponent(ILogger logger) 
        : base("email-validation", "Validates email addresses", ProcessState.Validating, logger)
    {
        // Define exit states
        AddExitStates(ProcessState.Valid, ProcessState.Invalid);
        
        // Register mappable triggers
        RegisterDefaultTrigger(ProcessTrigger.Validate);
    }

    public override void Configure(StateMachine<ProcessState, ProcessTrigger> stateMachine)
    {
        stateMachine.Configure(ProcessState.Validating)
            .Permit(ProcessTrigger.ValidationSuccess, ProcessState.Valid)
            .Permit(ProcessTrigger.ValidationFailure, ProcessState.Invalid)
            .OnEntry(() => PerformValidation());
    }
}
```

### Composing State Machines

```csharp
public class OrderProcessingGrain : ComposedStateMachineGrain<OrderState, OrderTrigger>
{
    protected override void RegisterComponents()
    {
        // Register reusable components
        RegisterComponent(new ValidationComponent<OrderState, OrderTrigger>(
            componentId: "order-validation",
            entryState: OrderState.Validating,
            validatingState: OrderState.ValidatingOrder,
            validState: OrderState.OrderValid,
            invalidState: OrderState.OrderInvalid,
            // ... other parameters
            logger: _logger
        ));

        RegisterComponent(new RetryComponent<OrderState, OrderTrigger>(
            componentId: "payment-retry",
            entryState: OrderState.PaymentPending,
            attemptingState: OrderState.ProcessingPayment,
            successState: OrderState.PaymentSuccess,
            failedState: OrderState.PaymentFailed,
            retryingState: OrderState.RetryingPayment,
            maxAttempts: 3,
            retryDelay: TimeSpan.FromSeconds(5),
            backoffStrategy: BackoffStrategy.Exponential,
            logger: _logger
        ));

        RegisterComponent(new ApprovalComponent<OrderState, OrderTrigger>(
            componentId: "manager-approval",
            entryState: OrderState.PendingApproval,
            pendingApprovalState: OrderState.AwaitingManager,
            approvedState: OrderState.Approved,
            rejectedState: OrderState.Rejected,
            escalatedState: OrderState.Escalated,
            configuration: new ApprovalConfiguration
            {
                ApprovalLevels = 2,
                Timeout = TimeSpan.FromHours(24),
                AutoApproveIfNoResponse = false,
                AllowEscalation = true
            },
            logger: _logger
        ));
    }

    protected override CompositionStrategy CompositionStrategy => 
        CompositionStrategy.Sequential; // or Parallel, Hierarchical, Mixed

    protected override OrderState GetInitialState() => OrderState.Draft;
}
```

### Available Composition Strategies

- **Sequential**: Components execute one after another
- **Parallel**: Components can execute simultaneously  
- **Hierarchical**: Components nested within parent components
- **Mixed**: Combination of different strategies

### Built-in Reusable Components

#### ValidationComponent
- Input validation with custom logic
- Success/failure state routing
- Configurable validation rules

#### RetryComponent  
- Automatic retry with backoff strategies
- Fixed, Linear, Exponential, or Jittered backoff
- Configurable max attempts and delays

#### ApprovalComponent
- Multi-level approval workflows
- Timeout and escalation support
- Required approvers configuration
- Auto-approval options

### Dynamic Component Management

```csharp
// Add components at runtime
await grain.AddComponentDynamicallyAsync(new CustomComponent());

// Remove components
await grain.RemoveComponentDynamicallyAsync("component-id");

// Access component data
var component = grain.GetComponent("component-id");
var allComponents = grain.GetComponents();
```

```

## Requirements

- .NET 9.0 or higher
- Microsoft Orleans 9.1.2 or higher
- Stateless 5.17.0 or higher

## Roadmap

This fork implements a phased approach to enhance Orleans state machines:

- ✅ **Phase 1 & 2**: Event Sourcing with JournaledGrain (Complete)
- ✅ **Phase 3**: Timers and Reminders (Complete)
- ✅ **Phase 4**: Hierarchical/Nested States (Complete)
- ✅ **Phase 5**: Distributed Sagas & Compensations (Complete)
- ✅ **Phase 6**: State Machine Versioning (Complete)
- ✅ **Phase 7a**: Distributed Tracing with OpenTelemetry (Complete)
- ✅ **Phase 7b**: State Machine Visualization & Analysis (Complete)
- ✅ **Phase 7c**: Health Checks and Monitoring Endpoints (Complete)
- ✅ **Phase 8**: State Machine Composition & Inheritance (Complete)
- ✅ **Phase 9**: Example Applications & Documentation (Complete)

See [docs/plan.md](docs/plan.md) for detailed roadmap.

## Author

**Michael Ivertowski**  
This fork is maintained by Michael Ivertowski under the MIT License.

## Acknowledgements

This project is a fork of the excellent [ManagedCode.Orleans.StateMachine](https://github.com/managedcode/Orleans.StateMachine) library. Deep respect and thanks to the ManagedCode team for creating the original foundation that made this enhanced version possible.

Special thanks to:
- The **ManagedCode** team for the original Orleans.StateMachine implementation
- The **Stateless** team for the powerful state machine library
- The **Microsoft Orleans** team for the actor framework

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## References

- [Original ManagedCode.Orleans.StateMachine](https://github.com/managedcode/Orleans.StateMachine) - The foundation for this fork
- [Stateless State Machine](https://github.com/dotnet-state-machine/stateless)
- [Microsoft Orleans](https://github.com/dotnet/orleans)
- [Orleans Event Sourcing](https://learn.microsoft.com/en-us/dotnet/orleans/grains/event-sourcing/)
- [NStateManager](https://github.com/scottctr/NStateManager) - Inspiration for some patterns
