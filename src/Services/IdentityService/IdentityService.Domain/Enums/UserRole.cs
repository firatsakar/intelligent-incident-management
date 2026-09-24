using System.Text.Json.Serialization;

namespace IdentityService.Domain.Enums;

/// <summary>
/// What a member of an organisation is allowed to do. Ordered from most to least able, the same
/// direction <c>IncidentPriority</c> runs, so "at least this role" is a <c>&lt;=</c> test.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    /// <summary>Everything an Engineer can do, plus the organisation's own settings and members.</summary>
    Admin,

    /// <summary>Works the incidents: status, team, integrations, telemetry sources.</summary>
    Engineer,

    /// <summary>Reads. Every screen, no writes.</summary>
    Viewer,
}
