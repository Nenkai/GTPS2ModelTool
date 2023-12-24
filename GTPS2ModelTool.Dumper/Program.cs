using PDTools.Files.Models.PS2;
using PDTools.Files.Models.PS2.ModelSet;
using PDTools.Files.Textures.PS2;
using PDTools.Files.Models.PS2.CarModel1;

using SixLabors.ImageSharp;

namespace GTPS2ModelTool.Dumper
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (Directory.Exists(args[0]))
            {
                foreach (var file in Directory.GetFiles(args[0], "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        Console.WriteLine($"Processing: {file}");
                        ProcessFile(file);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Skipped: {file} - {e.Message}");
                    }
                }
            }
            else
            {
                ProcessFile(args[0]);
            }
        }


        static void ProcessFile(string path)
        {
            using var fs = new FileStream(path, FileMode.Open);
            using var bs = new BinaryReader(fs);

            uint magic = bs.ReadUInt32();
            if (magic == 0x34524143) // CAR4
            {
                bs.BaseStream.Position = 0x18;
                uint modelSetOffset = bs.ReadUInt32();
                bs.BaseStream.Position = modelSetOffset;
                ProcessModelSet2(path, fs);
                return;
            }
            else if (magic == 0x534C444D) // MDLS
            {
                fs.Position = 0;
                ProcessModelSet2(path, fs);
                return;
            }
            else if (magic == ModelSet1.MAGIC)
            {
                fs.Position = 0;
                ProcessModelSet(path, fs);
                return;
            }
            else if (magic == TireFile.MAGIC)
            {
                fs.Position = 0x20;
                ProcessTextureSet(path, fs);
                return;
            }
            else if (magic == WheelFile.MAGIC)
            {
                fs.Position = 0x20;
                ProcessModelSet(path, fs);
                return;
            }
            else if (magic == TextureSet1.MAGIC) // Tex1
            {
                fs.Position = 0;
                ProcessTextureSet(path, fs);
                return;
            }

            // GT4 Crs MDLS
            fs.Position = 0x100;
            magic = bs.ReadUInt32();
            if (magic == 0x534C444D)
            {
                fs.Position = 0x100;
                ProcessModelSet2(path, fs);
                return;
            }

            fs.Position = 0x08;
            uint possibleModelSetOffset = bs.ReadUInt32();
            fs.Position = possibleModelSetOffset;

            magic = bs.ReadUInt32();
            if (magic == ModelSet1.MAGIC)
            {
                fs.Position = possibleModelSetOffset;
                ProcessModelSet(path, fs);
                return;
            }

            fs.Position = 0x20;
            magic = bs.ReadUInt32();
            if (magic == ModelSet1.MAGIC)
            {
                fs.Position = 0x20;
                ProcessModelSet(path, fs);
                return;
            }

            fs.Position = 0x5180;
            magic = bs.ReadUInt32();
            if (magic == ModelSet1.MAGIC)
            {
                fs.Position = 0x5180;
                ProcessModelSet(path, fs);
                return;
            }

            Console.WriteLine("Can not process this file");
        }

        static void ProcessTextureSet(string path, Stream stream)
        {
            var texSet = new TextureSet1();
            texSet.FromStream(stream);
            texSet.Dump();

            string outputDir = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path)) + "_textures";
            Directory.CreateDirectory(outputDir);
            for (int i = 0; i < texSet.pgluTextures.Count; i++)
            {
                using var image = texSet.GetTextureImage(i);

                if (texSet.pgluTextures.Count == 1)
                    image.Save(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(path) + ".png"));
                else
                {

                    image.Save(Path.Combine(outputDir, $"{i}.png"));
                }
            }
        }

        static void ProcessModelSet(string path, Stream stream)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string outputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_converted", $"{name}_textures");
            Directory.CreateDirectory(outputDir);

            var modelSet = new ModelSet1();
            modelSet.FromStream(stream);

            for (int i = 0; i < modelSet.TextureSets.Count; i++)
            {
                TextureSet1 texSet = modelSet.TextureSets[i];
                for (int k = 0; k < texSet.pgluTextures.Count; k++)
                {
                    try
                    {
                        using var image = texSet.GetTextureImage(k);
                        image.Save(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(path) + $".{i}.{k}.png"));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Skipped {k} - {e.Message}");
                    }
                }
            }

            outputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_converted");
            Directory.CreateDirectory(outputDir);

            for (int i = 0; i < modelSet.Shapes.Count; i++)
            {
                PGLUshape shape = modelSet.Shapes[i];
                shape.DumpShape(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(path) + $".{i}.obj"));
            }

        }

        static void ProcessModelSet2(string path, Stream stream)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string outputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_converted", $"{name}_textures");
            Directory.CreateDirectory(outputDir);

            var modelSet = new ModelSet2();
            modelSet.FromStream(stream);
            for (int i = 0; i < modelSet.TextureSetLists.Count; i++)
            {
                List<TextureSet1> list = modelSet.TextureSetLists[i];
                for (int j = 0; j < list.Count; j++)
                {
                    TextureSet1 texSet = list[j];
                    for (int k = 0; k < texSet.pgluTextures.Count; k++)
                    {
                        try
                        {
                            using var image = texSet.GetTextureImage(k);
                            image.Save(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(path) + $".{i}.{j}.{k}.png"));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Skipped {k} - {e.Message}");
                        }
                    }
                }
            }

            outputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_converted");
            Directory.CreateDirectory(outputDir);

            for (int i = 0; i < modelSet.Shapes.Count; i++)
            {
                PGLUshape shape = modelSet.Shapes[i];
                shape.DumpShape(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(path) + $".{i}.obj"));
            }
        }
    }
}