using FluentValidation;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Validators;

public class PushOptionsValidator : AbstractValidator<PushOptions>
{
    // Máximo aceito pela Expo Push API (28 dias).
    public const int MaxTtlSeconds = 28 * 24 * 60 * 60;

    public PushOptionsValidator()
    {
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority is not null);
        RuleFor(x => x.ChannelId).MaximumLength(100);
        RuleFor(x => x.TtlSeconds).InclusiveBetween(0, MaxTtlSeconds).When(x => x.TtlSeconds is not null);
    }
}
