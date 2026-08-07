using System.Diagnostics;
using System.Numerics;
using BitcoderCZ.Maths.Vectors;
using BitcoderCZ.Minecraft.MeshGenerator.Models.ResourcePacks;

namespace BitcoderCZ.Minecraft.MeshGenerator;

internal static class GeneratorUtils
{
    public static Matrix4x4 CreateVariantTransform(VariantModel variant)
    {
        if (variant is { RotationX: 0, RotationY: 0, RotationZ: 0 })
        {
            return Matrix4x4.Identity;
        }

        var center = new Vector3(0.5f, 0.5f, 0.5f);

        return Matrix4x4.CreateTranslation(-center)
             * CreateMinecraftRotation(variant.RotationX, variant.RotationY, variant.RotationZ)
             * Matrix4x4.CreateTranslation(center);
    }

    public static Matrix4x4 CreateElementTransform(BlockElementRotation? rot, float blockModelScale)
    {
        if (!rot.HasValue)
        {
            return Matrix4x4.Identity;
        }

        var r = rot.Value;
        var origin = r.Origin * blockModelScale;

        var radX = float.DegreesToRadians(r.X);
        var radY = float.DegreesToRadians(r.Y);
        var radZ = float.DegreesToRadians(r.Z);

        var matrix = Matrix4x4.Identity;

        // Move to Origin
        matrix *= Matrix4x4.CreateTranslation(-origin);

        // Rotate
        matrix *= CreateMinecraftRotation(r.X, r.Y, r.Z);

        if (r.ReScale)
        {
            var scaleX = r.X != 0 ? 1f / float.Cos(radX) : 1f;
            var scaleY = r.Y != 0 ? 1f / float.Cos(radY) : 1f;
            var scaleZ = r.Z != 0 ? 1f / float.Cos(radZ) : 1f;
            matrix *= Matrix4x4.CreateScale(scaleX, scaleY, scaleZ);
        }

        // Move back from Origin
        matrix *= Matrix4x4.CreateTranslation(origin);

        return matrix;
    }

    private static Matrix4x4 CreateMinecraftRotation(float degreesX, float degreesY, float degreesZ)
    {
        var radX = degreesX * (float.Pi / 180f);
        var radY = -degreesY * (float.Pi / 180f);
        var radZ = degreesZ * (float.Pi / 180f);

        return Matrix4x4.CreateRotationY(radY)
            * Matrix4x4.CreateRotationX(radX)
            * Matrix4x4.CreateRotationZ(radZ);
    }

    public static void BuildFace(Vector3 blockPosition, Direction dir, Vector3 from, Vector3 to, BlockFace face, Matrix4x4 transform, bool uvLock, MeshPrimitive.Builder primitive, float blockModelScale)
    {
        var startIndex = primitive.VertexCount;

        Span<Vector3> corners = stackalloc Vector3[4];
        GetFaceVertices(dir, from, to, corners, out var normal);

        Span<Vector2> uvs = stackalloc Vector2[4];
        CalculateUVs(face.UV, face.Rotation, uvs, blockModelScale);

        for (var i = 0; i < 4; i++)
        {
            var pos = blockPosition + Vector3.Transform(corners[i], transform);

            var norm = Vector3.Normalize(Vector3.TransformNormal(normal, transform));

            primitive.AddVertex(new MeshVertex(pos, norm, uvs[i]/*, face.TintIndex*/));
        }

        primitive.AddIndex(startIndex + 0);
        primitive.AddIndex(startIndex + 1);
        primitive.AddIndex(startIndex + 2);
        primitive.AddIndex(startIndex + 2);
        primitive.AddIndex(startIndex + 3);
        primitive.AddIndex(startIndex + 0);
    }

