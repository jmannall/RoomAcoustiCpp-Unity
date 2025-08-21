using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("RoomAcoustiC++/MeshLoader")]
public class RACMeshLoader : MonoBehaviour
{
    // global singleton
    public static RACMeshLoader racMeshLoader = null;

    private char sep = Path.DirectorySeparatorChar;

    [SerializeField, Tooltip("If enabled, the acoustic mesh will be rendered during play.")]
    private bool renderAcousticMeshDuringPlay = false;

    // Serialized to ensure persistence when switching between edit and play mode, but hidden from the GUI
    [SerializeField, HideInInspector]
    private string selectedSubfolder = "";

    [SerializeField, HideInInspector]
    private GameObject meshGameObject = null;

    [SerializeField, HideInInspector]
    float[] frequencies;
    [SerializeField, HideInInspector]
    float[,] absorptions;
    [SerializeField, HideInInspector]
    float[,] scatterings;

    private void Awake()
    {
        Debug.AssertFormat(racMeshLoader == null, "More than one instance of the RACMeshLoader created! Singleton violated.");
        racMeshLoader = this;

        LoadOBJ();
    }

    private void Start()
    {
        // Ensure we have a spawned mesh, in case Awake() didn't spawn
        LoadOBJ();

        if (!Application.isPlaying || renderAcousticMeshDuringPlay)
        {
            foreach (MeshRenderer render in GetComponentsInChildren<MeshRenderer>())
                render.enabled = true;
        }

        // TODO: Write a new InitRAVES which takes all information from Unity instead of expecting a folder path.
        RACManager.InitRAVES(several_attributes);

        // TODO: Pass the triangles manually (including their materials) without using RACMaterials (what about RACObjects?).
        /*
        foreach (MeshFilter mesh in GetComponentsInChildren<MeshFilter>())
        {
            // Only add RACObject if it doesn't already exist
            if (mesh.gameObject.GetComponent<RACObject>() == null)
                mesh.gameObject.AddComponent<RACObject>();
        }
        */
        RACManager.UpdatePlanesAndEdges();
    }

    private void LoadOBJ()
    {
        if (string.IsNullOrEmpty(selectedSubfolder))
        {
            // TODO: Handle this appropriately.
        }

#if UNITY_EDITOR
        // Ensure the model importer has Read/Write enabled so meshes are readable at runtime.
        ModelImporter imp = UnityEditor.AssetImporter.GetAtPath($"Assets/MOD-ART/Meshes/{selectedSubfolder}/mesh.obj") as ModelImporter;
        if (imp && !imp.isReadable)
        {
            imp.isReadable = true;
            imp.SaveAndReimport();
        }

        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/MOD-ART/Meshes/{selectedSubfolder}/mesh.obj");
        if (!src)
        {
            Debug.LogError($"OBJ not found:\nAssets/MOD-ART/Meshes/{selectedSubfolder}/mesh.obj");
            return;
        }

        meshGameObject = (GameObject)PrefabUtility.InstantiatePrefab(src);

        foreach (MeshFilter mf in meshGameObject.GetComponentsInChildren<MeshFilter>())
        {
            MeshCollider mc = mf.GetComponent<MeshCollider>() ?? mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/OBJCache"))
            AssetDatabase.CreateFolder("Assets/Resources", "OBJCache");
        PrefabUtility.SaveAsPrefabAsset(meshGameObject, $"Assets/Resources/OBJCache/{selectedSubfolder}.prefab");
#else
        GameObject prefab = Resources.Load<GameObject>($"OBJCache/{selectedSubfolder}");
        if (!prefab)
        {
            Debug.LogError($"Runtime prefab not found:\nAssets/Resources/OBJCache/{selectedSubfolder}.prefab");
            return;
        }

        meshGameObject = Object.Instantiate(prefab);
#endif
    }

    // Parser for one float vector of length M
    private float[] ParseCSVLine(string line, int M)
    {
        float[] v = new float[M];

        string[] toks = line.Split(new[] { ',', ';', ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (toks.Length != M)
        {
            Debug.LogError($"Expected {M} floats, got {toks.Length}.");
            return v;
        }

        for (int i = 0; i < M; i++)
            v[i] = float.Parse(toks[i], System.Globalization.CultureInfo.InvariantCulture);
        return v;
    }

    // Read the material data file
    private void LoadMaterialDataCsv(string csvPath, int N, int M)
    {
        var lines = File.ReadAllLines(csvPath);
        if (lines.Length != 2*N + 1)
        {
            Debug.LogError($"Expected {2*N + 1} CSV lines, got {lines.Length}.");
            return;
        }
        //throw new System.Exception($"CSV lines={lines.Length}, expected {2 * N + 1}");

        frequencies = ParseCSVLine(lines[0], M);
        absorptions = new float[N, M];
        scatterings = new float[N, M];

        int row = 1;
        float[] parsedLine;
        for (int n = 0; n < N; n++)
        {
            parsedLine = ParseCSVLine(lines[row++], M);
            for (int j = 0; j < M; j++)
                absorptions[n, j] = parsedLine[j];
            parsedLine = ParseCSVLine(lines[row++], M);
            for (int j = 0; j < M; j++)
                scatterings[n, j] = parsedLine[j];
        }
    }

    // TODO: retrieve info... to be used while passing triangles to RAC.
    public void dostuff(int subMeshIndex)
    {
        MeshRenderer myMeshRenderer = meshGameObject.GetComponentInChildren<MeshRenderer>();

        string matName = myMeshRenderer.sharedMaterials[subMeshIndex].name;
        var m = System.Text.RegularExpressions.Regex.Match(matName, @"NODE_(\d+)_MAT_(\d+)");
        if (!m.Success)
        {
            Debug.LogError($"Failed to regex-parse material string: \"{matName}\"" +
                           "\nExpected sub-string format: \"NODE_<integer>_MAT_<integer>\"");
            return;
        }

        int nodeIndex = int.Parse(m.Groups[1].Value);
        int matIndex = int.Parse(m.Groups[2].Value);
    }


    // Called by the editor to apply a new selection.
    public void __EditorAssignSelection(string root, string subfolder)
    {
        // Prevent operations during play mode
        if (Application.isPlaying)
        {
            Debug.LogError("Cannot assign selection during play mode.");
            return;
        }

        // TODO: What to do about the attributes if a failure occurs while reading a file?
        selectedSubfolder = subfolder;

        // TODO: infoTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{subfolder}/info.csv");

        // TODO: indexingTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{subfolder}/indexing.csv");

        // TODO: modeTextAssets[i] = 

        // TODO: #if UNITY_EDITOR LoadMaterialDataCsv($"Assets/MOD-ART/Meshes/{selectedSubfolder}/material_data.csv", numMaterials, numFreqBands);
        // TODO: #else ...load asset from different path

        LoadOBJ();
    }

    public string GetCurrentSelection()
    {
        return selectedSubfolder;
    }
}
