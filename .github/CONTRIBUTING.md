# Contributing to Orleans.StateMachineES

We love your input! We want to make contributing to Orleans.StateMachineES as easy and transparent as possible, whether it's:

- Reporting a bug
- Discussing the current state of the code
- Submitting a fix
- Proposing new features
- Becoming a maintainer

## 🚀 Quick Start for Contributors

1. **Fork** the repository
2. **Clone** your fork locally
3. **Create** a feature branch
4. **Make** your changes
5. **Test** thoroughly
6. **Submit** a pull request

## 📋 Development Process

### Prerequisites

- .NET 9.0 SDK or later
- Your favorite IDE (Visual Studio, Rider, VS Code)
- Git

### Setting Up the Development Environment

```bash
# Clone your fork
git clone https://github.com/your-username/Orleans.StateMachineES.git
cd Orleans.StateMachineES

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

### Project Structure

```
Orleans.StateMachineES/
├── src/
│   ├── Orleans.StateMachineES/           # Main library
│   └── Orleans.StateMachineES.Generators/ # Roslyn source generators
├── tests/
│   └── Orleans.StateMachineES.Tests/     # Test suite
├── examples/                             # Example applications
├── docs/                                 # Documentation
└── .github/                             # GitHub workflows and templates
```

## 🔍 Code Style and Standards

### C# Coding Standards

- Follow standard C# naming conventions
- Use nullable reference types (`#nullable enable`)
- Prefer `async/await` over `Task.Result` or `.Wait()`
- Use `ConfigureAwait(false)` in library code
- Write XML documentation for public APIs
- Keep files under 500 lines when possible

### Example:

```csharp
/// <summary>
/// Represents a state machine grain that can fire triggers asynchronously.
/// </summary>
/// <typeparam name="TState">The type of state in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of trigger in the state machine.</typeparam>
public abstract class StateMachineGrain<TState, TTrigger> : Grain, IStateMachineGrain<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    protected StateMachine<TState, TTrigger> StateMachine { get; private set; } = null!;

    /// <summary>
    /// Fires a trigger asynchronously and transitions the state machine.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    /// <returns>A task representing the async operation.</returns>
    public virtual async Task FireAsync(TTrigger trigger)
    {
        await StateMachine.FireAsync(trigger).ConfigureAwait(false);
    }
}
```

### Orleans-Specific Guidelines

- Use `IGrain` interfaces for all grain contracts
- Prefer `async Task` over `async void`
- Use `ILogger<T>` for logging
- Follow Orleans grain lifecycle patterns
- Use Orleans serialization attributes when needed

## 🧪 Testing Guidelines

### Testing Philosophy

- **Test-Driven Development**: Write tests before implementation when possible
- **Comprehensive Coverage**: Aim for 90%+ code coverage
- **Realistic Scenarios**: Test real-world usage patterns
- **Edge Cases**: Include error conditions and boundary cases

### Test Categories

1. **Unit Tests**: Test individual components in isolation
2. **Integration Tests**: Test components working together
3. **Orleans Tests**: Use `TestCluster` for grain testing
4. **Example Tests**: Ensure examples work correctly

### Testing Examples

```csharp
[Fact]
public async Task StateMachineGrain_FireTrigger_ShouldTransitionState()
{
    // Arrange
    using var host = await TestClusterApplication.StartAsync();
    var grain = host.Cluster.GrainFactory.GetGrain<ITestGrain>("test-id");

    // Act
    await grain.FireAsync(TestTrigger.Start);

    // Assert
    var state = await grain.GetStateAsync();
    state.Should().Be(TestState.Processing);
}
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov

# Run specific test class
dotnet test --filter "ClassName=BasicFunctionalityTests"

# Run tests with verbose output
dotnet test --verbosity normal
```

## 📝 Commit Guidelines

### Commit Message Format

We follow [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Types

- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, missing semi-colons, etc.)
- `refactor`: Code changes that neither fix bugs nor add features
- `perf`: Performance improvements
- `test`: Adding or modifying tests
- `chore`: Changes to build process or auxiliary tools

### Examples

```bash
feat(core): add support for parameterized triggers
fix(timer): resolve memory leak in timer disposal
docs(readme): update installation instructions
test(saga): add integration tests for saga orchestration
```

