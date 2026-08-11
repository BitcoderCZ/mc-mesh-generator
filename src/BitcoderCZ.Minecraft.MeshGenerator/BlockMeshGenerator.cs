using System.Buffers;
using System.Numerics;
using BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;
using BitcoderCZ.Minecraft.MeshGenerator.Utils;

namespace BitcoderCZ.Minecraft.MeshGenerator;

/// <summary>
/// Generate mesh for a block.
/// </summary>
public sealed partial class BlockMeshGenerator
{
    private readonly ResourcePackManager _resourcePack;
    private readonly Random _rng = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockMeshGenerator"/> class.
    /// </summary>
    /// <param name="resourcePackManager">The <see cref="ResourcePackManager"/> to use.</param>
    public BlockMeshGenerator(ResourcePackManager resourcePackManager)
    {
        _resourcePack = resourcePackManager;
    }

    /// <summary>
    /// Generates a mesh for the given block id.
    /// </summary>
    /// <param name="blockId">The block id.</param>
    /// <returns>The generated mesh.</returns>
    public async Task<MeshData> GenerateAsync(string blockId)
        => await GenerateAsync(new BlockState(blockId));

    /// <summary>
    /// Generates a mesh for the given block state.
    /// </summary>
    /// <param name="blockState">The block state.</param>
    /// <returns>The generated mesh.</returns>
    public async Task<MeshData> GenerateAsync(BlockState blockState)
    {
        var mesh = new MeshData.Builder();

        var modelVariants = ArrayPool<VariantModel>.Shared.Rent(64);

        var modelVariantsLength = _resourcePack.GetModelVariants(blockState, _rng, modelVariants);

        for (var i = 0; i < modelVariantsLength; i++)
        {
            await GenerateBlockMesh(modelVariants[i], blockState, mesh);
        }

        ArrayPool<VariantModel>.Shared.Return(modelVariants);

        return mesh.Drain();
    }

    /// <summary>
    /// Generates a mesh for the given block model.
    /// </summary>
    /// <param name="blockModel">The block model, e.g. minecraft:block/acacia_button.</param>
    /// <param name="blockState">An optional block state.</param>
    /// <returns>The generated mesh.</returns>
    public async Task<MeshData> GenerateBlockModelAsync(string blockModel, BlockState? blockState = null)
    {
        var mesh = new MeshData.Builder();

        await GenerateBlockMesh(new VariantModel()
        {
            Model = blockModel,
        }, blockState, mesh);

        return mesh.Drain();
    }

    private async Task GenerateBlockMesh(VariantModel modelVariant, BlockState? blockState, MeshData.Builder mesh)
    {
        var model = _resourcePack.GetModel(modelVariant.Model);

        switch (model.BuiltInInfo)
        {
            case BuiltInBlockModel.Generated:
                if (model.Textures is not null)
                {
                    foreach (var (_, textureValue) in model.Textures)
                    {
                        var actualTexture = textureValue;
                        while (actualTexture.StartsWith('#') && model.Textures is not null)
                        {
                            if (!model.Textures.TryGetValue(actualTexture[1..], out var resolvedTexture))
                            {
                                break;
                            }

                            actualTexture = resolvedTexture;
                        }

                        using var image = await _resourcePack.GetTextureImageAsync(actualTexture);
                        if (image is null)
                        {
                            continue;
                        }

                        var primitive = mesh.GetPrimitive(actualTexture);
                        GenerateGeneratedItemMesh(image, primitive);
                    }
                }

                break;
            case BuiltInBlockModel.Entity:
                {
                    switch (modelVariant.Model)
                    {
                        case string s when s.EndsWith("_banner", StringComparison.Ordinal):
                            {
                                var slate = mesh.GetPrimitive($"minecraft:entity/banner_base#{GetBannerColor(modelVariant.Model)}");
                                var pole = mesh.GetPrimitive("minecraft:entity/banner_base");
                                GenerateBannerEntityMesh(slate, pole);
                            }

                            break;
                        case string s when s.EndsWith("_bed", StringComparison.Ordinal):
                            {
                                var color = GetBedColor(modelVariant.Model);
                                var bedPrimitive = mesh.GetPrimitive($"minecraft:entity/bed/{color}");
                                GenerateBedEntityMesh(bedPrimitive);
                            }

                            break;
                        case string s when s.Contains("chest", StringComparison.Ordinal):
                            {
                                var chestType = "single";
                                if (blockState is { } blockStateValue && blockStateValue.TryGetProperty("type", out var typeValue))
                                {
                                    chestType = typeValue;
                                }

                                var chestName = "normal";
                                if (s.Contains("trapped", StringComparison.Ordinal))
                                {
                                    chestName = "trapped";
                                }
                                else if (s.Contains("ender", StringComparison.Ordinal))
                                {
                                    chestName = "ender";
                                }

                                var textureName = chestType == "single" ? chestName : $"{chestName}_{chestType}";
                                var chestPrimitive = mesh.GetPrimitive($"minecraft:entity/chest/{textureName}");

                                GenerateChestEntityMesh(chestPrimitive, chestType, GeneratorUtils.CreateVariantTransform(modelVariant));
                            }

                            break;
                    }
                }

                break;
            default:
                break;
        }

        var variantTransform = GeneratorUtils.CreateVariantTransform(modelVariant);

        foreach (var element in model.Elements)
        {
            var from = element.From * GeneratorUtils.BlockModelScale;
            var to = element.To * GeneratorUtils.BlockModelScale;

            var elementTransform = GeneratorUtils.CreateElementTransform(element.Rotation);
            var finalTransform = elementTransform * variantTransform;

            for (var i = 0; i < 6; i++)
            {
                var direction = (Direction)i;

                var face = element.Faces[(int)direction];

                if (face is null)
                {
                    continue;
                }

                var actualTexture = face.Texture;
                while (actualTexture.StartsWith('#') && model.Textures is not null)
                {
                    model.Textures.TryGetValue(actualTexture[1..], out actualTexture!);
                }

                var primitive = mesh.GetPrimitive(actualTexture);

                GeneratorUtils.BuildFace(Vector3.Zero, direction, from, to, face, finalTransform, modelVariant.UVLock, primitive);
            }
        }
    }

