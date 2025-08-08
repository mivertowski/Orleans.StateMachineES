using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.StateMachineES;
using Orleans.StateMachineES.Orthogonal;
using Stateless;

namespace SmartHome;

/// <summary>
/// Smart home system with orthogonal regions for different subsystems.
/// Each region operates independently but can be synchronized.
/// </summary>
public interface ISmartHomeSystemGrain : IOrthogonalStateMachine<SmartHomeState, SmartHomeTrigger>, IGrainWithStringKey
{
    Task<SmartHomeStatus> GetFullStatusAsync();
    Task ActivateSecurityAsync();
    Task DeactivateSecurityAsync();
    Task SetClimateAutoAsync();
    Task SetEnergyEfficientModeAsync();
    Task ActivateVacationModeAsync();
    Task DeactivateVacationModeAsync();
}

public enum SmartHomeState
{
    // Main states
    SystemOff,
    SystemOn,
    SystemMaintenance,
    
    // Security region states
    SecurityDisarmed,
    SecurityArmedHome,
    SecurityArmedAway,
    SecurityAlarm,
    
    // Climate region states
    ClimateOff,
    ClimateHeating,
    ClimateCooling,
    ClimateAuto,
    ClimateEco,
    
    // Energy region states
    EnergyNormal,
    EnergyPeakDemand,
    EnergyNightRate,
    EnergySaving,
    
    // Presence region states
    PresenceHome,
    PresenceAway,
    PresenceVacation,
    PresenceSleep
}

public enum SmartHomeTrigger
{
    // System triggers
    PowerOn,
    PowerOff,
    Maintenance,
    Resume,
    
    // Security triggers
    ArmHome,
    ArmAway,
    Disarm,
    TriggerAlarm,
    ClearAlarm,
    
    // Climate triggers
    StartHeating,
    StartCooling,
    SetAuto,
    SetEco,
    StopClimate,
    
    // Energy triggers
    EnterPeakDemand,
    ExitPeakDemand,
    EnterNightRate,
    ExitNightRate,
    EnableSaving,
    DisableSaving,
    
    // Presence triggers
    ArriveHome,
    LeaveHome,
    GoToSleep,
    WakeUp,
    StartVacation,
    EndVacation,
    
    // Synchronized triggers (affect multiple regions)
    VacationMode,
    EmergencyMode,
    EcoMode
}

[StorageProvider(ProviderName = "Default")]
public class SmartHomeSystemGrain : OrthogonalStateMachineGrain<SmartHomeState, SmartHomeTrigger>, ISmartHomeSystemGrain
{
    private readonly ILogger<SmartHomeSystemGrain> _logger;
    
    public SmartHomeSystemGrain(ILogger<SmartHomeSystemGrain> logger)
    {
        _logger = logger;
    }
    
    protected override StateMachine<SmartHomeState, SmartHomeTrigger> BuildStateMachine()
    {
        // Main system state machine
        var machine = new StateMachine<SmartHomeState, SmartHomeTrigger>(SmartHomeState.SystemOff);
        
        machine.Configure(SmartHomeState.SystemOff)
            .Permit(SmartHomeTrigger.PowerOn, SmartHomeState.SystemOn);
            
        machine.Configure(SmartHomeState.SystemOn)
            .Permit(SmartHomeTrigger.PowerOff, SmartHomeState.SystemOff)
            .Permit(SmartHomeTrigger.Maintenance, SmartHomeState.SystemMaintenance);
            
        machine.Configure(SmartHomeState.SystemMaintenance)
            .Permit(SmartHomeTrigger.Resume, SmartHomeState.SystemOn);
            
        return machine;
    }
    
