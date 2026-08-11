using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BitcoderCZ.Minecraft.MeshGenerator.Gltf;

/// <summary>
/// Convert <see cref="MeshData"/> to gltf.
/// </summary>
public sealed class GltfConverter : IDisposable
{
    private static readonly FrozenDictionary<string, string> TextureToColormap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "minecraft:block/grass_block_top", "minecraft:colormap/grass.png" },
        { "minecraft:block/grass_block_side_overlay", "minecraft:colormap/grass.png" },
        { "minecraft:block/fern", "minecraft:colormap/grass.png" },
        { "minecraft:block/large_fern_bottom", "minecraft:colormap/grass.png" },
        { "minecraft:block/large_fern_top", "minecraft:colormap/grass.png" },
        { "minecraft:block/tall_grass_bottom", "minecraft:colormap/grass.png" },
        { "minecraft:block/tall_grass_top", "minecraft:colormap/grass.png" },
        { "minecraft:block/short_grass", "minecraft:colormap/grass.png" },
        { "minecraft:block/oak_leaves", "minecraft:colormap/foliage.png" },
        { "minecraft:block/jungle_leaves", "minecraft:colormap/foliage.png" },
        { "minecraft:block/acacia_leaves", "minecraft:colormap/foliage.png" },
        { "minecraft:block/dark_oak_leaves", "minecraft:colormap/foliage.png" },
        { "minecraft:block/mangrove_leaves", "minecraft:colormap/foliage.png" },
        { "minecraft:block/vine", "minecraft:colormap/foliage.png" }
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, Vector4> HardcodedBlockColors = new Dictionary<string, Vector4>(StringComparer.Ordinal)
    {
        { "minecraft:block/spruce_leaves", HexToVector4(0x619961) },
        { "minecraft:block/birch_leaves", HexToVector4(0x80A755) },
        { "minecraft:block/lily_pad", HexToVector4(0x208030) },
        { "minecraft:block/pumpkin_stem", HexToVector4(0xEFC00F) },
        { "minecraft:block/attached_pumpkin_stem", HexToVector4(0xEFC00F) },
        { "minecraft:block/melon_stem", HexToVector4(0xFFFF00) },
        { "minecraft:block/attached_melon_stem", HexToVector4(0xFFFF00) },
    }.ToFrozenDictionary();

    private readonly ResourcePackManager _resourcePackManager;
    private readonly ConcurrentDictionary<string, Image<Rgba32>> _textureImageCache = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GltfConverter"/> class.
    /// </summary>
    /// <param name="resourcePackManager">The <see cref="ResourcePackManager"/> used to get textures.</param>
    public GltfConverter(ResourcePackManager resourcePackManager)
    {
        ArgumentNullException.ThrowIfNull(resourcePackManager);

        _resourcePackManager = resourcePackManager;
    }

    /// <summary>
    /// Converts the <see cref="MeshData"/> to gltf.
    /// </summary>
    /// <param name="mesh">The <see cref="MeshData"/> to convert.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<SharpGLTF.Schema2.ModelRoot> ConvertAsync(MeshData mesh)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>("ExportedMesh");

        var biome = new Biome("forest", 0.7f, 0.8f);

        foreach (var kvp in mesh.Primitives)
        {
            var (textureId, inlineColor) = ParseTextureKey(kvp.Key);
            var primitiveData = kvp.Value;

            var textureBytes = await _resourcePackManager.GetTextureDataAsync(textureId);

            var colorMultiplier = inlineColor ?? await TryGetColorMultiplierAsync(textureId, biome);

            var material = new MaterialBuilder(textureId)
                .WithBaseColor(new SharpGLTF.Memory.MemoryImage(textureBytes), colorMultiplier)
                .WithDoubleSide(false)
                .WithAlpha(IsTextureSemiTransparent(textureId) ? AlphaMode.BLEND : AlphaMode.MASK)
                .WithMetallicRoughness(0, 1);

            var textureBuilder = material.GetChannel(KnownChannel.BaseColor).Texture;
            textureBuilder.MinFilter = SharpGLTF.Schema2.TextureMipMapFilter.NEAREST;
            textureBuilder.MagFilter = SharpGLTF.Schema2.TextureInterpolationFilter.NEAREST;

            var gltfPrimitive = meshBuilder.UsePrimitive(material);

            var verts = primitiveData.Vertices;
            var indices = primitiveData.Indices;

            for (var i = 0; i < indices.Count; i += 3)
            {
                var v1 = CreateVertexBuilder(verts[indices[i]]);
                var v2 = CreateVertexBuilder(verts[indices[i + 1]]);
                var v3 = CreateVertexBuilder(verts[indices[i + 2]]);

                gltfPrimitive.AddTriangle(v1, v2, v3);
            }
        }

        var sceneBuilder = new SceneBuilder();
        sceneBuilder.AddRigidMesh(meshBuilder, Matrix4x4.Identity);

        return sceneBuilder.ToGltf2();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var img in _textureImageCache.Values)
        {
            img?.Dispose();
        }

        _textureImageCache.Clear();
    }

    private static (string textureId, Vector4? color) ParseTextureKey(string key)
    {
        var separatorIndex = key.IndexOf('#');
        if (separatorIndex < 0)
        {
            return (key, null);
        }

        var textureId = key[..separatorIndex];
        var hexSpan = key.AsSpan(separatorIndex + 1);

        if (int.TryParse(hexSpan, System.Globalization.NumberStyles.HexNumber, null, out var hex))
        {
            return (textureId, HexToVector4(hex));
        }

        return (textureId, null);
    }

    private static bool IsTextureSemiTransparent(string texture)
        => texture is "minecraft:block/water_still" or "minecraft:block/water_flow" or "minecraft:block/ice" or "minecraft:block/frosted_ice_0" or "minecraft:block/frosted_ice_1" or "minecraft:block/frosted_ice_2" or "minecraft:block/frosted_ice_3" or "minecraft:block/slime_block" or "minecraft:block/honey_block_bottom" or "minecraft:block/honey_block_side" or "minecraft:block/honey_block_top" or "minecraft:block/nether_portal" || texture.Contains("_glass", StringComparison.Ordinal);

    private static Vector4 HexToVector4(int hex)
        => new(
            ((hex >> 16) & 0xFF) / 255.0f,
            ((hex >> 8) & 0xFF) / 255.0f,
            (hex & 0xFF) / 255.0f,
            1.0f
        );

    private static bool TryGetBiomeOverride(string blockId, Biome biome, out Vector4 overrideColor)
    {
        if (biome.Name is "swamp")
        {
            overrideColor = HexToVector4(0x6A7039);
            return true;
        }

        if (biome.Name.Contains("badlands", StringComparison.Ordinal) && (blockId.Contains("grass", StringComparison.Ordinal) || blockId.Contains("fern", StringComparison.Ordinal)))
        {
            overrideColor = HexToVector4(0x90814D);
            return true;
        }

        overrideColor = Vector4.Zero;
        return false;
    }

    private static bool IsWaterTexture(string texture)
        => texture is "minecraft:block/water_still" or "minecraft:block/water_flow";

    private static Vector4 GetWaterColor(Biome biome) => biome.Name switch
    {
        "swamp" => HexToVector4(0x617B59),
        "mangrove_swamp" => HexToVector4(0x3A7A56),
        "warm_ocean" => HexToVector4(0x43D5EE),
        "lukewarm_ocean" or "deep_lukewarm_ocean" => HexToVector4(0x45ADF2),
        "cold_ocean" or "deep_cold_ocean" => HexToVector4(0x3D57D6),
        "frozen_ocean" or "deep_frozen_ocean" or "frozen_river" => HexToVector4(0x3938C9),
        _ when biome.Name.Contains("badlands", StringComparison.Ordinal) => HexToVector4(0x4E3853),
        _ => HexToVector4(0x3F76E4),
    };

    private static VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> CreateVertexBuilder(MeshVertex v)
    {
        var geometry = new VertexPositionNormal(v.Position, v.Normal);

        var material = new VertexTexture1(v.UV);

        return new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(geometry, material);
    }

    private async Task<Vector4?> TryGetColorMultiplierAsync(string textureName, Biome biome)
    {
        if (!textureName.AsSpan().Contains(':'))
        {
            textureName = "minecraft:" + textureName;
        }

        if (IsWaterTexture(textureName))
        {
            return GetWaterColor(biome);
        }

        if (HardcodedBlockColors.TryGetValue(textureName, out var color))
        {
            return color;
        }

        if (!TextureToColormap.TryGetValue(textureName, out var colormapPath))
        {
            return null;
        }

        if (TryGetBiomeOverride(textureName, biome, out color))
        {
            return color;
        }

        return await GetColorFromTexture(colormapPath, biome);
    }

    private async Task<Vector4?> GetColorFromTexture(string path, Biome biome)
    {
        var temp = float.Clamp(biome.Temperature, 0f, 1f);
        var humidity = float.Clamp(biome.Downfall, 0f, 1f) * temp;

        var u = (int)((1.0f - temp) * 255.0f);
        var v = (int)((1.0f - humidity) * 255.0f);

        try
        {
            if (!_textureImageCache.TryGetValue(path, out var img))
            {
                img = await _resourcePackManager.GetTextureImageAsync(path);
                // add to cache even if null, so don't have to look up from _resourcePackManager again
                _textureImageCache[path] = img;
            }

            if (img is null)
            {
                return null;
            }

            var pixel = img[u, v];
            return new Vector4(pixel.R / 255f, pixel.G / 255f, pixel.B / 255f, 1.0f);
        }
        catch
        {
            return null;
        }
    }

    private sealed record Biome(string Name, float Temperature, float Downfall);
}
