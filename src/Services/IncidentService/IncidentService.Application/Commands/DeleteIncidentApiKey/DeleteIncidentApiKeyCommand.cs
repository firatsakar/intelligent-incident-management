using IncidentService.Application.Abstractions;
using IncidentService.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IncidentService.Application.Commands.DeleteIncidentApiKey;

/// <summary>
/// Stops an API key working, at once (Adım 27). Incidents it opened keep its name.
/// </summary>
public sealed record DeleteIncidentApiKeyCommand(Guid Id) : IRequest;

public sealed class DeleteIncidentApiKeyCommandHandler : IRequestHandler<DeleteIncidentApiKeyCommand>
{
    private readonly IIncidentApiKeyRepository _keys;
    private readonly ILogger<DeleteIncidentApiKeyCommandHandler> _logger;

    public DeleteIncidentApiKeyCommandHandler(
        IIncidentApiKeyRepository keys,
        ILogger<DeleteIncidentApiKeyCommandHandler> logger
    )
    {
        _keys = keys;
        _logger = logger;
    }

    public async Task Handle(DeleteIncidentApiKeyCommand request, CancellationToken cancellationToken)
    {
        // Read through the organisation filter: another organisation's key is not found, not
        // forbidden.
        var key =
            await _keys.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new IncidentApiKeyNotFoundException(request.Id);

        _keys.Remove(key);
        await _keys.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("API key {KeyName} ({KeyPrefix}) deleted.", key.Name, key.KeyPrefix);
    }
}