    protected override void ConfigureOrthogonalRegions()
    {
        // Security subsystem - runs independently
        DefineOrthogonalRegion("Security", SmartHomeState.SecurityDisarmed, machine =>
        {
            machine.Configure(SmartHomeState.SecurityDisarmed)
                .Permit(SmartHomeTrigger.ArmHome, SmartHomeState.SecurityArmedHome)
                .Permit(SmartHomeTrigger.ArmAway, SmartHomeState.SecurityArmedAway);
                
            machine.Configure(SmartHomeState.SecurityArmedHome)
                .Permit(SmartHomeTrigger.Disarm, SmartHomeState.SecurityDisarmed)
                .Permit(SmartHomeTrigger.TriggerAlarm, SmartHomeState.SecurityAlarm)
                .OnEntry(() => _logger.LogInformation("Security armed for home mode"));
                
            machine.Configure(SmartHomeState.SecurityArmedAway)
                .Permit(SmartHomeTrigger.Disarm, SmartHomeState.SecurityDisarmed)
                .Permit(SmartHomeTrigger.TriggerAlarm, SmartHomeState.SecurityAlarm)
                .OnEntry(() => _logger.LogInformation("Security armed for away mode"));
                
            machine.Configure(SmartHomeState.SecurityAlarm)
                .Permit(SmartHomeTrigger.ClearAlarm, SmartHomeState.SecurityDisarmed)
                .OnEntry(() => _logger.LogWarning("SECURITY ALARM TRIGGERED!"));
        });
        
        // Climate control subsystem
        DefineOrthogonalRegion("Climate", SmartHomeState.ClimateOff, machine =>
        {
            machine.Configure(SmartHomeState.ClimateOff)
                .Permit(SmartHomeTrigger.StartHeating, SmartHomeState.ClimateHeating)
                .Permit(SmartHomeTrigger.StartCooling, SmartHomeState.ClimateCooling)
                .Permit(SmartHomeTrigger.SetAuto, SmartHomeState.ClimateAuto);
                
            machine.Configure(SmartHomeState.ClimateHeating)
                .Permit(SmartHomeTrigger.StopClimate, SmartHomeState.ClimateOff)
                .Permit(SmartHomeTrigger.SetEco, SmartHomeState.ClimateEco)
                .OnEntry(() => _logger.LogInformation("Heating system activated"));
                
            machine.Configure(SmartHomeState.ClimateCooling)
                .Permit(SmartHomeTrigger.StopClimate, SmartHomeState.ClimateOff)
                .Permit(SmartHomeTrigger.SetEco, SmartHomeState.ClimateEco)
                .OnEntry(() => _logger.LogInformation("Cooling system activated"));
                
            machine.Configure(SmartHomeState.ClimateAuto)
                .Permit(SmartHomeTrigger.StopClimate, SmartHomeState.ClimateOff)
                .Permit(SmartHomeTrigger.SetEco, SmartHomeState.ClimateEco)
                .OnEntry(() => _logger.LogInformation("Climate set to auto mode"));
                
            machine.Configure(SmartHomeState.ClimateEco)
                .Permit(SmartHomeTrigger.StopClimate, SmartHomeState.ClimateOff)
                .Permit(SmartHomeTrigger.SetAuto, SmartHomeState.ClimateAuto)
                .OnEntry(() => _logger.LogInformation("Eco mode activated for climate"));
        });
        
        // Energy management subsystem
        DefineOrthogonalRegion("Energy", SmartHomeState.EnergyNormal, machine =>
        {
            machine.Configure(SmartHomeState.EnergyNormal)
                .Permit(SmartHomeTrigger.EnterPeakDemand, SmartHomeState.EnergyPeakDemand)
                .Permit(SmartHomeTrigger.EnterNightRate, SmartHomeState.EnergyNightRate)
                .Permit(SmartHomeTrigger.EnableSaving, SmartHomeState.EnergySaving);
                
            machine.Configure(SmartHomeState.EnergyPeakDemand)
                .Permit(SmartHomeTrigger.ExitPeakDemand, SmartHomeState.EnergyNormal)
                .OnEntry(() => _logger.LogInformation("Peak demand period - reducing consumption"));
                
            machine.Configure(SmartHomeState.EnergyNightRate)
                .Permit(SmartHomeTrigger.ExitNightRate, SmartHomeState.EnergyNormal)
                .OnEntry(() => _logger.LogInformation("Night rate period - optimizing high-energy tasks"));
                
            machine.Configure(SmartHomeState.EnergySaving)
                .Permit(SmartHomeTrigger.DisableSaving, SmartHomeState.EnergyNormal)
                .OnEntry(() => _logger.LogInformation("Energy saving mode activated"));
        });
        
        // Presence detection subsystem
        DefineOrthogonalRegion("Presence", SmartHomeState.PresenceHome, machine =>
        {
            machine.Configure(SmartHomeState.PresenceHome)
                .Permit(SmartHomeTrigger.LeaveHome, SmartHomeState.PresenceAway)
                .Permit(SmartHomeTrigger.GoToSleep, SmartHomeState.PresenceSleep)
                .Permit(SmartHomeTrigger.StartVacation, SmartHomeState.PresenceVacation);
                
            machine.Configure(SmartHomeState.PresenceAway)
                .Permit(SmartHomeTrigger.ArriveHome, SmartHomeState.PresenceHome)
                .Permit(SmartHomeTrigger.StartVacation, SmartHomeState.PresenceVacation)
                .OnEntry(() => _logger.LogInformation("House is now unoccupied"));
                
            machine.Configure(SmartHomeState.PresenceSleep)
                .Permit(SmartHomeTrigger.WakeUp, SmartHomeState.PresenceHome)
                .OnEntry(() => _logger.LogInformation("Sleep mode activated"));
                
            machine.Configure(SmartHomeState.PresenceVacation)
                .Permit(SmartHomeTrigger.EndVacation, SmartHomeState.PresenceHome)
                .OnEntry(() => _logger.LogInformation("Vacation mode activated"));
        });
        
        // Map triggers to specific regions
        MapTriggerToRegions(SmartHomeTrigger.ArmHome, "Security");
        MapTriggerToRegions(SmartHomeTrigger.ArmAway, "Security");
        MapTriggerToRegions(SmartHomeTrigger.Disarm, "Security");
        
        MapTriggerToRegions(SmartHomeTrigger.StartHeating, "Climate");
        MapTriggerToRegions(SmartHomeTrigger.StartCooling, "Climate");
        MapTriggerToRegions(SmartHomeTrigger.SetAuto, "Climate");
        
        MapTriggerToRegions(SmartHomeTrigger.EnableSaving, "Energy");
        MapTriggerToRegions(SmartHomeTrigger.DisableSaving, "Energy");
        
        MapTriggerToRegions(SmartHomeTrigger.ArriveHome, "Presence");
        MapTriggerToRegions(SmartHomeTrigger.LeaveHome, "Presence");
        
        // Synchronized triggers affect multiple regions
        MapTriggerToRegions(SmartHomeTrigger.VacationMode, "Security", "Climate", "Energy", "Presence");
        MapTriggerToRegions(SmartHomeTrigger.EcoMode, "Climate", "Energy");
    }
    
