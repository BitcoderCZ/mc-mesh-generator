using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using BitcoderCZ.Maths.Vectors;
using BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;
using BitcoderCZ.Minecraft.MeshGenerator.Utils;
using BitcoderCZ.Utils;
using Cyotek.Data.Nbt;
using TagList = Cyotek.Data.Nbt.TagList;

namespace BitcoderCZ.Minecraft.MeshGenerator;

/// <summary>
/// Generate mesh from minecraft java world.
/// </summary>
public sealed class WorldMeshGenerator
{
    private static readonly SearchValues<string> FullAndOpaqueBlocks = SearchValues.Create(
    [
        "glass",
        "leaves",
        "slime",
        "honey",
        "ice",
        "trapdoor"
    ], StringComparison.Ordinal);

    private readonly ResourcePackManager _resourcePack;
    private readonly Random _rng = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldMeshGenerator"/> class.
    /// </summary>
    /// <param name="resourcePackManager">The <see cref="ResourcePackManager"/> to use, must contain the vanilla resource pack, or all the vanilla block states and block models.</param>
    public WorldMeshGenerator(ResourcePackManager resourcePackManager)
    {
        _resourcePack = resourcePackManager;
    }

    private delegate BlockState? GetBlockAtPos<TState>(int3 position, ref TState state);

    /// <summary>
    /// Generates mesh from a world directory.
    /// </summary>
    /// <param name="path">Path to the world directory.</param>
    /// <param name="worldOffset">Offset to apply to the mesh.</param>
    /// <param name="progress">An optional <see cref="IProgress{ProgressReport}"/> to report progress to.</param>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/>.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<MeshData> GenerateFromDirectoryAsync(string path, int3 worldOffset, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var cacheRegionsProgress = progress?.WrapRange(0.00, 0.90);
        cacheRegionsProgress?.Report(new ProgressReport(0.00, $"Caching regions"));
        var lastReportedPercentage = 0;

        var mesh = new MeshData.Builder();
        var subChunkCache = new Dictionary<int3, CachedSubChunk>();

        var regionFiles = Directory.GetFiles(Path.Combine(path, "region"));

        cacheRegionsProgress?.Report(new ProgressReport(0.00, null));

        var regionIndex = 0;
        foreach (var regionPath in regionFiles)
        {
            var regionFile = new FileInfo(regionPath);
            using var entryStream = regionFile.OpenRead();
            var regionData = GC.AllocateUninitializedArray<byte>(checked((int)regionFile.Length));
            await entryStream.ReadExactlyAsync(regionData, cancellationToken);

            CacheRegion(regionData, RegionUtils.PathToPos(regionFile.FullName), subChunkCache);

            regionIndex++;

            var currentPercentage = (int)((double)regionIndex / regionFiles.Length * 100);
            if (currentPercentage != lastReportedPercentage)
            {
                cacheRegionsProgress?.Report(new ProgressReport((double)regionIndex / regionFiles.Length, null));
            }

            lastReportedPercentage = currentPercentage;
        }

        var processChunksProgress = progress?.WrapRange(0.90, 1.00);
        ProcessChunks(subChunkCache, mesh, worldOffset, processChunksProgress);

