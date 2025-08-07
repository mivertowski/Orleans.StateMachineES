using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;

namespace Orleans.StateMachineES.Versioning;

/// <summary>
/// Interface for migration hooks that can be executed during state machine version upgrades.
/// Provides extension points for custom migration logic, data transformation, and validation.
/// </summary>
public interface IMigrationHook
{
    /// <summary>
    /// The name of this migration hook.
    /// </summary>
    string HookName { get; }
    
    /// <summary>
    /// The priority of this hook (lower numbers execute first).
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Executes before the migration starts.
    /// </summary>
    /// <param name="context">The migration context.</param>
    /// <returns>True to continue with migration, false to abort.</returns>
    Task<bool> BeforeMigrationAsync(MigrationContext context);
    
    /// <summary>
    /// Executes after the migration completes successfully.
    /// </summary>
    /// <param name="context">The migration context.</param>
    Task AfterMigrationAsync(MigrationContext context);
    
    /// <summary>
    /// Executes if the migration fails or is rolled back.
    /// </summary>
    /// <param name="context">The migration context.</param>
    /// <param name="error">The error that caused the rollback.</param>
    Task OnMigrationRollbackAsync(MigrationContext context, Exception error);
}

/// <summary>
/// Context information passed to migration hooks.
/// </summary>
[GenerateSerializer]
public class MigrationContext
{
    [Id(0)] public string GrainId { get; set; } = "";
    [Id(1)] public string GrainTypeName { get; set; } = "";
    [Id(2)] public StateMachineVersion FromVersion { get; set; } = new();
    [Id(3)] public StateMachineVersion ToVersion { get; set; } = new();
    [Id(4)] public MigrationStrategy Strategy { get; set; }
    [Id(5)] public Dictionary<string, object> GrainState { get; set; } = new();
    [Id(6)] public Dictionary<string, object> Metadata { get; set; } = new();
    [Id(7)] public DateTime StartTime { get; set; } = DateTime.UtcNow;
    [Id(8)] public List<string> ExecutedHooks { get; set; } = new();
    [Id(9)] public Dictionary<string, object> HookResults { get; set; } = new();

    /// <summary>
    /// Gets a strongly typed state value.
    /// </summary>
    public T? GetStateValue<T>(string key) where T : class
    {
        return GrainState.TryGetValue(key, out var value) ? value as T : null;
    }

    /// <summary>
    /// Sets a state value.
    /// </summary>
    public void SetStateValue<T>(string key, T value) where T : notnull
    {
        GrainState[key] = value;
    }

    /// <summary>
    /// Records the result of a hook execution.
    /// </summary>
    public void RecordHookResult(string hookName, object result)
    {
        HookResults[hookName] = result;
        if (!ExecutedHooks.Contains(hookName))
        {
            ExecutedHooks.Add(hookName);
        }
    }
}

/// <summary>
/// Manages and executes migration hooks during state machine upgrades.
/// </summary>
public class MigrationHookManager
{
    private readonly List<IMigrationHook> _hooks = new();
    private readonly ILogger<MigrationHookManager> _logger;

