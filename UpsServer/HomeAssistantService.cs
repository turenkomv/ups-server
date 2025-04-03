using System;

namespace UpsServer;

public class HomeAssistantService : IHostedService
{
    private const double _batteryRealCapacity = 286; // 286Wh
    private const double _powerLimit = 600; // 600W
    private const double _lowBattery = 20; // 20%

    private readonly HAClient _client;
    private readonly ILogger<HomeAssistantService> _logger;

    public HomeAssistantService(HAClient client, ILogger<HomeAssistantService> logger)
    {
        _client = client;
        _client.StatesChanged += UpdateUpsVariables;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken token) => _client.StartAsync(token);

    public Task StopAsync(CancellationToken token) => _client.StopAsync(token);

    private void UpdateUpsVariables(HAStates states)
    {
        double? batteryLevel =
              (states.Battery - states.DischargeLimit) * 100.0
            / (states.ChargeLimit - states.DischargeLimit);
        double? batteryCapacity =
            _batteryRealCapacity * (100.0 - (100.0 - states.ChargeLimit) - states.DischargeLimit) / 100.0;
        double? batteryRuntime = batteryCapacity * (batteryLevel / 100.0) / states.OutputPower * 60 * 60;
        double? load = _powerLimit / states.OutputPower;
        string status = states switch
        {
            { AcPluggedIn: true } when
                states.InputPower - states.OutputPower >= 5 && batteryLevel < 99 => "OL CHRG",
            { AcPluggedIn: true } => "OL",
            { AcPluggedIn: false } when batteryLevel <= _lowBattery => "OB LB",
            { AcPluggedIn: false } => "OB",
            _ => "",
        };

        lock (UpsVariables.Status) { UpsVariables.Status.Value = status; }
        lock (UpsVariables.Charge) { UpsVariables.Charge.Value = ToValue(batteryLevel); }
        lock (UpsVariables.Runtime) { UpsVariables.Runtime.Value = ToValue(batteryRuntime); }
        lock (UpsVariables.Load) { UpsVariables.Load.Value = ToValue(load); }
    }

    private string ToValue(double? value) =>
        value == null || !double.IsFinite(value.Value) ? "" : $"{(int)Math.Round(value.Value)}";
}