    protected override SmartHomeState GetCompositeState()
    {
        // Custom logic to determine overall system state based on all regions
        var regionStates = GetAllRegionStates();
        
        // If system is off, that overrides everything
        if (StateMachine.State == SmartHomeState.SystemOff)
            return SmartHomeState.SystemOff;
            
        // If in maintenance, that's the primary state
        if (StateMachine.State == SmartHomeState.SystemMaintenance)
            return SmartHomeState.SystemMaintenance;
            
        // If there's a security alarm, that's most important
        if (regionStates.TryGetValue("Security", out var securityState) && 
            securityState == SmartHomeState.SecurityAlarm)
            return SmartHomeState.SecurityAlarm;
            
        // Otherwise return the main system state
        return StateMachine.State;
    }
    
    protected override async Task OnRegionStateChangedAsync(
        string regionName,
        SmartHomeState previousState,
        SmartHomeState newState,
        SmartHomeTrigger trigger)
    {
        _logger.LogInformation("Region {Region} changed from {Previous} to {New} via {Trigger}",
            regionName, previousState, newState, trigger);
            
        // Implement cross-region reactions
        if (regionName == "Presence" && newState == SmartHomeState.PresenceAway)
        {
            // When everyone leaves, arm security and set climate to eco
            await FireInRegionAsync("Security", SmartHomeTrigger.ArmAway);
            await FireInRegionAsync("Climate", SmartHomeTrigger.SetEco);
            await FireInRegionAsync("Energy", SmartHomeTrigger.EnableSaving);
        }
        else if (regionName == "Presence" && newState == SmartHomeState.PresenceHome)
        {
            // When someone arrives home, disarm security and restore normal settings
            await FireInRegionAsync("Security", SmartHomeTrigger.Disarm);
            await FireInRegionAsync("Energy", SmartHomeTrigger.DisableSaving);
        }
        else if (regionName == "Security" && newState == SmartHomeState.SecurityAlarm)
        {
            // When alarm triggers, turn on all lights (would integrate with SmartLight grain)
            _logger.LogWarning("Security alarm! Activating all lights and sending notifications");
        }
    }
    
