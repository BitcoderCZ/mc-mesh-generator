using System.Numerics;
using BitcoderCZ.Maths.Vectors;

namespace BitcoderCZ.Minecraft.MeshGenerator;

/// <summary>
/// Represents a mesh.
/// </summary>
public sealed class MeshData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeshData"/> class.
    /// </summary>
    /// <param name="primitives">Primitives in the mesh.</param>
    /// <param name="boundsMin">The minimum vertex position of the mesh.</param>
    /// <param name="boundsMax">The maximum vertex position of the mesh.</param>
    public MeshData(IReadOnlyDictionary<string, MeshPrimitive> primitives, Vector3 boundsMin, Vector3 boundsMax)
    {
        Primitives = primitives;
        BoundsMin = boundsMin;
        BoundsMax = boundsMax;
    }

    /// <summary>
    /// Gets the primitives in the mesh, grouped by texture.
    /// </summary>
    /// <remarks>
    /// Optionally may contain hex color at the end for tint (e.g. minecraft:entity/banner_base#D83F36)
    /// </remarks>
    public IReadOnlyDictionary<string, MeshPrimitive> Primitives { get; }

    /// <summary>
    /// Gets the minimum vertex position of the mesh.
    /// </summary>
    public Vector3 BoundsMin { get; }

    /// <summary>
    /// Gets the maximum vertex position of the mesh.
    /// </summary>
    public Vector3 BoundsMax { get; }

    /// <summary>
    /// Builder for the <see cref="MeshData"/> class.
    /// </summary>
    public sealed class Builder
    {
        private Dictionary<string, MeshPrimitive.Builder> _primitives = [];

        private int3 _boundsMin = new(int.MaxValue);

        private int3 _boundsMax = new(int.MinValue);

        /// <summary>
        /// Expands the bounding box to fit the block.
        /// </summary>
        /// <param name="position">Position of the block.</param>
        public void RegisterBlock(int3 position)
        {
            _boundsMin = int3.Min(_boundsMin, position);
            _boundsMax = int3.Max(_boundsMax, position + int3.One);
        }

        /// <summary>
        /// Gets a <see cref="MeshPrimitive.Builder"/> by a texture.
        /// </summary>
        /// <param name="texture">The texture.</param>
        /// <returns>The <see cref="MeshPrimitive.Builder"/> associated with the texture.</returns>
        public MeshPrimitive.Builder GetPrimitive(string texture)
        {
            if (!_primitives.TryGetValue(texture, out var primitive))
            {
                primitive = new MeshPrimitive.Builder();
                _primitives[texture] = primitive;
            }

            return primitive;
        }

        /// <summary>
        /// Drains the data to a new <see cref="MeshData"/>.
        /// </summary>
        /// <returns>The new <see cref="MeshData"/>.</returns>
        public MeshData Drain()
        {
            var primitives = _primitives;
            var boundsMin = _boundsMin;
            var boundsMax = _boundsMax;

            _primitives = [];
            _boundsMin = new(int.MaxValue);
            _boundsMax = new(int.MinValue);

            return new MeshData(primitives.ToDictionary(item => item.Key, item => item.Value.Drain(), StringComparer.Ordinal), boundsMin, boundsMax);
        }
    }
}
