using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fel.Api.Security
{
    // Mismo problema que EmptyStringAsNullGuidConverter pero para int? (ej. Document.PaymentTermDays
    // cuando el <select> de plazo de pago queda sin elegir): el conversor por defecto rechaza "" con
    // un 400 ilegible y tumba el binding de todo el body. Se trata "" igual que null.
    public class EmptyStringAsNullInt32Converter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (string.IsNullOrEmpty(str)) return null;
                return int.TryParse(str, out var parsed) ? parsed : throw new JsonException($"'{str}' no es un entero válido.");
            }

            return reader.GetInt32();
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value.HasValue) writer.WriteNumberValue(value.Value);
            else writer.WriteNullValue();
        }
    }
}
