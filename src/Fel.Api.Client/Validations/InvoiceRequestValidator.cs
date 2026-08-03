using FluentValidation;
using Fel.Core.Models;
using System.Linq;

namespace Fel.Api.Validations
{
    public class IssuerDataValidator : AbstractValidator<IssuerData>
    {
        public IssuerDataValidator()
        {
            RuleFor(x => x.TaxId).NotEmpty().WithMessage("El TaxId (NIT/Documento) del emisor es obligatorio.");
            RuleFor(x => x.IdentificationCode).NotEmpty().WithMessage("El IdentificationCode del emisor (Ej: 31 para NIT) es obligatorio.");
            RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre o razón social del emisor es obligatorio.");
            RuleFor(x => x.TaxLevelCodes).NotEmpty().WithMessage("Debe enviar al menos una responsabilidad fiscal (Ej: O-47).");
            RuleFor(x => x.TaxSchemeId).NotEmpty().WithMessage("El régimen fiscal (TaxSchemeId) del emisor es obligatorio.");
            RuleFor(x => x.CityCode).NotEmpty().WithMessage("El CityCode del emisor es obligatorio (Código DANE).");
        }
    }

    public class CustomerDataValidator : AbstractValidator<CustomerData>
    {
        public CustomerDataValidator()
        {
            RuleFor(x => x.TaxId).NotEmpty().WithMessage("El TaxId del adquirente es obligatorio.");
            RuleFor(x => x.IdentificationCode).NotEmpty().WithMessage("El IdentificationCode del adquirente es obligatorio.");
            RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre o razón social del adquirente es obligatorio.");
            RuleFor(x => x.TaxLevelCodes).NotEmpty().WithMessage("Las responsabilidades fiscales del adquirente son obligatorias.");
            RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
                .WithMessage("El formato del correo electrónico del cliente no es válido.");
        }
    }

    public class PaymentMeansDataValidator : AbstractValidator<PaymentMeansData>
    {
        public PaymentMeansDataValidator()
        {
            RuleFor(x => x.Id).NotEmpty().WithMessage("El ID del medio de pago es obligatorio (1=Contado, 2=Crédito).");
            RuleFor(x => x.PaymentMeansCode).NotEmpty().WithMessage("El código del método de pago es obligatorio (Ej: 10=Efectivo).");
            RuleFor(x => x.PaymentDueDate)
                .NotNull()
                .When(x => x.Id == "2")
                .WithMessage("Si el medio de pago es a Crédito (Id: 2), la fecha de vencimiento (PaymentDueDate) es absolutamente obligatoria.");
        }
    }

    public class TaxSubtotalValidator : AbstractValidator<TaxSubtotal>
    {
        public TaxSubtotalValidator()
        {
            RuleFor(x => x.TaxId).NotEmpty().WithMessage("El TaxId del impuesto es obligatorio (Ej: 01=IVA).");
            RuleFor(x => x.TaxableAmount).GreaterThanOrEqualTo(0).WithMessage("La base gravable (TaxableAmount) no puede ser negativa.");
            RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0).WithMessage("El valor del impuesto (TaxAmount) no puede ser negativo.");
            RuleFor(x => x.Percent).GreaterThanOrEqualTo(0).WithMessage("El porcentaje del impuesto no puede ser negativo.");
        }
    }

    public class InvoiceLineValidator : AbstractValidator<InvoiceLine>
    {
        public InvoiceLineValidator()
        {
            RuleFor(x => x.ItemCode).NotEmpty().WithMessage("El ItemCode (Código del producto/servicio) es obligatorio en todas las líneas.");
            RuleFor(x => x.Description).NotEmpty().WithMessage("La descripción del producto es obligatoria en cada línea.");
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La cantidad del producto debe ser mayor a 0.");
            RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");
            RuleFor(x => x.LineExtensionAmount).GreaterThanOrEqualTo(0).WithMessage("El subtotal de la línea (LineExtensionAmount) no puede ser negativo.");
            
            // Validar impuestos en las lineas si existen
            RuleForEach(x => x.Taxes).SetValidator(new TaxSubtotalValidator());
        }
    }

    public class InvoiceRequestValidator : AbstractValidator<InvoiceRequest>
    {
        public InvoiceRequestValidator()
        {
            // Validaciones Estructurales Globales
            RuleFor(x => x.Prefix).NotEmpty().WithMessage("El prefijo de la factura es obligatorio.");
            RuleFor(x => x.DocumentNumber).NotEmpty().WithMessage("El número de documento es obligatorio.");
            RuleFor(x => x.IssueDate).NotEmpty().WithMessage("La fecha de emisión (IssueDate) es obligatoria.");
            RuleFor(x => x.Currency).Length(3).WithMessage("La moneda (Currency) debe estar en formato ISO 4217 de 3 letras (Ej: COP).");
            RuleFor(x => x.TotalAmount).GreaterThanOrEqualTo(0).WithMessage("El TotalAmount de la factura debe ser mayor o igual a 0.");

            // Validar Hijos obligatorios
            RuleFor(x => x.Issuer).NotNull().WithMessage("El nodo 'Issuer' es obligatorio.");
            RuleFor(x => x.Issuer).SetValidator(new IssuerDataValidator());

            RuleFor(x => x.Customer).NotNull().WithMessage("El nodo 'Customer' es obligatorio.");
            RuleFor(x => x.Customer).SetValidator(new CustomerDataValidator());

            // Validar Colecciones Obligatorias
            RuleFor(x => x.PaymentMeans)
                .NotEmpty().WithMessage("Debe declarar al menos un medio de pago (PaymentMeans).");
            RuleForEach(x => x.PaymentMeans).SetValidator(new PaymentMeansDataValidator());

            RuleFor(x => x.Lines)
                .NotEmpty().WithMessage("La factura debe contener al menos un producto o servicio en el nodo 'Lines'.");
            RuleForEach(x => x.Lines).SetValidator(new InvoiceLineValidator());

            RuleForEach(x => x.Taxes).SetValidator(new TaxSubtotalValidator());
        }
    }
}
