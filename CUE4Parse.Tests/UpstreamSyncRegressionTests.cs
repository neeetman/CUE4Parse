using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.GameTypes.DeadIsland2.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.Tests;

public class UpstreamSyncRegressionTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void UpstreamDeadIsland2ParserAlsoReadsOneDistanceField(int lodCount, bool valid)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(lodCount);
        for (var i = 0; i < lodCount; i++)
        {
            writer.Write((byte) 2); // strip LOD buffers
            writer.Write((byte) 0);
            writer.Write(0); // sections
            writer.Write(0.0f); // MaxDeviation
            writer.Write(-1); // Dambuster extension
            writer.Write(0); // no platform LODs
            writer.Write(0); // not inlined
        }
        writer.Write((byte) 0); // NumInlinedLODs
        writer.Write((byte) 0); // distance-field strip flags
        writer.Write((byte) 0);
        writer.Write(valid ? 1 : 0);
        if (valid) WriteDistanceField(writer);
        for (var i = 1; i <= 7; i++) writer.Write((float) i);
        for (var i = 0; i < 8; i++)
        {
            writer.Write(1);
            writer.Write(i + 0.5f);
        }
        var expectedEnd = stream.Position;
        writer.Write(0x12345678);

        using var bytes = new FByteArchive("upstream-di2-regression", stream.ToArray(), new VersionContainer(EGame.GAME_DeadIsland2));
        using var archive = new FAssetArchive(bytes, null);
        var renderData = new FDeadIsland2StaticMeshRenderData(archive);
        Assert.Equal(lodCount, renderData.LODs!.Length);
        Assert.Equal(7.0f, renderData.Bounds!.SphereRadius);
        Assert.Equal(Enumerable.Range(0, 8).Select(i => i + 0.5f), renderData.ScreenSize);
        Assert.Equal(expectedEnd, archive.Position);
        Assert.Equal(0x12345678, archive.Read<int>());
    }

    [Theory]
    [InlineData(EGame.GAME_DeadIsland2, 0, false, false)]
    [InlineData(EGame.GAME_DeadIsland2, 1, false, false)]
    [InlineData(EGame.GAME_DeadIsland2, 3, false, false)]
    [InlineData(EGame.GAME_DeadIsland2, 3, false, true)]
    [InlineData(EGame.GAME_DeadIsland2, 3, true, false)]
    [InlineData(EGame.GAME_UE4_25, 0, false, false)]
    [InlineData(EGame.GAME_UE4_25, 3, false, false)]
    [InlineData(EGame.GAME_UE4_25, 3, false, true)]
    [InlineData(EGame.GAME_UE4_25, 3, true, false)]
    public void DistanceFieldsLeaveBoundsAndScreenSizesAligned(EGame game, int lodCount, bool stripped, bool valid)
    {
        // A minimal cooked render-data stream. LODs are cooked out so the test exercises
        // distance-field framing independently of vertex/index buffer serialization.
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(lodCount);
        for (var i = 0; i < lodCount; i++)
        {
            writer.Write((byte) 0); // LOD strip flags
            writer.Write((byte) 0);
            writer.Write(0); // sections
            writer.Write(0.0f); // MaxDeviation
            if (game == EGame.GAME_DeadIsland2) writer.Write(-1); // Dambuster extension
            writer.Write(1); // bIsLODCookedOut (UE bool is int32)
            writer.Write(0); // bInlined
        }
        writer.Write((byte) 0); // NumInlinedLODs
        writer.Write((byte) (stripped ? 2 : 0)); // distance-field strip flags
        writer.Write((byte) 0);
        if (!stripped)
        {
            var fields = game == EGame.GAME_DeadIsland2 ? Math.Min(1, lodCount) : lodCount;
            for (var i = 0; i < fields; i++)
            {
                writer.Write(valid ? 1 : 0);
                if (!valid) continue;
                WriteDistanceField(writer);
            }
        }
        for (var i = 1; i <= 7; i++) writer.Write((float) i); // Bounds
        if (game != EGame.GAME_DeadIsland2) writer.Write(1); // shared static lighting
        for (var i = 0; i < 8; i++)
        {
            writer.Write(1); // FPerPlatformFloat.bCooked
            writer.Write(i + 0.5f);
        }
        var expectedEnd = stream.Position;
        writer.Write(0x12345678); // next serialized field must remain untouched

        using var bytes = new FByteArchive("sync-regression", stream.ToArray(), new VersionContainer(game));
        using var archive = new FAssetArchive(bytes, null);
        var renderData = new FStaticMeshRenderData(archive);

        Assert.Equal(lodCount, renderData.LODs!.Length);
        Assert.Equal(1.0f, renderData.Bounds!.Origin.X);
        Assert.Equal(7.0f, renderData.Bounds.SphereRadius);
        Assert.Equal(Enumerable.Range(0, 8).Select(i => i + 0.5f), renderData.ScreenSize);
        Assert.Equal(expectedEnd, archive.Position);
        Assert.Equal(0x12345678, archive.Read<int>());
    }

    [Fact]
    public void DakarAndArcheAgeHaveDistinctIdsWithoutChangingExistingDakarConfigurations()
    {
        Assert.Equal((uint) EGame.GAME_UE4_27 + 40, (uint) EGame.GAME_DakarDesertRally);
        Assert.NotEqual(EGame.GAME_DakarDesertRally, EGame.GAME_ArcheAgeWar);
        foreach (var game in new[] { EGame.GAME_DakarDesertRally, EGame.GAME_ArcheAgeWar })
        {
            Assert.True(game > EGame.GAME_UE4_27 && game < EGame.GAME_UE4_28);
            Assert.Equal(EGame.GAME_UE4_27.GetVersion(), game.GetVersion());
            Assert.Equal(game, Enum.Parse<EGame>(game.ToString()));
        }
    }

    private static void WriteDistanceField(BinaryWriter writer)
    {
        writer.Write(0); // compressed volume byte array
        writer.Write(new byte[12]); // Size
        writer.Write(new byte[25]); // FBox (packed)
        writer.Write(new byte[8]); // DistanceMinMax
        writer.Write(1); // bMeshWasClosed
        writer.Write(0); // bBuiltAsIfTwoSided
        writer.Write(0); // bMeshWasPlane
    }
}
