namespace MonitoredShop;

public static class PaymentGateway
{
    // Fail fast: a checkout that waits 30 s on the gateway holds a request thread and a customer
    // staring at a spinner. The gateway answers well under a second on a normal day.
    public static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);
}
