namespace NotificationService.Application.DTOs;

/// <summary>
/// Delivery health per integration.
///
/// Today this information exists only inside one incident's detail, which answers "did anyone
/// hear about this one" but never "is this channel working". A channel that has been failing
/// quietly for a day is invisible until somebody opens the right incident, and the whole point
/// of a notification is that nobody had to go looking.
/// </summary>
public sealed record NotificationStatsDto
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    public required IReadOnlyList<IntegrationHealthDto> Integrations { get; init; }

    public required int TotalSent { get; init; }
    public required int TotalFailed { get; init; }
    public required int TotalPending { get; init; }
}

public sealed record IntegrationHealthDto
{
    public required Guid IntegrationId { get; init; }

    /// <summary>
    /// Null when the integration has since been deleted. Its deliveries are kept on purpose —
    /// they are the record that somebody was told — so the rows outlive the configuration.
    /// </summary>
    public required string? Name { get; init; }
    public required string? Channel { get; init; }
    public required bool IsEnabled { get; init; }

    public required int Sent { get; init; }
    public required int Failed { get; init; }
    public required int Pending { get; init; }

    /// <summary>
    /// Median time from the delivery row being created to it being sent. Null when nothing
    /// succeeded in the window — which is not the same as "instant", and must not render as zero.
    /// </summary>
    public required double? MedianDispatchSeconds { get; init; }

    /// <summary>The most recent failure reason, which is the only actionable thing here.</summary>
    public required string? LastError { get; init; }
    public required DateTime? LastErrorAt { get; init; }
    public required DateTime? LastSentAt { get; init; }
}
