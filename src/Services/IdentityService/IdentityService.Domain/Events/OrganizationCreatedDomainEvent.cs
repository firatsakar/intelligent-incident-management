using BuildingBlocks.SharedKernel;

namespace IdentityService.Domain.Events;

/// <summary>
/// Raised when an organisation comes into existence, so the other services can do whatever they
/// have to do before it can be used.
/// </summary>
/// <remarks>
/// Today that is exactly one thing. <c>DetectionRuleSeeder</c> writes a single catch-all detection
/// rule when its table is empty; once rules belong to an organisation that row belongs to nobody,
/// and an organisation with no rule ingests logs, builds signatures and then detects nothing at
/// all — without an error anywhere. The service that has to write the rule is
/// TelemetryIngestionService, the place an organisation is born is here, and the two have separate
/// databases. This event is how the fact crosses.
/// </remarks>
public sealed record OrganizationCreatedDomainEvent(Guid OrganizationId, string Name) : DomainEvent;
