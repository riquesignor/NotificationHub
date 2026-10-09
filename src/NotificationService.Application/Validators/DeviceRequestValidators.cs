using FluentValidation;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Validators;

// O registro aceita a chave pública (embutida no app), então os limites
// também protegem o banco contra payload abusivo.
public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public const int MaxAttributes = 50;
    public const int MaxAttributeKeyLength = 64;
    public const int MaxAttributeValueLength = 512;

    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Token).NotEmpty().MaximumLength(500);
        RuleFor(x => x.DeviceType).IsInEnum();
        RuleFor(x => x.DeviceName).MaximumLength(200);

        When(x => x.Attributes is not null, () =>
        {
            RuleFor(x => x.Attributes!.Count)
                .LessThanOrEqualTo(MaxAttributes)
                .OverridePropertyName("Attributes");
            RuleForEach(x => x.Attributes!)
                .Must(a => a.Key.Length is > 0 and <= MaxAttributeKeyLength)
                .WithMessage($"Chave de atributo deve ter entre 1 e {MaxAttributeKeyLength} caracteres.")
                .Must(a => a.Value is not null && a.Value.Length <= MaxAttributeValueLength)
                .WithMessage($"Valor de atributo deve ter no máximo {MaxAttributeValueLength} caracteres.")
                .OverridePropertyName("Attributes");
        });
    }
}

public class UnregisterDeviceRequestValidator : AbstractValidator<UnregisterDeviceRequest>
{
    public UnregisterDeviceRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(500);
    }
}
