namespace IncidentService.Domain.Exceptions;

public sealed class IncidentNotFoundException : Exception
{
    public IncidentNotFoundException(Guid id) : base($"Incident with id '{id}' was not found.") { }

}
