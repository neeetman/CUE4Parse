using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.Assets.Exports.StaticMesh;

public class FRawStaticIndexBuffer() : FRawIndexBuffer
{
    public FRawStaticIndexBuffer(FArchive Ar) : this()
    {
        if (Ar.Ver < EUnrealEngineObjectUE4Version.SUPPORT_32BIT_STATIC_MESH_INDICES)
        {
            SetIndices(Ar.ReadBulkArray<ushort>());
        }
        else
        {
            var is32bit = Ar.ReadBoolean();
            if (Ar.Game == GAME_TheOuterWorlds)
            {
                ReadIndianaIndices(Ar, is32bit);
                return;
            }
            var data = Ar.ReadBulkArray<byte>();
            using var tempAr = new FByteArchive("IndicesReader", data, Ar.Versions);

            if (Ar.Versions["RawIndexBuffer.HasShouldExpandTo32Bit"])
            {
                var bShouldExpandTo32Bit = Ar.ReadBoolean();
            }
            
            if (tempAr.Length == 0)
            {
                tempAr.Dispose();
                return;
            }

            if (is32bit)
            {
                var count = (int)tempAr.Length / 4;
                SetIndices(tempAr.ReadArray<uint>(count));
            }
            else
            {
                var count = (int)tempAr.Length / 2;
                SetIndices(tempAr.ReadArray<ushort>(count));

                if (Ar.Ver < EUnrealEngineObjectUE3Version.RENDERING_REFACTOR) Ar.Read<int>();
            }

            if (Ar.Game == GAME_PlayerUnknownsBattlegrounds && Buffer is not null)
            {
                var cur = 0;
                for (var i = 0; i < Buffer.Length; i++)
                {
                    cur += (short)Buffer[i];
                    Buffer[i] = (ushort)cur;
                }
            }
        }
    }

    private void ReadIndianaIndices(FArchive ar, bool is32Bit)
    {
        // Indiana stores one signed delta per index after the first. 0x7f
        // selects the next absolute index from a separate table.
        var count = ar.Read<int>();
        var deltaStride = ar.Read<int>();
        if (count == 0 && deltaStride == 0) return;
        var deltaCount = ar.Read<int>();
        if (count <= 0 || deltaStride != 1 || deltaCount != count - 1 ||
            deltaCount > ar.Length - ar.Position - 8)
            throw new ParserException(ar, "Invalid Indiana index delta array");
        var deltas = ar.ReadArray<sbyte>(deltaCount);
        var absoluteStride = ar.Read<int>();
        var absoluteCount = ar.Read<int>();
        if (absoluteStride != (is32Bit ? 4 : 2) ||
            absoluteCount != 1 + deltas.Count(delta => delta == 127) ||
            absoluteCount > (ar.Length - ar.Position) / absoluteStride)
            throw new ParserException(ar, "Invalid Indiana absolute index array");

        var indices = new uint[count];
        long index = ReadAbsolute();
        indices[0] = (uint) index;
        for (var i = 0; i < deltas.Length; i++)
        {
            index = deltas[i] == 127 ? ReadAbsolute() : index + deltas[i];
            if (index < 0 || index > (is32Bit ? uint.MaxValue : ushort.MaxValue))
                throw new ParserException(ar, "Indiana index delta out of range");
            indices[i + 1] = (uint) index;
        }
        SetIndices(indices);

        uint ReadAbsolute() => is32Bit ? ar.Read<uint>() : ar.Read<ushort>();
    }
}
