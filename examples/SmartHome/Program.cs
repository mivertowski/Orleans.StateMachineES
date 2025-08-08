using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Hosting;
using Orleans.StateMachineES;
using SmartHome;
using SmartHome.Devices;  // Generated namespace for SmartLight
using SmartHome.Climate;  // Generated namespace for Thermostat

var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            .ConfigureLogging(logging => logging.AddConsole())
            .AddMemoryGrainStorageAsDefault()
            .AddMemoryGrainStorage("PubSubStore")
            .UseInMemoryReminderService();
    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<SmartHomeDemo>();
    })
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    });

using var host = builder.Build();
await host.RunAsync();

/// <summary>
/// Demo application showing source-generated state machines and orthogonal regions
/// </summary>
public class SmartHomeDemo : BackgroundService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SmartHomeDemo> _logger;
    
    public SmartHomeDemo(IGrainFactory grainFactory, ILogger<SmartHomeDemo> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken); // Wait for Orleans to start
        
        _logger.LogInformation("=== Smart Home Demo Starting ===");
        
        // Demo 1: Using source-generated SmartLight grain
        await DemoSmartLight();
        
        // Demo 2: Using source-generated Thermostat grain
        await DemoThermostat();
        
        // Demo 3: Using orthogonal SmartHomeSystem grain
        await DemoOrthogonalSmartHome();
        
        // Demo 4: Integrated scenario
        await DemoIntegratedSmartHome();
        
        _logger.LogInformation("=== Smart Home Demo Complete ===");
    }
    
    private async Task DemoSmartLight()
    {
        _logger.LogInformation("\n--- Demo 1: Source-Generated Smart Light ---");
        
        // The ISmartLightGrain interface and enums are generated from SmartLight.statemachine.yaml
        var light = _grainFactory.GetGrain<ISmartLightGrain>("living-room-light");
        
        // Use the generated typed methods
        await light.FireTurnOnAsync();
        _logger.LogInformation("Light turned on");
        
        // Check state using generated method
        var isOn = await light.IsOnAsync();
        _logger.LogInformation($"Light is on: {isOn}");
        
        // Dim the light
        await light.FireDimAsync();
        var isDimmed = await light.IsDimmedAsync();
        _logger.LogInformation($"Light is dimmed: {isDimmed}");
        
        // Set color mode
        await light.FireTurnOnAsync(); // Back to full on
        await light.FireSetColorAsync();
        var isColorMode = await light.IsColorModeAsync();
        _logger.LogInformation($"Light in color mode: {isColorMode}");
        
        // Activate night mode
        await light.FireDeactivateAsync(); // Exit color mode
        await light.FireActivateNightModeAsync();
        _logger.LogInformation("Night mode activated");
        
        // Turn off
        await light.FireTurnOffAsync();
        var state = await light.GetStateAsync();
        _logger.LogInformation($"Final light state: {state}");
        
        // Use the generated extension methods
        var isTerminal = SmartLightState.Off.IsTerminal();
        var description = SmartLightState.ColorMode.GetDescription();
        _logger.LogInformation($"Off is terminal: {isTerminal}, ColorMode description: {description}");
    }
    
    private async Task DemoThermostat()
    {
        _logger.LogInformation("\n--- Demo 2: Source-Generated Thermostat ---");
        
        // The IThermostatGrain interface is generated from Thermostat.statemachine.json
        var thermostat = _grainFactory.GetGrain<IThermostatGrain>("main-thermostat");
        
        // Set to heating mode
        await thermostat.FireHeatAsync();
        var isHeating = await thermostat.IsHeatingAsync();
        _logger.LogInformation($"Thermostat heating: {isHeating}");
        
        // Switch to auto mode
        await thermostat.FireStopAsync(); // Back to idle
        await thermostat.FireAutoModeAsync();
        _logger.LogInformation("Thermostat in auto mode");
        
        // The generated code includes the guard method stub
        // In a real implementation, you would implement CheckIsScheduledAway()
        
        // Try emergency heat
        await thermostat.FireStopAsync(); // Back to idle
        await thermostat.FireHeatAsync();
        await thermostat.FireEmergencyHeatAsync();
        var isEmergency = await thermostat.IsEmergencyAsync();
        _logger.LogInformation($"Emergency heat active: {isEmergency}");
        
        // Return to idle
        await thermostat.FireStopAsync();
        var finalState = await thermostat.GetStateAsync();
        _logger.LogInformation($"Thermostat final state: {finalState}");
    }
    
    private async Task DemoOrthogonalSmartHome()
    {
        _logger.LogInformation("\n--- Demo 3: Orthogonal Smart Home System ---");
        
        var smartHome = _grainFactory.GetGrain<ISmartHomeSystemGrain>("my-home");
        
        // Power on the system
        await smartHome.FireAsync(SmartHomeTrigger.PowerOn);
        _logger.LogInformation("Smart home system powered on");
        
        // Get initial status
        var status = await smartHome.GetFullStatusAsync();
        _logger.LogInformation($"Initial status:\n{status}");
        
        // Activate security (only affects security region)
        await smartHome.ActivateSecurityAsync();
        _logger.LogInformation("Security activated");
        
        // Set climate to auto (only affects climate region)
        await smartHome.SetClimateAutoAsync();
        _logger.LogInformation("Climate set to auto");
        
        // Simulate leaving home - triggers cross-region reactions
        await smartHome.FireInRegionAsync("Presence", SmartHomeTrigger.LeaveHome);
        _logger.LogInformation("Everyone left home - automatic adjustments triggered");
        
        await Task.Delay(1000); // Let reactions complete
        
        status = await smartHome.GetFullStatusAsync();
        _logger.LogInformation($"After leaving home:\n{status}");
        
        // Activate vacation mode - affects all regions
        await smartHome.ActivateVacationModeAsync();
        _logger.LogInformation("Vacation mode activated");
        
        status = await smartHome.GetFullStatusAsync();
        _logger.LogInformation($"Vacation mode status:\n{status}");
        
        // Return from vacation
        await smartHome.DeactivateVacationModeAsync();
        _logger.LogInformation("Vacation mode deactivated");
        
        // Check if specific regions can fire triggers
        var canArmSecurity = smartHome.CanFireInRegion("Security", SmartHomeTrigger.ArmHome);
        var canStartHeating = smartHome.CanFireInRegion("Climate", SmartHomeTrigger.StartHeating);
        _logger.LogInformation($"Can arm security: {canArmSecurity}, Can start heating: {canStartHeating}");
        
        // Final status
        status = await smartHome.GetFullStatusAsync();
        _logger.LogInformation($"Final status:\n{status}");
    }
    
    private async Task DemoIntegratedSmartHome()
    {
        _logger.LogInformation("\n--- Demo 4: Integrated Smart Home Scenario ---");
        
        // Combine orthogonal system with individual devices
        var smartHome = _grainFactory.GetGrain<ISmartHomeSystemGrain>("my-home");
        var livingRoomLight = _grainFactory.GetGrain<ISmartLightGrain>("living-room-light");
        var bedroomLight = _grainFactory.GetGrain<ISmartLightGrain>("bedroom-light");
        var thermostat = _grainFactory.GetGrain<IThermostatGrain>("main-thermostat");
        
        _logger.LogInformation("Simulating morning routine...");
        
        // Wake up
        await smartHome.FireInRegionAsync("Presence", SmartHomeTrigger.WakeUp);
        
        // Turn on lights
        await livingRoomLight.FireTurnOnAsync();
        await bedroomLight.FireTurnOnAsync();
        
        // Start heating
        await thermostat.FireHeatAsync();
        
        // Disarm security
        await smartHome.DeactivateSecurityAsync();
        
        _logger.LogInformation("Morning routine complete");
        
        // Get status of everything
        var systemStatus = await smartHome.GetFullStatusAsync();
        var livingRoomState = await livingRoomLight.GetStateAsync();
        var thermostatState = await thermostat.GetStateAsync();
        
        _logger.LogInformation($@"Home Status:
  System: {systemStatus.CompositeState}
  Living Room Light: {livingRoomState}
  Thermostat: {thermostatState}
  Security: {systemStatus.SecurityState}
  Presence: {systemStatus.PresenceState}");
        
        _logger.LogInformation("\nSimulating bedtime routine...");
        
        // Go to sleep
        await smartHome.FireInRegionAsync("Presence", SmartHomeTrigger.GoToSleep);
        
        // Activate night mode on lights
        await livingRoomLight.FireTurnOffAsync();
        await bedroomLight.FireActivateNightModeAsync();
        
        // Set thermostat to auto
        await thermostat.FireStopAsync();
        await thermostat.FireAutoModeAsync();
        
        // Arm security for home
        await smartHome.ActivateSecurityAsync();
        
        _logger.LogInformation("Bedtime routine complete");
        
        // Final integrated status
        systemStatus = await smartHome.GetFullStatusAsync();
        var bedroomState = await bedroomLight.GetStateAsync();
        thermostatState = await thermostat.GetStateAsync();
        
        _logger.LogInformation($@"Night Status:
  System: {systemStatus.CompositeState}
  Bedroom Light: {bedroomState}
  Thermostat: {thermostatState}
  Security: {systemStatus.SecurityState}
  Presence: {systemStatus.PresenceState}");
    }
}