    public Task ActivateSecurityAsync()
    {
        return FireInRegionAsync("Security", SmartHomeTrigger.ArmHome);
    }
    
    public Task DeactivateSecurityAsync()
    {
        return FireInRegionAsync("Security", SmartHomeTrigger.Disarm);
    }
    
    public Task SetClimateAutoAsync()
    {
        return FireInRegionAsync("Climate", SmartHomeTrigger.SetAuto);
    }
    
    public Task SetEnergyEfficientModeAsync()
    {
        return FireInRegionAsync("Energy", SmartHomeTrigger.EnableSaving);
    }
    
    public async Task ActivateVacationModeAsync()
    {
        _logger.LogInformation("Activating vacation mode across all subsystems");
        
        // Vacation mode affects all regions
        await FireInRegionAsync("Presence", SmartHomeTrigger.StartVacation);
        await FireInRegionAsync("Security", SmartHomeTrigger.ArmAway);
        await FireInRegionAsync("Climate", SmartHomeTrigger.SetEco);
        await FireInRegionAsync("Energy", SmartHomeTrigger.EnableSaving);
    }
    
    public async Task DeactivateVacationModeAsync()
    {
        _logger.LogInformation("Deactivating vacation mode");
        
        await FireInRegionAsync("Presence", SmartHomeTrigger.EndVacation);
        await FireInRegionAsync("Security", SmartHomeTrigger.Disarm);
        await FireInRegionAsync("Climate", SmartHomeTrigger.SetAuto);
        await FireInRegionAsync("Energy", SmartHomeTrigger.DisableSaving);
    }
    
    public Task<SmartHomeStatus> GetFullStatusAsync()
    {
        var summary = GetStateSummary();
        
        return Task.FromResult(new SmartHomeStatus
        {
            SystemState = summary.MainState,
            SecurityState = GetRegionState("Security"),
            ClimateState = GetRegionState("Climate"),
            EnergyState = GetRegionState("Energy"),
            PresenceState = GetRegionState("Presence"),
            CompositeState = summary.CompositeState,
            LastUpdated = summary.Timestamp
        });
    }
}

public class SmartHomeStatus
{
    public SmartHomeState SystemState { get; set; }
    public SmartHomeState SecurityState { get; set; }
    public SmartHomeState ClimateState { get; set; }
    public SmartHomeState EnergyState { get; set; }
    public SmartHomeState PresenceState { get; set; }
    public SmartHomeState CompositeState { get; set; }
    public DateTime LastUpdated { get; set; }
    
    public override string ToString()
    {
        return $@"Smart Home Status:
  System: {SystemState}
  Security: {SecurityState}
  Climate: {ClimateState}
  Energy: {EnergyState}
  Presence: {PresenceState}
  Overall: {CompositeState}
  Updated: {LastUpdated:HH:mm:ss}";
    }
}