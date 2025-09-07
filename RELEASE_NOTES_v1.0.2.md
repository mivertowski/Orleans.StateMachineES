# Orleans.StateMachineES v1.0.2 Release Notes

## 🎯 Overview

Version 1.0.2 addresses critical user feedback regarding async operations in state machine callbacks. This release introduces compile-time safety through Roslyn analyzers, runtime validation, and comprehensive documentation to prevent common pitfalls when working with async operations in Stateless state machines.

## 🚀 New Features

### Compile-Time Safety with Roslyn Analyzers

Two new analyzers provide immediate feedback during development:

- **OSMES001**: Warns about async lambdas in state callbacks (OnEntry, OnExit)
- **OSMES002**: Errors on FireAsync calls within callbacks

These analyzers catch common mistakes at compile time, preventing runtime issues before they occur.

### Runtime Validation

- Added thread-local state tracking to detect callback execution context
- `FireAsync` methods now throw `InvalidOperationException` if called from within callbacks
- Clear, actionable error messages guide developers to correct patterns

### Enhanced Error Messages

- **Event Replay Failures**: Now include event index, state transition details, and timestamps
- **Invalid State Transitions**: Display permitted triggers and current state context
- **Callback Violations**: Provide clear guidance on moving logic to grain methods

### Comprehensive Documentation

- New `ASYNC_PATTERNS.md` guide with:
  - Clear examples of what NOT to do
  - Multiple correct implementation patterns
  - Complete workflow examples
  - Best practices for async operations

## 🔧 Improvements

### Developer Experience
- Prominent warnings in README about Stateless async limitations
- Compile-time analyzers prevent common mistakes
- Runtime validation catches issues with clear error messages
- Detailed documentation helps developers understand proper patterns

### Error Handling
- `EventReplayException` now includes event context and failure details
- All error messages provide actionable guidance
- Stack traces include relevant state machine context

## 📦 Breaking Changes

None. All changes are backward compatible.

## 🐛 Bug Fixes

- Fixed duplicate `EventReplayException` class definition
- Resolved analyzer warning format issues
- Fixed test compilation errors with proper namespace usage

## 📚 Documentation

- Added comprehensive [Async Patterns Guide](docs/ASYNC_PATTERNS.md)
- Updated README with async operation warnings
- Added analyzer documentation references

## 🔍 Technical Details

### Why These Changes?

The underlying Stateless library doesn't support async operations in callbacks by design. This creates confusion for developers expecting async/await to work everywhere. Our solution provides multiple layers of protection:

1. **Compile-time**: Analyzers catch issues during development
2. **Runtime**: Validation prevents incorrect usage
3. **Documentation**: Clear guidance on correct patterns

### Implementation Approach

- Thread-local storage tracks callback execution context
- Roslyn analyzers use syntax tree analysis for pattern detection
- Error messages include comprehensive context for debugging

## 📈 Migration Guide

No migration required. Version 1.0.2 is fully backward compatible with 1.0.x.

To benefit from the new analyzers:
1. Update to Orleans.StateMachineES 1.0.2
2. Rebuild your solution
3. Address any new warnings/errors in your code
4. Follow patterns in the [Async Patterns Guide](docs/ASYNC_PATTERNS.md)

## 🙏 Acknowledgments

Special thanks to our users who provided detailed feedback about async operation challenges. Your input directly shaped these improvements.

## 📊 Stats

- **Analyzers Added**: 2
- **Documentation Pages**: 1 comprehensive guide
- **Error Messages Enhanced**: 3 categories
- **Test Coverage**: Maintained at 98%

## 🔗 Links

- [GitHub Release](https://github.com/mivertowski/Orleans.StateMachineES/releases/tag/v1.0.2)
- [NuGet Package](https://www.nuget.org/packages/Orleans.StateMachineES/1.0.2)
- [Async Patterns Guide](https://github.com/mivertowski/Orleans.StateMachineES/blob/main/docs/ASYNC_PATTERNS.md)

---

**Full Changelog**: https://github.com/mivertowski/Orleans.StateMachineES/compare/v1.0.1...v1.0.2