using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class VATTool : EditorWindow
{
    private const int MAX_TEXTURE_SIZE = 4096;

    private AnimationClip clip;
    private GameObject animatedGameObject;
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private float minSamplingRate = 60.0f;
    private bool powerOfTwo = true;

    private bool hasResults = false;
    private Texture2D results_texture;
    private float results_duration;
    private Vector2 results_bounds;

    [MenuItem("Tools/Vertex Animation Texture Tool")]
    static void Init()
    {
        VATTool window = (VATTool)GetWindow(typeof(VATTool));
        window.Show();
    }

    private void OnGUI()
    {
        clip = (AnimationClip)EditorGUILayout.ObjectField("Animation clip", clip, typeof(AnimationClip), false);
        animatedGameObject = (GameObject)EditorGUILayout.ObjectField("Animated GameObject", animatedGameObject, typeof(GameObject), true);
        skinnedMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Skinned mesh renderer", skinnedMeshRenderer, typeof(SkinnedMeshRenderer), true);
        EditorGUILayout.Space();
        minSamplingRate = EditorGUILayout.FloatField("Sampling rate (per sec.)", minSamplingRate);
        powerOfTwo = EditorGUILayout.Toggle("Power of two", powerOfTwo);

        GUI.enabled =
            clip &&
            animatedGameObject &&
            skinnedMeshRenderer &&
            skinnedMeshRenderer.sharedMesh
            && minSamplingRate > 0;

        if (GUILayout.Button("Generate"))
            GenerateTexture();

        GUI.enabled = true;


        if (hasResults)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Last results:");
            EditorGUI.indentLevel++;
            EditorGUILayout.ObjectField("Asset: ", results_texture, typeof(Texture), false);
            EditorGUILayout.FloatField("Duration: ", results_duration);
            EditorGUILayout.Vector2Field("Bounds: ", results_bounds);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }

    public void GenerateTexture()
    {
        Vector3[] defaultVertexPositions = skinnedMeshRenderer.sharedMesh.vertices; //the vertex positions when the mesh is not animated

        int textureHeight = defaultVertexPositions.Length;
        if (powerOfTwo)
            textureHeight = GetNearestPowerOfTwo(textureHeight);

        if (textureHeight > MAX_TEXTURE_SIZE)
        {
            EditorUtility.DisplayDialog(
                "Error",
                string.Format("Vertices count of {0} exceeds the max texture size ({1})", skinnedMeshRenderer.sharedMesh.name, MAX_TEXTURE_SIZE),
                "OK");
            return;
        }

        int textureWidth = Mathf.CeilToInt(clip.length * minSamplingRate);
        if (powerOfTwo)
            textureWidth = GetNearestPowerOfTwo(textureWidth);

        if (textureWidth > MAX_TEXTURE_SIZE || textureHeight > MAX_TEXTURE_SIZE)
        {
            EditorUtility.DisplayDialog("Error", string.Format("Animation clip is too long to be sampled at {0}FPS for a max texture size of {1}!", minSamplingRate, MAX_TEXTURE_SIZE), "OK");
            return;
        }

        Vector3[][] frames = new Vector3[textureWidth][];
        Mesh bakedMesh = new Mesh(); //we need to bake the skinned mesh to a regular mesh in order to get its vertex positions on each frame
        List<Vector3> tmpVPos = new List<Vector3>(); //tmp list to store the vertex positions of the baked mesh
        Vector2 bounds = new Vector2(float.PositiveInfinity, float.NegativeInfinity); //minimum and maximum x, y or z values of each vertex positions, bounds.x is min / bounds.y is max

        Undo.RegisterFullObjectHierarchyUndo(animatedGameObject, "Sample animation"); //remember the current "pose" of the gameobject to be animated, horrible but necessary

        for (int x = 0; x < textureWidth; x++)
        {
            float t = (x / (float)textureWidth) * clip.length;

            clip.SampleAnimation(animatedGameObject, t);
            skinnedMeshRenderer.BakeMesh(bakedMesh, false);
            bakedMesh.GetVertices(tmpVPos);

            for (int y = 0; y < tmpVPos.Count; y++)
            {
                tmpVPos[y] -= defaultVertexPositions[y]; //get the offset of the vertex position on THIS frame from its "default" position when the mesh is still

                bounds.x = Mathf.Min(bounds.x, tmpVPos[y].x, tmpVPos[y].y, tmpVPos[y].z);
                bounds.y = Mathf.Max(bounds.y, tmpVPos[y].x, tmpVPos[y].y, tmpVPos[y].z);
            }

            frames[x] = tmpVPos.ToArray();
        }

        Undo.PerformUndo(); //reset the animated pose, i hate this

        Texture2D texture = new Texture2D(textureWidth,
                                          textureHeight,
                                          TextureFormat.RGBA32,
                                          false);

        for (int x = 0; x < textureWidth; x++)
        {
            for (int y = 0; y < frames[x].Length; y++)
            {
                Color col = new Color(
                   Mathf.InverseLerp(bounds.x, bounds.y, frames[x][y].x),
                   Mathf.InverseLerp(bounds.x, bounds.y, frames[x][y].y),
                   Mathf.InverseLerp(bounds.x, bounds.y, frames[x][y].z)
                   );

                texture.SetPixel(x, y, col);
            }
        }

        texture.Apply();

        string path = EditorUtility.SaveFilePanelInProject("Save Texture", "VATTexture_" + clip.name, "png", "Select destination");

        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog("Error", "Path is invalid!", "OK");
            return;
        }

        byte[] pngData = texture.EncodeToPNG();

        if (pngData != null)
        {
            System.IO.File.WriteAllBytes(path, pngData);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.RoundToInt(Mathf.Max(GetNearestPowerOfTwo(textureWidth), GetNearestPowerOfTwo(textureHeight)));
            importer.npotScale = powerOfTwo ? TextureImporterNPOTScale.ToNearest : TextureImporterNPOTScale.None;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            AssetDatabase.Refresh();

            hasResults = true;
            results_duration = clip.length;
            results_bounds = bounds;
            results_texture = (Texture2D)AssetDatabase.LoadMainAssetAtPath(path);
        }
    }

    private int GetNearestPowerOfTwo(int x)
    {
        if (x < 0) { return 0; }
        --x;
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        return x + 1;
    }
}
