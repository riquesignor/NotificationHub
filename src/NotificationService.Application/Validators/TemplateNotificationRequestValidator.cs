using FluentValidation;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Validators;

public class TemplateNotificationRequestValidator : AbstractValidator<TemplateNotificationRequest>
{
    public TemplateNotificationRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.TemplateName).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty()
            .MaximumLength(NotificationRequestValidator.MaxIdempotencyKeyLength)
            .When(x => x.IdempotencyKey is not null);
        RuleFor(x => x.Push!).SetValidator(new PushOptionsValidator()).When(x => x.Push is not null);
    }
}
