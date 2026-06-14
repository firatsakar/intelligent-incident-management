using BuildingBlocks.SharedKernel;
using IncidentService.Domain.Enums;
using IncidentService.Domain.Events;

namespace IncidentService.Domain.Aggregates;

public sealed class Incident : AggregateRoot
{
    private Incident() { }

    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public IncidentStatus Status { get; private set; }
    public IncidentPriority Priority { get; private set; }
    public IncidentSource Source { get; private set; }
    public string? AssignedTeam { get; private set; }

    public static Incident Create (
        string title, 
        string description, 
        IncidentPriority priority, 
        IncidentSource source, 
        string assignedTeam = null
        )
    {
        
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Status = IncidentStatus.Open,
            Priority = priority,
            Source = source,
            AssignedTeam = assignedTeam
        };

        incident.AddDomainEvent(new IncidentCreatedDomainEvent
        {
            IncidentId = incident.Id,
            Title = incident.Title
        });

        return incident;
    }

    public void UpdateStatus(IncidentStatus newStatus)
    {
        Status = newStatus;
        SetUpdatedAt();
    }

    public void AssignTeam(string team)
    {
        AssignedTeam = team;
        SetUpdatedAt();
    }
}
