using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;

namespace CUE4Parse_Conversion.Dto;

public partial class MeshLodDto<TVertex>
{
    internal static MeshLodDto<SkinnedMeshVertex> FromSkeletalMesh(SkeletalMeshDto owner, uint sourceLodIndex, FStaticLODModel lod, float screenSize)
    {
        ArgumentNullException.ThrowIfNull(lod.Indices?.Buffer, "LOD has no index buffer");

        var chunkIndex = 0;
        var chunkVertexIndex = 0;
        var lastChunkVertex = -1L;
        var boneMap = Array.Empty<ushort>();
        var bUseVerticesFromSections = false;

        var vertexCount = lod.VertexBufferGPUSkin.GetVertexCount();
        if (vertexCount == 0 && lod.Sections.Length > 0 && lod.Sections[0].SoftVertices.Length > 0)
        {
            bUseVerticesFromSections = true;
            foreach (var section in lod.Sections)
            {
                vertexCount += section.SoftVertices.Length;
            }
        }

        // Sections can overlap (e.g. Dead Island 2 keeps disabled sections spanning the vertex ranges of the enabled ones).
        // A vertex's bone indices refer to the bone map of the enabled section that owns it, so resolve ownership up front.
        ushort[]?[]? ownerBoneMaps = null;
        if (!bUseVerticesFromSections && lod.Chunks.Length == 0 && lod.Sections.Any(x => x.bDisabled))
        {
            ownerBoneMaps = new ushort[]?[vertexCount];
            foreach (var disabledPass in new[] { false, true })
            {
                foreach (var section in lod.Sections)
                {
                    if (section.bDisabled != disabledPass) continue;
                    var end = Math.Min((long) section.BaseVertexIndex + section.NumVertices, vertexCount);
                    for (var v = (long) section.BaseVertexIndex; v < end; v++)
                    {
                        ownerBoneMaps[v] ??= section.BoneMap;
                    }
                }
            }
        }

        var extraUvs = new FMeshUVFloat[Math.Max(0, lod.NumTexCoords - 1)][];
        var vertices = new SkinnedMeshVertex[vertexCount];

        for (var i = 0; i < extraUvs.Length; i++)
        {
            extraUvs[i] = new FMeshUVFloat[vertices.Length];
        }

        FColor[]? vertexColors = null;
        if (lod.ColorVertexBuffer is { Data.Length: > 0 })
        {
            vertexColors = new FColor[vertices.Length]; // we don't need colors that don't belong to any vertex
            Array.Copy(lod.ColorVertexBuffer.Data, vertexColors, vertexColors.Length);
        }
        else if (bUseVerticesFromSections)
        {
            vertexColors = new FColor[vertices.Length]; // vertices from sections have their color baked into the vertex struct
        }

        for (var i = 0; i < vertices.Length; i++)
        {
            while (i >= lastChunkVertex) // this will fix any issues with empty chunks or sections
            {
                if (lod.Chunks.Length > 0) // proceed to next chunk or section
                {
                    // pre-UE4.13 code: chunks
                    var c = lod.Chunks[chunkIndex++];
                    lastChunkVertex = c.BaseVertexIndex + c.NumRigidVertices + c.NumSoftVertices;
                    boneMap = c.BoneMap;
                }
                else
                {
                    // UE4.13+ code: chunk information migrated to sections
                    if (chunkIndex >= lod.Sections.Length)
                    {
                        lastChunkVertex = long.MaxValue; // vertices past the last section keep its bone map
                        break;
                    }

                    var s = lod.Sections[chunkIndex++];
                    lastChunkVertex = s.BaseVertexIndex + s.NumVertices;
                    boneMap = s.BoneMap;
                }

                chunkVertexIndex = 0;
            }

            if (ownerBoneMaps?[i] is { } ownerBoneMap)
            {
                boneMap = ownerBoneMap;
            }

            FSkelMeshVertexBase vertex;
            if (bUseVerticesFromSections)
            {
                var v = lod.Sections[chunkIndex].SoftVertices[chunkVertexIndex++];
                vertex = v;
                if (vertexColors != null)
                {
                    vertexColors[i] = v.Color;
                }
            }
            else if (!lod.VertexBufferGPUSkin.bUseFullPrecisionUVs)
            {
                vertex = lod.VertexBufferGPUSkin.VertsHalf[i];
            }
            else
            {
                vertex = lod.VertexBufferGPUSkin.VertsFloat[i];
            }

            vertices[i] = new SkinnedMeshVertex(vertex, boneMap);

            for (var j = 0; j < extraUvs.Length; j++)
            {
                extraUvs[j][i].U = vertex.UVs[j + 1].U;
                extraUvs[j][i].V = vertex.UVs[j + 1].V;
            }
        }

        var sections = new MeshSectionDto[lod.Sections.Length];
        for (var i = 0; i < sections.Length; i++)
        {
            sections[i] = new MeshSectionDto(lod.Sections[i]);

            // A disabled section is not drawn; Dead Island 2 leaves some pointing past the vertex buffer. Drop their triangles.
            if (lod.Sections[i].bDisabled && !IndicesInRange(lod.Indices.Buffer, sections[i], vertices.Length))
            {
                sections[i].NumFaces = 0;
            }
        }

        return new MeshLodDto<SkinnedMeshVertex>(owner, sourceLodIndex, lod.Indices.Buffer, vertices, sections, extraUvs, vertexColors, screenSize);
    }

    private static bool IndicesInRange(uint[] indices, MeshSectionDto section, int vertexCount)
    {
        var end = (long) section.FirstIndex + (long) section.NumFaces * 3;
        if (section.FirstIndex < 0 || end > indices.Length) return false;
        for (var k = section.FirstIndex; k < end; k++)
        {
            if (indices[k] >= vertexCount) return false;
        }

        return true;
    }
}
