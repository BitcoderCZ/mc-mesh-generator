using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;

// https://minecraft.wiki/w/Resource_pack#Texture_animation
#pragma warning disable MA0048 // File name must match type name
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public sealed class TextureInfoJson
{
    public required TextureAnimationJson Animation { get; init; }
}

public sealed class TextureAnimationJson
{
    public bool Interpolate { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    [JsonPropertyName("frametime")]
    public int FrameTime { get; init; } = 1;

    public int[]? Frames { get; init; }
}

public sealed class TextureInfo
{
    public required TextureAnimation Animation { get; init; }
}

public sealed class TextureAnimation
{
    public required bool Interpolate { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required int FrameTime { get; init; }

    public required ImmutableArray<int> Frames { get; init; }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore MA0048 // File name must match type name
