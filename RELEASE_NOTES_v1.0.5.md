# Orleans.StateMachineES v1.0.5 Release Notes

## 🔧 Critical Bug Fix Release

Version 1.0.5 addresses a critical bug that could cause rare race conditions and unpredictable behavior in production Orleans applications. This release removes all `ConfigureAwait(false)` calls from grain code to maintain Orleans' single-threaded execution model guarantees.

## 🐛 Bug Fixes

### Critical: Orleans Task Scheduler Compliance (Issue #5)

**Problem**: Using `ConfigureAwait(false)` in Orleans grain code violates Orleans' programming model by allowing async continuations to run on arbitrary thread pool threads instead of through Orleans' task scheduler. This breaks the single-threaded execution guarantee that Orleans provides, potentially causing:
- Race conditions when accessing grain state
- Concurrent modification of grain fields
- Unpredictable behavior in multi-step operations
- Thread safety violations in event sourcing scenarios

**Solution**: Removed all 42 occurrences of `ConfigureAwait(false)` from grain code across the entire codebase:

#### StateMachineGrain.cs (7 instances removed)
- `ActivateAsync()` - Line 63
- `DeactivateAsync()` - Line 71
- `FireAsync()` - Line 81
- `FireAsync<TArg0>()` - Line 91
- `FireAsync<TArg0, TArg1>()` - Line 101
- `FireAsync<TArg0, TArg1, TArg2>()` - Line 111
- `OnActivateAsync()` - Line 255

#### EventSourcedStateMachineGrain.cs (25 instances removed)
- `ActivateAsync()` and `DeactivateAsync()` methods
- All 4 `FireAsync()` overloads with semaphore synchronization
- `RecordTransitionEvent()` - Event persistence and snapshot operations
- `PublishToStreamAsync()` - Orleans Streams integration
- `OnActivateAsync()` and `InitializeStateMachineAsync()` - Activation lifecycle
- `ReplayEventsAsync()` - Event sourcing replay logic
- `OnDeactivateAsync()` - Cleanup and final snapshot

#### Versioning Components (10 instances removed)
- **VersionCompatibilityChecker.cs** (6) - Service methods callable from grain context
- **MigrationPathCalculator.cs** (2) - Migration path calculation
- **CompatibilityRulesEngine.cs** (2) - Compatibility rule evaluation

**Impact**: This fix ensures that all async operations in grain code properly flow through Orleans' execution context, maintaining thread safety and preventing race conditions.

**Credit**: Thank you to [@zbarrier](https://github.com/zbarrier) for identifying and reporting this issue in [#5](https://github.com/mivertowski/Orleans.StateMachineES/issues/5).

## 🔄 Migration Guide

### From v1.0.4 to v1.0.5

**This is a critical bug fix with no breaking changes.** Simply update your package references:

```xml
<PackageReference Include="Orleans.StateMachineES" Version="1.0.5" />
<PackageReference Include="Orleans.StateMachineES.Abstractions" Version="1.0.5" />
<PackageReference Include="Orleans.StateMachineES.Generators" Version="1.0.5" />
```

**Recommended Action**: Update immediately, especially if you're experiencing:
- Intermittent state inconsistencies
- Race conditions in production
- Unexplained concurrency issues
- Event sourcing replay errors

No code changes required in your applications.

## 📋 Technical Details

### Why ConfigureAwait(false) is Dangerous in Orleans

Orleans grains are **turn-based** - they process one request at a time on a single logical thread. This is enforced by Orleans' task scheduler, which ensures:
1. Grain activations run on a consistent execution context
2. No two operations on the same grain run concurrently
3. Grain state can be accessed without locks

When you use `ConfigureAwait(false)`:
- The async continuation can resume on a **different thread** from the thread pool
- Orleans loses control of the execution context
- Multiple operations may run **concurrently** on the same grain
- This violates Orleans' single-threaded guarantee

### Example of the Problem

```csharp
// BEFORE (Broken):
public async Task ProcessOrderAsync(Order order)
{
    await ValidateOrderAsync().ConfigureAwait(false);
    // ⚠️ This line might run on a different thread!
    // Another request could be modifying _orderTotal concurrently!
    _orderTotal += order.Amount;
}

// AFTER (Fixed):
public async Task ProcessOrderAsync(Order order)
{
    await ValidateOrderAsync();
    // ✅ This line runs on the Orleans scheduler
    // No concurrent access possible
    _orderTotal += order.Amount;
}
```

## 📊 Statistics

- **Files Modified**: 5
- **ConfigureAwait(false) Removed**: 42 instances
  - StateMachineGrain: 7
  - EventSourcedStateMachineGrain: 25
  - Versioning components: 10
- **Breaking Changes**: 0
- **Performance Impact**: Negligible (Orleans scheduler optimizations apply)

## 🔍 Verification

All existing tests pass without modification, confirming:
- No functional changes to behavior
- Maintains all existing functionality
- Backward compatible with v1.0.4

## 📦 Package Updates

- **Orleans.StateMachineES**: 1.0.5
- **Orleans.StateMachineES.Abstractions**: 1.0.5
- **Orleans.StateMachineES.Generators**: 1.0.5

All packages maintain synchronized versioning.

## 📦 Installation

```bash
dotnet add package Orleans.StateMachineES --version 1.0.5
dotnet add package Orleans.StateMachineES.Generators --version 1.0.5
```

## 📚 Resources

### Orleans Best Practices
- [Orleans Documentation on Async/Await](https://learn.microsoft.com/en-us/dotnet/orleans/grains/grain-communications)
- Orleans grains should **never** use `ConfigureAwait(false)`
- Always let async operations flow through Orleans' execution context

### Project Documentation
- [CHANGELOG.md](CHANGELOG.md) - Full change history
- [GitHub Issue #5](https://github.com/mivertowski/Orleans.StateMachineES/issues/5) - Original bug report
- [Main Documentation](README.md)

## 🔗 Links

- [NuGet Package](https://www.nuget.org/packages/Orleans.StateMachineES/1.0.5)
- [GitHub Repository](https://github.com/mivertowski/Orleans.StateMachineES)
- [Issue Tracker](https://github.com/mivertowski/Orleans.StateMachineES/issues)

## 🙏 Acknowledgments

Special thanks to:
- **@zbarrier** for identifying and reporting this critical issue
- The Orleans community for maintaining excellent documentation on grain best practices

---

**Full Changelog**: [v1.0.4...v1.0.5](https://github.com/mivertowski/Orleans.StateMachineES/compare/v1.0.4...v1.0.5)
