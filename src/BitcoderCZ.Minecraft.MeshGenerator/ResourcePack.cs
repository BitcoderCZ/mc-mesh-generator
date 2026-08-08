using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using BitcoderCZ.Buffers;
using BSVBufferArray = BitcoderCZ.Buffers.FixedArray1<BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks.VariantModel>;
using BSVBuffer = BitcoderCZ.Buffers.ImmutableInlineArray<BitcoderCZ.Buffers.FixedArray1<BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks.VariantModel>, BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks.VariantModel>;
using MPSBufferArray = BitcoderCZ.Buffers.FixedArray1<string>;
using MPSBuffer = BitcoderCZ.Buffers.ImmutableInlineArray<BitcoderCZ.Buffers.FixedArray1<string>, string>;
using System.Runtime.InteropServices;
using BitcoderCZ.Utils;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace BitcoderCZ.Minecraft.MeshGenerator;

// https://minecraft.wiki/w/Resource_pack
// https://minecraft.wiki/w/Model

/// <summary>
/// Represents a minecraft java edition resource pack.
/// </summary>
public sealed class ResourcePack
{
    private readonly DirectoryInfo _rootDir;

    private readonly FrozenDictionary<string, BlockModel> _blockModels;
    private readonly FrozenDictionary<string, HashSet<string>> _variantPropertySchema;
    private readonly FrozenDictionary<BlockState, (BSVBuffer Buffer, int TotalWeight)> _blockStatesVariant;
    private readonly FrozenDictionary<string, ImmutableArray<MultipartCase>> _blockStatesMultipart;

    private ResourcePack(string name, DirectoryInfo rootDir, FrozenDictionary<string, BlockModel> blockModels, FrozenDictionary<string, HashSet<string>> variantPropertySchema, FrozenDictionary<BlockState, (BSVBuffer Buffer, int TotalWeight)> blockStatesVariant, FrozenDictionary<string, ImmutableArray<MultipartCase>> blockStatesMultipart)
    {
        Name = name;
        _rootDir = rootDir;
        _blockModels = blockModels;
        _variantPropertySchema = variantPropertySchema;
        _blockStatesVariant = blockStatesVariant;
        _blockStatesMultipart = blockStatesMultipart;
    }

