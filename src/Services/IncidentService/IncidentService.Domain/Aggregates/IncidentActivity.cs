using BuildingBlocks.SharedKernel;
using IncidentService.Domain.Constants;
using IncidentService.Domain.Enums;
using IncidentService.Domain.ValueObjects;

namespace IncidentService.Domain.Aggregates;

/// <summary>
/// One thing that happened to an incident, who did it and when: opened, moved, assigned,
/// analysed, commented on. <see cref="Entity.CreatedAt"/> is the moment.
/// </summary>
/// <remarks>
/// <para>
/// Append-only, and written in the same <c>SaveChanges</c> as the change it describes, so the two
/// commit or fail together — a history that can disagree with the incident is worse than none. The
/// incident row stays the current state and this is what it went through. Event sourcing would make
/// this the only state and rebuild the incident from it, which nothing here needs.
/// </para>
/// <para>
/// Names are copied rather than referenced, as <see cref="Incident.ReportedBy"/> is: the record has
/// to say who did it after the person has left or the key is deleted.
/// </para>
/// </remarks>
public sealed class IncidentActivity : Entity
{
    public const int TextMaxLength = 4000;

    public const int ActorNameMaxLength = 128;

    // Wide enough for the widest thing it holds, a team name.
    public const int ValueMaxLength = IncidentConstants.TeamMaxLength;

    private IncidentActivity() { }

    public Guid OrganizationId { get; private set; }

    public Guid IncidentId { get; private set; }

    public IncidentActivityKind Kind { get; private set; }

    public ActivityActorKind ActorKind { get; private set; }

    public Guid? ActorId { get; private set; }

    public string? ActorName { get; private set; }

    // What it was and what it became — a status, a team or a priority, by kind. Strings, so one
    // pair of columns serves all three.
    public string? From { get; private set; }

    public string? To { get; private set; }

    // Only on the change that closed the incident, which is where a verdict is given.
    public IncidentVerdict? Verdict { get; private set; }

    // A comment's text, the category the analysis chose, or why it failed.
    public string? Text { get; private set; }

    public static IncidentActivity Opened(Incident incident, ActivityActor actor) =>
        New(incident, IncidentActivityKind.Opened, actor);

    /// <summary>After the incident has moved: its status and verdict now are the "to".</summary>
    public static IncidentActivity StatusChanged(Incident incident, IncidentStatus from, ActivityActor actor) =>
        New(
            incident,
            IncidentActivityKind.StatusChanged,
            actor,
            from: from.ToString(),
            to: incident.Status.ToString(),
            verdict: incident.Verdict
        );

    public static IncidentActivity TeamAssigned(Incident incident, string? from, ActivityActor actor) =>
        New(incident, IncidentActivityKind.TeamAssigned, actor, from: from, to: incident.AssignedTeam);

    /// <summary>
    /// After the analysis was applied. The priority is the part worth a before and after: the
    /// analysis sets it, and "the AI raised this to Critical" is what someone reading back wants.
    /// </summary>
    public static IncidentActivity AnalysisApplied(Incident incident, IncidentPriority from) =>
        New(
            incident,
            IncidentActivityKind.AnalysisApplied,
            ActivityActor.Ai,
            from: from.ToString(),
            to: incident.Priority.ToString(),
            text: incident.AiSuggestedCategory
        );

    public static IncidentActivity AnalysisFailed(Incident incident) =>
        New(incident, IncidentActivityKind.AnalysisFailed, ActivityActor.Ai, text: incident.AiAnalysisError);

    /// <summary>Plain text, kept as written apart from the surrounding whitespace.</summary>
    public static IncidentActivity Commented(Incident incident, string text, ActivityActor actor)
    {
        var body = text.Trim();

        if (body.Length == 0)
            throw new ArgumentException("A comment needs some text.", nameof(text));

        if (body.Length > TextMaxLength)
            throw new ArgumentException($"A comment must not exceed {TextMaxLength} characters.", nameof(text));

        return New(incident, IncidentActivityKind.Commented, actor, text: body);
    }

    private static IncidentActivity New(
        Incident incident,
        IncidentActivityKind kind,
        ActivityActor actor,
        string? from = null,
        string? to = null,
        IncidentVerdict? verdict = null,
        string? text = null
    ) =>
        new()
        {
            OrganizationId = incident.OrganizationId,
            IncidentId = incident.Id,
            Kind = kind,
            ActorKind = actor.Kind,
            ActorId = actor.Id,
            ActorName = Clip(actor.Name, ActorNameMaxLength),
            From = Clip(from, ValueMaxLength),
            To = Clip(to, ValueMaxLength),
            Verdict = verdict,
            // An analysis error is whatever the model client threw, and has no length of its own.
            Text = Clip(text, TextMaxLength),
        };

    private static string? Clip(string? value, int max) =>
        value is { Length: var length } && length > max ? value[..max] : value;
}
