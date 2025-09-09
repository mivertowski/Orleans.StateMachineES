# Orleans.StateMachineES Analyzers Documentation

## Overview

Orleans.StateMachineES includes a comprehensive set of Roslyn analyzers that provide compile-time safety and guidance for common state machine patterns and pitfalls. These analyzers help developers avoid runtime errors and follow best practices when implementing state machines with Orleans.

## Available Analyzers

### OSMES001: Async Lambda in State Callback

**Severity**: Warning  
**Category**: Usage

Detects async lambdas in state callbacks (OnEntry, OnExit, OnEntryFrom, OnExitFrom) that won't be awaited due to Stateless library limitations.

#### Problem Example
```csharp
Configure(State.Processing)
    .OnEntry(async () => await ProcessDataAsync()); // ⚠️ OSMES001
```

#### Solution
```csharp
Configure(State.Processing)
    .OnEntry(() => Console.WriteLine("Entered Processing"));

// Move async logic to grain method
public async Task StartProcessingAsync()
{
    await FireAsync(Trigger.Start);
    await ProcessDataAsync(); // Async work after transition
}
```

### OSMES002: FireAsync Within Callback

**Severity**: Error  
**Category**: Usage

Detects FireAsync calls within state callbacks that can cause deadlocks or reentrancy issues.

#### Problem Example
```csharp
Configure(State.Active)
    .OnEntry(() => 
    {
        FireAsync(Trigger.Process).Wait(); // 🚫 OSMES002
    });
```

#### Solution
```csharp
Configure(State.Active)
    .OnEntry(() => LogEntry("Active"));
    
// Trigger transitions from grain methods instead
public async Task ProcessAsync()
{
    await FireAsync(Trigger.Process);
}
```

### OSMES003: Missing BuildStateMachine Implementation

**Severity**: Error  
**Category**: Implementation

Detects classes deriving from StateMachineGrain that don't properly implement BuildStateMachine.

#### Problem Example
```csharp
public class MyStateMachine : StateMachineGrain<State, Trigger>
{
    protected override void BuildStateMachine()
    {
        // Empty or missing implementation 🚫 OSMES003
    }
}
```

#### Solution
```csharp
public class MyStateMachine : StateMachineGrain<State, Trigger>
{
    protected override void BuildStateMachine()
    {
        Configure(State.Idle)
            .Permit(Trigger.Start, State.Active);
            
        Configure(State.Active)
            .Permit(Trigger.Stop, State.Idle);
    }
}
```

### OSMES004: Unreachable State

**Severity**: Warning  
**Category**: Design

Detects states that have no incoming transitions and aren't the initial state.

#### Problem Example
```csharp
protected override void BuildStateMachine()
{
    Configure(State.Idle)
        .Permit(Trigger.Start, State.Active);
    
    Configure(State.Active)
        .Permit(Trigger.Stop, State.Idle);
    
    Configure(State.Orphaned) // ⚠️ OSMES004: No way to reach this state
        .OnEntry(() => Console.WriteLine("Never reached"));
}
```

#### Solution
```csharp
protected override void BuildStateMachine()
{
    Configure(State.Idle)
        .Permit(Trigger.Start, State.Active)
        .Permit(Trigger.Error, State.Orphaned); // Add transition
    
    Configure(State.Active)
        .Permit(Trigger.Stop, State.Idle);
    
    Configure(State.Orphaned)
        .OnEntry(() => Console.WriteLine("Error state"));
}
```

### OSMES005: Duplicate State Configuration

**Severity**: Warning  
**Category**: Design

Detects states that are configured multiple times in BuildStateMachine.

#### Problem Example
```csharp
protected override void BuildStateMachine()
{
    Configure(State.Active)
        .Permit(Trigger.Pause, State.Paused);
    
    // Later in the method...
    Configure(State.Active) // ⚠️ OSMES005: Duplicate configuration
        .OnEntry(() => Console.WriteLine("Active"));
}
```

