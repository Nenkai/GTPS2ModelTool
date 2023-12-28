using System;
using System.Globalization;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;
using System.Numerics;

using PDTools.Files.Models.PS2;

using SharpTriStrip;

using GTPS2ModelTool.Core.WavefrontObj;
using GTPS2ModelTool.Core.TriStriper;

using NLog;

namespace GTPS2ModelTool.Core
{
    public class PGLUShapeBuilder
    {
        public const int MAX_VERTS_PER_VIF_PACKET = 64;

        private static Logger Logger = LogManager.GetCurrentClassLogger();

        private static bool _optimize = false;

        public static PGLUshape BuildShape(ModelMesh mesh, List<string> textures, List<Material> materials, bool externalTexture = false)
        {
            var shape = new PGLUshape();

            TriStripMesh triStripMesh = TriStriper.TriStriper.GenerateTriStrips(mesh) ?? throw new Exception("Could not create tri-strips.");

            // Main packet + gif tag
            bool hasUV = false;
            bool hasVertColors = false;
            foreach (var chunk in triStripMesh.Chunks)
            { 
                var packet = new VIFPacket();

                // Determine mat/texture
                ushort textureIndex = 0;
                ushort materialIndex = 0;
                if (externalTexture)
                {
                    textureIndex = VIFDescriptor.EXTERNAL_TEXTURE;
                    materialIndex = 0;

                    Logger.Trace($"Chunk - {chunk.Vertices.Count} verts, mat id: {materialIndex}, tex id: {textureIndex}");
                }
                else
                {
                    if (chunk.ObjMatId != -1)
                    {
                        //materialIndex = (ushort)(chunk.MaterialId + 1);
                        Material material = materials[chunk.ObjMatId];
                        if (!string.IsNullOrEmpty(material.MapDiffuse))
                            textureIndex = (ushort)(textures.IndexOf(material.MapDiffuse) + 1);
                        materialIndex = (ushort)(chunk.PGLUMatId + 1);
                    }

                    Logger.Trace($"Chunk - {chunk.Vertices.Count} verts, " +
                        $"mat id: {(materialIndex == 0 ? "None" : (materialIndex - 1).ToString())}, " +
                        $"tex id: {(textureIndex == 0 ? "None" : (textureIndex - 1).ToString())}");
                }

                shape.VIFPackets.Add(packet);
                shape.VIFDescriptors.Add(new VIFDescriptor()
                {
                    pgluTextureIndex = textureIndex,
                    pgluMaterialIndex = materialIndex,
                });

                SCE_GS_PRIM prim = SCE_GS_PRIM.SCE_GS_PRIM_TRISTRIP |  // Tri-striped mesh
                    SCE_GS_PRIM.SCE_GS_PRIM_IIP | // Gouraud, whoever that bloke is
                    SCE_GS_PRIM.SCE_GS_PRIM_FGE; // And fogging too

                if (textureIndex != 0)
                    prim |= SCE_GS_PRIM.SCE_GS_PRIM_TME; // Textured

                if (externalTexture)
                    prim |= SCE_GS_PRIM.SCE_GS_PRIM_ABE;

                // VIFCommand holds part of a gif tag within 3 ints
                var mainCommand = new VIFCommand()
                {
                    VUAddr = 0xC0C0,
                    Num = 1,
                    CommandOpcode = (VIFCommandOpcode)((byte)VIFCommandOpcode.UNPACK | (byte)VIFCommandOpcodeUnpack.UNPACK_V3_32), // Size of gif tag + 1 of registers
                    IRQ = false,
                    GIFTag = new GIFTag()
                    {
                        EndOfPacket = true,
                        Pre = true,
                        Prim = prim,
                        Regs = new List<byte>()
                        {
                            2, 1, 4, 1
                        }
                    }
                };
                packet.Commands.Add(mainCommand);

                // Verts
                VIFCommand vertCommands = MakeVertsCommand(chunk.Vertices);
                packet.Commands.Add(vertCommands);
                mainCommand.GIFTag.NLoop = vertCommands.Num;

                // Resets
                VIFCommand resetsCommand = MakeResetsCommand(chunk.TristripNumbers);
                packet.Commands.Add(resetsCommand);

                // Vertex colors
                if (chunk.Colors.Count > 0)
                {
                    uint[] cols = new uint[chunk.Colors.Count];
                    for (int i = 0; i < chunk.Colors.Count; i++)
                    {
                        var col = chunk.Colors[i];
                        cols[i] = (uint)((byte)(col.X * 255) | (byte)(col.Y * 255) << 8 | (byte)(col.Z * 255) << 16 | 0x80 << 24);
                    }

                    VIFCommand vertexColorsCommand = MakeVertexColorsCommand(cols);
                    packet.Commands.Add(vertexColorsCommand);
                    hasVertColors = true;
                }

                // Normals
                if (chunk.Normals.Count > 0)
                {
                    VIFCommand normalsCommand = MakeNormalsCommand(chunk.Normals, externalTexture);
                    packet.Commands.Add(normalsCommand);
                }
                
                // UVs
                if (!externalTexture && chunk.TextureCords.Count > 0)
                {
                    VIFCommand uvCommand = MakeUVCommand(vertCommands.Num, default, chunk.TextureCords);
                    packet.Commands.Add(uvCommand);
                    hasUV = true;
                }
            }

            foreach (var chunk in triStripMesh.Chunks)
                shape.TotalStripVerts += (ushort)chunk.Vertices.Count;

            shape.NumTriangles = (ushort)triStripMesh.Faces.Count;

            if (externalTexture)
            {
                shape.Unk1 = 2;
                shape.Unk3 = 1;
            }
            else if (hasUV)
                shape.Unk1 = 1; // 5 also works, although no idea what it does

            Logger.Info($"Tri-striped mesh into {triStripMesh.Chunks.Count} chunks - {shape.NumTriangles} triangles, {shape.TotalStripVerts} strip points, unk1: {shape.Unk1}");

            return shape;
        }

