using System.Text.Json;
using System.Text.Json.Serialization;
using Data.Enums;

namespace WebApi.Configuration;

public class OrderTypeJsonConverter : JsonConverter<OrderType>
{
    public override OrderType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var name = reader.GetString();
            if (Enum.GetNames<OrderType>().Any(value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase)))
            {
                return Enum.Parse<OrderType>(name!, ignoreCase: true);
            }
        }

        throw new JsonException("OrderType must be a supported enum name.");
    }

    public override void Write(Utf8JsonWriter writer, OrderType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
