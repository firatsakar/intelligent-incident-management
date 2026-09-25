using AgentOrchestrator.Domain.ValueObjects;

namespace AgentOrchestrator.Application.Changes;

/// <summary>
/// The code an analysis may read: which repository, with the organisation's token, around when the
/// problem started. Built by the command handler — which can see the organisation's connection —
/// and handed to the analyzer, which cannot.
/// </summary>
public sealed record CodeContext(RepositoryMapping Repository, string Token, DateTime ProblemStartedAt)
{
    // The token is data here, but it must never become text: a record prints every property.
    public override string ToString() => $"CodeContext {{ Repository = {Repository.FullName}, ProblemStartedAt = {ProblemStartedAt:O} }}";
}