    private static void GetFaceVertices(Direction dir, Vector3 from, Vector3 to, Span<Vector3> corners, out Vector3 normal)
    {
        Debug.Assert(corners.Length is 4);

        // Z may need to be flipped
        switch (dir)
        {
            case Direction.Up: // +Y
                normal = Vector3.UnitY;
                corners[0] = new Vector3(from.X, to.Y, from.Z);
                corners[1] = new Vector3(from.X, to.Y, to.Z);
                corners[2] = new Vector3(to.X, to.Y, to.Z);
                corners[3] = new Vector3(to.X, to.Y, from.Z);
                break;
            case Direction.Down: // -Y
                normal = -Vector3.UnitY;
                corners[0] = new Vector3(from.X, from.Y, to.Z);
                corners[1] = new Vector3(from.X, from.Y, from.Z);
                corners[2] = new Vector3(to.X, from.Y, from.Z);
                corners[3] = new Vector3(to.X, from.Y, to.Z);
                break;
            case Direction.East: // +X
                normal = Vector3.UnitX;
                corners[0] = new Vector3(to.X, to.Y, to.Z);
                corners[1] = new Vector3(to.X, from.Y, to.Z);
                corners[2] = new Vector3(to.X, from.Y, from.Z);
                corners[3] = new Vector3(to.X, to.Y, from.Z);
                break;
            case Direction.West: // -X
                normal = -Vector3.UnitX;
                corners[0] = new Vector3(from.X, to.Y, from.Z);
                corners[1] = new Vector3(from.X, from.Y, from.Z);
                corners[2] = new Vector3(from.X, from.Y, to.Z);
                corners[3] = new Vector3(from.X, to.Y, to.Z);
                break;
            case Direction.North: // -Z
                normal = -Vector3.UnitZ;
                corners[0] = new Vector3(to.X, to.Y, from.Z);
                corners[1] = new Vector3(to.X, from.Y, from.Z);
                corners[2] = new Vector3(from.X, from.Y, from.Z);
                corners[3] = new Vector3(from.X, to.Y, from.Z);
                break;
            case Direction.South: // +Z
                normal = Vector3.UnitZ;
                corners[0] = new Vector3(from.X, to.Y, to.Z);
                corners[1] = new Vector3(from.X, from.Y, to.Z);
                corners[2] = new Vector3(to.X, from.Y, to.Z);
                corners[3] = new Vector3(to.X, to.Y, to.Z);
                break;
            default:
                normal = Vector3.Zero;
                break;
        }
    }

    private static void CalculateUVs(UVCoordinates uv, int rotation, Span<Vector2> result, float blockModelScale)
    {
        Debug.Assert(result.Length is 4);

        // Scale 0-16 to 0-1.
        var u0 = uv.Min.X * blockModelScale;
        var v0 = uv.Min.Y * blockModelScale;
        var u1 = uv.Max.X * blockModelScale;
        var v1 = uv.Max.Y * blockModelScale;

        // top-left, bottom-left, bottom-right, top-right
        result[0] = new Vector2(u0, v0);
        result[1] = new Vector2(u0, v1);
        result[2] = new Vector2(u1, v1);
        result[3] = new Vector2(u1, v0);

        // If rotation is applied (90, 180, 270), shift the array
        if (rotation != 0)
        {
            var shifts = rotation / 90 % 4;
            if (shifts is 1)
            {
                var tmp = result[0];
                result[0] = result[1];
                result[1] = result[2];
                result[2] = result[3];
                result[3] = tmp;
            }
            else if (shifts is 2)
            {
                var tmp0 = result[0];
                var tmp1 = result[1];
                result[0] = result[2];
                result[1] = result[3];
                result[2] = tmp0;
                result[3] = tmp1;
            }
            else if (shifts is 3)
            {
                var tmp = result[3];
                result[3] = result[2];
                result[2] = result[1];
                result[1] = result[0];
                result[0] = tmp;
            }
        }
    }

    public static int3 GetDirectionOffset(Direction dir)
        => dir switch
        {
            Direction.East => new int3(1, 0, 0),
            Direction.West => new int3(-1, 0, 0),
            Direction.Up => new int3(0, 1, 0),
            Direction.Down => new int3(0, -1, 0),
            Direction.South => new int3(0, 0, 1),
            Direction.North => new int3(0, 0, -1),
            _ => int3.Zero
        };

    public static Vector3 GetDirectionVector3(Direction dir)
        => dir switch
        {
            Direction.East => Vector3.UnitX,
            Direction.West => -Vector3.UnitX,
            Direction.Up => Vector3.UnitY,
            Direction.Down => -Vector3.UnitY,
            Direction.South => Vector3.UnitZ,
            Direction.North => -Vector3.UnitZ,
            _ => Vector3.Zero
        };

    public static Direction GetClosestDirection(Vector3 normal)
    {
        normal = Vector3.Normalize(normal);
        var maxDot = -2f; // init lower than any possible dot product (-1 to 1)
        var closest = Direction.Up;

        for (var i = 0; i < 6; i++)
        {
            var dir = (Direction)i;
            var dot = Vector3.Dot(normal, GetDirectionVector3(dir));
            if (dot > maxDot)
            {
                maxDot = dot;
                closest = dir;
            }
        }

        return closest;
    }
}