#### Solution
```csharp
protected override void BuildStateMachine()
{
    // Combine all configuration for each state
    Configure(State.Active)
        .Permit(Trigger.Pause, State.Paused)
        .OnEntry(() => Console.WriteLine("Active"));
}
```

## Code Fix Providers

### AsyncLambdaCodeFixProvider

Automatically converts async lambdas to synchronous callbacks and generates separate async methods.

**Before Fix**:
```csharp
Configure(State.Processing)
    .OnEntry(async () => 
    {
        await LoadDataAsync();
        await ProcessAsync();
    });
```

**After Fix**:
```csharp
Configure(State.Processing)
    .OnEntry(() => _ = OnProcessingEntryAsync());

private async Task OnProcessingEntryAsync()
{
    await LoadDataAsync();
    await ProcessAsync();
}
```

## Configuration

### Using .editorconfig

You can customize analyzer severities in your `.editorconfig` file:

```ini
[*.cs]
# Treat async lambda warning as error
dotnet_diagnostic.OSMES001.severity = error

# Disable unreachable state warning for generated code
[**/Generated/**.cs]
dotnet_diagnostic.OSMES004.severity = none

# Suppress duplicate configuration in tests
[**/*Tests.cs]
dotnet_diagnostic.OSMES005.severity = none
```

### Global Suppression

For project-wide suppression, add to your project file:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);OSMES004;OSMES005</NoWarn>
</PropertyGroup>
```

### Inline Suppression

Suppress specific instances using pragmas:

```csharp
#pragma warning disable OSMES004 // Unreachable state
Configure(State.Debug)
    .OnEntry(() => Console.WriteLine("Debug mode"));
#pragma warning restore OSMES004
```

## Best Practices

1. **Enable All Analyzers**: Keep all analyzers enabled during development to catch issues early.

2. **Configure Severities**: Adjust severities based on your project needs:
   - Use `error` for critical issues in production code
   - Use `warning` for code quality issues
   - Use `none` only for generated or test code

3. **Use Code Fixes**: Take advantage of automatic code fixes when available to quickly resolve issues.

4. **Document Suppressions**: When suppressing analyzer warnings, add comments explaining why:
   ```csharp
   // Orphaned state used only in unit tests
   #pragma warning disable OSMES004
   ```

5. **CI/CD Integration**: Consider treating warnings as errors in CI/CD:
   ```xml
   <PropertyGroup Condition="'$(CI)' == 'true'">
     <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
   </PropertyGroup>
   ```

## Troubleshooting

### Analyzer Not Working

1. Ensure the analyzer package is referenced:
   ```xml
   <PackageReference Include="Orleans.StateMachineES.Generators" Version="1.0.2" />
   ```

2. Clean and rebuild the solution:
   ```bash
   dotnet clean
   dotnet build
   ```

3. Check if analyzers are loaded:
   - In Visual Studio: View → Other Windows → Syntax Visualizer
   - In VS Code: Check Problems panel

### False Positives

If you encounter false positives:

1. Verify you're using the latest version of the analyzers
2. Check if the code pattern is supported
3. Report issues at: https://github.com/mivertowski/Orleans.StateMachineES/issues

### Performance Impact

The analyzers are designed to have minimal impact on build performance:
- They run concurrently
- They use efficient pattern matching
- They skip generated code by default

If you experience slow builds, you can temporarily disable analyzers:
```xml
<PropertyGroup>
  <RunAnalyzers>false</RunAnalyzers>
</PropertyGroup>
```

## Future Enhancements

Planned analyzer improvements:
- Detection of infinite state loops
- Validation of trigger parameter types
- Performance hints for large state machines
- Integration with Orleans Dashboard

## Contributing

To contribute new analyzers or improvements:

1. Follow the existing analyzer patterns
2. Include comprehensive tests
3. Update documentation
4. Add entries to AnalyzerReleases.Unshipped.md

See [CONTRIBUTING.md](../CONTRIBUTING.md) for more details.