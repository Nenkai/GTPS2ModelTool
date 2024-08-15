using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GTPS2ModelTool.Core.WavefrontObj;

public class ModelObject
{
    public Dictionary<string, ModelMesh> Meshes { get; set; } = [];
    public MaterialObject MaterialObject { get; set; }

    public static ModelObject LoadFromFile(string path)
    {
        var obj = new ModelObject();

        string[] Lines = File.ReadAllLines(path);

        List<Vertex> vertices = [];
        List<Vector3> normals = [];
        List<Vector2> TextureCords = [];
        ModelMesh currentMesh = new ModelMesh("_default_");

        int currentMaterial = -1;

        //Load File
        for (int a = 0; a < Lines.Length; a++)
        {
            string line = Lines[a];
            if (line.StartsWith("o "))
            {
                string[] splitLine = line.Split(' ');
                for (int i = 1; i < splitLine.Length; i++)
                {
                    if (string.IsNullOrEmpty(splitLine[i]))
                        continue;

                    string objectName = splitLine[i];

                    if (currentMesh.Name != "_default_")
                        obj.Meshes.Add(currentMesh.Name, currentMesh);

                    currentMesh = new ModelMesh(objectName);
                }
            }

            if (line.StartsWith("mtllib"))
            {
                string[] splitLine = line.Split(' ');
                for (int i = 1; i < splitLine.Length; i++)
                {
                    if (string.IsNullOrEmpty(splitLine[i]))
                        continue;

                    string materialFile = splitLine[i];
                    string currentDir = Path.GetDirectoryName(path);
                    string materialFilePath = Path.Combine(currentDir, materialFile);
                    if (!File.Exists(materialFilePath))
                        ThrowError($"Referenced material file '{materialFile}' does not exist", a + 1, line);

                    obj.MaterialObject = MaterialObject.LoadFromFile(materialFilePath);
                    break;
                }
            }

            if (line.StartsWith("usemtl"))
            {
                if (obj.MaterialObject is null)
                    ThrowError($"usemtl found but no mtl file declared", a + 1, line);

                string[] splitLine = line.Split(' ');
                for (int i = 1; i < splitLine.Length; i++)
                {
                    if (string.IsNullOrEmpty(splitLine[i]))
                        continue;

                    if (splitLine[i] == "(null)" || splitLine[i] == "None")
                        break;

                    Material material = obj.MaterialObject.Materials.Find(l => l.Name == splitLine[i]);
                    if (material is not null)
                        currentMaterial = material.Id;
                    else
                        ThrowError($"usemtl uses '{splitLine[i]}' but not found in mtl file", a + 1, line);

                    break;
                }
            }

            if (line.StartsWith("v "))
            {
                string[] splitLine = line.Split(' ');

                Vertex vert = new Vertex();
                Vector3 position = new Vector3();
                Vector3 color = default;

                int cnt = 0;
                for (int j = 0; j < splitLine.Length; j++)
                {
                    if (splitLine[j].Contains('v'))
                        continue;

                    if (string.IsNullOrEmpty(splitLine[j]))
                        continue;

                    if (!float.TryParse(splitLine[j], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out float value))
                        ThrowError("Failed to parse obj vertex", a + 1, line);

                    if (cnt == 0)
                        position.X = value;
                    else if (cnt == 1)
                        position.Y = value;
                    else if (cnt == 2)
                    {
                        position.Z = value;
                        vert.Position = position;
                    }
                    else if (cnt == 3)
                        color.X = value;
                    else if (cnt == 4)
                        color.Y = value;
                    else if (cnt == 5)
                    {
                        color.Z = value;
                        vert.Color = color;
                    }
                    else
                        ThrowError("Too many vertex values", a + 1, line);

                    cnt++;
                }
                vertices.Add(vert);
            }

            if (line.StartsWith("vt "))
            {
                string[] splitLine = line.Split(' ');
                Vector2 vector2 = new Vector2();

                int cnt = 0;
                for (int j = 0; j < splitLine.Length; j++)
                {
                    if (splitLine[j].Contains("vt"))
                        continue;

                    if (string.IsNullOrEmpty(splitLine[j]))
                        continue;

                    if (!float.TryParse(splitLine[j], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out float value))
                        ThrowError("Failed to parse obj vt", a + 1, line);

                    if (cnt == 0)
                        vector2.X = value;
                    else if (cnt == 1)
                        vector2.Y = value;
                    else if (cnt == 2)
                        ;
                    else
                        ThrowError("Too many vt values", a + 1, line);

                    cnt++;
                }
                TextureCords.Add(vector2);
            }

            if (line.StartsWith("vn "))
            {
                string[] splitLine = line.Split(' ');

                Vector3 vector3 = new Vector3();

                int cnt = 0;
                for (int j = 0; j < splitLine.Length; j++)
                {
                    if (splitLine[j].Contains("vn"))
                        continue;

                    if (string.IsNullOrEmpty(splitLine[j]))
                        continue;

                    if (!float.TryParse(splitLine[j], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out float value))
                        ThrowError("Failed to parse obj normal", a + 1, line);

                    if (cnt == 0)
                        vector3.X = value;
                    else if (cnt == 1)
                        vector3.Y = value;
                    else if (cnt == 2)
                        vector3.Z = value;
                    else
                        ThrowError("Too many normal values", a + 1, line);

                    cnt++;
                }
                normals.Add(vector3);
            }

            if (line.StartsWith("f "))
            {
                string filtered = Regex.Replace(line, @"\s+", " ");

                string[] splitLine = filtered.Split(' ');
                Face faces = new Face();

                if (splitLine.Length < 1 + 3)
                    ThrowError("Invalid face.", a + 1, line);

                string[] SplitPoint = splitLine[1].Split('/');
                faces.V1Pos = int.Parse(SplitPoint[0]) - 1;
                if (SplitPoint.Length > 1 && !string.IsNullOrEmpty(SplitPoint[1]))
                    faces.UV1Pos = int.Parse(SplitPoint[1]) - 1;
                if (SplitPoint.Length > 2)
                    faces.Normal1Pos = int.Parse(SplitPoint[2]) - 1;

                SplitPoint = splitLine[2].Split('/');
                faces.V2Pos = int.Parse(SplitPoint[0]) - 1;
                if (SplitPoint.Length > 1 && !string.IsNullOrEmpty(SplitPoint[1]))
                    faces.UV2Pos = int.Parse(SplitPoint[1]) - 1;
                if (SplitPoint.Length > 2)
                    faces.Normal2Pos = int.Parse(SplitPoint[2]) - 1;

                SplitPoint = splitLine[3].Split('/');
                faces.V3Pos = int.Parse(SplitPoint[0]) - 1;
                if (SplitPoint.Length > 1 && !string.IsNullOrEmpty(SplitPoint[1]))
                    faces.UV3Pos = int.Parse(SplitPoint[1]) - 1;

                if (SplitPoint.Length > 2)
                    faces.Normal3Pos = int.Parse(SplitPoint[2]) - 1;
                faces.MaterialId = currentMaterial;

                if (splitLine.Length >= 5 && !string.IsNullOrEmpty(splitLine[4]))
                    ThrowError("Quads are not supported.", a + 1, line);

                if (faces.V1Pos != faces.V3Pos && faces.V1Pos != faces.V2Pos && faces.V3Pos != faces.V2Pos)
                    currentMesh.Faces.Add(faces);
            }
        }
        obj.Meshes.Add(currentMesh.Name, currentMesh);

        // Map faces to verts
        foreach (ModelMesh mesh in obj.Meshes.Values)
        {
            for (int b = 0; b < mesh.Faces.Count; b++)
            {
                Face Face = mesh.Faces[b];

                Face.Vert1 = vertices[Face.V1Pos];
                Face.Vert2 = vertices[Face.V2Pos];
                Face.Vert3 = vertices[Face.V3Pos];

                if (Face.Normal1Pos != -1)
                    Face.Normal1 = normals[Face.Normal1Pos];

                if (Face.Normal2Pos != -1)
                    Face.Normal2 = normals[Face.Normal2Pos];

                if (Face.Normal3Pos != -1)
                    Face.Normal3 = normals[Face.Normal3Pos];

                if (Face.UV1Pos != -1)
                    Face.UV1 = TextureCords[Face.UV1Pos];

                if (Face.UV2Pos != -1)
                    Face.UV2 = TextureCords[Face.UV2Pos];

                if (Face.UV3Pos != -1)
                    Face.UV3 = TextureCords[Face.UV3Pos];

                mesh.Faces[b] = Face;
            }
        }

        return obj;
    }

    public void RemapMaterialIndices(int mtlIndex, int pgluMatIndex)
    {
        // Pointless
        foreach (ModelMesh mesh in Meshes.Values)
        {
            for (int b = 0; b < mesh.Faces.Count; b++)
            {
                if (mesh.Faces[b].MaterialId == mtlIndex)
                    mesh.Faces[b].PGLUMatIndex = pgluMatIndex;
            }
        }
    }

    private static void ThrowError(string message, int lineNumber, string line)
    {
        throw new ObjFileException($"{message} - Line {lineNumber}: {line}");
    }
}


public class ObjFileException : Exception
{
    public ObjFileException(string message) : base(message)
    {

    }
}
