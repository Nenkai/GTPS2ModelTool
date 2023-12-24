using SharpTriStrip;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using GTPS2ModelTool.Core.WavefrontObj;

namespace GTPS2ModelTool.Core.TriStriper
{
    public class TriStriper
    {
        public static TriStripMesh GenerateTriStrips(ModelMesh inputMesh)
        {
            var triStripMesh = new TriStripMesh();
            triStripMesh.Faces = inputMesh.Faces;

            if (inputMesh.Faces.Count != 0)
            {
                List<VectorPoint> points = new List<VectorPoint>();
                List<IndiceFace> faces = new List<IndiceFace>();
                for (int i = 0; i < inputMesh.Faces.Count; i++)
                {
                    IndiceFace TempFace = new IndiceFace();
                    bool Test = false;
                    for (int j = 0; j < points.Count; j++)
                    {
                        if (points[j].Vertex.Position == inputMesh.Faces[i].Vert1.Position)
                        {
                            if (points[j].Normal == inputMesh.Faces[i].Normal1)
                            {
                                if (points[j].TextureCoord == inputMesh.Faces[i].UV1)
                                {
                                    if (points[j].ObjMatId == inputMesh.Faces[i].MaterialId)
                                    {
                                        Test = true;
                                        TempFace.Id1 = j;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (!Test)
                    {
                        TempFace.Id1 = points.Count;
                        VectorPoint vectorPoint = new VectorPoint();
                        vectorPoint.ObjMatId = inputMesh.Faces[i].MaterialId;
                        vectorPoint.PGLUMatId = inputMesh.Faces[i].PGLUMatIndex;
                        vectorPoint.Vertex = inputMesh.Faces[i].Vert1;
                        vectorPoint.Normal = inputMesh.Faces[i].Normal1;
                        vectorPoint.TextureCoord = inputMesh.Faces[i].UV1;
                        points.Add(vectorPoint);
                    }

                    Test = false;
                    for (int j = 0; j < points.Count; j++)
                    {
                        if (points[j].Vertex.Position == inputMesh.Faces[i].Vert2.Position)
                        {
                            if (points[j].Normal == inputMesh.Faces[i].Normal2)
                            {
                                if (points[j].TextureCoord == inputMesh.Faces[i].UV2)
                                {
                                    if (points[j].ObjMatId == inputMesh.Faces[i].MaterialId)
                                    {
                                        Test = true;
                                        TempFace.Id2 = j;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (!Test)
                    {
                        TempFace.Id2 = points.Count;
                        VectorPoint vectorPoint = new VectorPoint();
                        vectorPoint.ObjMatId = inputMesh.Faces[i].MaterialId;
                        vectorPoint.PGLUMatId = inputMesh.Faces[i].PGLUMatIndex;
                        vectorPoint.Vertex = inputMesh.Faces[i].Vert2;
                        vectorPoint.Normal = inputMesh.Faces[i].Normal2;
                        vectorPoint.TextureCoord = inputMesh.Faces[i].UV2;
                        points.Add(vectorPoint);
                    }

                    Test = false;
                    for (int j = 0; j < points.Count; j++)
                    {
                        if (points[j].Vertex.Position == inputMesh.Faces[i].Vert3.Position)
                        {
                            if (points[j].Normal == inputMesh.Faces[i].Normal3)
                            {
                                if (points[j].TextureCoord == inputMesh.Faces[i].UV3)
                                {
                                    if (points[j].ObjMatId == inputMesh.Faces[i].MaterialId)
                                    {
                                        Test = true;
                                        TempFace.Id3 = j;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (!Test)
                    {
                        TempFace.Id3 = points.Count;
                        VectorPoint vectorPoint = new VectorPoint();
                        vectorPoint.ObjMatId = inputMesh.Faces[i].MaterialId;
                        vectorPoint.PGLUMatId = inputMesh.Faces[i].PGLUMatIndex;
                        vectorPoint.Vertex = inputMesh.Faces[i].Vert3;
                        vectorPoint.Normal = inputMesh.Faces[i].Normal3;
                        vectorPoint.TextureCoord = inputMesh.Faces[i].UV3;
                        points.Add(vectorPoint);
                    }

                    faces.Add(TempFace);
                }

                List<IndiceTristrip> strips = GenerateTristripNivda(faces);
                if (strips is null)
                    return null;

                // Split Tristrips for PS2 VIF Parts
                TristripMeshChunk chunk = new TristripMeshChunk();

                int? lastMaterial = null;
                for (int i = 0; i < strips.Count; i++)
                {
                    IndiceTristrip triStrip = strips[i];
                    if (lastMaterial is not null && points[triStrip.Indices[0]].ObjMatId != lastMaterial)
                    {
                        // TODO: Check if this works properly for any strip that is on the same material?
                        triStripMesh.Chunks.Add(chunk);
                        chunk = new TristripMeshChunk();
                        chunk.TristripNumbers.Add(strips[i].Indices.Count);
                        for (int a = 0; a < strips[i].Indices.Count; a++)
                        {
                            VectorPoint point = points[strips[i].Indices[a]];

                            chunk.Vertices.Add(point.Vertex.Position);

                            if (point.TextureCoord.HasValue)
                                chunk.TextureCords.Add(point.TextureCoord.Value);

                            if (point.Normal.HasValue)
                                chunk.Normals.Add(point.Normal.Value);

                            if (point.Vertex.Color is not null)
                                chunk.Colors.Add(point.Vertex.Color.Value);

                            chunk.ObjMatId = point.ObjMatId;
                            chunk.PGLUMatId = point.PGLUMatId;
                        }
                    }
                    else
                    {
                        if (chunk.Vertices.Count + triStrip.Indices.Count < 64)
                        {
                            chunk.TristripNumbers.Add(triStrip.Indices.Count);

                            for (int a = 0; a < strips[i].Indices.Count; a++)
                            {
                                VectorPoint point = points[strips[i].Indices[a]];

                                chunk.Vertices.Add(point.Vertex.Position);
                                if (point.TextureCoord.HasValue)
                                    chunk.TextureCords.Add(point.TextureCoord.Value);

                                if (point.Normal.HasValue)
                                    chunk.Normals.Add(point.Normal.Value);

                                if (point.Vertex.Color is not null)
                                    chunk.Colors.Add(point.Vertex.Color.Value);
                                chunk.ObjMatId = point.ObjMatId;
                            }
                        }
                        else
                        {
                            triStripMesh.Chunks.Add(chunk);
                            chunk = new TristripMeshChunk();
                            chunk.TristripNumbers.Add(strips[i].Indices.Count);
                            for (int a = 0; a < strips[i].Indices.Count; a++)
                            {
                                VectorPoint point = points[strips[i].Indices[a]];

                                chunk.Vertices.Add(point.Vertex.Position);

                                if (point.TextureCoord.HasValue)
                                    chunk.TextureCords.Add(point.TextureCoord.Value);

                                if (point.Normal.HasValue)
                                    chunk.Normals.Add(point.Normal.Value);

                                if (point.Vertex.Color is not null)
                                    chunk.Colors.Add(point.Vertex.Color.Value);
                                chunk.ObjMatId = point.ObjMatId;
                            }
                        }
                    }

                    lastMaterial = points[triStrip.Indices[0]].ObjMatId;
                }

                if (!chunk.Equals(new TristripMeshChunk()))
                {
                    triStripMesh.Chunks.Add(chunk);
                }
            }

            return triStripMesh;
        }

        public static List<IndiceTristrip> GenerateTristripNivda(List<IndiceFace> indiceFaces)
        {
            List<IndiceTristrip> tristripList = new List<IndiceTristrip>();

            ushort[] Index = new ushort[indiceFaces.Count * 3];

            for (int i = 0; i < indiceFaces.Count; i++)
            {
                Index[i * 3] = (ushort)indiceFaces[i].Id1;
                Index[i * 3 + 1] = (ushort)indiceFaces[i].Id2;
                Index[i * 3 + 2] = (ushort)indiceFaces[i].Id3;
            }

            var TempPrimativeGroup = ToTriangleStrips(Index, false, 24, false);

            if (TempPrimativeGroup == null)
            {
                return null;
            }

            for (int i = 0; i < TempPrimativeGroup.Length; i++)
            {
                var TempIndiceTristrip = new IndiceTristrip();
                TempIndiceTristrip.Indices = new List<int>();

                for (int a = 0; a < TempPrimativeGroup[i].Indices.Length; a++)
                {
                    TempIndiceTristrip.Indices.Add(TempPrimativeGroup[i].Indices[a]);
                }
                tristripList.Add(TempIndiceTristrip);
            }

            return tristripList;
        }

        public static TriStrip.PrimitiveGroup[] ToTriangleStrips(ushort[] indexBuffer, bool validateStrips, int MaxTristrips, bool StichStrip)
        {
            var triStrip = new TriStrip(); // create new class instance

            triStrip.DisableRestart(); // we want separate strips, so restart is not needed
            triStrip.SetCacheSize(MaxTristrips); // GeForce1/2 vertex cache size is 16
            triStrip.SetListsOnly(false); // we want separate strips, not optimized list
            triStrip.SetMinStripSize(0); // minimum triangle count in a strip is 0
            triStrip.SetStitchStrips(StichStrip); // don't stitch strips into one huge strip

            if (triStrip.GenerateStrips(indexBuffer, out var result, validateStrips))
            {
                return result; // if strips were generated and validated correctly, return
            }

            return null; // if something went wrong, return null (or throw instead)
        }

        public struct VectorPoint
        {
            public bool Tristripped;
            public Vertex Vertex;
            public Vector3? Normal;
            public Vector2? TextureCoord;
            public int ObjMatId;
            public int PGLUMatId;

        }

        // Other
        public struct IndiceFace
        {
            public int MaterialID;
            public int Neighbours;

            public int Id1;
            public int Id2;
            public int Id3;
        }

        public struct IndiceTristrip
        {
            public int MaterialID;
            public List<int> Indices;
        }
    }
}
