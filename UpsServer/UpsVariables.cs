using NUTDotNetShared;

namespace UpsServer;

public static class UpsVariables
{
    public static UPSVariable Status { get; } = new("ups.status", VarFlags.String);
    public static UPSVariable Charge { get; } = new("battery.charge", VarFlags.Number);
    public static UPSVariable Runtime { get; } = new("battery.runtime", VarFlags.Number);
    public static UPSVariable Load { get; } = new("ups.load", VarFlags.Number);
}
