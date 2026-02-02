# Installation

This guide walks you through installing Orleans.StateMachineES in your project.

## Package Overview

Orleans.StateMachineES consists of three NuGet packages:

| Package | Purpose | Required |
|---------|---------|----------|
| **Orleans.StateMachineES** | Main library with state machine grains | Yes |
| **Orleans.StateMachineES.Abstractions** | Core interfaces (auto-included) | Auto |
| **Orleans.StateMachineES.Generators** | Roslyn analyzers for compile-time safety | Recommended |

## Installation Methods

### Using .NET CLI (Recommended)

```bash
# Install main library
dotnet add package Orleans.StateMachineES

# Install analyzers (recommended)
dotnet add package Orleans.StateMachineES.Generators
```

### Using Package Manager Console

```powershell
Install-Package Orleans.StateMachineES
Install-Package Orleans.StateMachineES.Generators
```

### Using Visual Studio

1. Right-click your project in Solution Explorer
2. Select **Manage NuGet Packages**
3. Search for `Orleans.StateMachineES`
4. Click **Install** on both packages

### Manual Package Reference

Add to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Orleans.StateMachineES" Version="1.1.0" />
  <PackageReference Include="Orleans.StateMachineES.Generators" Version="1.1.0" />
</ItemGroup>
```

## Version Compatibility

| Orleans.StateMachineES | .NET Version | Orleans Version |
|------------------------|--------------|-----------------|
| 1.0.x | .NET 9.0+ | 9.1.0+ |

## Verify Installation

After installation, verify the packages are correctly referenced:

```bash
dotnet list package
```

You should see:

```
Project 'YourProject' has the following package references
   [net9.0]:
   Top-level Package                           Requested   Resolved
   > Orleans.StateMachineES                    1.1.0       1.1.0
   > Orleans.StateMachineES.Generators         1.1.0       1.1.0
```

## Analyzer Configuration

The Roslyn analyzers are enabled by default. To configure them, add to your `.editorconfig` or `.globalconfig`:

```ini
# Treat async lambda warnings as errors in production
dotnet_diagnostic.OSMES001.severity = error

# Customize severity for specific analyzers
dotnet_diagnostic.OSMES004.severity = suggestion  # Unreachable states
dotnet_diagnostic.OSMES005.severity = none        # Multiple entry callbacks
```

See the [Analyzers Guide](../guides/analyzers.md) for complete configuration options.

## Project Setup

### Basic Orleans Setup

Ensure your Orleans silo is configured. In your `Program.cs`:

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans(siloBuilder =>
    {
        siloBuilder.UseLocalhostClustering();
        // Add other Orleans configuration
    });

var host = builder.Build();
await host.RunAsync();
```

### Enable Event Sourcing (Optional)

If you plan to use event sourcing, configure a storage provider:

```csharp
siloBuilder.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();

    // Add storage for event sourcing
    siloBuilder.AddMemoryGrainStorage("EventStore");

    // Or use Azure Table Storage
    siloBuilder.AddAzureTableGrainStorage("EventStore", options =>
    {
        options.ConfigureTableServiceClient("UseDevelopmentStorage=true");
    });
});
```

## Troubleshooting

### Missing Abstractions Package

If you see errors about missing `IStateMachineGrain<,>`:

```bash
dotnet restore
dotnet build
```

The Abstractions package should be automatically restored as a dependency.

### Analyzer Not Working

If analyzer warnings don't appear:

1. Clean and rebuild:
   ```bash
   dotnet clean
   dotnet build
   ```

2. Check analyzer status in Visual Studio:
   - Go to **Dependencies** → **Analyzers** → **Orleans.StateMachineES.Generators**
   - Ensure analyzers are listed and not disabled

3. Restart your IDE

### Version Conflicts

If you encounter version conflicts with Orleans packages:

```bash
# Check all package versions
dotnet list package --include-transitive

# Update all packages to compatible versions
dotnet add package Microsoft.Orleans.Sdk --version 9.1.2
```

## Next Steps

Now that you have Orleans.StateMachineES installed:

1. [Create your first state machine →](first-state-machine.md)
2. Learn [core concepts](core-concepts.md)
3. Explore [examples](../examples/index.md)

## Additional Resources

- [NuGet Package Page](https://www.nuget.org/packages/Orleans.StateMachineES)
- [GitHub Repository](https://github.com/mivertowski/Orleans.StateMachineES)
- [Release Notes](../reference/release-notes.md)
- [Migration Guide](../reference/migration-guide.md) (for upgrading from older versions)
