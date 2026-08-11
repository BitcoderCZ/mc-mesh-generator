using System.Diagnostics;
using System.Numerics;
using BitcoderCZ.Minecraft.MeshGenerator.Utils;

namespace BitcoderCZ.Minecraft.MeshGenerator;

public sealed partial class BlockMeshGenerator
{
    private static string GetBannerColor(string bannerModel)
    {
        Debug.Assert(bannerModel.EndsWith("_banner", StringComparison.Ordinal));
        Debug.Assert(bannerModel.Contains('/'));

        var slashIndex = bannerModel.IndexOf('/');

        var color = bannerModel.AsSpan()[(slashIndex + 1)..^"_banner".Length];

        return color switch
        {
            "white" => "F9FFFE",
            "orange" => "F9801D",
            "magenta" => "C74EBD",
            "light_blue" => "3AB3DA",
            "yellow" => "FED83D",
            "lime" => "80C71F",
            "pink" => "F38BAA",
            "gray" => "474F52",
            "light_gray" => "9D9D97",
            "cyan" => "169C9C",
            "purple" => "8932B8",
            "blue" => "3C44AA",
            "brown" => "835432",
            "green" => "5E7C16",
            "red" => "B02E26",
            "black" => "1D1D21",
            _ => "F9FFFE", // white
        };
    }

