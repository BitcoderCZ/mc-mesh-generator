using System.Diagnostics;
using System.Numerics;
using BitcoderCZ.Minecraft.MeshGenerator.Utils;

namespace BitcoderCZ.Minecraft.MeshGenerator;

public sealed partial class BlockMeshGenerator
{
    private static string GetBedColor(string bedModel)
    {
        Debug.Assert(bedModel.EndsWith("_bed", StringComparison.Ordinal));

        var slashIndex = bedModel.LastIndexOf('/');
        var colonIndex = bedModel.LastIndexOf(':');
        var startIndex = int.Max(slashIndex, colonIndex) + 1;

        var color = bedModel.AsSpan()[startIndex..^"_bed".Length];

        return color.ToString();
    }

    private static void GenerateBedEntityMesh(MeshPrimitive.Builder bed)
    {
        const float s = GeneratorUtils.BlockModelScale;

        static Vector2 UV(float u, float v)
        {
            return new Vector2(u / 64f, v / 64f);
        }

        AddQuad(bed,
            new Vector3(0f * s, 9f * s, 16f * s), new Vector3(16f * s, 9f * s, 16f * s),
            new Vector3(16f * s, 9f * s, 0f * s), new Vector3(0f * s, 9f * s, 0f * s),
            new Vector3(0f, 1f, 0f),
            UV(6f, 22f), UV(22f, 22f), UV(22f, 6f), UV(6f, 6f));

        AddQuad(bed,
            new Vector3(0f * s, 3f * s, 0f * s), new Vector3(16f * s, 3f * s, 0f * s),
            new Vector3(16f * s, 3f * s, 16f * s), new Vector3(0f * s, 3f * s, 16f * s),
            new Vector3(0f, -1f, 0f),
            UV(44f, 6f), UV(28f, 6f), UV(28f, 22f), UV(44f, 22f));

        AddQuad(bed,
            new Vector3(16f * s, 3f * s, 0f * s), new Vector3(0f * s, 3f * s, 0f * s),
            new Vector3(0f * s, 9f * s, 0f * s), new Vector3(16f * s, 9f * s, 0f * s),
            new Vector3(0f, 0f, -1f),
            UV(22f, 0f), UV(6f, 0f), UV(6f, 6f), UV(22f, 6f));

        AddQuad(bed,
            new Vector3(0f * s, 3f * s, 16f * s), new Vector3(16f * s, 3f * s, 16f * s),
            new Vector3(16f * s, 9f * s, 16f * s), new Vector3(0f * s, 9f * s, 16f * s),
            new Vector3(0f, 0f, 1f),
            UV(38f, 0f), UV(22f, 0f), UV(22f, 6f), UV(38f, 6f));

        AddQuad(bed,
            new Vector3(0f * s, 3f * s, 0f * s), new Vector3(0f * s, 3f * s, 16f * s),
            new Vector3(0f * s, 9f * s, 16f * s), new Vector3(0f * s, 9f * s, 0f * s),
            new Vector3(-1f, 0f, 0f),
            UV(0f, 6f), UV(0f, 22f), UV(6f, 22f), UV(6f, 6f));

        AddQuad(bed,
            new Vector3(16f * s, 3f * s, 16f * s), new Vector3(16f * s, 3f * s, 0f * s),
            new Vector3(16f * s, 9f * s, 0f * s), new Vector3(16f * s, 9f * s, 16f * s),
            new Vector3(1f, 0f, 0f),
            UV(28f, 22f), UV(28f, 6f), UV(22f, 6f), UV(22f, 22f));

        AddQuad(bed,
            new Vector3(0f * s, 9f * s, 32f * s), new Vector3(16f * s, 9f * s, 32f * s),
            new Vector3(16f * s, 9f * s, 16f * s), new Vector3(0f * s, 9f * s, 16f * s),
            new Vector3(0f, 1f, 0f),
            UV(6f, 44f), UV(22f, 44f), UV(22f, 28f), UV(6f, 28f));

        AddQuad(bed,
            new Vector3(0f * s, 3f * s, 16f * s), new Vector3(16f * s, 3f * s, 16f * s),
            new Vector3(16f * s, 3f * s, 32f * s), new Vector3(0f * s, 3f * s, 32f * s),
            new Vector3(0f, -1f, 0f),
            UV(44f, 28f), UV(28f, 28f), UV(28f, 44f), UV(44f, 44f));

        AddQuad(bed,
            new Vector3(16f * s, 3f * s, 16f * s), new Vector3(0f * s, 3f * s, 16f * s),
            new Vector3(0f * s, 9f * s, 16f * s), new Vector3(16f * s, 9f * s, 16f * s),
            new Vector3(0f, 0f, -1f),
            UV(22f, 22f), UV(6f, 22f), UV(6f, 28f), UV(22f, 28f));

        AddQuad(bed,
            new Vector3(0f * s, 3f * s, 32f * s), new Vector3(16f * s, 3f * s, 32f * s),
            new Vector3(16f * s, 9f * s, 32f * s), new Vector3(0f * s, 9f * s, 32f * s),
            new Vector3(0f, 0f, 1f),
            UV(38f, 22f), UV(22f, 22f), UV(22f, 28f), UV(38f, 28f));

        AddQuad(bed,
            new Vector3(0f * s, 3f * s, 16f * s), new Vector3(0f * s, 3f * s, 32f * s),
            new Vector3(0f * s, 9f * s, 32f * s), new Vector3(0f * s, 9f * s, 16f * s),
            new Vector3(-1f, 0f, 0f),
            UV(0f, 28f), UV(0f, 44f), UV(6f, 44f), UV(6f, 28f));

        AddQuad(bed,
            new Vector3(16f * s, 3f * s, 32f * s), new Vector3(16f * s, 3f * s, 16f * s),
            new Vector3(16f * s, 9f * s, 16f * s), new Vector3(16f * s, 9f * s, 32f * s),
            new Vector3(1f, 0f, 0f),
            UV(28f, 44f), UV(28f, 28f), UV(22f, 28f), UV(22f, 44f));

        void AddLeg(float minX, float minZ, float u, float v, int rotation)
        {
            var maxX = minX + 3f * s;
            var maxZ = minZ + 3f * s;
            var minY = 0f * s;
            var maxY = 3f * s;

            void GetSideUVs(int regionIndex, Span<Vector2> uvs)
            {
                var u0 = u + (regionIndex * 3f);
                var u1 = u0 + 3f;
                var v0 = v + 3f;
                var v1 = v + 6f;
                uvs[3] = UV(u1, v0);
                uvs[2] = UV(u0, v0);
                uvs[1] = UV(u0, v1);
                uvs[0] = UV(u1, v1);
            }

            Span<Vector2> westUVs = stackalloc Vector2[4];
            GetSideUVs((0 + rotation) % 4, westUVs);
            Span<Vector2> northUVs = stackalloc Vector2[4];
            GetSideUVs((1 + rotation) % 4, northUVs);
            Span<Vector2> eastUVs = stackalloc Vector2[4];
            GetSideUVs((2 + rotation) % 4, eastUVs);
            Span<Vector2> southUVs = stackalloc Vector2[4];
            GetSideUVs((3 + rotation) % 4, southUVs);

            static void RotateUVs(Span<Vector2> corners, int rot)
            {
                rot = (rot % 4 + 4) % 4;
                if (rot == 0)
                {
                    return;
                }

                Span<Vector2> temp = stackalloc Vector2[4];
                corners.CopyTo(temp);

                for (var i = 0; i < 4; i++)
                {
                    corners[i] = temp[(i + rot) % 4];
                }
            }

            Span<Vector2> topUVs = [UV(u + 3f, v + 3f), UV(u + 6f, v + 3f), UV(u + 6f, v), UV(u + 3f, v)];
            RotateUVs(topUVs, rotation);

            Span<Vector2> bottomUVs = [UV(u + 6f, v + 3f), UV(u + 9f, v + 3f), UV(u + 9f, v), UV(u + 6f, v)];
            RotateUVs(bottomUVs, (4 - rotation) % 4);

            AddQuad(bed,
                new Vector3(minX, maxY, maxZ), new Vector3(maxX, maxY, maxZ),
                new Vector3(maxX, maxY, minZ), new Vector3(minX, maxY, minZ),
                new Vector3(0f, 1f, 0f),
                topUVs[0], topUVs[1], topUVs[2], topUVs[3]);

            AddQuad(bed,
                new Vector3(minX, minY, minZ), new Vector3(maxX, minY, minZ),
                new Vector3(maxX, minY, maxZ), new Vector3(minX, minY, maxZ),
                new Vector3(0f, -1f, 0f),
                bottomUVs[0], bottomUVs[1], bottomUVs[2], bottomUVs[3]);

            AddQuad(bed,
                new Vector3(maxX, minY, minZ), new Vector3(minX, minY, minZ),
                new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ),
                new Vector3(0f, 0f, -1f),
                northUVs[0], northUVs[1], northUVs[2], northUVs[3]);

            AddQuad(bed,
                new Vector3(minX, minY, maxZ), new Vector3(maxX, minY, maxZ),
                new Vector3(maxX, maxY, maxZ), new Vector3(minX, maxY, maxZ),
                new Vector3(0f, 0f, 1f),
                southUVs[0], southUVs[1], southUVs[2], southUVs[3]);

            AddQuad(bed,
                new Vector3(minX, minY, minZ), new Vector3(minX, minY, maxZ),
                new Vector3(minX, maxY, maxZ), new Vector3(minX, maxY, minZ),
                new Vector3(-1f, 0f, 0f),
                westUVs[0], westUVs[1], westUVs[2], westUVs[3]);

            AddQuad(bed,
                new Vector3(maxX, minY, maxZ), new Vector3(maxX, minY, minZ),
                new Vector3(maxX, maxY, minZ), new Vector3(maxX, maxY, maxZ),
                new Vector3(1f, 0f, 0f),
                eastUVs[0], eastUVs[1], eastUVs[2], eastUVs[3]);
        }

        AddLeg(0f * s, 0f * s, 50f, 18f, 0);
        AddLeg(0f * s, 29f * s, 50f, 12f, 1);
        AddLeg(13f * s, 29f * s, 50f, 0f, 2);
        AddLeg(13f * s, 0f * s, 50f, 6f, 3);
    }
}
