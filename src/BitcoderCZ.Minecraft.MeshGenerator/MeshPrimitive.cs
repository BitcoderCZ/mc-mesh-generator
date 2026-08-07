namespace BitcoderCZ.Minecraft.MeshGenerator;

/// <summary>
/// Part of a mesh, using a single texture.
/// </summary>
public sealed class MeshPrimitive
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeshPrimitive"/> class.
    /// </summary>
    /// <param name="vertices"></param>
    /// <param name="indices"></param>
    public MeshPrimitive(IReadOnlyList<MeshVertex> vertices, IReadOnlyList<int> indices)
    {
        Vertices = vertices;
        Indices = indices;
    }

    /// <summary>
    /// Gets a list of the vertices.
    /// </summary>
    public IReadOnlyList<MeshVertex> Vertices { get; } = [];

    /// <summary>
    /// Gets a list of the indices.
    /// </summary>
    public IReadOnlyList<int> Indices { get; } = [];

    /// <summary>
    /// Builder for the <see cref="MeshPrimitive"/> class.
    /// </summary>
    public sealed class Builder
    {
        private List<MeshVertex> _vertices = [];

        private List<int> _indices = [];

        /// <summary>
        /// Gets the number of vertices.
        /// </summary>
        public int VertexCount => _vertices.Count;

        /// <summary>
        /// Adds a vertex.
        /// </summary>
        /// <param name="vertex">The vertex to add.</param>
        public void AddVertex(MeshVertex vertex)
            => _vertices.Add(vertex);

        /// <summary>
        /// Adds an index.
        /// </summary>
        /// <param name="index">The index to add.</param>
        public void AddIndex(int index)
            => _indices.Add(index);

        /// <summary>
        /// Drains the data to a new <see cref="MeshPrimitive"/>.
        /// </summary>
        /// <returns>The new <see cref="MeshPrimitive"/>.</returns>
        public MeshPrimitive Drain()
        {
            var vertices = _vertices;
            var indices = _indices;

            _vertices = [];
            _indices = [];

            return new MeshPrimitive(vertices, indices);
        }
    }
}
