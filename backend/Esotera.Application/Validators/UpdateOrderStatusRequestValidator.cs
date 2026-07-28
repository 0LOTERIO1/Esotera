using Esotera.Application.DTOs.Orders;
using Esotera.Domain.Enums;
using FluentValidation;

namespace Esotera.Application.Validators;

public class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status é obrigatório.")
            .Must(OrderStatus.IsValid).WithMessage("Status inválido.");
    }
}
