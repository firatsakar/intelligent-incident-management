namespace IncidentService.Application.Abstractions;

/// <summary>
/// An open incident with the same external id was saved first (Adım 27).
/// </summary>
/// <remarks>
/// Two requests carrying the same alert can both find no open incident and both try to open one;
/// the database's unique index lets exactly one of them in. The repository turns the loser's
/// failure into this, having taken its incident back out of the unit of work, so the caller can
/// answer with the winner's incident instead of an error.
/// </remarks>
public sealed class DuplicateExternalIdException : Exception
{
    public DuplicateExternalIdException(Exception inner)
        : base("An open incident with this external id already exists.", inner) { }
}
