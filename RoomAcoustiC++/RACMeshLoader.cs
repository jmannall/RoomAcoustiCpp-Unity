using System.IO;
using UnityEngine;
using System;
using System.Globalization;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RACMeshLoader : MonoBehaviour
{
    // global singleton
    public static RACMeshLoader racMeshLoader = null;

    [SerializeField, Tooltip("If enabled, the acoustic mesh will be rendered.")]
    private bool renderAcousticMesh = false;

    // Serialized to ensure persistence when switching between edit and play mode, but hidden from the GUI
    // TODO: For each of these, consider if it's actually necessary to [SerializeField, HideInInspector].
    [SerializeField, HideInInspector]
    private string selectedSubfolder = "";
    private static string meshesRoot = "Assets/MOD-ART/Meshes";
    private static string prefabCache = "MoD-ART";

    [SerializeField, HideInInspector]
    private GameObject meshGameObject = null;

    [SerializeField, HideInInspector]
    private int numFreqs = -1;
    [SerializeField, HideInInspector]
    private int numMaterials = -1;
    [SerializeField, HideInInspector]
    private float[] frequencies;
    [SerializeField, HideInInspector]
    private float[,] absorptions;
    [SerializeField, HideInInspector]
    private float[,] scatterings;

    private static string[] tokenizePath(string path) => path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
    private static string[] tokenizeFile(string file) => file.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    private static string[] tokenizeLine(string line) => line.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    private static float ParseF(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    private static int ParseI(string s) => int.Parse(s, CultureInfo.InvariantCulture);

#if UNITY_EDITOR
    private static void RecursiveMKDir(string pathToFile)
    {
        if (Path.HasExtension(pathToFile))
            pathToFile = Path.GetDirectoryName(pathToFile);

        if (string.IsNullOrEmpty(pathToFile))
            return;

        string[] parts = tokenizePath(pathToFile);

        string currentRoot = "Assets";
        int start = Array.IndexOf(parts, currentRoot);
        if (start < 0)
            return;

        for (int i = start+1; i < parts.Length; i++)
        {
            string nextRoot = currentRoot + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextRoot))
                AssetDatabase.CreateFolder(currentRoot, parts[i]);
            currentRoot = nextRoot;
        }
    }
