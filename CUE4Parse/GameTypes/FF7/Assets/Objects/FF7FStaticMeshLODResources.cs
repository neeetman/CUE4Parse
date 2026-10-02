using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse.GameTypes.FF7.Assets.Objects;

public static class FF7FStaticMeshLODResources
{
    public static bool SerializeIndexBuffer(FArchive Ar, out int[] sectionTrianglesCount, out uint[] indexBuffer)
    {
        indexBuffer = [];
        sectionTrianglesCount = [];
        var version = Ar.Read<int>();
        var tempAr = new FByteArchive("FF7Rebirth_SM", Ar.ReadArray<byte>(), Ar.Versions);
        tempAr.Position += 28; // skip header, idk what that data is
        var batches = tempAr.ReadArray<FF7Batch>();
        if (batches.Length == 0) return false;

        var indicesBuffer = tempAr.ReadArray<uint>();
        var batchesBuffer = tempAr.ReadArray<uint>();
        // The cluster records are 80 bytes (FF7BatchInfo) in older cooks and 68 bytes in the 1.0.0.5 cook
        // (the MassiveEnvironment cluster record); neither is used here, so the size is decided by
        // which one leaves the following arrays consistent.
        var batchInfoCount = tempAr.Read<int>();
        var batchInfoSize = BatchInfoSize(tempAr, batches.Length, batchInfoCount);
        tempAr.Position += (long) batchInfoCount * batchInfoSize;

        var lods = tempAr.ReadArray(() => new FF7Lod(tempAr));
        var batchesIndices = tempAr.ReadArray<int>(); // batches reordering ???
        var somestructs = tempAr.ReadArray(() => new Ff7SomeStruct(tempAr));

        var index = tempAr.Read<int>(); // always 0
        var sectionsIndices = tempAr.ReadArray<uint>();
        var lodinfos = tempAr.ReadArray(() => new FF7LodInfo(tempAr));

        // 1.0.0.5 cooks store every meshlet LOD level of a section in one buffer; the cluster groups
        // (`lods`) carry each level's error interval. Keep the finest level only, or the coarse levels
        // are drawn on top of it.
        var keepBatch = batchInfoSize == 68 ? FinestBatches(lods, batches.Length) : null;
        var keepTriangle = new bool[batchesBuffer.Length];
        var ib = new List<uint>(batchesBuffer.Length * 3);
        for (var b = 0; b < batches.Length; b++)
        {
            var batch = batches[b];
            for (var i = 0; i < batch.TrianglesCount; i++)
                keepTriangle[batch.TotatTriangles + i] = keepBatch == null || keepBatch[b];
        }
        foreach (var batch in batches)
        {
            var vertices = new HashSet<uint>();
            var startIndex = batch.TotalVertices;
            var triangleStartIndex = batch.TotatTriangles;
            for (var i = 0; i < batch.TrianglesCount; i++)
            {
                uint x = batchesBuffer[triangleStartIndex+i];
                var iv = indicesBuffer[startIndex + (x & 0x3ff)];
                var jv = indicesBuffer[startIndex + ((x >> 10) & 0x3ff)];
                var kv = indicesBuffer[startIndex + ((x >> 20) & 0x3ff)];
                ib.Add(iv);
                ib.Add(jv);
                ib.Add(kv);
                vertices.Add(iv);
                vertices.Add(jv);
                vertices.Add(kv);
            }
        }
        var fullIndexBuffer = ib.ToArray();

        List<uint> indexbuffer = [];
        sectionTrianglesCount = new int[sectionsIndices.Length];
        for (int i = 0; i < sectionsIndices.Length; i++)
        {
            var section = lodinfos[sectionsIndices[i]];
            var kept = 0;
            for (var t = section.BatchesOffset; t < section.BatchesOffset + section.BatchesCount; t++)
            {
                if (!keepTriangle[t]) continue;
                indexbuffer.Add(fullIndexBuffer[t * 3]);
                indexbuffer.Add(fullIndexBuffer[t * 3 + 1]);
                indexbuffer.Add(fullIndexBuffer[t * 3 + 2]);
                kept++;
            }
            sectionTrianglesCount[i] = kept;
        }

        // The trailing section/LOD-info copies are not used, and their layout differs between cooks
        // (Box_Single_01A_Physics in 1.0.0.5 ends 4 bytes into an FF7LodInfo); reading them threw
        // away an index buffer that was already complete.

        tempAr.Dispose();
        indexBuffer = indexbuffer.ToArray();
        return true;
    }