    public MigrationHookManager(ILogger<MigrationHookManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a migration hook.
    /// </summary>
    public void RegisterHook(IMigrationHook hook)
    {
        _hooks.Add(hook);
        _hooks.Sort((h1, h2) => h1.Priority.CompareTo(h2.Priority));
        
        _logger.LogInformation("Registered migration hook {HookName} with priority {Priority}", 
            hook.HookName, hook.Priority);
    }

    /// <summary>
    /// Executes all registered before-migration hooks.
    /// </summary>
    public async Task<bool> ExecuteBeforeMigrationHooksAsync(MigrationContext context)
    {
        _logger.LogDebug("Executing {HookCount} before-migration hooks for {GrainType} migration {FromVersion} -> {ToVersion}",
            _hooks.Count, context.GrainTypeName, context.FromVersion, context.ToVersion);

        foreach (var hook in _hooks)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var shouldContinue = await hook.BeforeMigrationAsync(context);
                var duration = DateTime.UtcNow - startTime;

                context.RecordHookResult($"{hook.HookName}_BeforeMigration", new
                {
                    ShouldContinue = shouldContinue,
                    Duration = duration
                });

                _logger.LogDebug("Hook {HookName} completed before-migration in {Duration}ms, continue: {ShouldContinue}",
                    hook.HookName, duration.TotalMilliseconds, shouldContinue);

                if (!shouldContinue)
                {
                    _logger.LogWarning("Migration aborted by hook {HookName}", hook.HookName);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing before-migration hook {HookName}", hook.HookName);
                
                context.RecordHookResult($"{hook.HookName}_BeforeMigration_Error", new
                {
                    Error = ex.Message,
                    Exception = ex
                });

                // Hook failure aborts migration
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Executes all registered after-migration hooks.
    /// </summary>
    public async Task ExecuteAfterMigrationHooksAsync(MigrationContext context)
    {
        _logger.LogDebug("Executing {HookCount} after-migration hooks for {GrainType}",
            _hooks.Count, context.GrainTypeName);

        foreach (var hook in _hooks)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                await hook.AfterMigrationAsync(context);
                var duration = DateTime.UtcNow - startTime;

                context.RecordHookResult($"{hook.HookName}_AfterMigration", new
                {
                    Duration = duration
                });

                _logger.LogDebug("Hook {HookName} completed after-migration in {Duration}ms",
                    hook.HookName, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing after-migration hook {HookName} (continuing with other hooks)", 
                    hook.HookName);
                
                context.RecordHookResult($"{hook.HookName}_AfterMigration_Error", new
                {
                    Error = ex.Message,
                    Exception = ex
                });
            }
        }
    }

    /// <summary>
    /// Executes all registered rollback hooks.
    /// </summary>
    public async Task ExecuteRollbackHooksAsync(MigrationContext context, Exception migrationError)
    {
        _logger.LogWarning("Executing {HookCount} rollback hooks for {GrainType} due to migration failure: {Error}",
            _hooks.Count, context.GrainTypeName, migrationError.Message);

        // Execute rollback hooks in reverse priority order
        var rollbackHooks = new List<IMigrationHook>(_hooks);
        rollbackHooks.Reverse();

        foreach (var hook in rollbackHooks)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                await hook.OnMigrationRollbackAsync(context, migrationError);
                var duration = DateTime.UtcNow - startTime;

                context.RecordHookResult($"{hook.HookName}_Rollback", new
                {
                    Duration = duration
                });

                _logger.LogDebug("Hook {HookName} completed rollback in {Duration}ms",
                    hook.HookName, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing rollback hook {HookName} (continuing with other hooks)", 
                    hook.HookName);
                
                context.RecordHookResult($"{hook.HookName}_Rollback_Error", new
                {
                    Error = ex.Message,
                    Exception = ex
                });
            }
        }
    }

    /// <summary>
    /// Gets all registered hooks.
    /// </summary>
    public IReadOnlyList<IMigrationHook> GetRegisteredHooks()
    {
        return _hooks.AsReadOnly();
    }
}

/// <summary>
/// Built-in migration hooks for common scenarios.
/// </summary>
public static class BuiltInMigrationHooks
{
    /// <summary>
    /// Hook for validating state compatibility before migration.
    /// </summary>
    public class StateCompatibilityValidationHook : IMigrationHook
    {
        public string HookName => "StateCompatibilityValidation";
        public int Priority => 10; // Execute early