#endif

    private void OnValidate()
    {
        Debug.AssertFormat(racMeshLoader == null, "More than one instance of the RACMeshLoader created! Singleton violated.");
        racMeshLoader = this;
    }

    private void Awake()
    {
        // If this is the start of play, load the appropriate assets (specified by selectedSubfolder).
        LoadAllMeshData();
    }

    private void Start()
    {
        // Load assets in case Awake() didn't -- e.g., if this object awoke before its editor.
        LoadAllMeshData();

        SendWallsToRAC();

        // TODO: Write a new InitRAVES which takes all information from Unity instead of expecting a folder path.
        // TODO: We must NOT make use of absolute paths in the final version.
        char sep = Path.DirectorySeparatorChar;
        string resourcePath = Path.Combine(
            Application.dataPath.Replace('/', sep),  // This already includes "/Assets"
            meshesRoot.Replace("Assets/", "").Replace('/', sep),
            selectedSubfolder);
        RACManager.InitRAVES(resourcePath + sep);

        RACManager.UpdatePlanesAndEdges();
    }

    private void Update()
    {
        foreach (MeshRenderer render in meshGameObject.GetComponentsInChildren<MeshRenderer>())
            render.enabled = renderAcousticMesh;
    }

    private bool LoadAllMeshData()
    {
        if (string.IsNullOrEmpty(selectedSubfolder))
        {
            Debug.LogWarning("Cannot load mesh and materials: no subfolder selected.");
            return false;
        }

        try
        {
            LoadMeshFromObj();
            LoadMaterialsFromCsv();
        }
        catch (Exception e)
        {
            // TODO: Throw exceptions at all failure points in the loaders.
            Debug.LogError($"Failed to load mesh and materials: {e.Message}");
            return false;
        }
        return true;
    }

    // Called by the editor to apply a new selection.
    public bool __EditorAssignSelection(string subfolder)
    {
        // Prevent operations during play mode
        if (Application.isPlaying)
        {
            Debug.LogError("Cannot assign selection during play mode.");
            return false;
        }

        // TODO: What to do about the attributes if a failure occurs while reading a file?
        selectedSubfolder = subfolder;

        // TODO: infoTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{subfolder}/info.csv");

        // TODO: indexingTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{subfolder}/indexing.csv");

        // TODO: modesTextAsset = 

        return LoadAllMeshData();
    }

    public string GetRootFolder()
    {
        return meshesRoot;
    }

    public string GetCurrentSelection()
    {
        return selectedSubfolder;
    }

    private void LoadMeshFromObj()
    {
        if (string.IsNullOrEmpty(selectedSubfolder))
        {
            // TODO: Handle this appropriately.
            return;
        }

        // Clean up previous mesh if it exists
        if (meshGameObject != null)
#if UNITY_EDITOR
            DestroyImmediate(meshGameObject);
#else
            Destroy(meshGameObject);
#endif

#if UNITY_EDITOR
        string importPath = $"{meshesRoot}/{selectedSubfolder}/mesh.obj";
        string assetPath = $"Assets/Resources/{prefabCache}/{selectedSubfolder}/mesh.prefab";

        // Ensure the model importer has Read/Write enabled so meshes are readable at runtime.
        // Also, ensure the original material data is respected.
        ModelImporter imp = AssetImporter.GetAtPath(importPath) as ModelImporter;
        if (imp && !imp.isReadable)
        {
            imp.isReadable = true;
            //imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            //imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            //imp.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            imp.SaveAndReimport();
        }

        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(importPath);
        if (!src)
        {
            Debug.LogError($"OBJ not found at import path:\n{importPath}");
            return;
        }

        meshGameObject = (GameObject)PrefabUtility.InstantiatePrefab(src);

        foreach (MeshFilter mf in meshGameObject.GetComponentsInChildren<MeshFilter>())
        {
            MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }

        RecursiveMKDir(assetPath);
        PrefabUtility.SaveAsPrefabAsset(meshGameObject, assetPath);
#else
        GameObject prefab = Resources.Load<GameObject>($"{prefabCache}/{selectedSubfolder}/mesh");
        if (!prefab)
        {
            Debug.LogError($"Runtime prefab not found:\nAssets/Resources/{prefabCache}/{selectedSubfolder}/mesh.prefab");
            return;
        }

        meshGameObject = Object.Instantiate(prefab);
#endif
        // Set the mesh as a child of this RACMeshLoader GameObject
        meshGameObject.transform.SetParent(this.transform);

        foreach (MeshRenderer render in meshGameObject.GetComponentsInChildren<MeshRenderer>())
            render.enabled = renderAcousticMesh;
    }

    // Parser for one float vector of length M
    private float[] ParseCoeffLine(string line, int M)
    {
        float[] v = new float[M];

        string[] tokens = tokenizeLine(line);
        if (tokens.Length != M)
        {
            Debug.LogError($"Expected {M} floats, got {tokens.Length}.");
            return v;
        }

        for (int i = 0; i < M; i++)
            v[i] = ParseF(tokens[i]);
        return v;
    }

    // Read the material data file
    private void LoadMaterialsFromCsv()
    {
        if (string.IsNullOrEmpty(selectedSubfolder))
        {
            // TODO: Handle this appropriately.
            return;
        }

#if UNITY_EDITOR
        // Write a TextAsset file under Resources so it can be loaded at runtime.
        string importPath = $"{meshesRoot}/{selectedSubfolder}/materials.csv";
        string assetPath = $"Assets/Resources/{prefabCache}/{selectedSubfolder}/materials.csv";

        RecursiveMKDir(assetPath);
        File.WriteAllText(assetPath, File.ReadAllText(importPath));

        AssetDatabase.ImportAsset(assetPath);
        TextAsset csvTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
#else
        // Load the TextAsset saved in Resources (path is relative to any Resources/ folder, no extension).
        TextAsset csvTextAsset = UnityEngine.Resources.Load<UnityEngine.TextAsset>($"{prefabCache}/{subfolderName}/materials");
        if (!csvTextAsset)
        {
            Debug.LogError($"Runtime prefab not found:\nAssets/Resources/{prefabCache}/{selectedSubfolder}/materials.csv");
            return;
        }
#endif

        // Get all lines from the TextAsset.
        string[] lines = tokenizeFile(csvTextAsset.text);

        string[] tokens = tokenizeLine(lines[0]);
        if (tokens.Length != 2)
        {
            Debug.LogError($"The first line of materials.csv should have two tokens (number of materials and number of frequency bands).\nInstead, the line was:\n{lines[0]}");
            return;
        }
        numMaterials = ParseI(tokens[0]);
        numFreqs = ParseI(tokens[1]);

        if (lines.Length != (2 * numMaterials + 2))
        {
            Debug.LogError($"Expected 2 * {numMaterials} + 2 = {2 * numMaterials + 2} CSV lines, got {lines.Length}.");
            return;
        }

        // The second line contains the frequency band centers.
        frequencies = ParseCoeffLine(lines[1], numFreqs);

        // Following lines contain material data.
        int lineIndex = 2;
        float[] parsedLine;
        absorptions = new float[numMaterials, numFreqs];
        scatterings = new float[numMaterials, numFreqs];
        for (int n = 0; n < numMaterials; n++)
        {
            parsedLine = ParseCoeffLine(lines[lineIndex++], numFreqs);
            for (int j = 0; j < numFreqs; j++)
                absorptions[n, j] = parsedLine[j];
            parsedLine = ParseCoeffLine(lines[lineIndex++], numFreqs);
            for (int j = 0; j < numFreqs; j++)
                scatterings[n, j] = parsedLine[j];
        }
    }

    private float[] ResizeCoeffs(float[] inputFreqs, float[] inputCoeffs)
    {
        List<float> targetFreqs = null;

        if (Application.isPlaying && RACManager.racManager != null)
            targetFreqs = RACManager.racManager.frequencyBands;
        else
        {
#if UNITY_EDITOR
            RACManager racManagerInstance = UnityEngine.Object.FindAnyObjectByType<RACManager>();
            if (racManagerInstance != null)
                targetFreqs = racManagerInstance.frequencyBands;
#endif
        }

        float[] resizedCoeffs = new float[targetFreqs.Count];

        if (targetFreqs == null)
        {
            Debug.LogError("Unable to retrieve frequency band centers from RACManager.");
            return resizedCoeffs;
        }

        if (inputFreqs.Length != inputCoeffs.Length || inputFreqs.Length < 1)
        {
            Debug.LogError("The arguments of ResizeCoeffs must have the same size and contain at least one element each.");
            return resizedCoeffs;
        }

        for (int i = 0; i < targetFreqs.Count; i++)
        {
            float tFreq = targetFreqs[i];
            float coeff;
            if (tFreq < inputFreqs[0])
                coeff = inputCoeffs[0];
            else if (tFreq > inputFreqs[inputFreqs.Length - 1])
                coeff = inputCoeffs[inputCoeffs.Length - 1];
            else
            {
                int closestIndex = 0;
                float smallestDiff = Mathf.Abs(tFreq - inputFreqs[0]);

                for (int j = 1; j < inputFreqs.Length; j++)
                {
                    float diff = Mathf.Abs(tFreq - inputFreqs[j]);
                    if (diff < smallestDiff)
                    {
                        smallestDiff = diff;
                        closestIndex = j;
                    }
                }

                closestIndex = Mathf.Clamp(closestIndex, 0, inputCoeffs.Length - 1);
                coeff = inputCoeffs[closestIndex];
            }

            resizedCoeffs[i] = coeff;
        }

        return resizedCoeffs;
    }

    private void SendWallsToRAC()
    {
        // Some "buffer" variables which will hold temporary values during the loop
        int[] flattenedVertexTriplets;              // Indices of the vertices forming all triangles in a submesh (flattened Nx3 array)
        float[] absBuffer = new float[numFreqs];    // Absorption coeffs of one material
        float[] absResized;                         // Absorption coeffs of one material, resized to the expected frequency bands
        Vector3[] vertsBuffer = new Vector3[3];     // 3D coordinates of three vertices forming a triangle

        foreach (MeshFilter mf in meshGameObject.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = mf.sharedMesh;
            if (!mesh) continue; // TODO: Log debug message
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (!mr) continue; // TODO: Log debug message

            Vector3[] allVerts = mesh.vertices;
            Transform meshTransform = mf.transform;

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (s >= mr.sharedMaterials.Length) continue; // TODO: Log debug message
                if (!mr.sharedMaterials[s]) continue; // TODO: Log debug message

                string matName = mr.sharedMaterials[s].name;

                var matMatch = System.Text.RegularExpressions.Regex.Match(matName, @"Node_(\d+)_Mat_(\d+)");
                if (!matMatch.Success)
                {
                    Debug.LogError($"Failed to regex-parse material string: \"{matName}\"" +
                                    "\nExpected sub-string format: \"Node_<integer>_Mat_<integer>\"");
                    continue;
                }

                int nodeIndex = int.Parse(matMatch.Groups[1].Value);
                int matIndex = int.Parse(matMatch.Groups[2].Value);

                for (int j = 0; j < numFreqs; j++)
                    absBuffer[j] = absorptions[matIndex, j];

                flattenedVertexTriplets = mesh.GetIndices(s);
                for (int i = 0; i < flattenedVertexTriplets.Length; i += 3)
                {
                    vertsBuffer[0] = meshTransform.TransformPoint(allVerts[flattenedVertexTriplets[i]]);
                    vertsBuffer[1] = meshTransform.TransformPoint(allVerts[flattenedVertexTriplets[i+1]]);
                    vertsBuffer[2] = meshTransform.TransformPoint(allVerts[flattenedVertexTriplets[i+2]]);

                    //Debug.Log($"InitWall: Node index {nodeIndex}, absorption {absBuffer} (material index {matIndex}), vertices {vertsBuffer}");

                    absResized = ResizeCoeffs(frequencies, absBuffer);
                    RACManager.InitWall(ref vertsBuffer, ref absResized);
                }
            }
        }
    }
}
