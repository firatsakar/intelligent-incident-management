using FluentValidation;
using IncidentService.Application.Abstractions;
using IncidentService.Application.DTOs;
using IncidentService.Domain.Aggregates;
using IncidentService.Domain.Exceptions;
using MediatR;

namespace IncidentService.Application.Commands.AddIncidentComment;

/// <summary>
/// A note from someone working the incident, in its activity trail (Adım 14). Not editable and not
/// deletable: the trail is a record, and a correction is another comment.
/// </summary>
public sealed record AddIncidentCommentCommand(Guid IncidentId, string Text) : IRequest<IncidentActivityDto>;

public sealed class AddIncidentCommentCommandValidator : AbstractValidator<AddIncidentCommentCommand>
{
    public AddIncidentCommentCommandValidator()
    {
        RuleFor(x => x.Text)
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("A comment needs some text.")
            .Must(text => text is null || text.Trim().Length <= IncidentActivity.TextMaxLength)
            .WithMessage($"A comment must not exceed {IncidentActivity.TextMaxLength} characters.");
    }
}

public sealed class AddIncidentCommentCommandHandler : IRequestHandler<AddIncidentCommentCommand, IncidentActivityDto>
{
    private readonly IIncidentRepository _incidents;
    private readonly IIncidentActivityRepository _activity;
    private readonly IRealtimeNotifier _realtime;
    private readonly ICurrentUser _user;

    public AddIncidentCommentCommandHandler(
        IIncidentRepository incidents,
        IIncidentActivityRepository activity,
        IRealtimeNotifier realtime,
        ICurrentUser user
    )
    {
        _incidents = incidents;
        _activity = activity;
        _realtime = realtime;
        _user = user;
    }

    public async Task<IncidentActivityDto> Handle(AddIncidentCommentCommand request, CancellationToken cancellationToken)
    {
        var incident =
            await _incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new IncidentNotFoundException(request.IncidentId);

        // The incident itself does not change — its "last changed" is about the incident, and a
        // comment is about the people working it.
        var comment = IncidentActivity.Commented(incident, request.Text, _user.AsActor());

        _activity.Add(comment);
        await _incidents.SaveChangesAsync(cancellationToken);

        var dto = IncidentActivityDto.FromDomain(comment);
        await _realtime.ActivityRecordedAsync(dto, cancellationToken);

        return dto;
    }
}
