using System.Text.RegularExpressions;
using Fel.Core.Entities;

namespace Fel.Api.Client.Models
{
    // Una fila de la búsqueda de terceros de los selectores de los formularios (factura, documento soporte, nómina): lo justo para elegir uno y para que la
    // pantalla lo use (el mandante de una línea copia su identificación y nombre). El tercero completo se pide por id cuando se necesita.
    public class CustomerSearchItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string IdentificationType { get; set; } = string.Empty;
        public string IdentificationNumber { get; set; } = string.Empty;
        public string? VerificationDigit { get; set; }
        public string? Email { get; set; }
        public PartyType PartyType { get; set; }
    }

    public static class CustomerSearch
    {
        public const int DefaultLimit = 20;
        public const int MaxLimit = 50;

        // Todas las palabras escritas deben aparecer, en cualquier orden, en el nombre o en la identificación. Una identificación escrita con puntos o
        // guion (900.123.456-7) se compara solo por sus dígitos. includeContact: la página de Terceros también busca por correo y teléfono.
        public static IQueryable<Customer> Filter(IQueryable<Customer> query, string? text, bool includeContact = false)
        {
            foreach (var term in (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var t = term;
                var digits = Regex.IsMatch(t, @"^[\d.\-]+$") ? Regex.Replace(t, @"[^\d]", "") : t;
                query = includeContact
                    ? query.Where(c => c.Name.Contains(t) || c.IdentificationNumber.Contains(digits) || c.Email.Contains(t) || c.Phone.Contains(t))
                    : query.Where(c => c.Name.Contains(t) || c.IdentificationNumber.Contains(digits));
            }
            return query;
        }

        public static IQueryable<CustomerSearchItem> Project(IQueryable<Customer> query) =>
            query.Select(c => new CustomerSearchItem
            {
                Id = c.Id,
                Name = c.Name,
                IdentificationType = c.IdentificationType,
                IdentificationNumber = c.IdentificationNumber,
                VerificationDigit = c.VerificationDigit,
                Email = c.Email,
                PartyType = c.PartyType
            });
    }
}