    private static void GenerateGeneratedItemMesh(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image, MeshPrimitive.Builder primitive)
    {
        var scale = GeneratorUtils.BlockModelScale;

        var zFront = 8.5f * scale;
        var zBack = 7.5f * scale;

        var width = image.Width;
        var height = image.Height;

        AddQuad(
            primitive,
            new Vector3(0f, 0f, zFront),
            new Vector3(16f * scale, 0f, zFront),
            new Vector3(16f * scale, 16f * scale, zFront),
            new Vector3(0f, 16f * scale, zFront),
            new Vector3(0f, 0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f)
        );

        AddQuad(
            primitive,
            new Vector3(16f * scale, 0f, zBack),
            new Vector3(0f, 0f, zBack),
            new Vector3(0f, 16f * scale, zBack),
            new Vector3(16f * scale, 16f * scale, zBack),
            new Vector3(0f, 0f, -1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        );

        bool IsOpaque(int px, int py)
        {
            return px >= 0 && px < width && py >= 0 && py < height && image[px, py].A > 0;
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!IsOpaque(x, y))
                {
                    continue;
                }

                var xMin = x / (float)width * 16f * scale;
                var xMax = (x + 1) / (float)width * 16f * scale;

                var yMin = (height - 1 - y) / (float)height * 16f * scale;
                var yMax = (height - y) / (float)height * 16f * scale;

                var uMin = x / (float)width;
                var uMax = (x + 1) / (float)width;
                var vMin = y / (float)height;
                var vMax = (y + 1) / (float)height;

                if (!IsOpaque(x, y - 1))
                {
                    AddQuad(
                        primitive,
                        new Vector3(xMin, yMax, zFront),
                        new Vector3(xMax, yMax, zFront),
                        new Vector3(xMax, yMax, zBack),
                        new Vector3(xMin, yMax, zBack),
                        new Vector3(0f, 1f, 0f),
                        new Vector2(uMin, vMax),
                        new Vector2(uMax, vMax),
                        new Vector2(uMax, vMin),
                        new Vector2(uMin, vMin)
                    );
                }

                if (!IsOpaque(x, y + 1))
                {
                    AddQuad(
                        primitive,
                        new Vector3(xMin, yMin, zBack),
                        new Vector3(xMax, yMin, zBack),
                        new Vector3(xMax, yMin, zFront),
                        new Vector3(xMin, yMin, zFront),
                        new Vector3(0f, -1f, 0f),
                        new Vector2(uMin, vMax),
                        new Vector2(uMax, vMax),
                        new Vector2(uMax, vMin),
                        new Vector2(uMin, vMin)
                    );
                }

                if (!IsOpaque(x - 1, y))
                {
                    AddQuad(
                        primitive,
                        new Vector3(xMin, yMin, zBack),
                        new Vector3(xMin, yMin, zFront),
                        new Vector3(xMin, yMax, zFront),
                        new Vector3(xMin, yMax, zBack),
                        new Vector3(-1f, 0f, 0f),
                        new Vector2(uMin, vMax),
                        new Vector2(uMax, vMax),
                        new Vector2(uMax, vMin),
                        new Vector2(uMin, vMin)
                    );
                }

                if (!IsOpaque(x + 1, y))
                {
                    AddQuad(
                        primitive,
                        new Vector3(xMax, yMin, zFront),
                        new Vector3(xMax, yMin, zBack),
                        new Vector3(xMax, yMax, zBack),
                        new Vector3(xMax, yMax, zFront),
                        new Vector3(1f, 0f, 0f),
                        new Vector2(uMin, vMax),
                        new Vector2(uMax, vMax),
                        new Vector2(uMax, vMin),
                        new Vector2(uMin, vMin)
                    );
                }
            }
        }
    }

    private static void AddQuad(MeshPrimitive.Builder primitive, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
    {
        var baseIndex = primitive.VertexCount;

        primitive.AddVertex(new MeshVertex(v0, normal, uv0));
        primitive.AddVertex(new MeshVertex(v1, normal, uv1));
        primitive.AddVertex(new MeshVertex(v2, normal, uv2));
        primitive.AddVertex(new MeshVertex(v3, normal, uv3));

        primitive.AddIndex(baseIndex + 0);
        primitive.AddIndex(baseIndex + 1);
        primitive.AddIndex(baseIndex + 2);

        primitive.AddIndex(baseIndex + 0);
        primitive.AddIndex(baseIndex + 2);
        primitive.AddIndex(baseIndex + 3);
    }
}
