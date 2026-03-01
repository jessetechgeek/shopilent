namespace Shopilent.API.Common.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;
    public string ApiPrefix { get; set; } = "/api";
    public PolicyOptions Normal { get; set; } = new();
    public PolicyOptions Auth { get; set; } = new();
    public List<string> TrustedNetworks { get; set; } = ["172.16.0.0/12", "10.0.0.0/8", "192.168.0.0/16"];
}

public sealed class PolicyOptions
{
    public int PermitLimit { get; set; } = 120;
    public int WindowSeconds { get; set; } = 60;
}
