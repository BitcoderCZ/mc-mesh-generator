using System.Collections.Concurrent;
using System.Globalization;
using BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;
using SixLabors.ImageSharp.PixelFormats;

namespace BitcoderCZ.Minecraft.MeshGenerator;

/// <summary>
/// Manages multiple <see cref="ResourcePack"/>s.
/// </summary>
public sealed class ResourcePackManager : IDisposable
{
    private readonly ResourcePack[] _packs;

    private readonly ConcurrentDictionary<string, SixLabors.ImageSharp.Image<Rgba32>> _textureCache = new(StringComparer.Ordinal);

    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private ResourcePackManager(ResourcePack[] packs)
    {
        _packs = packs;
    }

    /// <summary>
    /// Gets the number of resource packs loaded in the <see cref="ResourcePackManager"/>.
    /// </summary>
    public int LoadedPackCount => _packs.Length;

    /// <summary>
    /// Loads the extracted resource packs from a directory.
    /// </summary>
    /// <remarks>
    /// Packs are sorted using <see cref="CompareOptions.NumericOrdering"/>, it is recommended to prefix them with the other in which they should be loaded, e.g. "1. vanilla", "2. my custom resource pack".
    /// </remarks>
    /// <param name="directory">A directory containing the extracted resource packs.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public static async Task<ResourcePackManager> LoadAllAsync(DirectoryInfo directory, CancellationToken cancellationToken = default)
        => await LoadAllAsync(directory, StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering), cancellationToken);

    /// <summary>
    /// Loads the extracted resource packs from a directory.
    /// </summary>
    /// <param name="directory">A directory containing the extracted resource packs.</param>
    /// <param name="comparer">A <see cref="StringComparer"/> used to sort the directories, later resource packs override the former.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public static async Task<ResourcePackManager> LoadAllAsync(DirectoryInfo directory, StringComparer comparer, CancellationToken cancellationToken = default)
        => await LoadAsync(directory.EnumerateDirectories().Select(directory => (directory.Name, directory)).OrderBy(item => item.Name, comparer).ToList(), cancellationToken);

    /// <summary>
    /// Loads a list of resource packs.
    /// </summary>
    /// <remarks>
    /// Packs are loaded in order, meaning data from the last resource pack will override the rest, the vanilla resource pack should be first.
    /// </remarks>
    /// <param name="packsToLoad">A list of resource packs to load.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public static async Task<ResourcePackManager> LoadAsync(IReadOnlyList<(string Name, DirectoryInfo Directory)> packsToLoad, CancellationToken cancellationToken = default)
    {
        var packs = new ResourcePack[packsToLoad.Count];

        // Load in reverse (from base to highest priority custom)
        // This allows custom packs to reference block models from base packs.
        for (var i = packsToLoad.Count - 1; i >= 0; i--)
        {
            var packDef = packsToLoad[packsToLoad.Count - 1 - i];

            BlockModel? FallbackResolver(string modelName)
            {
                for (var j = i + 1; j < packs.Length; j++)
                {
                    if (packs[j].TryGetBlockModel(modelName, out var baseModel))
                    {
                        return baseModel;
                    }
                }

                return null;
            }

            var packName = packDef.Name.Trim();

            var index = packName.LastIndexOf(' ');

            if (index != -1)
            {
                packName = packName[(index + 1)..];
            }

            packs[i] = await ResourcePack.LoadFromDirectoryAsync(packName, packDef.Directory, FallbackResolver, cancellationToken);
        }

        return new ResourcePackManager(packs);
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
        for (var i = 0; i < _packs.Length; i++)
        {
            var count = _packs[i].GetModelVariants(blockState, rng, result);
            if (count > 0)
            {
                return count;
            }
        }

        throw new KeyNotFoundException($"BlockState variant for '{blockState.BlockId}' not found in any loaded resource pack.");
    }

    /// <summary>
    /// Gets a <see cref="BlockModel"/> by name.
    /// </summary>
    /// <param name="modelName">Name of the model.</param>
    /// <returns>The <see cref="BlockModel"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the model does not exist in the resource pack.</exception>
    public BlockModel GetBlockModel(string modelName)
    {
        for (var i = 0; i < _packs.Length; i++)
        {
            if (_packs[i].TryGetBlockModel(modelName, out var model))
            {
                return model;
            }
        }

        throw new KeyNotFoundException($"BlockModel '{modelName}' not found in any loaded resource pack.");
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
        for (var i = 0; i < _packs.Length; i++)
        {
            var textureData = await _packs[i].TryGetTextureDataAsync(name, cancellationToken);
            if (textureData is not null)
            {
                return textureData;
            }
        }

        throw new FileNotFoundException($"Texture '{name}' not found in any loaded resource pack.");
    }

    internal async Task<SixLabors.ImageSharp.Image<Rgba32>> GetTextureImageAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_textureCache.TryGetValue(name, out var image))
        {
            return image;
        }

        await _cacheLock.WaitAsync(cancellationToken);

        try
        {
            if (_textureCache.TryGetValue(name, out image))
            {
                return image;
            }

            for (var i = 0; i < _packs.Length; i++)
            {
                var textureData = await _packs[i].TryGetTextureDataAsync(name, cancellationToken);
                if (textureData is not null)
                {
                    using (var ms = new MemoryStream(textureData))
                    {
                        image = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(ms, cancellationToken);
                        _textureCache[name] = image;
                        return image;
                    }
                }
            }
        }
        finally
        {
            _cacheLock.Release();
        }

        throw new FileNotFoundException($"Colormap texture '{name}' not found in any loaded resource pack.");
    }

    /// <inheritdoc/>
    public void Dispose()
        => _cacheLock.Dispose();
}