        private static VIFCommand MakeVertsCommand(IList<Vector3> vertices)
        {
            if (_optimize)
            {
                var vertCommand = new VIFCommand()
                {
                    VUAddr = 0x8000,
                    CommandOpcode = (VIFCommandOpcode)((byte)VIFCommandOpcode.UNPACK | (byte)VIFCommandOpcodeUnpack.UNPACK_V3_16),
                    IRQ = false,
                };

                for (int i = 0; i < vertices.Count; i++)
                {
                    var vert = vertices[i];
                    vertCommand.UnpackData.Add(new short[] {(short)(vert.X * 4096f), (short)(vert.Y * 4096f), (short)(vert.Z * 4096f)});
                }

                vertCommand.Num = (byte)vertCommand.UnpackData.Count;
                return vertCommand;
            }
            else
            {
                var vertCommand = new VIFCommand()
                {
                    VUAddr = 0xC000,
                    CommandOpcode = (VIFCommandOpcode)((byte)VIFCommandOpcode.UNPACK | (byte)VIFCommandOpcodeUnpack.UNPACK_V3_32),
                    IRQ = false,
                };

                for (int i = 0; i < vertices.Count; i++)
                {
                    var vert = vertices[i];
                    vertCommand.UnpackData.Add(new int[] {BitConverter.SingleToInt32Bits(vert.X),
                                BitConverter.SingleToInt32Bits(vert.Y),
                                BitConverter.SingleToInt32Bits(vert.Z)});
                }

                vertCommand.Num = (byte)vertCommand.UnpackData.Count;
                return vertCommand;
            }
            
        }

        private static VIFCommand MakeResetsCommand(List<int> strips)
        {
            var vertsPerStripCommand = new VIFCommand()
            {
                VUAddr = 0xC040,
                Num = (byte)(1 + strips.Count), // Size of this + every group
                CommandOpcode = (VIFCommandOpcode)((byte)VIFCommandOpcode.UNPACK | (byte)VIFCommandOpcodeUnpack.UNPACK_S_8),
                IRQ = false,
            };

            vertsPerStripCommand.UnpackData.Add(new byte[] { (byte)strips.Count }); // Number of resets
            for (int i = 0; i < strips.Count; i++)
            {
                vertsPerStripCommand.UnpackData.Add(new byte[] { (byte)((strips[i] - 2) * 3) });
            }

            return vertsPerStripCommand;
        }

