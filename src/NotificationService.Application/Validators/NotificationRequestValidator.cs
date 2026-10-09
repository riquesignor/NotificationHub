using FluentValidation;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Validators;

public class NotificationRequestValidator : AbstractValidator<NotificationRequest>
{
    public const int MaxIdempotencyKeyLength = 200;

    public NotificationRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(MaxIdempotencyKeyLength).When(x => x.IdempotencyKey is not null);
        RuleFor(x => x.Push!).SetValidator(new PushOptionsValidator()).When(x => x.Push is not null);
    }
}
