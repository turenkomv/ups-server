
using NUTDotNetServer;
using NUTDotNetShared;

namespace UpsServer;

public sealed class UpsService : IHostedService, IDisposable
{
    private readonly NUTServer _nutServer;
    private readonly ILogger<UpsService> _logger;

    public UpsService(ILogger<UpsService> logger)
    {
        _logger = logger;
        _nutServer = new NUTServer(listenPort: 3493);
        ServerUPS ups = new("ups", "EcoFlow River 3 Plus");
        ups.Variables.Add(UpsVariables.Status);
        ups.Variables.Add(UpsVariables.Charge);
        ups.Variables.Add(UpsVariables.Runtime);
        ups.Variables.Add(UpsVariables.Load);
        ups.Variables.Add(new("device.mfr", VarFlags.String) { Value = "EcoFlow" });
        ups.Variables.Add(new("device.model", VarFlags.String) { Value = "River 3 Plus" });
        ups.Variables.Add(new("ups.mfr", VarFlags.String) { Value = "EcoFlow" });
        ups.Variables.Add(new("ups.model", VarFlags.String) { Value = "River 3 Plus" });
        ups.Variables.Add(new("input.voltage", VarFlags.Number) { Value = "" });
        ups.Variables.Add(new("output.voltage", VarFlags.Number) { Value = "" });
        ups.Variables.Add(new("battery.voltage", VarFlags.Number) { Value = "" });
        _nutServer.UPSs.Add(ups);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _nutServer.Start();
        _logger.LogInformation("UpsService started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _nutServer.Stop();
        _logger.LogInformation("UpsService stopped");
        return Task.CompletedTask;
    }


    public void Dispose()
    {
        _nutServer.Dispose();
    }
}