        return mesh.Drain();
    }

    /// <summary>
    /// Generates mesh from a zip containing the world.
    /// </summary>
    /// <param name="worldDataZip">A zip containing the world.</param>
    /// <param name="worldOffset">Offset to apply to the mesh.</param>
    /// <param name="progress">An optional <see cref="IProgress{ProgressReport}"/> to report progress to.</param>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/>.</param>
    /// <returns>The generated mesh for the world.</returns>
    public async Task<MeshData> GenerateFromZipAsync(ZipArchive worldDataZip, int3 worldOffset, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        var cacheRegionsProgress = progress?.WrapRange(0.00, 0.90);
        cacheRegionsProgress?.Report(new ProgressReport(0.00, $"Caching regions"));
        var lastReportedPercentage = 0;

        var mesh = new MeshData.Builder();
        var subChunkCache = new Dictionary<int3, CachedSubChunk>();

        var regionCount = worldDataZip.Entries.Count(entry => entry.FullName.StartsWith("region", StringComparison.Ordinal) && !(entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')));

        cacheRegionsProgress?.Report(new ProgressReport(0.00, null));

        var regionIndex = 0;
        foreach (var entry in worldDataZip.Entries)
        {
            if (entry.FullName.StartsWith("region", StringComparison.Ordinal) && !(entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')))
            {
                using var entryStream = await entry.OpenAsync(cancellationToken);
                var regionData = GC.AllocateUninitializedArray<byte>(checked((int)entry.Length));
                await entryStream.ReadExactlyAsync(regionData, cancellationToken);

                CacheRegion(regionData, RegionUtils.PathToPos(entry.FullName), subChunkCache);

                regionIndex++;

                var currentPercentage = (int)((double)regionIndex / regionCount * 100);
                if (currentPercentage != lastReportedPercentage)
                {
                    cacheRegionsProgress?.Report(new ProgressReport((double)regionIndex / regionCount, null));
                }

                lastReportedPercentage = currentPercentage;
            }
        }

        var processChunksProgress = progress?.WrapRange(0.90, 1.00);
        ProcessChunks(subChunkCache, mesh, worldOffset, processChunksProgress);

        return mesh.Drain();
    }

    private void ProcessChunks(Dictionary<int3, CachedSubChunk> subChunkCache, MeshData.Builder mesh, int3 worldOffset, IProgress<ProgressReport>? progress)
    {
        progress?.Report(new ProgressReport(0.00, $"Processing chunks"));
        var lastReportedPercentage = 0;

        var chunkIndex = 0;
        foreach (var subChunk in subChunkCache.Values)
        {
            ProcessCachedSubChunk(subChunk, subChunkCache, mesh, worldOffset);
            chunkIndex++;

            var currentPercentage = (int)((double)chunkIndex / subChunkCache.Count * 100);
            if (currentPercentage != lastReportedPercentage)
            {
                progress?.Report(new ProgressReport((double)chunkIndex / subChunkCache.Count, null));
            }

            lastReportedPercentage = currentPercentage;
        }
    }

    private static void CacheRegion(byte[] regionData, int2 regionPosition, Dictionary<int3, CachedSubChunk> cache)
    {
        foreach (var localPosition in RegionUtils.GetChunkPositions(regionData))
        {
            var chunkNBT = RegionUtils.ReadChunkNTB(regionData, localPosition);
            var chunkPos = RegionUtils.LocalToChunk(localPosition, regionPosition);

            // https://minecraft.wiki/w/Chunk_format
            foreach (var item in ((TagList)chunkNBT.DocumentRoot["sections"]).Value)
            {
                var subChunkNBT = (TagCompound)item;
                if (!subChunkNBT.Contains("block_states"))
                {
                    continue;
                }

                var blockStates = (TagCompound)subChunkNBT["block_states"];
                if (!blockStates.Contains("palette"))
                {
                    continue;
                }

                var subChunkCoord = new int3(chunkPos.X, ((TagByte)subChunkNBT["Y"]).Value, chunkPos.Y);

                var blocks = blockStates.Contains("data")
                    ? ChunkUtils.ReadBlockData((TagLongArray)blockStates["data"])
                    : ChunkUtils.EmptySubChunk;

                cache[subChunkCoord] = new CachedSubChunk
                {
                    Palette = (TagList)blockStates["palette"],
                    Blocks = blocks,
                    ChunkPosition = subChunkCoord
                };
            }
        }
    }

    private void ProcessCachedSubChunk(CachedSubChunk subChunk, Dictionary<int3, CachedSubChunk> cache, MeshData.Builder mesh, int3 offset)
    {
        var foundVisibleBlock = false;
        foreach (var entry in subChunk.Palette.Value)
        {
            if (!ChunkUtils.InvisibleBlocks.Contains(((TagString)((TagCompound)entry)["Name"]).Value))
            {
                foundVisibleBlock = true;
                break;
            }
        }

        if (!foundVisibleBlock)
        {
            return;
        }

        var chunkBlockPosition = subChunk.ChunkPosition * ChunkUtils.SubChunkSize;
        var blockPosition = int3.Zero;

        var propertiesArray = ArrayPool<KeyValuePair<string, string>>.Shared.Rent(64);
        var modelVariants = ArrayPool<VariantModel>.Shared.Rent(64);

        var state = new GetBlockAtPosState(cache, offset);

        foreach (var blockIndex in subChunk.Blocks)
        {
            var paletteEntry = (TagCompound)subChunk.Palette.Value[blockIndex];
            var blockName = ((TagString)paletteEntry["Name"]).Value;

            if (!ChunkUtils.InvisibleBlocks.Contains(blockName))
            {
                if (blockName is "minecraft:water" or "minecraft:lava")
                {
                    // TODO:
                    goto incrementPos;
                }

                var currentWorldPos = chunkBlockPosition + blockPosition + offset;
                mesh.RegisterBlock(currentWorldPos);

                var propertiesArrayLength = 0;
                if (paletteEntry.Value.TryGetValue("Properties", out var propertiesTag))
                {
                    foreach (var tag in ((TagCompound)propertiesTag).Value)
                    {
                        if (propertiesArrayLength >= propertiesArray.Length)
                        {
                            ArrayPool<KeyValuePair<string, string>>.Shared.Return(propertiesArray);
                            propertiesArray = ArrayPool<KeyValuePair<string, string>>.Shared.Rent(propertiesArray.Length * 2);
                        }

                        propertiesArray[propertiesArrayLength++] = new(tag.Name, ((TagString)tag).Value);
                    }
                }

                var blockState = BlockState.CreateNoCopy(blockName, propertiesArray, propertiesArrayLength);
                var modelVariantsLength = _resourcePack.GetModelVariants(blockState, _rng, modelVariants);

                foreach (var modelVariant in modelVariants.AsSpan(0, modelVariantsLength))
                {
                    GenerateBlockMesh(modelVariant, currentWorldPos, mesh, ref state, static (queryWorldPos, ref state) =>
                    {
                        var rawBlockPos = queryWorldPos - state.Offset;

                        var targetSubChunkCoord = new int3(
                            (int)float.Floor((float)rawBlockPos.X / ChunkUtils.Width),
                            (int)float.Floor((float)rawBlockPos.Y / ChunkUtils.SubChunkSize),
                            (int)float.Floor((float)rawBlockPos.Z / ChunkUtils.Width)
                        );

                        if (!state.Cache.TryGetValue(targetSubChunkCoord, out var targetSubChunk))
                        {
                            return null;
                        }

                        var localPos = rawBlockPos - (targetSubChunkCoord * ChunkUtils.SubChunkSize);

                        var targetIndex = localPos.X + localPos.Z * ChunkUtils.Width + localPos.Y * ChunkUtils.Width * ChunkUtils.Width;
                        var targetBlockIndex = targetSubChunk.Blocks[targetIndex];

                        return ChunkUtils.TagToBlockStateVisibleFromPool((TagCompound)targetSubChunk.Palette.Value[targetBlockIndex]);
                    },
                    blockState => ArrayPool<KeyValuePair<string, string>>.Shared.Return(blockState._properties));
                }
            }

        incrementPos:
            blockPosition.X++;
            if (blockPosition.X >= ChunkUtils.Width)
            {
                blockPosition.X = 0;
                blockPosition.Z++;
                if (blockPosition.Z >= ChunkUtils.Width)
                {
                    blockPosition.Z = 0;
                    blockPosition.Y++;
                }
            }
        }

        Debug.Assert(blockPosition == new int3(0, ChunkUtils.SubChunkSize, 0));

        ArrayPool<KeyValuePair<string, string>>.Shared.Return(propertiesArray);
        ArrayPool<VariantModel>.Shared.Return(modelVariants);
    }

    private void GenerateBlockMesh<TState>(VariantModel modelVariant, int3 blockPosition, MeshData.Builder mesh, ref TState state, GetBlockAtPos<TState> getBlockAtPos, Action<BlockState> disposeBlockState)
        where TState : struct
    {
        var model = _resourcePack.GetModel(modelVariant.Model);

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

                if (face.CullFace.HasValue)
                {
                    // Rotate the defined cull direction based on the variant's transform
                    var cullNormal = GeneratorUtils.GetDirectionVector3(face.CullFace.Value);
                    var rotatedNormal = Vector3.TransformNormal(cullNormal, variantTransform);
                    var actualCullDir = GeneratorUtils.GetClosestDirection(rotatedNormal);

                    var neighborPos = blockPosition + GeneratorUtils.GetDirectionOffset(actualCullDir);

                    var neighbor = getBlockAtPos(neighborPos, ref state);
                    if (neighbor is not null)
                    {
                        // todo: compute faceGrid for this face too, cull if they are equal
                        if (IsBlockFullAndOpaque(neighbor.Value, (Direction)((int)actualCullDir ^ 1)))
                        {
                            disposeBlockState(neighbor.Value);
                            continue;
                        }

                        disposeBlockState(neighbor.Value);
                    }
                }

                var actualTexture = face.Texture;
                while (actualTexture.StartsWith('#') && model.Textures is not null)
                {
                    model.Textures.TryGetValue(actualTexture[1..], out actualTexture!);
                }

                var primitive = mesh.GetPrimitive(actualTexture);

                GeneratorUtils.BuildFace(blockPosition, direction, from, to, face, finalTransform, modelVariant.UVLock, primitive);
            }
        }
    }

    private bool IsBlockFullAndOpaque(BlockState blockState, Direction faceDirection)
    {
        // todo: implement properly instead of hardcoded list
        if (blockState.BlockId.ContainsAny(FullAndOpaqueBlocks))
        {
            return false;
        }

        var modelVariants = ArrayPool<VariantModel>.Shared.Rent(64);

        // todo: the rng doesn't change this... right?
        var modelVariantsLength = _resourcePack.GetModelVariants(blockState, _rng, modelVariants);

        var result = IsFaceFullAndOpaque(modelVariants.AsSpan(0, modelVariantsLength), faceDirection);

        ArrayPool<VariantModel>.Shared.Return(modelVariants);

        return result;
    }

    private bool IsFaceFullAndOpaque(ReadOnlySpan<VariantModel> modelVariants, Direction faceDirection)
    {
        Span<bool> faceGrid = stackalloc bool[16 * 16];
        faceGrid.Clear();

        var normal = GeneratorUtils.GetDirectionVector3(faceDirection);

        foreach (var modelVariant in modelVariants)
        {
            var model = _resourcePack.GetModel(modelVariant.Model);
            if (model is null || model.Elements.IsDefaultOrEmpty)
            {
                continue;
            }

            var variantTransform = GeneratorUtils.CreateVariantTransform(modelVariant);

            foreach (var element in model.Elements)
            {
                var from = element.From * GeneratorUtils.BlockModelScale;
                var to = element.To * GeneratorUtils.BlockModelScale;

                var elementTransform = GeneratorUtils.CreateElementTransform(element.Rotation);
                var finalTransform = elementTransform * variantTransform;

                CalculateTransformedAABB(from, to, finalTransform, out var min, out var max);

                ProjectElementToFaceGrid(min, max, normal, faceGrid);
            }
        }

        for (var i = 0; i < 256; i++)
        {
            if (!faceGrid[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void CalculateTransformedAABB(Vector3 from, Vector3 to, Matrix4x4 transform, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);

        Span<Vector3> corners =
        [
            new Vector3(from.X, from.Y, from.Z),
            new Vector3(to.X, from.Y, from.Z),
            new Vector3(from.X, to.Y, from.Z),
            new Vector3(to.X, to.Y, from.Z),
            new Vector3(from.X, from.Y, to.Z),
            new Vector3(to.X, from.Y, to.Z),
            new Vector3(from.X, to.Y, to.Z),
            new Vector3(to.X, to.Y, to.Z)
        ];

        for (var i = 0; i < 8; i++)
        {
            var transformed = Vector3.Transform(corners[i], transform);
            min = Vector3.Min(min, transformed);
            max = Vector3.Max(max, transformed);
        }
    }

    private static void ProjectElementToFaceGrid(Vector3 min, Vector3 max, Vector3 normal, Span<bool> grid)
    {
        const float Epsilon = 0.01f;
        float uMin = 0, uMax = 0, vMin = 0, vMax = 0;
        var touchesFace = false;

        if (normal.X < -0.5f)      // West Face
        {
            touchesFace = min.X <= Epsilon;
            uMin = min.Z; uMax = max.Z; vMin = min.Y; vMax = max.Y;
        }
        else if (normal.X > 0.5f)  // East Face
        {
            touchesFace = max.X >= 1.0f - Epsilon;
            uMin = min.Z; uMax = max.Z; vMin = min.Y; vMax = max.Y;
        }
        else if (normal.Y < -0.5f) // Down Face
        {
            touchesFace = min.Y <= Epsilon;
            uMin = min.X; uMax = max.X; vMin = min.Z; vMax = max.Z;
        }
        else if (normal.Y > 0.5f)  // Up Face
        {
            touchesFace = max.Y >= 1.0f - Epsilon;
            uMin = min.X; uMax = max.X; vMin = min.Z; vMax = max.Z;
        }
        else if (normal.Z < -0.5f) // North Face
        {
            touchesFace = min.Z <= Epsilon;
            uMin = min.X; uMax = max.X; vMin = min.Y; vMax = max.Y;
        }
        else if (normal.Z > 0.5f)  // South Face
        {
            touchesFace = max.Z >= 1.0f - Epsilon;
            uMin = min.X; uMax = max.X; vMin = min.Y; vMax = max.Y;
        }

        if (!touchesFace)
        {
            return;
        }

        // Convert normalized (0.0 - 1.0) coordinates to grid indices (0 - 16)
        var startU = int.Clamp((int)double.Round(uMin * 16), 0, 16);
        var endU = int.Clamp((int)double.Round(uMax * 16), 0, 16);
        var startV = int.Clamp((int)double.Round(vMin * 16), 0, 16);
        var endV = int.Clamp((int)double.Round(vMax * 16), 0, 16);

        for (var v = startV; v < endV; v++)
        {
            for (var u = startU; u < endU; u++)
            {
                grid[u + v * 16] = true;
            }
        }
    }

    private sealed class CachedSubChunk
    {
        public TagList Palette { get; init; } = null!;
        public int[] Blocks { get; init; } = null!;
        public int3 ChunkPosition { get; init; }
    }

    private readonly record struct GetBlockAtPosState(Dictionary<int3, CachedSubChunk> Cache, int3 Offset);
}