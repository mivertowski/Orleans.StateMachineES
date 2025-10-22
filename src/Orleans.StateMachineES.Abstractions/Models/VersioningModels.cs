namespace Orleans.StateMachineES.Abstractions.Models;

/// <summary>
/// Represents a semantic version for state machines.
/// </summary>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Abstractions.Models.StateMachineVersion")]
public sealed record StateMachineVersion : IComparable<StateMachineVersion>
{
    /// <summary>
    /// Gets the major version number.
    /// </summary>
    [Id(0)]
    public int Major { get; init; }

    /// <summary>
    /// Gets the minor version number.
    /// </summary>
    [Id(1)]
    public int Minor { get; init; }

    /// <summary>
    /// Gets the patch version number.
    /// </summary>
    [Id(2)]
    public int Patch { get; init; }

    /// <summary>
    /// Gets the pre-release identifier.
    /// </summary>
    [Id(3)]
    public string? PreRelease { get; init; }

    /// <summary>
    /// Gets the build metadata.
    /// </summary>
    [Id(4)]
    public string? Build { get; init; }

    /// <summary>
    /// Initializes a new instance of the StateMachineVersion record.
    /// </summary>
    /// <param name="major">The major version number.</param>
    /// <param name="minor">The minor version number.</param>
    /// <param name="patch">The patch version number.</param>
    /// <param name="preRelease">The pre-release identifier.</param>
    /// <param name="build">The build metadata.</param>
    public StateMachineVersion(int major, int minor, int patch, string? preRelease = null, string? build = null)
    {
        Major = major >= 0 ? major : throw new ArgumentOutOfRangeException(nameof(major));
        Minor = minor >= 0 ? minor : throw new ArgumentOutOfRangeException(nameof(minor));
        Patch = patch >= 0 ? patch : throw new ArgumentOutOfRangeException(nameof(patch));
        PreRelease = preRelease;
        Build = build;
    }

    /// <summary>
    /// Compares this version to another version.
    /// </summary>
    /// <param name="other">The other version to compare to.</param>
    /// <returns>A value indicating the relative order of the versions.</returns>
    public int CompareTo(StateMachineVersion? other)
    {
        if (other is null) return 1;

        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0) return majorComparison;

        var minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0) return minorComparison;

        var patchComparison = Patch.CompareTo(other.Patch);
        if (patchComparison != 0) return patchComparison;

        // Pre-release versions have lower precedence than normal versions
        if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
            return 1;
        if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease))
            return -1;

        return string.Compare(PreRelease, other.PreRelease, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a version string into a StateMachineVersion.
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <returns>The parsed version.</returns>
    /// <exception cref="ArgumentException">Thrown when the version string is invalid.</exception>
    public static StateMachineVersion Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version string cannot be null or empty.", nameof(version));

        var parts = version.Split('-', '+');
        var versionPart = parts[0];
        var preRelease = parts.Length > 1 && !parts[1].Contains('+') ? parts[1] : null;
        var build = parts.Length > 1 && parts[^1].Contains('+') ? parts[^1] : null;

        var versionNumbers = versionPart.Split('.');
        if (versionNumbers.Length != 3)
            throw new ArgumentException("Version must be in format major.minor.patch", nameof(version));

        if (!int.TryParse(versionNumbers[0], out var major) ||
            !int.TryParse(versionNumbers[1], out var minor) ||
            !int.TryParse(versionNumbers[2], out var patch))
        {
            throw new ArgumentException("Version numbers must be valid integers.", nameof(version));
        }

        return new StateMachineVersion(major, minor, patch, preRelease, build);
    }

    /// <summary>
    /// Determines whether one version is greater than another.
    /// </summary>
    public static bool operator >(StateMachineVersion left, StateMachineVersion right)
    {
        return left?.CompareTo(right) > 0;
    }

    /// <summary>
    /// Determines whether one version is less than another.
    /// </summary>
    public static bool operator <(StateMachineVersion left, StateMachineVersion right)
    {
        return left?.CompareTo(right) < 0;
    }

    /// <summary>
    /// Determines whether one version is greater than or equal to another.
    /// </summary>
    public static bool operator >=(StateMachineVersion left, StateMachineVersion right)
    {
        return left?.CompareTo(right) >= 0;
    }

    /// <summary>
    /// Determines whether one version is less than or equal to another.
    /// </summary>
    public static bool operator <=(StateMachineVersion left, StateMachineVersion right)
    {
        return left?.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Determines if this version is compatible with another version.
    /// Minor and patch version increases are considered compatible.
    /// Major version changes are not compatible.
    /// </summary>
    /// <param name="other">The version to check compatibility with.</param>
    /// <returns>True if versions are compatible, false otherwise.</returns>
    public bool IsCompatibleWith(StateMachineVersion other)
    {
        if (other == null) return false;
        
        // Same major version is compatible
        if (Major == other.Major)
            return true;
            
        // Older major versions are not compatible with newer ones
        return false;
    }

    /// <summary>
    /// Returns the string representation of this version.
    /// </summary>
    /// <returns>The string representation.</returns>
    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";
        if (!string.IsNullOrEmpty(PreRelease))
            version += $"-{PreRelease}";
        if (!string.IsNullOrEmpty(Build))
            version += $"+{Build}";
        return version;
    }
}

/// <summary>
/// Migration effort level.
/// </summary>
public enum MigrationEffort
{
    /// <summary>
    /// Low effort required.
    /// </summary>
    Low,
    
    /// <summary>
    /// Medium effort required.
    /// </summary>
    Medium,
    
    /// <summary>
    /// High effort required.
    /// </summary>
    High
}

/// <summary>
/// Risk levels for migrations.
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// Low risk migration.
    /// </summary>
    Low = 0,
    
    /// <summary>
    /// Medium risk migration.
    /// </summary>
    Medium = 1,
    
    /// <summary>
    /// High risk migration.
    /// </summary>
    High = 2,
    
    /// <summary>
    /// Critical risk migration.
    /// </summary>
    Critical = 3,
    
    /// <summary>
    /// Very high risk migration.
    /// </summary>
    VeryHigh = 4
}

/// <summary>
/// Level of version compatibility.
/// </summary>
public enum VersionCompatibilityLevel
{
    /// <summary>
    /// Versions are fully compatible with no changes required.
    /// </summary>
    FullyCompatible,
    
    /// <summary>
    /// Versions are compatible with minor adjustments.
    /// </summary>
    Compatible,
    
    /// <summary>
    /// Versions are partially compatible with some limitations.
    /// </summary>
    PartiallyCompatible,
    
    /// <summary>
    /// Migration is required for compatibility.
    /// </summary>
    RequiresMigration,
    
    /// <summary>
    /// Versions are incompatible.
    /// </summary>
    Incompatible
}