    private static int BatchInfoSize(FArchive Ar, int batchCount, int batchInfoCount)
    {
        var start = Ar.Position;
        foreach (var size in (int[]) [80, 68])
        {
            var lodsAt = start + (long) batchInfoCount * size;
            if (lodsAt + 4 > Ar.Length) continue;
            Ar.Position = lodsAt;
            var lodCount = Ar.Read<int>();
            var orderAt = lodsAt + 4 + (long) lodCount * 64;
            if (lodCount < 0 || orderAt + 4 > Ar.Length) continue;
            Ar.Position = orderAt;
            if (Ar.Read<int>() == batchCount)
            {
                Ar.Position = start;
                return size;
            }
        }
        Ar.Position = start;
        return 80;
    }

    private static bool[] FinestBatches(FF7Lod[] groups, int batchCount)
    {
        var keep = new bool[batchCount];
        bool Pick(Func<FF7Lod, bool> select)
        {
            var any = false;
            foreach (var g in groups)
            {
                if (!select(g)) continue;
                for (var b = Math.Max(g.Offset, 0); b < g.Offset + g.Count && b < batchCount; b++)
                {
                    keep[b] = true;
                    any = true;
                }
            }
            return any;
        }
        // Error interval in Position3.Z / Position3.W: the finest level starts below zero and is finite;
        // an interval reaching 1e10 is the always-on root proxy.
        if (!Pick(g => g.Position3.Z < 0 && g.Position3.W < 1e9f) && !Pick(g => g.Position3.Z <= 0))
            Array.Fill(keep, true);
        return keep;
    }
}

public struct FF7Batch
{
    public int VerticesCount;
    public int TotalVertices;
    public int TrianglesCount;
    public int TotatTriangles;
};

public struct FF7BatchInfo(FArchive Ar)
{
    public FVector4 Position1 = Ar.Read<FVector4>();
    public FVector4 Position2 = Ar.Read<FVector4>();
    public FVector SomeVector1= Ar.Read<FHalfVector>();
    public FVector SomeVector2= Ar.Read<FHalfVector>();
    public float Distance = Ar.Read<float>();
    public FVector MinVertexPosition = Ar.Read<FVector>();
    public float SomeFloat = Ar.Read<float>();
    public FVector MaxVertexPosition = Ar.Read<FVector>();
    public uint Hash = Ar.Read<uint>();
}

public struct Ff7SomeStruct(FArchive Ar)
{
    public FVector4[][] Vectors = Ar.ReadArray(3, () => Ar.ReadArray<FVector4>(8));
    public int[] Flags = Ar.ReadArray<int>(8);
}

public struct FF7Lod(FArchive Ar)
{
    public int Offset = Ar.Read<int>();
    public int Count = Ar.Read<int>();
    public int NextOffset = Ar.Read<int>();
    public int NextCount = Ar.Read<int>();

    public FVector4 Position1 = Ar.Read<FVector4>();
    public FVector4 Position2 = Ar.Read<FVector4>();
    public FVector4 Position3 = Ar.Read<FVector4>();
}

public struct FF7LodInfo(FArchive Ar)
{
    public int Index = Ar.Read<int>();
    public int idk = Ar.Read<int>();
    public int Offset = Ar.Read<int>();
    public int Count = Ar.Read<int>();
    public int IndicesOffset = Ar.Read<int>();
    public int IndicesCount = Ar.Read<int>();
    public int BatchesOffset = Ar.Read<int>();
    public int BatchesCount = Ar.Read<int>();
    public int VerticesOffset = Ar.Read<int>();
    public int VerticesCount = Ar.Read<int>();
    public ushort[] SomeInts1 = Ar.ReadArray<ushort>(4);
    public float[] SomeFloats = Ar.ReadArray<float>(5);
    public ushort[] SomeInts2 = Ar.ReadArray<ushort>(14);
}
