using System.Text.Json;
using System.Text.Json.Serialization;

namespace BitcoderCZ.Minecraft.MeshGenerator.Utils;

internal sealed class SingleOrListConverter<T> : JsonConverter<List<T>>
{
    public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        var typeInfo = options.GetTypeInfo<T>();

        if (reader.TokenType is JsonTokenType.StartArray)
        {
            var list = new List<T>();

            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.EndArray)
                {
                    return list;
                }

                var item = JsonSerializer.Deserialize(ref reader, typeInfo);
                list.Add(item!);
            }

            throw new JsonException("Unexpected end of JSON stream while reading array.");
        }

        var singleItem = JsonSerializer.Deserialize(ref reader, typeInfo);
        return singleItem is not null ? [singleItem] : [];
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var typeInfo = options.GetTypeInfo<T>();

        if (value.Count is 1)
        {
            JsonSerializer.Serialize(writer, value[0], typeInfo);
        }
        else
        {
            writer.WriteStartArray();

            foreach (var item in value)
            {
                JsonSerializer.Serialize(writer, item, typeInfo);
            }

            writer.WriteEndArray();
        }
    }
}
