namespace UpsServer;

public class HAStates
{
    public double? Battery { get; set; }
    public bool? AcPluggedIn { get; set; }
    public double? InputPower { get; set; }
    public double? OutputPower { get; set; }
    public double? ChargeLimit { get; set; }
    public double? DischargeLimit { get; set; }
}
