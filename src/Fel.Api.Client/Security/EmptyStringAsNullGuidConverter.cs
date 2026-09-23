using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fel.Api.Security
{
    // Varios formularios del portal de clientes envían "" en campos Guid? opcionales (ej.
    // DocumentItem.ProductId cuando la línea se digitó manualmente, sin elegir un producto del
    // catálogo). El conversor por defecto de System.Text.Json rechaza "" para Guid? con un 400
    // ilegible ("The JSON value could not be converted..."). Este conversor trata "" igual que
    // null, evitando ese rechazo sin tener que sanear cada formulario del frontend uno por uno.
    public class EmptyStringAsNullGuidConverter : JsonConverter<Guid?>
    {
        public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;

            var str = reader.GetString();
            if (string.IsNullOrEmpty(str)) return null;

            return Guid.TryParse(str, out var guid) ? guid : throw new JsonException($"'{str}' no es un Guid válido.");
        }

        public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
        {
            if (value.HasValue) writer.WriteStringValue(value.Value);
            else writer.WriteNullValue();
        }
    }
}
