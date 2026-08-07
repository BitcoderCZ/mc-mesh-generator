using System.Numerics;
using System.Runtime.InteropServices;

namespace BitcoderCZ.Minecraft.MeshGenerator;

/// <summary>
/// Represents a meshes vertex.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct MeshVertex
{
    /// <summary>
    /// Position of the vertex.
    /// </summary>
    public readonly Vector3 Position;

    /// <summary>
    /// Normal of the vertex.
    /// </summary>
    public readonly Vector3 Normal;

    /// <summary>
    /// UV coordinate of the vertex.
    /// </summary>
    public readonly Vector2 UV;
    // public readonly int TintIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshVertex"/> struct.
    /// </summary>
    /// <param name="position">Position of the vertex.</param>
    /// <param name="normal">Normal of the vertex.</param>
    /// <param name="uv">UV coordinate of the vertex.</param>
    public MeshVertex(Vector3 position, Vector3 normal, Vector2 uv/*, int tintIndex*/)
    {
        Position = position;
        Normal = normal;
        UV = uv;
        // TintIndex = tintIndex;
    }
}
