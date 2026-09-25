using BuildingBlocks.SharedKernel;
using FluentValidation;
using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Commands.RenameOrganization;

/// <summary>
/// The organisation's name, as its Admins choose it (Adım 25). The first-run setup gives it one;
/// this is how it is corrected later. Every other service stores only the organisation's id, so
/// the name lives here and nowhere else — renaming touches one row.
/// </summary>
public sealed record RenameOrganizationCommand(string Name) : IRequest<OrganizationDto>;

public sealed record OrganizationDto(Guid Id, string Name);

public sealed class RenameOrganizationCommandValidator : AbstractValidator<RenameOrganizationCommand>
{
    public RenameOrganizationCommandValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("The organisation needs a name.")
            .MaximumLength(128);
    }
}

public sealed class RenameOrganizationCommandHandler : IRequestHandler<RenameOrganizationCommand, OrganizationDto>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationContext _organization;

    public RenameOrganizationCommandHandler(IOrganizationRepository organizations, IOrganizationContext organization)
    {
        _organizations = organizations;
        _organization = organization;
    }

    public async Task<OrganizationDto> Handle(RenameOrganizationCommand request, CancellationToken cancellationToken)
    {
        // The caller's own organisation, from the scope the middleware set from the token; there is
        // no id in the request to point at anyone else's.
        var organization =
            await _organizations.GetByIdAsync(_organization.Required, cancellationToken)
            ?? throw new InvalidOperationException("The signed-in organisation does not exist.");

        organization.Rename(request.Name);
        await _organizations.SaveChangesAsync(cancellationToken);

        return new OrganizationDto(organization.Id, organization.Name);
    }
}
