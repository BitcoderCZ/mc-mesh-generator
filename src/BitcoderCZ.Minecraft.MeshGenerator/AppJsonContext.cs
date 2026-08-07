using System.Text.Json.Serialization;
using BitcoderCZ.Minecraft.MeshGenerator.JsonConverters;
using BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;

namespace BitcoderCZ.Minecraft.MeshGenerator;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    Converters = new[]
    {
        typeof(JsonConverter_float2),
        typeof(JsonConverter_int3),
        typeof(JsonConverter_Vector3),
        typeof(JsonConverter_float3),
        typeof(JsonConverter_double3),
        typeof(JsonConverter_UVCoordinates),
        typeof(VariantModelArrayConverter),
    }
)]
[JsonSerializable(typeof(BlockModelJson))]
[JsonSerializable(typeof(BlockStateJson))]
[JsonSerializable(typeof(TextureInfoJson))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