        private static VIFCommand MakeNormalsCommand(List<Vector3> normals, bool ext = false)
        {
            // Normals for reflections/external textures are a bit different - diff address
            var normalsCommand = new VIFCommand()
            {
                VUAddr = ext ? (ushort)0xC040 : (ushort)0xC080,
                Num = (byte)normals.Count,
                CommandOpcode = (VIFCommandOpcode)((byte)VIFCommandOpcode.UNPACK | (byte)VIFCommandOpcodeUnpack.UNPACK_V3_32),
                IRQ = false,
            };

            if (ext)
                normalsCommand.CommandOpcode |= (VIFCommandOpcode)0x10;

            for (int i = 0; i < normals.Count; i++)
            {
                var normal = normals[i];
                normalsCommand.UnpackData.Add(new int[] {BitConverter.SingleToInt32Bits(normal.X),
                                BitConverter.SingleToInt32Bits(normal.Y),
                                BitConverter.SingleToInt32Bits(normal.Z)});
            }

            return normalsCommand;
        }

        private static VIFCommand MakeVertexColorsCommand(Span<uint> rgbaColors)
        {
            var vertexColorCommand = new VIFCommand()
            {
                VUAddr = 0xC080,
                Num = (byte)rgbaColors.Length,
                CommandOpcode = (VIFCommandOpcode)((byte)VIFCommandOpcode.UNPACK | (byte)VIFCommandOpcodeUnpack.UNPACK_V4_8),
                IRQ = false,
            };

            for (int i = 0; i < rgbaColors.Length; i++)
            {
                vertexColorCommand.UnpackData.Add(new byte[] {(byte)(rgbaColors[i] & 0xFF),
                    (byte)((rgbaColors[i] >> 8) & 0xFF),
                    (byte)((rgbaColors[i] >> 16) & 0xFF),
                    (byte)((rgbaColors[i] >> 24) & 0xFF) 
                });
            }

            return vertexColorCommand;
        }

        private static VIFCommand MakeUVCommand(byte numVerts, Span<TriStrip.PrimitiveGroup> groups, IList<Vector2> uvs)
        {
            if (_optimize)
            {
                var uvCommand = new VIFCommand()
                {
                    VUAddr = 0x8040,
                    Num = numVerts,
                    CommandOpcode = (VIFCommandOpcode)((byte)VIFCommandOpcode.UNPACK | (byte)VIFCommandOpcodeUnpack.UNPACK_V2_16), // 2 floats
                    IRQ = false,
                };

                foreach (var uv in uvs)
                    uvCommand.UnpackData.Add(new short[] { (short)(uv.X * 4096f), (short)((1.0f - uv.Y) * 4096f) }); // V has to be flipped for ps2

                return uvCommand;
            }
            else
            {
                var uvCommand = new VIFCommand()
                {
                    VUAddr = 0xC040,
                    Num = numVerts,
                    CommandOpcode = (VIFCommandOpcode)((byte)VIFCommandOpcode.UNPACK | (byte)0x10 | (byte)VIFCommandOpcodeUnpack.UNPACK_V2_32), // 2 floats
                    IRQ = false,
                };

                foreach (var uv in uvs)
                    uvCommand.UnpackData.Add(new int[] { BitConverter.SingleToInt32Bits(uv.X), BitConverter.SingleToInt32Bits(1.0f - uv.Y) }); // V has to be flipped for ps2


                return uvCommand;
            }
        }
    }

    public struct Triangle
    {
        public ushort A;
        public ushort B;
        public ushort C;

        public Triangle(ushort a, ushort b, ushort c)
        {
            A = a;
            B = b;
            C = c;
        }

        public ushort GetIndexOf(ushort f)
        {
            if (A == f)
                return 0;
            else if (B == f)
                return 1;
            else if (C == f)
                return 2;

            return 500;
        }

        public bool IsValidTriangle()
        {
            return this.A != this.B && this.A != this.C && this.B != this.C;
        }

        public bool IsSameTriangle(Triangle other)
        {
            return ((this.A == other.A || this.A == other.B || this.A == other.C) &&
                (this.B == other.A || this.B == other.B || this.B == other.C) &&
                (this.C == other.A || this.C == other.B || this.C == other.C));
        }

        public override string ToString()
        {
            return $"{A},{B},{C}";
        }
    }
}