    /// <summary>
    /// Gets the name of the resource pack.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Loads a resource pack asynchronously.
    /// </summary>
    /// <param name="packName">Name of the resource pack.</param>
    /// <param name="rootDirectory">A directory containing the extracted resourcepack.</param>
    /// <param name="fallbackResolver">A delegate called to get a <see cref="BlockModel"/>, when one is not found in this resource pack.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public static async Task<ResourcePack> LoadFromDirectoryAsync(string packName, DirectoryInfo rootDirectory, Func<string, BlockModel?>? fallbackResolver = null, CancellationToken cancellationToken = default)
    {
        var blockModelsJson = new Dictionary<string, BlockModelJson>(StringComparer.Ordinal);
        Dictionary<BlockState, (BSVBuffer Buffer, int TotalWeight)> blockStatesVariant = [];
        Dictionary<string, ImmutableArray<MultipartCase>> blockStatesMultipart = [];

        var assetsDir = new DirectoryInfo(Path.Combine(rootDirectory.FullName, "assets"));

        if (assetsDir.Exists)
        {
            // Iterate over all namespaces found in the assets folder
            foreach (var namespaceDir in assetsDir.EnumerateDirectories())
            {
                var @namespace = namespaceDir.Name;

                var blockModelsDir = new DirectoryInfo(Path.Combine(namespaceDir.FullName, "models", "block"));
                if (blockModelsDir.Exists)
                {
                    foreach (var file in blockModelsDir.EnumerateFiles("*.json"))
                    {
                        var modelName = Path.GetFileNameWithoutExtension(file.Name);
                        BlockModelJson model;
                        using (var fs = File.OpenRead(file.FullName))
                        {
                            model = await JsonSerializer.DeserializeAsync(fs, AppJsonContext.Default.BlockModelJson, cancellationToken) ?? new();
                        }

                        blockModelsJson.Add($"{@namespace}:block/{modelName}", model);
                    }
                }

                var blockStatesDir = new DirectoryInfo(Path.Combine(namespaceDir.FullName, "blockstates"));
                if (blockStatesDir.Exists)
                {
                    foreach (var file in blockStatesDir.EnumerateFiles("*.json"))
                    {
                        var blockName = $"{@namespace}:{Path.GetFileNameWithoutExtension(file.Name)}";
                        BlockStateJson json;
                        using (var fs = File.OpenRead(file.FullName))
                        {
                            json = await JsonSerializer.DeserializeAsync(fs, AppJsonContext.Default.BlockStateJson, cancellationToken) ?? new();
                        }

                        if (json.Variants is not null)
                        {
                            foreach (var variant in json.Variants)
                            {
                                var props = ParseVariantString(variant.Key);
                                var state = BlockState.CreateNoCopy(blockName, props);

                                var totalWeight = 0;
                                foreach (var item in variant.Value)
                                {
                                    totalWeight += item.Weight;
                                }

                                blockStatesVariant[state] = (ImmutableInlineArray.Create<BSVBufferArray, VariantModel>(variant.Value), totalWeight);
                            }
                        }
                        else if (json.Multipart is not null)
                        {
                            var builder = ImmutableArray.CreateBuilder<MultipartCase>(json.Multipart.Length);
                            foreach (var @case in json.Multipart)
                            {
                                var totalWeight = 0;
                                foreach (var item in @case.Apply)
                                {
                                    totalWeight += item.Weight;
                                }

                                ImmutableArray<ImmutableArray<KeyValuePair<string, MPSBuffer>>> conditions = default;
                                if (@case.When is { } when)
                                {
                                    if (when.And is not null)
                                    {
                                        var conditionsBuilder = ImmutableArray.CreateBuilder<KeyValuePair<string, MPSBuffer>>(when.And.Count);
                                        foreach (var list in when.And)
                                        {
                                            foreach (var item in list)
                                            {
                                                conditionsBuilder.Add(new(item.Key, CreateMultiPartState(item.Value)));
                                            }
                                        }

                                        conditions = [conditionsBuilder.DrainToImmutable()];
                                    }
                                    else if (when.Or is not null)
                                    {
                                        var conditionsBuilder = ImmutableArray.CreateBuilder<ImmutableArray<KeyValuePair<string, MPSBuffer>>>(when.Or.Count);
                                        var innerConditionsBuilder = ImmutableArray.CreateBuilder<KeyValuePair<string, MPSBuffer>>(4);

                                        foreach (var list in when.Or)
                                        {
                                            foreach (var item in list)
                                            {
                                                innerConditionsBuilder.Add(new(item.Key, CreateMultiPartState(item.Value)));
                                            }

                                            conditionsBuilder.Add(innerConditionsBuilder.DrainToImmutable());
                                        }

                                        conditions = conditionsBuilder.DrainToImmutable();
                                    }
                                    else if (when.Properties is not null)
                                    {
                                        var conditionsBuilder = ImmutableArray.CreateBuilder<KeyValuePair<string, MPSBuffer>>(when.Properties.Count);
                                        foreach (var item in when.Properties)
                                        {
                                            conditionsBuilder.Add(new(item.Key, CreateMultiPartState(item.Value.GetString() ?? "")));
                                        }

                                        conditions = [conditionsBuilder.DrainToImmutable()];
                                    }
                                }

                                builder.Add(new MultipartCase()
                                {
                                    When = new MultipartCaseCondition()
                                    {
                                        Conditions = conditions,
                                    },
                                    Apply = @case.Apply,
                                    TotalWeight = totalWeight,
                                });
                            }

                            blockStatesMultipart[blockName] = builder.DrainToImmutable();
                        }
                    }
                }
            }
        }

        var blockModels = new Dictionary<string, BlockModel>(blockModelsJson.Count, StringComparer.Ordinal);
        foreach (var (modelName, _) in blockModelsJson)
        {
            ResolveBlockModel(modelName);
        }

        Dictionary<string, HashSet<string>> variantPropertySchema = new(blockStatesVariant.Count, StringComparer.Ordinal);
        foreach (var item in blockStatesVariant)
        {
            if (variantPropertySchema.ContainsKey(item.Key.BlockId))
            {
                continue;
            }

            var propertyNames = new HashSet<string>(item.Key.PropertyCount, StringComparer.Ordinal);
            foreach (var prop in item.Key.Properties)
            {
                propertyNames.Add(prop.Key);
            }

            variantPropertySchema.Add(item.Key.BlockId, propertyNames);
        }

        return new ResourcePack(packName, rootDirectory, blockModels.ToFrozenDictionary(), variantPropertySchema.ToFrozenDictionary(), blockStatesVariant.ToFrozenDictionary(), blockStatesMultipart.ToFrozenDictionary());

        BlockModel? ResolveBlockModel(string modelName)
        {
            var normalizedName = NormalizeModelName(modelName);

            if (blockModels.TryGetValue(normalizedName, out var existingModel))
            {
                return existingModel;
            }

            if (!blockModelsJson.TryGetValue(normalizedName, out var json))
            {
                if (fallbackResolver is not null)
                {
                    return fallbackResolver(normalizedName);
                }

                return null;
            }

            var parent = json.Parent is null ? null : ResolveBlockModel(json.Parent);

            var textures = MergeDictionaries(json.Textures, parent?.Textures);

            ImmutableArray<BlockElement> elements;
            if (json.Elements is null)
            {
                elements = parent?.Elements ?? [];
            }
            else
            {
                var elementBuilder = ImmutableArray.CreateBuilder<BlockElement>(json.Elements.Length);

                foreach (var element in json.Elements)
                {
                    BlockElementRotation? rotation = null;
                    if (element.Rotation is { } eRot)
                    {
                        if (eRot.Axis is { } axis && eRot.Angle is { } angle)
                        {
                            rotation = new BlockElementRotation()
                            {
                                Origin = eRot.Origin,
                                ReScale = eRot.ReScale,
                                X = axis is Axis.X ? angle : 0,
                                Y = axis is Axis.Y ? angle : 0,
                                Z = axis is Axis.Z ? angle : 0,
                            };
                        }
                        else
                        {
                            rotation = new BlockElementRotation()
                            {
                                Origin = eRot.Origin,
                                ReScale = eRot.ReScale,
                                X = eRot.X,
                                Y = eRot.Y,
                                Z = eRot.Z,
                            };
                        }
                    }

                    var faces = new BlockElementFaces();
                    faces[0] = CreateBlockFace(element.Faces.East, element.From, element.To, 0);
                    faces[1] = CreateBlockFace(element.Faces.West, element.From, element.To, 1);
                    faces[2] = CreateBlockFace(element.Faces.Up, element.From, element.To, 2);
                    faces[3] = CreateBlockFace(element.Faces.Down, element.From, element.To, 3);
                    faces[4] = CreateBlockFace(element.Faces.South, element.From, element.To, 4);
                    faces[5] = CreateBlockFace(element.Faces.North, element.From, element.To, 5);

                    elementBuilder.Add(new BlockElement()
                    {
                        From = element.From,
                        To = element.To,
                        Rotation = rotation,
                        Shade = element.Shade,
                        LightEmission = element.LightEmission,
                        Faces = faces,
                    });
                }

                elements = elementBuilder.DrainToImmutable();
            }

            var model = new BlockModel()
            {
                Display = MergeDictionaries(json.Display, parent?.Display),
                Textures = textures,
                Elements = elements,
            };

            blockModels[normalizedName] = model;

            return model;
        }

        static BlockFace? CreateBlockFace(BlockFaceJson? json, Vector3 from, Vector3 to, int faceIndex)
        {
            if (json is null)
            {
                return null;
            }

            if (json.UV is not { } uv)
            {
                const float MaxValue = 16f;

                uv = faceIndex switch
                {
                    0 => new UVCoordinates(from.Z, MaxValue - to.Y, to.Z, MaxValue - from.Y),
                    1 => new UVCoordinates(MaxValue - to.Z, MaxValue - to.Y, MaxValue - from.Z, MaxValue - from.Y),
                    2 => new UVCoordinates(from.X, from.Z, to.X, to.Z),
                    3 => new UVCoordinates(from.X, MaxValue - to.Z, to.X, MaxValue - from.Z),
                    4 => new UVCoordinates(from.X, MaxValue - to.Y, to.X, MaxValue - from.Y),
                    5 => new UVCoordinates(MaxValue - to.X, MaxValue - to.Y, MaxValue - from.X, MaxValue - from.Y),
                    _ => new UVCoordinates(0, 0, MaxValue, MaxValue)
                };
            }

            var texture = json.Texture;

            // todo: should we do something when the # is missing?
            if (!texture.StartsWith('#'))
            {
                if (texture is "all")
                {
                    texture = "#all";
                }
                else
                {
                    Debug.Assert(json.Texture.StartsWith('#'));
                }
            }

            return new BlockFace()
            {
                UV = uv,
                Texture = json.Texture,
                CullFace = json.CullFace switch
                {
                    null => null,
                    DirectionJson.East => Direction.East,
                    DirectionJson.West => Direction.West,
                    DirectionJson.Up or DirectionJson.Top => Direction.Up,
                    DirectionJson.Down or DirectionJson.Bottom => Direction.Down,
                    DirectionJson.South => Direction.South,
                    DirectionJson.North => Direction.North,
                    _ => throw new UnreachableException(),
                },
                Rotation = json.Rotation,
                TintIndex = json.TintIndex,
            };
        }

        static KeyValuePair<string, string>[] ParseVariantString(string variantStr)
        {
            if (string.IsNullOrWhiteSpace(variantStr))
            {
                return [];
            }

            return [.. variantStr.Split(',')
                .Select(part => part.Split('='))
                .Where(parts => parts.Length == 2)
                .Select(parts => new KeyValuePair<string, string>(parts[0], parts[1]))];
        }

        static MPSBuffer CreateMultiPartState(string value)
        {
            var span = value.AsSpan();
            if (!span.Contains('|'))
            {
                return ImmutableInlineArray.Create<MPSBufferArray, string>(value);
            }

            var result = new MPSBuffer.Builder();

            foreach (var range in span.Split('|'))
            {
                result.Add(value[range]);
            }

            return result.DrainToImmutable(true);
        }
    }

