using System.Text.Json;
using System.Text.Json.Serialization;
using BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;

namespace BitcoderCZ.Minecraft.MeshGenerator.JsonConverters;

internal sealed class VariantModelArrayConverter : JsonConverter<VariantModel[]>
{
    public override VariantModel[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<VariantModel>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return [.. list];
                }

                var item = JsonSerializer.Deserialize(ref reader, AppJsonContext.Default.VariantModel)!;
                list.Add(item);
            }

            throw new JsonException("Invalid JSON array");
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var single = JsonSerializer.Deserialize<VariantModel>(ref reader, AppJsonContext.Default.VariantModel)!;
            return [single];
        }

        throw new JsonException($"Unexpected token {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, VariantModel[] value, JsonSerializerOptions options)
    {
        if (value.Length is 1)
        {
            JsonSerializer.Serialize(writer, value[0], AppJsonContext.Default.VariantModel);
        }
        else
        {
            JsonSerializer.Serialize(writer, value, AppJsonContext.Default.VariantModel);
        }
    }
}