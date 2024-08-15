using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTools.Files.Textures.PS2;
using PDTools.Files.Models.PS2.ModelSet;
using PDTools.Files.Models.PS2.CarModel1;
using PDTools.Utils;

using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

using IOPath = System.IO.Path;
using SixLabors.Fonts;
using PDTools.Files.Textures.PS2.GSPixelFormats;
using SixLabors.ImageSharp;

namespace GTPS2ModelTool;

class TextureSet1Debug
{
    public static void Print()
    {
        using var file = File.OpenRead(@"D:\Modding_Research\Gran_Turismo\Gran_Turismo_3\data\cars\day\sb0039");

        var carModel = new CarModel1();
        carModel.FromStream(file);

        var modelSet = carModel.ModelSet;

        var firstTexture = modelSet.TextureSets[0].pgluTextures.FirstOrDefault(e => e.tex0.TBP0_TextureBaseAddress == 0);
        /*
        Console.WriteLine($"Total block size: {modelSet.TextureSets[0].TotalBlockSize:X8}");
        foreach (var texture in modelSet.TextureSets[0].pgluTextures.OrderBy(e => e.tex0.TBP0_TextureBaseAddress))
        {
            int blockSize = Tex1Utils.FindBlockIndexAtPosition(texture.tex0.PSM, (int)Math.Pow(2, texture.tex0.TW_TextureWidth) - 1, (int)Math.Pow(2, texture.tex0.TH_TextureHeight) - 1);
            Console.WriteLine($"IDX:{modelSet.TextureSets[0].pgluTextures.IndexOf(texture)}, tbp:{texture.tex0.TBP0_TextureBaseAddress:X8} psm:{texture.tex0.PSM} region: {texture.ClampSettings.MAXU+1}x{texture.ClampSettings.MAXV+1} " +
                $"({Math.Pow(2, texture.tex0.TW_TextureWidth)}x{Math.Pow(2, texture.tex0.TH_TextureHeight)}) - cbp:{texture.tex0.CBP_ClutBlockPointer:X8} csa:{texture.tex0.CSA_ClutEntryOffset} - cnt:{blockSize:X8}");
        }
        */
    }

    public static void CheckCalculatedTransfersOfCars()
    {
        List<(int, int)> transfers = new List<(int, int)>();

        foreach (var file in Directory.GetFiles(@"D:\Modding_Research\Gran_Turismo\Gran_Turismo_3\data\cars\day"))
        {
            try
            {
                var carModel = new CarModel1();

                using var fs = new FileStream(file, FileMode.Open);
                carModel.FromStream(fs);

                var modelSet = carModel.ModelSet;

                for (int i1 = 0; i1 < modelSet.TextureSets.Count; i1++)
                {
                    TextureSet1? texset = modelSet.TextureSets[i1];
                    if (texset.GSTransfers.Count == 0)
                        continue;

                    List<(int, int)> calculatedTransfers = Tex1Utils.CalculateSwizzledTransferSizes(texset.TotalBlockSize * GSMemory.BLOCK_SIZE_BYTES);

                    if (texset.GSTransfers.Count != calculatedTransfers.Count)
                    {
                        Console.WriteLine($"Count mismatch (texset #{i1}) - {IOPath.GetFileNameWithoutExtension(file)} (has: {texset.GSTransfers.Count}, calc: {calculatedTransfers.Count})");
                        continue;
                    }

                    for (int i = 0; i < texset.GSTransfers.Count; i++)
                    {
                        GSTransfer transfer = texset.GSTransfers[i];
                        if (transfer.Width != calculatedTransfers[i].Item1 || transfer.Height != calculatedTransfers[i].Item2)
                        {
                            Console.WriteLine($"W/H mismatch (texset #{i1}) - {IOPath.GetFileNameWithoutExtension(file)} transfer #{i} = {transfer.Width}x{transfer.Height}, calculated {calculatedTransfers[i].Item1}x{calculatedTransfers[i].Item2}");
                        }
                    }

                    Console.WriteLine($"OK (texset #{i1}): {IOPath.GetFileNameWithoutExtension(file)} ({texset.GSTransfers.Count} transfers)");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"ERR: {file} - {e.Message}");
            }
        }
    }

    public static void DrawImageWithBlockGrid(Image sourceImage, GSPixelFormat format)
    {
        int w = format.CalcImageTbwWidth(sourceImage.Width - 1, sourceImage.Height - 1);
        int h = (int)MiscUtils.AlignValue((uint)sourceImage.Height, (uint)format.PageHeight);

        using var img = new Image<Rgba32>(w, h);

        FontFamily fontFamily = SystemFonts.Get("Arial");
        var font = fontFamily.CreateFont(11.0f, FontStyle.Regular);

        DrawingOptions goptions = new()
        {
            GraphicsOptions = new()
            {
                Antialias = false,
                AntialiasSubpixelDepth = 0,
            },
        };

        int pagesPerRow = format.GetNumPagesPerRow(sourceImage.Width, sourceImage.Height);
        int lastBlockIndex = format.GetLastBlockIndexForImageDimensions(sourceImage.Width, sourceImage.Height);
        List<ushort> unusedBlocks = format.GetUnusedBlocks(sourceImage.Width, sourceImage.Height, out _);

        img.Mutate(i =>
        {
            i.DrawImage(sourceImage, 1.0f);

            for (int y = 0; y < img.Height; y += format.BlockHeight)
            {
                if (y % format.PageHeight == 0)
                    i.DrawLine(Color.Gray, 1f, [new(0, y), new(img.Width, y)]);
                else
                    i.DrawLine(Color.Red, 1f, [new(0, y), new(img.Width, y)]);
            }

            for (int x = 0; x < img.Width; x += format.BlockWidth)
            {
                if (x % format.PageWidth == 0)
                    i.DrawLine(Color.Gray, 1f, [new(x, 0), new(x, img.Height)]);
                else
                    i.DrawLine(Color.Red, 1f, [new(x, 0), new(x, img.Height)]);
            }

            for (int y = 0; y < img.Height; y += format.BlockHeight)
            {
                for (int x = 0; x < img.Width; x += format.BlockWidth)
                {
                    ushort blockIndex = format.GetBlockIndex(x, y, pagesPerRow);
                    if (unusedBlocks.IndexOf(blockIndex) != -1)
                        i.DrawText(goptions, blockIndex.ToString(), font, Color.Gray, new PointF(x, y));
                    else if (blockIndex > lastBlockIndex)
                        i.DrawText(goptions, blockIndex.ToString(), font, Color.DarkSlateGray, new PointF(x, y));
                    else
                        i.DrawText(goptions, blockIndex.ToString(), font, Color.Red, new PointF(x, y));
                }
            }

            i.DrawLine(Color.Yellow, 1f, 
                [new(0, 0), new(sourceImage.Width, 0), // Top Left
                new(sourceImage.Width - 1, 0), new(sourceImage.Width - 1, sourceImage.Height - 1), // Top Right
                new(sourceImage.Width - 1, sourceImage.Height - 1), new(0, sourceImage.Height - 1), // Bottom Right
                new(0, sourceImage.Height - 1), new(0, 0), // Bottom Left
            ]);
        });

        img.Save("test.png");
    }
}