    /// <summary>
    /// Gets the model variants to render for a given <see cref="BlockState"/>.
    /// </summary>
    /// <param name="blockState">The <see cref="BlockState"/>.</param>
    /// <param name="rng">The RNG deciding which model to choose.</param>
    /// <param name="result">The result model variants.</param>
    /// <returns>The number of model variants.</returns>
    public int GetModelVariants(BlockState blockState, Random rng, Span<VariantModel> result)
    {
        ThrowHelper.ThrowIfLessThan(result.Length, 1);

        // way more variant blocks, so try variant first
        if (_variantPropertySchema.TryGetValue(blockState.BlockId, out var propertySchema))
        {
            if (blockState.PropertyCount == propertySchema.Count && _blockStatesVariant.TryGetValue(blockState, out var variant))
            {
                var (variants, totalWeight) = variant;
                result[0] = PickRandomVariant(variants, totalWeight, rng);
                return 1;
            }

            if (propertySchema.Count is 0 && _blockStatesVariant.TryGetValue(new BlockState(blockState.BlockId), out variant))
            {
                var (variants, totalWeight) = variant;
                result[0] = PickRandomVariant(variants, totalWeight, rng);
                return 1;
            }

            var propertiesArray = ArrayPool<KeyValuePair<string, string>>.Shared.Rent(propertySchema.Count);
            var propertiesArrayLength = 0;
            foreach (var item in blockState.Properties)
            {
                if (propertySchema.Contains(item.Key))
                {
                    propertiesArray[propertiesArrayLength++] = item;
                }
            }

            if (_blockStatesVariant.TryGetValue(BlockState.CreateNoCopy(blockState.BlockId, propertiesArray, propertiesArrayLength), out variant))
            {
                var (variants, totalWeight) = variant;
                result[0] = PickRandomVariant(variants, totalWeight, rng);
                ArrayPool<KeyValuePair<string, string>>.Shared.Return(propertiesArray);
                return 1;
            }

            ArrayPool<KeyValuePair<string, string>>.Shared.Return(propertiesArray);
        }

        if (!_blockStatesMultipart.TryGetValue(blockState.BlockId, out var multipart))
        {
            return 0;
        }

        var resultLength = 0;
        foreach (var item in multipart)
        {
            if (item.When is null || DoesConditionMatch(item.When.Value, blockState))
            {
                result[resultLength++] = PickRandomVariant(item.Apply, item.TotalWeight, rng);
            }
        }

        Debug.Assert(resultLength > 0);
        return resultLength;

        static VariantModel PickRandomVariant<TCollection>(TCollection variants, int totalWeight, Random rng)
            where TCollection : IReadOnlyList<VariantModel>
        {
            if (variants.Count is 1)
            {
                return variants[0];
            }

            var r = rng.NextSingle() * totalWeight;

            var cumulative = 0f;
            foreach (var variant in variants)
            {
                cumulative += variant.Weight;
                if (r < cumulative)
                {
                    return variant;
                }
            }

            return variants[^1];
        }

        static bool DoesConditionMatch(MultipartCaseCondition condition, BlockState blockState)
        {
            if (condition.Conditions.IsDefaultOrEmpty)
            {
                return true;
            }

            foreach (var andGroup in condition.Conditions.AsSpan())
            {
                if (AndGroupMatches(andGroup, blockState))
                {
                    return true;
                }
            }

            return false;
        }

        static bool AndGroupMatches(ImmutableArray<KeyValuePair<string, MPSBuffer>> andGroup, BlockState blockState)
        {
            foreach (var requirement in andGroup.AsSpan())
            {
                var targetProperty = requirement.Key;
                var allowedValues = requirement.Value;

                if (!StateSatisfiesRequirement(blockState, targetProperty, allowedValues))
                {
                    return false;
                }
            }

            return true;
        }

        static bool StateSatisfiesRequirement(BlockState blockState, string key, MPSBuffer allowedValues)
        {
            foreach (var property in blockState.Properties)
            {
                if (property.Key == key)
                {
                    for (var i = 0; i < allowedValues.Count; i++)
                    {
                        if (allowedValues[i] == property.Value)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a <see cref="BlockModel"/> by name.
    /// </summary>
    /// <param name="modelName">Name of the model.</param>
    /// <returns>The <see cref="BlockModel"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the model does not exist in the resource pack.</exception>
    public BlockModel GetBlockModel(string modelName)
    {
        if (TryGetBlockModel(modelName, out var model))
        {
            return model;
        }

        throw new KeyNotFoundException($"BlockModel '{modelName}' not found in loaded resource pack '{Name}'.");
    }

    /// <summary>
    /// Attempts to retreive a <see cref="BlockModel"/> by name.
    /// </summary>
    /// <param name="modelName">Name of the model.</param>
    /// <param name="model">The <see cref="BlockModel"/>.</param>
    /// <returns><see langword="true"/> if the resource pack contains an block model with the specified name; otherwise, <see langword="false"/>.</returns>
    public bool TryGetBlockModel(string modelName, [NotNullWhen(true)] out BlockModel? model)
    {
        if (_blockModels.TryGetValue(modelName, out model))
        {
            return true;
        }

        return _blockModels.TryGetValue(NormalizeModelName(modelName), out model);
    }

    /// <summary>
    /// Get's the file contents of a texture.
    /// </summary>
    /// <remarks>
    /// For animated textures, only the first frame is returned, in the png format.
    /// </remarks>
    /// <param name="name">Name of the texture.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the texture file does not exist.</exception>
    public async Task<byte[]> GetTextureDataAsync(string name, CancellationToken cancellationToken = default)
    {
        var textureData = await TryGetTextureDataAsync(name, cancellationToken);

        return textureData is null ? throw new FileNotFoundException() : textureData;
    }

    /// <summary>
    /// Attempts to retreive file contents of a texture.
    /// </summary>
    /// <remarks>
    /// For animated textures, only the first frame is returned, in the png format.
    /// </remarks>
    /// <param name="name">Name of the texture.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<byte[]?> TryGetTextureDataAsync(string name, CancellationToken cancellationToken = default)
    {
        // Extract namespace from texture, defaulting to "minecraft"
        var @namespace = "minecraft";
        var path = name;

        var colonIdx = name.IndexOf(':');
        if (colonIdx >= 0)
        {
            @namespace = name[..colonIdx];
            path = name[(colonIdx + 1)..];
        }

        var file = Path.Combine(_rootDir.FullName, "assets", @namespace, "textures", path);

        if (!Path.HasExtension(file))
        {
            file += ".png";
        }

        if (!File.Exists(file))
        {
            return null;
        }

        var infoFile = file + ".mcmeta";
        if (!File.Exists(infoFile))
        {
            try
            {
                return await File.ReadAllBytesAsync(file, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        // we only support non animated textures, so crop to the first frame
        TextureInfoJson? textureInfoJson;
        using (var fs = File.OpenRead(infoFile))
        {
            textureInfoJson = await JsonSerializer.DeserializeAsync(fs, AppJsonContext.Default.TextureInfoJson, cancellationToken) ?? new TextureInfoJson() { Animation = new TextureAnimationJson(), };
        }

        Image textureImage;
        using (var fs = File.OpenRead(file))
        {
            textureImage = await Image.LoadAsync(fs, cancellationToken);
        }

        if (textureInfoJson.Animation.Width is not { } width)
        {
            if (textureInfoJson.Animation.Height is not null)
            {
                width = textureImage.Width;
            }
            else
            {
                width = int.Min(textureImage.Width, textureImage.Height);
            }
        }

        if (textureInfoJson.Animation.Height is not { } height)
        {
            if (textureInfoJson.Animation.Width is not null)
            {
                height = textureImage.Height;
            }
            else
            {
                height = int.Min(textureImage.Width, textureImage.Height);
            }
        }

        var frameCount = textureImage.Height / height;

        var textureInfo = new TextureInfo()
        {
            Animation = new TextureAnimation()
            {
                Interpolate = textureInfoJson.Animation.Interpolate,
                Width = width,
                Height = height,
                FrameTime = textureInfoJson.Animation.FrameTime,
                Frames = textureInfoJson.Animation.Frames is { } frames ? ImmutableCollectionsMarshal.AsImmutableArray(frames) : [.. Enumerable.Range(0, frameCount)],
            },
        };

        var firstFrameIndex = textureInfo.Animation.Frames.IsDefaultOrEmpty ? 0 : textureInfo.Animation.Frames[0];

        textureImage.Mutate(ctx =>
        {
            ctx.Crop(new Rectangle(0, firstFrameIndex * height, width, height));
        });

        using (var ms = new MemoryStream())
        {
            await textureImage.SaveAsPngAsync(ms, cancellationToken);
            textureImage.Dispose();

            return ms.ToArray();
        }
    }

    private static IReadOnlyDictionary<TKey, TValue> MergeDictionaries<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? @new, IReadOnlyDictionary<TKey, TValue>? @base)
        where TKey : notnull
    {
        if (@base is null or { Count: 0 })
        {
            return @new ?? new Dictionary<TKey, TValue>();
        }

        if (@new is null or { Count: 0 })
        {
            return @base ?? new Dictionary<TKey, TValue>();
        }

        var result = new Dictionary<TKey, TValue>(@new.Count + @base.Count);

        foreach (var (key, item) in @base)
        {
            result.Add(key, item);
        }

        foreach (var (key, item) in @new)
        {
            result[key] = item; // override base
        }

        return result;
    }

    private static string NormalizeModelName(string modelName)
    {
        var @namespace = "minecraft";
        var path = modelName;

        var colonIdx = modelName.IndexOf(':');
        if (colonIdx >= 0)
        {
            @namespace = modelName[..colonIdx];
            path = modelName[(colonIdx + 1)..];
        }

        if (!path.Contains('/'))
        {
            path = $"block/{path}";
        }

        return $"{@namespace}:{path}";
    }
}