using System.Buffers;
using System.Numerics;
using BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;

namespace BitcoderCZ.Minecraft.MeshGenerator;

/// <summary>
/// Generate mesh for a block.
/// </summary>
public sealed class BlockMeshGenerator
{
    private const float BlockModelScale = 1f / 16f;

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
    public MeshData Generate(string blockId)
        => Generate(new BlockState(blockId));

    /// <summary>
    /// Generates a mesh for the given block state.
    /// </summary>
    /// <param name="blockState">The block state.</param>
    /// <returns>The generated mesh.</returns>
    public MeshData Generate(BlockState blockState)
    {
        var mesh = new MeshData.Builder();

        var modelVariants = ArrayPool<VariantModel>.Shared.Rent(64);

        var modelVariantsLength = _resourcePack.GetModelVariants(blockState, _rng, modelVariants);

        foreach (var modelVariant in modelVariants.AsSpan(0, modelVariantsLength))
        {
            GenerateBlockMesh(modelVariant, mesh);
        }

        ArrayPool<VariantModel>.Shared.Return(modelVariants);

        return mesh.Drain();
    }

    private void GenerateBlockMesh(VariantModel modelVariant, MeshData.Builder mesh)
    {
        var model = _resourcePack.GetBlockModel(modelVariant.Model);

        var variantTransform = GeneratorUtils.CreateVariantTransform(modelVariant);

        foreach (var element in model.Elements)
        {
            var from = element.From * BlockModelScale;
            var to = element.To * BlockModelScale;

            var elementTransform = GeneratorUtils.CreateElementTransform(element.Rotation, BlockModelScale);
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

                GeneratorUtils.BuildFace(Vector3.Zero, direction, from, to, face, finalTransform, modelVariant.UVLock, primitive, BlockModelScale);
            }
        }
    }
}
