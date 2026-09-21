namespace MonitoredShop;

public static class ShopOptions
{
    // The value the Service property carries into every log event. The telemetry connector reads
    // this property to decide which service an error belongs to.
    public const string ServiceName = "checkout-service";

    public const string DefaultSeqUrl = "http://localhost:8082";
}
