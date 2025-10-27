# Orleans.StateMachineES.Generators

[![NuGet](https://img.shields.io/nuget/v/Orleans.StateMachineES.Generators.svg)](https://www.nuget.org/packages/Orleans.StateMachineES.Generators/)

Comprehensive Roslyn analyzers for Orleans.StateMachineES that catch state machine anti-patterns at compile time.

## Installation

```bash
dotnet add package Orleans.StateMachineES.Generators
```

## Diagnostic Rules

This package provides 10 analyzers that enforce best practices:

### Critical Errors

| ID | Description | Severity |
|----|-------------|----------|
| **OSMES002** | FireAsync called within state callback | Error |
| **OSMES003** | Missing BuildStateMachine implementation | Error |
| **OSMES009** | Missing initial state assignment | Error |

### Warnings

| ID | Description | Category |
|----|-------------|----------|
| **OSMES001** | Async lambda in state callback | Usage |
| **OSMES004** | Unreachable state detected | Design |
| **OSMES005** | Duplicate state configuration | Design |
| **OSMES006** | State with no trigger handlers | Usage |
| **OSMES007** | Circular transitions with no exit | Design |
| **OSMES008** | Guard condition too complex (cyclomatic > 10) | Maintainability |
| **OSMES010** | Unsafe enum value cast | Reliability |

## Configuration

### In `.editorconfig`

```ini
# Set severity levels
dotnet_diagnostic.OSMES001.severity = warning
dotnet_diagnostic.OSMES002.severity = error

# Disable specific rules
dotnet_diagnostic.OSMES008.severity = none

# Configure for test projects
[*Tests.cs]
dotnet_diagnostic.OSMES001.severity = none
```

### In `.csproj`

```xml
<PropertyGroup>
  <!-- Treat analyzer warnings as errors -->
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>

  <!-- Disable specific analyzer -->
  <NoWarn>$(NoWarn);OSMES008</NoWarn>
</PropertyGroup>
```

## Example Diagnostics

### OSMES001: Async Lambda Warning

```csharp
// ❌ Bad - async lambda in callback
machine.Configure(State.Active)
    .OnEntry(async () => await DoSomethingAsync()); // Warning OSMES001

// ✅ Good - use sync callbacks only
machine.Configure(State.Active)
    .OnEntry(() => Console.WriteLine("Entered Active"));
```

### OSMES002: FireAsync in Callback Error

```csharp
// ❌ Bad - calling FireAsync from callback
machine.Configure(State.Active)
    .OnEntry(() => FireAsync(Trigger.Next)); // Error OSMES002

// ✅ Good - fire triggers from grain methods
public async Task ProcessAsync()
{
    await FireAsync(Trigger.Start);
    // Callback runs here synchronously
    await FireAsync(Trigger.Next); // After callback completes
}
```

### OSMES009: Missing Initial State Error

```csharp
// ❌ Bad - no initial state
protected override StateMachine<State, Trigger> BuildStateMachine()
{
    var machine = new StateMachine<State, Trigger>();
    // Error OSMES009: Missing initial state
    return machine;
}

// ✅ Good - initial state specified
protected override StateMachine<State, Trigger> BuildStateMachine()
{
    var machine = new StateMachine<State, Trigger>(State.Idle);
    return machine;
}
```

## Documentation

Detailed documentation for each analyzer:
- [OSMES006 - Unhandled Triggers](https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES006.md)
- [OSMES007 - Circular Transitions](https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES007.md)
- [OSMES008 - Guard Complexity](https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES008.md)
- [OSMES009 - Missing Initial State](https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES009.md)
- [OSMES010 - Invalid Enum Values](https://github.com/mivertowski/Orleans.StateMachineES/docs/analyzers/OSMES010.md)

## Requirements

- .NET Standard 2.0 (analyzer)
- Compatible with .NET 6.0+, .NET 9.0+
- Visual Studio 2019 16.3+ or VS Code with C# Dev Kit

## Main Package

```bash
dotnet add package Orleans.StateMachineES
```

## Support

- [GitHub Issues](https://github.com/mivertowski/Orleans.StateMachineES/issues)
- [Main Documentation](https://github.com/mivertowski/Orleans.StateMachineES)

## License

MIT License - see LICENSE file for details
