using FluentValidation;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Validators;

public class TemplateNotificationRequestValidator : AbstractValidator<TemplateNotificationRequest>
{
    public TemplateNotificationRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.TemplateName).NotEmpty();
    }
}
