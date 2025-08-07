# Orleans.StateMachineES

[![NuGet](https://img.shields.io/nuget/v/ivlt.Orleans.StateMachineES.svg)](https://www.nuget.org/packages/ivlt.Orleans.StateMachineES/)
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

## Installation

```bash
dotnet add package ivlt.Orleans.StateMachineES
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
using ivlt.Orleans.StateMachineES.EventSourcing;

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
using ivlt.Orleans.StateMachineES.Timers;

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
using ivlt.Orleans.StateMachineES.Hierarchical;

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
using ivlt.Orleans.StateMachineES.Versioning;

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
using ivlt.Orleans.StateMachineES.Sagas;

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
- 📋 **Phase 7**: Advanced Observability
- 📋 **Phase 8**: Workflow Orchestration

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
