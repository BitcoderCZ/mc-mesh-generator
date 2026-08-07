using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MPSBuffer = BitcoderCZ.Buffers.ImmutableInlineArray<BitcoderCZ.Buffers.FixedArray1<string>, string>;
using System.Diagnostics;
using BitcoderCZ.Minecraft.MeshGenerator.JsonConverters;

namespace BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;

/// <summary>
/// Represent a block state - block id + properties.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct BlockState : IEquatable<BlockState>
{
    /// <summary>
    /// Gets the block id this state is for.
    /// </summary>
    public string BlockId { get; }

    internal readonly KeyValuePair<string, string>[] _properties;
    private readonly short _propertiesLength;
    private readonly int _hashCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockState"/> struct.
    /// </summary>
    /// <param name="blockId">The block id.</param>
    public BlockState(string blockId)
    {
        BlockId = blockId;
        _properties = [];

        _hashCode = CalculateHash();
    }

    private BlockState(string blockId, KeyValuePair<string, string>[] properties, short propertiesLength)
    {
        BlockId = blockId;

        _properties = properties;
        _propertiesLength = propertiesLength;
        _properties.AsSpan(0, _propertiesLength).Sort((a, b) => a.Key.CompareTo(b.Key, StringComparison.Ordinal));

        _hashCode = CalculateHash();
    }

    /// <summary>
    /// Gets the amount of properties.
    /// </summary>
    public int PropertyCount => _propertiesLength;

    /// <summary>
    /// Gets the properties this state is for.
    /// </summary>
    public ReadOnlySpan<KeyValuePair<string, string>> Properties => _properties.AsSpan(0, _propertiesLength);

    /// <summary>
    /// Creates a new instance of the <see cref="BlockState"/> struct.
    /// </summary>
    /// <param name="blockId">The block id.</param>
    /// <param name="properties">The block properties.</param>
    /// <returns>The new <see cref="BlockState"/> instance.</returns>
    public static BlockState Create(string blockId, IEnumerable<KeyValuePair<string, string>> properties)
        => CreateNoCopy(blockId, [.. properties.OrderBy(p => p.Key, StringComparer.Ordinal)]);

    /// <summary>
    /// Creates a new instance of the <see cref="BlockState"/> struct, *without copying the <paramref name="properties"/> array*.
    /// </summary>
    /// <param name="blockId">The block id.</param>
    /// <param name="properties">The block properties, *MUST NOT BE USED AFTER BEING PASSED INTO THIS METHOD*.</param>
    /// <returns>The new <see cref="BlockState"/> instance.</returns>
    public static BlockState CreateNoCopy(string blockId, KeyValuePair<string, string>[] properties)
        => CreateNoCopy(blockId, properties, properties.Length);

    /// <summary>
    /// Creates a new instance of the <see cref="BlockState"/> struct, *without copying the <paramref name="properties"/> array*.
    /// </summary>
    /// <param name="blockId">The block id.</param>
    /// <param name="properties">The block properties, *MUST NOT BE USED AFTER BEING PASSED INTO THIS METHOD*.</param>
    /// <param name="propertiesLength">The number of properties to use, independent of <paramref name="properties"/>.Length.</param>
    /// <returns>The new <see cref="BlockState"/> instance.</returns>
    public static BlockState CreateNoCopy(string blockId, KeyValuePair<string, string>[] properties, int propertiesLength)
    {
        Debug.Assert(propertiesLength >= 0);
        Debug.Assert(propertiesLength <= properties.Length);

        return new BlockState(blockId, properties, (short)propertiesLength);
    }

    /// <summary>Returns a value that indicates whether the 2 <see cref="BlockState"/>s are equal.</summary>
    /// <param name="left">The first <see cref="BlockState"/> to compare.</param>
    /// <param name="right">The second <see cref="BlockState"/> to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> and <paramref name="right"/> are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(BlockState left, BlockState right)
        => left.Equals(right);

    /// <summary>Returns a value that indicates whether the 2 <see cref="BlockState"/>s are not equal.</summary>
    /// <param name="left">The first <see cref="BlockState"/> to compare.</param>
    /// <param name="right">The second <see cref="BlockState"/> to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> and <paramref name="right"/> are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(BlockState left, BlockState right)
        => !(left == right);

    /// <inheritdoc/>
    public bool Equals(BlockState other)
    {
        if (_hashCode != other._hashCode ||
            BlockId != other.BlockId ||
            _propertiesLength != other._propertiesLength)
        {
            return false;
        }

        for (var i = 0; i < _propertiesLength; i++)
        {
            if (_properties[i].Key != other._properties[i].Key ||
                _properties[i].Value != other._properties[i].Value)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is BlockState state && Equals(state);

    /// <inheritdoc/>
    public override int GetHashCode()
        => _hashCode;

    private int CalculateHash()
    {
        var hash = new HashCode();
        hash.Add(BlockId);
        foreach (var prop in Properties)
        {
            hash.Add(prop.Key);
            hash.Add(prop.Value);
        }

        return hash.ToHashCode();
    }
}

// https://minecraft.wiki/w/Blockstates_definition#JSON_format
#pragma warning disable MA0048 // File name must match type name
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public sealed class BlockStateJson
{
    // mutually exclusive with with Variants
    public MultipartCaseJson[]? Multipart { get; init; }

    // mutually exclusive with with Multipart
    // if there is only 1 variant, key is ""
    [JsonPropertyName("variants")]
    public Dictionary<string, VariantModel[]>? Variants { get; init; }
}

public sealed class MultipartCaseJson
{
    // if null, always applies
    public MultipartCaseConditionJson? When { get; init; }

    [JsonConverter(typeof(SingleOrListConverter<VariantModel>))]
    public required List<VariantModel> Apply { get; init; }
}

public sealed class MultipartCase
{
    // if null, always applies
    public MultipartCaseCondition? When { get; init; }

    public required List<VariantModel> Apply { get; init; }

    public int TotalWeight { get; init; }
}

[StructLayout(LayoutKind.Auto)]
public readonly struct MultipartCaseConditionJson
{
    // mutually exclusive with And, Properties
    [JsonPropertyName("OR")]
    public List<Dictionary<string, string>>? Or { get; init; }

    // mutually exclusive with Or, Properties
    [JsonPropertyName("AND")]
    public List<Dictionary<string, string>>? And { get; init; }

    // mutually exclusive with And, Or
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Properties { get; init; }
}

[StructLayout(LayoutKind.Auto)]
public readonly struct MultipartCaseCondition
{
    // Or<And<State>>
    public ImmutableArray<ImmutableArray<KeyValuePair<string, MPSBuffer>>> Conditions { get; init; }
}

public sealed class VariantModel : IEquatable<VariantModel>
{
    public required string Model { get; init; }

    [JsonPropertyName("x")]
    public int RotationX { get; init; } // in degrees

    [JsonPropertyName("y")]
    public int RotationY { get; init; } // in degrees

    [JsonPropertyName("z")]
    public int RotationZ { get; init; } // in degrees

    // locks the rotation of the texture of a block, if set to true. This way the texture does not rotate with the block when the x and y rotation.
    [JsonPropertyName("uvlock")]
    public bool UVLock { get; init; }

    [JsonPropertyName("weight")]
    public int Weight { get; init; } = 1;

    /// <inheritdoc/>
    public bool Equals(VariantModel? other)
        => other is not null && Model == other.Model && RotationX == other.RotationX && RotationY == other.RotationY && RotationZ == other.RotationZ && UVLock == other.UVLock && Weight == other.Weight;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => Equals(obj as VariantModel);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(Model, RotationX, RotationY, RotationZ, UVLock, Weight);
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore MA0048 // File name must match type name
