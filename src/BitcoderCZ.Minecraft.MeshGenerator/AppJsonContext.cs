using System.Text.Json.Serialization;
using BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;

namespace BitcoderCZ.Minecraft.MeshGenerator;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BlockModelJson))]
[JsonSerializable(typeof(BlockStateJson))]
[JsonSerializable(typeof(TextureInfoJson))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
