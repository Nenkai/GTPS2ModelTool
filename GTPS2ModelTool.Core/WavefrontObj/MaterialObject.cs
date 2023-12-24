using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Globalization;

using PDTools.Files;

namespace GTPS2ModelTool.Core.WavefrontObj
{
    public class MaterialObject
    {
        public List<Material> Materials { get; set; } = new();

        public static MaterialObject LoadFromFile(string path)
        {
            var obj = new MaterialObject();

            string[] Lines = File.ReadAllLines(path);

            Material currentMaterial = null;

            for (int i = 0; i < Lines.Length; i++)
            {
                string line = Lines[i].TrimStart();

                if (line.StartsWith("newmtl"))
                {
                    string[] splitLine = line.Split(' ');
                    for (int j = 1; j < splitLine.Length; j++)
                    {
                        if (string.IsNullOrEmpty(splitLine[j]))
                            continue;

                        string materialName = splitLine[j];
                        currentMaterial = new Material(materialName);
                        obj.Materials.Add(currentMaterial);
                        currentMaterial.Id = obj.Materials.Count - 1;
                        break;
                    }
                }

                if (line.StartsWith("Ka"))
                {
                    if (currentMaterial is null)
                        throw new Exception("Material file error - 'Ka' found but no material declared.");

                    currentMaterial.Ambient = ParseColor4(i + 1, line);
                }

                if (line.StartsWith("Kd"))
                {
                    if (currentMaterial is null)
                        throw new Exception("Material file error - 'Kd' found but no material declared.");

                    currentMaterial.Diffuse = ParseColor4(i + 1, line);
                }

                if (line.StartsWith("Ks"))
                {
                    if (currentMaterial is null)
                        throw new Exception("Material file error - 'Ks' found but no material declared.");

                    currentMaterial.Specular = ParseColor4(i + 1, line);
                }

                if (line.StartsWith("Ke"))
                {
                    if (currentMaterial is null)
                        throw new Exception("Material file error - 'Ke' found but no material declared.");

                    currentMaterial.Emissive = ParseColor4(i + 1, line);
                }

                if (line.StartsWith("map_Ka"))
                {
                    if (currentMaterial is null)
                        throw new Exception("Material file error - 'map_Ka' found but no material declared.");

                    string[] splitLine = line.Split(' ');
                    for (int j = 1; j < splitLine.Length; j++)
                    {
                        if (string.IsNullOrEmpty(splitLine[j]))
                            continue;

                        string name = splitLine[j];
                        currentMaterial.MapAmbient = name;
                        break;
                    }
                }

                if (line.StartsWith("map_Kd"))
                {
                    if (currentMaterial is null)
                        throw new Exception("Material file error - 'map_Kd' found but no material declared.");

                    string[] splitLine = line.Split(' ');
                    for (int j = 1; j < splitLine.Length; j++)
                    {
                        if (string.IsNullOrEmpty(splitLine[j]))
                            continue;

                        string name = splitLine[j];
                        currentMaterial.MapDiffuse = name;
                        break;
                    }
                }
            }

            return obj;
        }

        private static Color4f ParseColor4(int lineNumber, string line)
        {
            Color4f color4 = default;

            int cnt = 0;

            string[] splitLine = line.Split(' ');
            for (int j = 1; j < splitLine.Length; j++)
            {
                if (string.IsNullOrEmpty(splitLine[j]))
                    continue;

                if (!float.TryParse(splitLine[j], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out float value))
                    throw new Exception($"Failed to parse obj vertex at line {lineNumber}");

                if (cnt == 0)
                    color4.R = value;
                else if (cnt == 1)
                    color4.G = value;
                else if (cnt == 2)
                    color4.B = value;
                else if (cnt == 3)
                    color4.A = value;
                else
                    throw new Exception($"Too many color4 values at line {lineNumber}");

                cnt++;
            }

            return color4;
        }
    }
}
