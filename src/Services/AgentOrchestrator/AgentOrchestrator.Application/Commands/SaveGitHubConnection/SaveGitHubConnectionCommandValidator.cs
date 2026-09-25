using AgentOrchestrator.Domain.ValueObjects;
using FluentValidation;

namespace AgentOrchestrator.Application.Commands.SaveGitHubConnection;

public sealed class SaveGitHubConnectionCommandValidator : AbstractValidator<SaveGitHubConnectionCommand>
{
    // GitHub's own rules: an owner is letters, digits and single hyphens, at most 39; a repository
    // name is letters, digits, '.', '_' and '-', at most 100. Checked here so a typo is refused
    // with its field named instead of surfacing later as an analysis that quietly found nothing.
    private const string OwnerPattern = "^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$";
    private const string RepositoryPattern = "^[A-Za-z0-9._-]{1,100}$";

    public SaveGitHubConnectionCommandValidator()
    {
        RuleFor(x => x.Token).MaximumLength(512);

        RuleFor(x => x.Repositories).NotNull();

        RuleForEach(x => x.Repositories)
            .ChildRules(mapping =>
            {
                mapping.RuleFor(m => m.Service).NotEmpty().MaximumLength(128);
                mapping.RuleFor(m => m.Owner).NotEmpty().Matches(OwnerPattern);
                mapping.RuleFor(m => m.Repository).NotEmpty().Matches(RepositoryPattern);
                mapping
                    .RuleFor(m => m.Branch)
                    .MaximumLength(255)
                    .Must(branch => branch is null || !branch.Any(char.IsWhiteSpace))
                    .WithMessage("A branch name has no spaces.");
            });

        // One repository per service, and so at most one default: two answers to "where is this
        // service's code" is a question the analysis would have to guess at.
        RuleFor(x => x.Repositories)
            .Must(repositories =>
                repositories
                    .Select(r => r.Service?.Trim() ?? string.Empty)
                    .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .All(group => group.Count() == 1)
            )
            .WithMessage("Each service can be mapped to one repository only.")
            .When(x => x.Repositories is not null);

        RuleFor(x => x.Repositories)
            .Must(repositories => repositories.Count <= 50)
            .WithMessage("At most 50 repositories.")
            .When(x => x.Repositories is not null);
    }
}
