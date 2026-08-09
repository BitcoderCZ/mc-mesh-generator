using System.Numerics;

namespace BitcoderCZ.Minecraft.MeshGenerator;

public sealed partial class BlockMeshGenerator
{
    private static void GenerateChestEntityMesh(MeshPrimitive.Builder primitive, string chestType, Matrix4x4 transform)
    {
        if (chestType == "left")
        {
            AddEntityCuboid(primitive, new Vector3(0f, 0f, 1f), new Vector3(15f, 10f, 14f), 0, 19, transform); // Base
            AddEntityCuboid(primitive, new Vector3(0f, 9f, 1f), new Vector3(15f, 5f, 14f), 0, 0, transform);  // Lid
            AddEntityCuboid(primitive, new Vector3(0f, 7f, 15f), new Vector3(1f, 4f, 1f), 0, 0, transform);   // Latch
        }
        else if (chestType == "right")
        {
            AddEntityCuboid(primitive, new Vector3(1f, 0f, 1f), new Vector3(15f, 10f, 14f), 0, 19, transform); // Base
            AddEntityCuboid(primitive, new Vector3(1f, 9f, 1f), new Vector3(15f, 5f, 14f), 0, 0, transform);  // Lid
            AddEntityCuboid(primitive, new Vector3(15f, 7f, 15f), new Vector3(1f, 4f, 1f), 0, 0, transform);  // Latch
        }
        else // single
        {
            AddEntityCuboid(primitive, new Vector3(1f, 0f, 1f), new Vector3(14f, 10f, 14f), 0, 19, transform); // Base
            AddEntityCuboid(primitive, new Vector3(1f, 9f, 1f), new Vector3(14f, 5f, 14f), 0, 0, transform);  // Lid
            AddEntityCuboid(primitive, new Vector3(7f, 7f, 15f), new Vector3(2f, 4f, 1f), 0, 0, transform);   // Latch
        }
    }

    private static void AddEntityCuboid(MeshPrimitive.Builder primitive, Vector3 pos, Vector3 size, int u, int v, Matrix4x4 transform)
    {
        const float scale = GeneratorUtils.BlockModelScale;

        const float tw = 64f;
        const float th = 64f;

        var x1 = pos.X * scale;
        var y1 = pos.Y * scale;
        var z1 = pos.Z * scale;
        var x2 = (pos.X + size.X) * scale;
        var y2 = (pos.Y + size.Y) * scale;
        var z2 = (pos.Z + size.Z) * scale;

        var dx = size.X;
        var dy = size.Y;
        var dz = size.Z;

        var u0 = u;
        var u1 = u + dz;
        var u2 = u + dz + dx;
        var u3_down = u + dz + dx + dx;
        var u3_side = u + dz + dx + dz;
        var u4_side = u + dz + dx + dz + dx;

        var v0 = v;
        var v1 = v + dz;
        var v2 = v + dz + dy;

        void AddTransformedQuad(Vector3 v0Vec, Vector3 v1Vec, Vector3 v2Vec, Vector3 v3Vec, Vector3 normal, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            v0Vec = Vector3.Transform(v0Vec, transform);
            v1Vec = Vector3.Transform(v1Vec, transform);
            v2Vec = Vector3.Transform(v2Vec, transform);
            v3Vec = Vector3.Transform(v3Vec, transform);
            normal = Vector3.Normalize(Vector3.TransformNormal(normal, transform));

            AddQuad(primitive, v0Vec, v1Vec, v2Vec, v3Vec, normal, uv0, uv1, uv2, uv3);
        }

        AddTransformedQuad(
               new Vector3(x1, y2, z2), new Vector3(x2, y2, z2), new Vector3(x2, y2, z1), new Vector3(x1, y2, z1),
               new Vector3(0, 1, 0),
               new Vector2(u2 / tw, v1 / th),
               new Vector2(u3_down / tw, v1 / th),
               new Vector2(u3_down / tw, v0 / th),
               new Vector2(u2 / tw, v0 / th));

        AddTransformedQuad(
            new Vector3(x1, y1, z1), new Vector3(x2, y1, z1), new Vector3(x2, y1, z2), new Vector3(x1, y1, z2),
            new Vector3(0, -1, 0),
            new Vector2(u1 / tw, v0 / th),
            new Vector2(u2 / tw, v0 / th),
            new Vector2(u2 / tw, v1 / th),
            new Vector2(u1 / tw, v1 / th));

        AddTransformedQuad(
            new Vector3(x1, y1, z1), new Vector3(x1, y1, z2), new Vector3(x1, y2, z2), new Vector3(x1, y2, z1),
            new Vector3(-1, 0, 0),
            new Vector2(u0 / tw, v2 / th),
            new Vector2(u1 / tw, v2 / th),
            new Vector2(u1 / tw, v1 / th),
            new Vector2(u0 / tw, v1 / th));

        AddTransformedQuad(
            new Vector3(x2, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y2, z1), new Vector3(x2, y2, z1),
            new Vector3(0, 0, -1),
            new Vector2(u1 / tw, v2 / th),
            new Vector2(u2 / tw, v2 / th),
            new Vector2(u2 / tw, v1 / th),
            new Vector2(u1 / tw, v1 / th));

        AddTransformedQuad(
            new Vector3(x2, y1, z2), new Vector3(x2, y1, z1), new Vector3(x2, y2, z1), new Vector3(x2, y2, z2),
            new Vector3(1, 0, 0),
            new Vector2(u2 / tw, v2 / th),
            new Vector2(u3_side / tw, v2 / th),
            new Vector2(u3_side / tw, v1 / th),
            new Vector2(u2 / tw, v1 / th));

        AddTransformedQuad(
            new Vector3(x1, y1, z2), new Vector3(x2, y1, z2), new Vector3(x2, y2, z2), new Vector3(x1, y2, z2),
            new Vector3(0, 0, 1),
            new Vector2(u3_side / tw, v2 / th),
            new Vector2(u4_side / tw, v2 / th),
            new Vector2(u4_side / tw, v1 / th),
            new Vector2(u3_side / tw, v1 / th));
    }
}