    private static void GenerateBannerEntityMesh(MeshPrimitive.Builder slate, MeshPrimitive.Builder pole)
    {
        const float s = GeneratorUtils.BlockModelScale;

        static void AddFace(MeshPrimitive.Builder builder, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal, float uMin, float vMin, float uMax, float vMax)
        {
            AddQuad(builder,
                v0, v1, v2, v3, normal,
                new Vector2(uMin / 64f, vMax / 64f),
                new Vector2(uMax / 64f, vMax / 64f),
                new Vector2(uMax / 64f, vMin / 64f),
                new Vector2(uMin / 64f, vMin / 64f)
            );
        }

        const float slateMinX = -2f * s, slateMaxX = 18f * s;
        const float slateMinY = 4f * s, slateMaxY = 44f * s;
        const float slateMinZ = 9f * s, slateMaxZ = 10f * s;

        AddFace(
            slate,
            new Vector3(slateMinX, slateMinY, slateMaxZ),
            new Vector3(slateMaxX, slateMinY, slateMaxZ),
            new Vector3(slateMaxX, slateMaxY, slateMaxZ),
            new Vector3(slateMinX, slateMaxY, slateMaxZ),
            new Vector3(0f, 0f, 1f),
            0f, 0f, 20f, 40f
        );

        AddFace(
            slate,
            new Vector3(slateMaxX, slateMinY, slateMinZ),
            new Vector3(slateMinX, slateMinY, slateMinZ),
            new Vector3(slateMinX, slateMaxY, slateMinZ),
            new Vector3(slateMaxX, slateMaxY, slateMinZ),
            new Vector3(0f, 0f, -1f),
            21f, 0f, 41f, 40f
        );

        AddFace(
            slate,
            new Vector3(slateMaxX, slateMinY, slateMaxZ),
            new Vector3(slateMaxX, slateMinY, slateMinZ),
            new Vector3(slateMaxX, slateMaxY, slateMinZ),
            new Vector3(slateMaxX, slateMaxY, slateMaxZ),
            new Vector3(1f, 0f, 0f),
            20f, 0f, 21f, 40f
        );

        AddFace(
            slate,
            new Vector3(slateMinX, slateMinY, slateMinZ),
            new Vector3(slateMinX, slateMinY, slateMaxZ),
            new Vector3(slateMinX, slateMaxY, slateMaxZ),
            new Vector3(slateMinX, slateMaxY, slateMinZ),
            new Vector3(-1f, 0f, 0f),
            41f, 0f, 42f, 40f
        );

        AddFace(
            slate,
            new Vector3(slateMinX, slateMaxY, slateMaxZ),
            new Vector3(slateMaxX, slateMaxY, slateMaxZ),
            new Vector3(slateMaxX, slateMaxY, slateMinZ),
            new Vector3(slateMinX, slateMaxY, slateMinZ),
            new Vector3(0f, 1f, 0f),
            0f, 0f, 20f, 1f
        );

        AddFace(
            slate,
            new Vector3(slateMinX, slateMinY, slateMinZ),
            new Vector3(slateMaxX, slateMinY, slateMinZ),
            new Vector3(slateMaxX, slateMinY, slateMaxZ),
            new Vector3(slateMinX, slateMinY, slateMaxZ),
            new Vector3(0f, -1f, 0f),
            0f, 39f, 20f, 40f
        );

        float poleMinX = 7f * s, poleMaxX = 9f * s;
        float poleMinY = 0f * s, poleMaxY = 42f * s;
        float poleMinZ = 7f * s, poleMaxZ = 9f * s;

        AddFace(
            pole,
            new Vector3(poleMinX, poleMinY, poleMaxZ),
            new Vector3(poleMaxX, poleMinY, poleMaxZ),
            new Vector3(poleMaxX, poleMaxY, poleMaxZ),
            new Vector3(poleMinX, poleMaxY, poleMaxZ),
            new Vector3(0f, 0f, 1f),
            46f, 2f, 48f, 44f
        );

        AddFace(
            pole,
            new Vector3(poleMaxX, poleMinY, poleMinZ),
            new Vector3(poleMinX, poleMinY, poleMinZ),
            new Vector3(poleMinX, poleMaxY, poleMinZ),
            new Vector3(poleMaxX, poleMaxY, poleMinZ),
            new Vector3(0f, 0f, -1f),
            50f, 2f, 52f, 44f
        );

        AddFace(
            pole,
            new Vector3(poleMaxX, poleMinY, poleMaxZ),
            new Vector3(poleMaxX, poleMinY, poleMinZ),
            new Vector3(poleMaxX, poleMaxY, poleMinZ),
            new Vector3(poleMaxX, poleMaxY, poleMaxZ),
            new Vector3(1f, 0f, 0f),
            44f, 2f, 46f, 44f
        );

        AddFace(
            pole,
            new Vector3(poleMinX, poleMinY, poleMinZ),
            new Vector3(poleMinX, poleMinY, poleMaxZ),
            new Vector3(poleMinX, poleMaxY, poleMaxZ),
            new Vector3(poleMinX, poleMaxY, poleMinZ),
            new Vector3(-1f, 0f, 0f),
            48f, 2f, 50f, 44f
        );

        AddFace(
            pole,
            new Vector3(poleMinX, poleMaxY, poleMaxZ),
            new Vector3(poleMaxX, poleMaxY, poleMaxZ),
            new Vector3(poleMaxX, poleMaxY, poleMinZ),
            new Vector3(poleMinX, poleMaxY, poleMinZ),
            new Vector3(0f, 1f, 0f),
            46f, 0f, 48f, 2f
        );

        AddFace(
            pole,
            new Vector3(poleMinX, poleMinY, poleMinZ),
            new Vector3(poleMaxX, poleMinY, poleMinZ),
            new Vector3(poleMaxX, poleMinY, poleMaxZ),
            new Vector3(poleMinX, poleMinY, poleMaxZ),
            new Vector3(0f, -1f, 0f),
            48f, 0f, 50f, 2f
        );

        const float cbMinX = -2f * s, cbMaxX = 18f * s;
        const float cbMinY = 42f * s, cbMaxY = 44f * s;
        const float cbMinZ = 7f * s, cbMaxZ = 9f * s;

        AddFace(
            pole,
            new Vector3(cbMinX, cbMinY, cbMaxZ),
            new Vector3(cbMaxX, cbMinY, cbMaxZ),
            new Vector3(cbMaxX, cbMaxY, cbMaxZ),
            new Vector3(cbMinX, cbMaxY, cbMaxZ),
            new Vector3(0f, 0f, 1f),
            2f, 44f, 22f, 46f
        );

        AddFace(
            pole,
            new Vector3(cbMaxX, cbMinY, cbMinZ),
            new Vector3(cbMinX, cbMinY, cbMinZ),
            new Vector3(cbMinX, cbMaxY, cbMinZ),
            new Vector3(cbMaxX, cbMaxY, cbMinZ),
            new Vector3(0f, 0f, -1f),
            24f, 44f, 44f, 46f
        );

        AddFace(
            pole,
            new Vector3(cbMinX, cbMaxY, cbMaxZ),
            new Vector3(cbMaxX, cbMaxY, cbMaxZ),
            new Vector3(cbMaxX, cbMaxY, cbMinZ),
            new Vector3(cbMinX, cbMaxY, cbMinZ),
            new Vector3(0f, 1f, 0f),
            2f, 42f, 22f, 44f
        );

        AddFace(
            pole,
            new Vector3(cbMinX, cbMinY, cbMinZ),
            new Vector3(cbMaxX, cbMinY, cbMinZ),
            new Vector3(cbMaxX, cbMinY, cbMaxZ),
            new Vector3(cbMinX, cbMinY, cbMaxZ),
            new Vector3(0f, -1f, 0f),
            22f, 42f, 42f, 44f
        );

        AddFace(
            pole,
            new Vector3(cbMaxX, cbMinY, cbMaxZ),
            new Vector3(cbMaxX, cbMinY, cbMinZ),
            new Vector3(cbMaxX, cbMaxY, cbMinZ),
            new Vector3(cbMaxX, cbMaxY, cbMaxZ),
            new Vector3(1f, 0f, 0f),
            0f, 44f, 2f, 46f
        );

        AddFace(
            pole,
            new Vector3(cbMinX, cbMinY, cbMinZ),
            new Vector3(cbMinX, cbMinY, cbMaxZ),
            new Vector3(cbMinX, cbMaxY, cbMaxZ),
            new Vector3(cbMinX, cbMaxY, cbMinZ),
            new Vector3(-1f, 0f, 0f),
            22f, 44f, 24f, 46f
        );
    }
}