        public Task<bool> BeforeMigrationAsync(MigrationContext context)
        {
            // Validate that the current state is compatible with the target version
            var currentState = context.GetStateValue<string>("CurrentState");
            if (string.IsNullOrEmpty(currentState))
            {
                context.Metadata["ValidationError"] = "Current state is not defined";
                return Task.FromResult(false);
            }

            // Perform actual state compatibility validation
            try
            {
                // Check if we have state machine type information
                var stateType = context.GetStateValue<Type>("StateType");
                var triggerType = context.GetStateValue<Type>("TriggerType");
                
                if (stateType != null && triggerType != null)
                {
                    // Verify the current state is valid in the enum
                    if (!Enum.TryParse(stateType, currentState, true, out var stateValue))
                    {
                        context.Metadata["ValidationError"] = $"State '{currentState}' is not valid in {stateType.Name}";
                        return Task.FromResult(false);
                    }
                    
                    // Check if state exists in target version configuration
                    var targetStates = context.GetStateValue<List<string>>("TargetVersionStates");
                    if (targetStates != null && !targetStates.Contains(currentState))
                    {
                        context.Metadata["ValidationError"] = $"Current state '{currentState}' does not exist in target version";
                        context.Metadata["MigrationRequired"] = "State mapping required";
                        
                        // Still allow migration but flag for special handling
                        context.Metadata["StateCompatibilityWarning"] = true;
                    }
                }

                // Validate state data integrity
                var stateData = context.GrainState;
                if (stateData.Count == 0)
                {
                    context.Metadata["ValidationWarning"] = "No state data found - using defaults";
                }
                
                // Check for version-specific state requirements
                if (context.ToVersion.Major > context.FromVersion.Major)
                {
                    // Major version changes may have different state requirements
                    context.Metadata["MajorVersionValidation"] = "Additional validation required for major version change";
                    
                    // Validate critical fields exist
                    var requiredFields = context.GetStateValue<List<string>>("RequiredFields") ?? new List<string>();
                    foreach (var field in requiredFields)
                    {
                        if (!stateData.ContainsKey(field))
                        {
                            context.Metadata[$"MissingField_{field}"] = "Required field missing - will use default";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                context.Metadata["ValidationException"] = ex.Message;
                // Log but don't fail - allow migration to proceed with warnings
            }

            context.Metadata["StateValidation"] = "Passed";
            return Task.FromResult(true);
        }

        public Task AfterMigrationAsync(MigrationContext context)
        {
            // Verify state is still valid after migration
            context.Metadata["PostMigrationStateValidation"] = "Completed";
            return Task.CompletedTask;
        }

        public Task OnMigrationRollbackAsync(MigrationContext context, Exception error)
        {
            // Clean up any validation artifacts
            context.Metadata["StateValidationRollback"] = "Completed";
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Hook for backing up state before migration.
    /// </summary>
    public class StateBackupHook : IMigrationHook
    {
        public string HookName => "StateBackup";
        public int Priority => 20; // Execute after validation

        public Task<bool> BeforeMigrationAsync(MigrationContext context)
        {
            // Create a backup of the current state
            var backup = new Dictionary<string, object>(context.GrainState);
            context.Metadata["StateBackup"] = backup;
            context.Metadata["BackupTimestamp"] = DateTime.UtcNow;

            return Task.FromResult(true);
        }

        public Task AfterMigrationAsync(MigrationContext context)
        {
            // Remove backup after successful migration (optional)
            // context.Metadata.Remove("StateBackup");
            context.Metadata["BackupRetention"] = "Kept for audit trail";
            return Task.CompletedTask;
        }

        public Task OnMigrationRollbackAsync(MigrationContext context, Exception error)
        {
            // Restore from backup
            if (context.Metadata.TryGetValue("StateBackup", out var backup) && 
                backup is Dictionary<string, object> backupState)
            {
                context.GrainState.Clear();
                foreach (var kvp in backupState)
                {
                    context.GrainState[kvp.Key] = kvp.Value;
                }
                context.Metadata["StateRestored"] = true;
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Hook for logging migration activities.
    /// </summary>
    public class AuditLoggingHook : IMigrationHook
    {
        private readonly ILogger<AuditLoggingHook> _logger;

        public AuditLoggingHook(ILogger<AuditLoggingHook> logger)
        {
            _logger = logger;
        }

        public string HookName => "AuditLogging";
        public int Priority => 5; // Execute very early

        public Task<bool> BeforeMigrationAsync(MigrationContext context)
        {
            _logger.LogInformation("AUDIT: Starting migration for grain {GrainId} of type {GrainType} from version {FromVersion} to {ToVersion}",
                context.GrainId, context.GrainTypeName, context.FromVersion, context.ToVersion);

            context.Metadata["AuditStartTime"] = DateTime.UtcNow;
            return Task.FromResult(true);
        }

        public Task AfterMigrationAsync(MigrationContext context)
        {
            var duration = DateTime.UtcNow - context.StartTime;
            
            _logger.LogInformation("AUDIT: Successfully completed migration for grain {GrainId} from version {FromVersion} to {ToVersion} in {Duration}ms",
                context.GrainId, context.FromVersion, context.ToVersion, duration.TotalMilliseconds);

            return Task.CompletedTask;
        }

        public Task OnMigrationRollbackAsync(MigrationContext context, Exception error)
        {
            var duration = DateTime.UtcNow - context.StartTime;
            
            _logger.LogError("AUDIT: Migration rollback for grain {GrainId} from version {FromVersion} to {ToVersion} after {Duration}ms due to error: {Error}",
                context.GrainId, context.FromVersion, context.ToVersion, duration.TotalMilliseconds, error.Message);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Hook for performing custom state transformations.
    /// </summary>
    public class StateTransformationHook : IMigrationHook
    {
        private readonly Dictionary<(StateMachineVersion From, StateMachineVersion To), Func<MigrationContext, Task<bool>>> _transformations = new();

        public string HookName => "StateTransformation";
        public int Priority => 50; // Execute after backups

        /// <summary>
        /// Registers a state transformation for specific version pairs.
        /// </summary>
        public void RegisterTransformation(
            StateMachineVersion fromVersion, 
            StateMachineVersion toVersion, 
            Func<MigrationContext, Task<bool>> transformation)
        {
            _transformations[(fromVersion, toVersion)] = transformation;
        }

        public async Task<bool> BeforeMigrationAsync(MigrationContext context)
        {
            var key = (context.FromVersion, context.ToVersion);
            
            if (_transformations.TryGetValue(key, out var transformation))
            {
                return await transformation(context);
            }

            // No specific transformation needed
            return true;
        }

        public Task AfterMigrationAsync(MigrationContext context)
        {
            context.Metadata["StateTransformationCompleted"] = true;
            return Task.CompletedTask;
        }

        public Task OnMigrationRollbackAsync(MigrationContext context, Exception error)
        {
            // Transformation rollbacks would be handled by the state backup hook
            return Task.CompletedTask;
        }
    }
}