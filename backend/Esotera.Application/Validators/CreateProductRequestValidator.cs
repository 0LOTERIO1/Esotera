using Esotera.Application.DTOs.Products;
using FluentValidation;

namespace Esotera.Application.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(200).WithMessage("Nome deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug é obrigatório.")
            .MaximumLength(200).WithMessage("Slug deve ter no máximo 200 caracteres.")
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug deve conter apenas letras minúsculas, números e hífens.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Preço deve ser maior que zero.")
            .LessThan(1_000_000).WithMessage("Preço inválido.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Categoria é obrigatória.");

        RuleFor(x => x.ShortDescription)
            .MaximumLength(500).When(x => x.ShortDescription != null);

        RuleFor(x => x.Description)
            .MaximumLength(10000).When(x => x.Description != null);
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).When(x => x.Name != null)
            .NotEmpty().When(x => x.Name != null);

        RuleFor(x => x.Slug)
            .MaximumLength(200).When(x => x.Slug != null)
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .When(x => !string.IsNullOrWhiteSpace(x.Slug))
            .WithMessage("Slug deve conter apenas letras minúsculas, números e hífens.");

        RuleFor(x => x.Price)
            .GreaterThan(0).When(x => x.Price.HasValue)
            .LessThan(1_000_000).When(x => x.Price.HasValue);

        RuleFor(x => x.ShortDescription)
            .MaximumLength(500).When(x => x.ShortDescription != null);

        RuleFor(x => x.Description)
            .MaximumLength(10000).When(x => x.Description != null);
    }
}

public class ReorderProductImagesRequestValidator : AbstractValidator<ReorderProductImagesRequest>
{
    public ReorderProductImagesRequestValidator()
    {
        RuleFor(x => x.ImageIds)
            .NotNull().WithMessage("Informe a ordem das imagens.")
            .Must(ids => ids.Length > 0).WithMessage("Informe ao menos uma imagem.")
            .Must(ids => ids.Distinct().Count() == ids.Length)
            .WithMessage("A lista de imagens contém IDs duplicados.");
    }
}
