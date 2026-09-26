using BuildingBlocks.SharedKernel;

namespace TelemetryIngestionService.Domain.Services;

/// <summary>
/// The credential a pushed source sends with every batch.
/// </summary>
/// <remarks>
/// How it is made and hashed is <see cref="AccessKey"/>'s, shared with the incident API key since
/// Adım 27; what is the telemetry service's own is the prefix.
/// </remarks>
public static class IngestKey
{
    public const string Prefix = "iim_otlp_";

    public readonly record struct Issued(string Key, string Hash, string DisplayPrefix);

    public static Issued Generate()
    {
        var issued = AccessKey.Generate(Prefix);

        return new Issued(issued.Key, issued.Hash, issued.DisplayPrefix);
    }

    public static string Hash(string key) => AccessKey.Hash(key);
}
