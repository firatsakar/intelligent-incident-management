using BuildingBlocks.SharedKernel;
using FluentValidation;
using FluentValidation.Results;
using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IncidentService.Application.Commands.CreateIncidentApiKey;

/// <summary>A new API key for the organisation, returned with its value once (Adım 27).</summary>
public sealed record CreateIncidentApiKeyCommand(string Name, string CreatedBy) : IRequest<IssuedIncidentApiKeyDto>;

public sealed class CreateIncidentApiKeyCommandValidator : AbstractValidator<CreateIncidentApiKeyCommand>
{
    public CreateIncidentApiKeyCommandValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Give the key a name — it is shown on every incident it opens.")
            .MaximumLength(IncidentApiKey.NameMaxLength)
            .WithMessage($"Name must not exceed {IncidentApiKey.NameMaxLength} characters.");
    }
}

public sealed class CreateIncidentApiKeyCommandHandler
    : IRequestHandler<CreateIncidentApiKeyCommand, IssuedIncidentApiKeyDto>
{
    private readonly IIncidentApiKeyRepository _keys;
    private readonly IOrganizationContext _organization;
    private readonly ILogger<CreateIncidentApiKeyCommandHandler> _logger;

    public CreateIncidentApiKeyCommandHandler(
        IIncidentApiKeyRepository keys,
        IOrganizationContext organization,
        ILogger<CreateIncidentApiKeyCommandHandler> logger
    )
    {
        _keys = keys;
        _organization = organization;
        _logger = logger;
    }

    public async Task<IssuedIncidentApiKeyDto> Handle(
        CreateIncidentApiKeyCommand request,
        CancellationToken cancellationToken
    )
    {
        var name = request.Name.Trim();

        // The name is what an incident says it was sent with; two keys with one name would make
        // that say nothing.
        if (await _keys.NameExistsAsync(name, cancellationToken))
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(request.Name), "An API key with this name already exists.")]
            );
        }

        var (key, secret) = IncidentApiKey.Issue(_organization.Required, name, request.CreatedBy);

        await _keys.AddAsync(key, cancellationToken);
        await _keys.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "API key {KeyName} ({KeyPrefix}) created by {CreatedBy}.",
            key.Name,
            key.KeyPrefix,
            key.CreatedBy
        );

        return IssuedIncidentApiKeyDto.FromDomain(key, secret);
    }
}