## 🐛 Bug Reports

Great bug reports tend to have:

- A quick summary and/or background
- Steps to reproduce
  - Be specific!
  - Give sample code if you can
- What you expected would happen
- What actually happens
- Notes (possibly including why you think this might be happening, or stuff you tried that didn't work)

Use our [bug report template](.github/ISSUE_TEMPLATE/bug_report.yml) for consistent reporting.

## 🚀 Feature Requests

We love feature ideas! Please use our [feature request template](.github/ISSUE_TEMPLATE/feature_request.yml) and include:

- **Problem**: What problem does this solve?
- **Solution**: How should it work?
- **Use Case**: Provide concrete examples
- **Alternatives**: What other approaches did you consider?

## 📚 Documentation

Documentation improvements are always welcome:

- API documentation (XML comments)
- Getting started guides
- Examples and tutorials
- Architecture documentation
- Troubleshooting guides

### Documentation Standards

- Use clear, concise language
- Include code examples that work
- Keep examples up to date
- Use proper markdown formatting
- Link to related concepts

## 🔄 Pull Request Process

1. **Branch**: Create a feature branch from `main`
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Develop**: Make your changes following our guidelines

3. **Test**: Ensure all tests pass and add new tests for your changes
   ```bash
   dotnet test
   dotnet build --verbosity normal  # Check for warnings
   ```

4. **Document**: Update documentation as needed

5. **Commit**: Follow our commit message guidelines

6. **Push**: Push your branch and create a pull request
   ```bash
   git push origin feature/your-feature-name
   ```

7. **Review**: Address feedback from maintainers

### Pull Request Checklist

- [ ] Tests pass locally
- [ ] No build warnings
- [ ] Documentation updated
- [ ] Follows coding standards
- [ ] Includes appropriate tests
- [ ] Commit messages are clear
- [ ] PR description explains the change

## 📋 Code Review Process

### What We Look For

- **Correctness**: Does it work as intended?
- **Performance**: Is it efficient?
- **Security**: Are there any vulnerabilities?
- **Maintainability**: Is it readable and well-structured?
- **Testing**: Is it adequately tested?
- **Documentation**: Is it properly documented?

### Review Timeline

- Small changes: 1-2 days
- Medium changes: 3-5 days  
- Large changes: 1-2 weeks

## 🌟 Recognition

Contributors will be recognized in:

- Release notes
- Contributors section of README
- GitHub contributor graphs

## 🤝 Community

- **Discussions**: Use GitHub Discussions for questions and ideas
- **Issues**: Report bugs and request features
- **Pull Requests**: Contribute code and documentation

## ⚡ Orleans.StateMachineES Specific Guidelines

### State Machine Design

- Keep state enums simple and descriptive
- Use meaningful trigger names
- Consider hierarchical states for complex workflows
- Use guards for conditional transitions
- Implement proper error handling

### Event Sourcing

- Events should be immutable
- Use descriptive event names
- Include all necessary data in events
- Version events for schema evolution

### Performance Considerations

- Minimize grain activations
- Use timers and reminders appropriately
- Consider state machine composition for large workflows
- Profile memory usage in long-running processes

### Example Applications

When adding examples:

- Include complete, working code
- Add appropriate documentation
- Follow real-world patterns
- Include error handling
- Provide clear README instructions

## 🚨 Security

### Reporting Security Issues

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please send an email to security@managedcode.com with:
- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Any suggested fixes

### Security Guidelines

- Never commit secrets or credentials
- Use secure defaults
- Validate all inputs
- Follow Orleans security best practices
- Consider distributed system security implications

## 📞 Getting Help

- **GitHub Discussions**: For questions and community help
- **GitHub Issues**: For bugs and feature requests
- **Documentation**: Check docs/ folder and README
- **Examples**: Look at examples/ folder for usage patterns

## 📄 License

By contributing, you agree that your contributions will be licensed under the same license as the project (MIT License).

---

## 🎉 Thank You!

Your contributions make Orleans.StateMachineES better for everyone. We appreciate your time and effort in helping improve this project!

**Happy coding!** 🚀