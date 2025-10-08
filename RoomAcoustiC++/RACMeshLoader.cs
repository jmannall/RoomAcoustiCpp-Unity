using UnityEngine;
using System;
using System.IO;
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
    // The imported resources are loaded from "Assets/Resources/PythonExports/{sceneName}/{selectedSubfolder}/".
    // The processed mesh prefabs are stored as "Assets/Resources/ProcessedPrefabs/{sceneName}/{selectedSubfolder}.prefab".
    // Note that, out of all methods which take a file path as input,
    //      some assume a path relative to the project root,
    //      some assume a path relative to "Assets/",
    //      some assume a path relative to "Assets/Resources/",
    //      some (external) require a global path.
    [SerializeField, HideInInspector]
    private string selectedSceneFolder = "";

    [SerializeField, HideInInspector]
    private bool loadedAndReady = false;

    [SerializeField, HideInInspector]
    private GameObject meshGameObject = null;

    // Data read from materials.csv
    [SerializeField, HideInInspector]
    private int numFreqBands = -1;
    [SerializeField, HideInInspector]
    private int numMaterials = -1;
    [SerializeField, HideInInspector]
    private float[] frequencies;
    [SerializeField, HideInInspector]
    private string[] materialNames;
    [SerializeField, HideInInspector]
    private float[,] absorptions;
    [SerializeField, HideInInspector]
    private float[,] scatterings;

    // Data read from path_indexing.csv
    [SerializeField, HideInInspector]
    private int numNodes = -1;
    [SerializeField, HideInInspector]
    private int numPaths = -1;
    [SerializeField, HideInInspector]
    private int[,] pathIndexing;

    // Data read from MoD-ART.csv
    [SerializeField, HideInInspector]
    private int numFDNs = -1;
    [SerializeField, HideInInspector]
    private int[] bandIdxs;
    [SerializeField, HideInInspector]
    private float[] T60s;
    [SerializeField, HideInInspector]
    private float[,] rightVecs;
    [SerializeField, HideInInspector]
    private float[,] leftVecs;

    private static string[] tokenizePath(string path) => path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
    private static string[] tokenizeFile(string file) => file.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    private static string[] tokenizeLine(string line) => line.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    private static string UnityPath(params string[] parts) => string.Join('/', parts);
    private static float ParseF(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    private static int ParseI(string s) => int.Parse(s, CultureInfo.InvariantCulture);

#if UNITY_EDITOR
    private static void RecursiveMKDir(string pathToFile)
    {
        if (Path.HasExtension(pathToFile))
            pathToFile = Path.GetDirectoryName(pathToFile);

        if (string.IsNullOrEmpty(pathToFile))
        {
            Debug.LogError("Cannot create folder: null or empty path.");
            return;
        }

        string[] parts = tokenizePath(pathToFile);

        string currentRoot = "Assets";
        int start = Array.IndexOf(parts, currentRoot);
        if (start < 0)
        {
            Debug.LogError("Cannot create folder: requested location is not in \"Assets\".");
            return;
        }

        for (int i = start + 1; i < parts.Length; i++)
        {
            string nextRoot = currentRoot + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextRoot))
                AssetDatabase.CreateFolder(currentRoot, parts[i]);
            currentRoot = nextRoot;
        }
    }
#endif

    void OnValidate()
    {
        if (racMeshLoader == null)
            racMeshLoader = this;
        else
            Debug.AssertFormat(racMeshLoader == this, "More than one instance of the RACMeshLoader created! Singleton violated.");
    }

    void Awake()
    {
        // Ensure that the mesh loader has no children, before attempting a fresh load of the mesh.
        for (int i = this.transform.childCount - 1; i >= 0; i--)
#if UNITY_EDITOR
            DestroyImmediate(this.transform.GetChild(i).gameObject);
#else
            Destroy(this.transform.GetChild(i).gameObject);
# endif

        // If this is the start of play, load the appropriate assets (specified by selectedSubfolder).
        if (LoadAllMeshData())
            loadedAndReady = true;
    }

    void Start()
    {
        if (!loadedAndReady)
        {
            Debug.LogError("Cannot start MeshLoader: it has not been loaded.");
            return;
        }
        if (RACManager.racManager == null)
        {
            Debug.LogError("Unable to locate RACManager instance: failed to start RACMeshLoader.");
            return;
        }

        // TODO: Is this old approach still necessary? The way `RACManager.racManager` works has changed slightly.
        //        if (Application.isPlaying && RACManager.racManager != null)
        //            targetFreqs = RACManager.racManager.GetFrequencyBands();
        //        else
        //        {
        //#if UNITY_EDITOR
        //            RACManager racManagerInstance = UnityEngine.Object.FindAnyObjectByType<RACManager>();
        //            if (racManagerInstance != null)
        //                targetFreqs = racManagerInstance.GetFrequencyBands();
        //#endif
        //        }
        List<float> targetFreqs = RACManager.racManager.GetFrequencyBands();

        SendWallsToRAC(targetFreqs);

        // TODO: Cross-check the front-back convention used for path indexing

        // RACManager expects the indexing array to be flattened.
        int[] flattenedPathIndexing = new int[numNodes * numNodes];
        for (int i = 0; i < numNodes; i++)
            for (int j = 0; j < numNodes; j++)
                flattenedPathIndexing[i * numNodes + j] = pathIndexing[i, j];

        // Internal parameters `bandIdxs, T60s, leftVecs, rightVecs, numFDNs` match what was read from the files.
        // They need to be truncated/repeated to match the current frequency bands of RACManager.
        // Also, the eigenvectors need to be flattened.
        int resized_numFDNs = 0;
        List<int> resized_bandIdxs = new();
        List<float> resized_T60s = new();
        List<float> resized_leftVecs = new();
        List<float> resized_rightVecs = new();
        // The internal variables' shapes are:
        // bandIdxs = int[numFDNs];
        // T60s = float[numFDNs];
        // rightVecs = float[numFDNs, numPaths];
        // leftVecs = float[numFDNs, numPaths];

        // Fill out all resized variables.
        int oldBandIdx, newBandIdx;
        for (int localIdx = 0; localIdx < numFDNs; ++localIdx)
        {
            // Find the best match for this slope's frequency band among the requested bands.
            oldBandIdx = bandIdxs[localIdx];
            newBandIdx = BestBandMatch(frequencies[oldBandIdx], targetFreqs);

            // Only record this slope if its frequency band matches one of the requested bands.
            // If the user removed any of the octave bands through the GUI, some slopes will not find a match and will be ignored.
            if (Mathf.Approximately(frequencies[oldBandIdx], targetFreqs[newBandIdx]))
            {
                resized_numFDNs++;
                resized_bandIdxs.Add(newBandIdx);
                resized_T60s.Add(T60s[localIdx]);
                for (int i = 0; i < numPaths; ++i)
                {
                    resized_leftVecs.Add(leftVecs[localIdx, i]);
                    resized_rightVecs.Add(rightVecs[localIdx, i]);
                }
            }
        }

        // TODO: If the user added any octave bands through the GUI, the requested bands need to be filled by replicating "edge" attributes.
        for (int targetFreqIdx = 0; targetFreqIdx < targetFreqs.Count; ++targetFreqIdx)
        {
            if (targetFreqs[targetFreqIdx] < frequencies[0])
            {
                for (int localIdx = 0; localIdx < numFDNs; ++localIdx)
                {
                    if (bandIdxs[localIdx] == 0)
                    {
                        resized_numFDNs++;
                        resized_bandIdxs.Add(targetFreqIdx);
                        resized_T60s.Add(T60s[localIdx]);
                        for (int i = 0; i < numPaths; ++i)
                        {
                            resized_leftVecs.Add(leftVecs[localIdx, i]);
                            resized_rightVecs.Add(rightVecs[localIdx, i]);
                        }
                    }
                }
            }
            else if (targetFreqs[targetFreqIdx] > frequencies[numFreqBands - 1])
            {
                for (int localIdx = 0; localIdx < numFDNs; ++localIdx)
                {
                    if (bandIdxs[localIdx] == numFreqBands - 1)
                    {
                        resized_numFDNs++;
                        resized_bandIdxs.Add(targetFreqIdx);
                        resized_T60s.Add(T60s[localIdx]);
                        for (int i = 0; i < numPaths; ++i)
                        {
                            resized_leftVecs.Add(leftVecs[localIdx, i]);
                            resized_rightVecs.Add(rightVecs[localIdx, i]);
                        }
                    }
                }
            }
        }

        // Send parameters to IRPlotter first; it needs to construct its lists before receiving any residue callbacks.
        if (IRPlotter.irPlotter != null)
            IRPlotter.irPlotter.RegisterSlopes(targetFreqs, resized_bandIdxs, resized_T60s);

        RACManager.InitMoDART(
          flattenedPathIndexing,
          resized_bandIdxs.ToArray(), resized_T60s.ToArray(),
          resized_leftVecs.ToArray(), resized_rightVecs.ToArray(),
          resized_numFDNs, numNodes, numPaths
          );

        RACManager.UpdatePlanesAndEdges();
    }

    void Update()
    {
        foreach (MeshRenderer render in meshGameObject.GetComponentsInChildren<MeshRenderer>())
            render.enabled = renderAcousticMesh;
    }

    private bool LoadAllMeshData()
    {
        if (string.IsNullOrEmpty(selectedSceneFolder))
        {
            Debug.LogWarning("Cannot load mesh and materials: no subfolder selected.");
            return false;
        }

        try
        {
            LoadMeshFromObj();
            LoadMaterialsFromCsv();
            LoadIndexingFromMtx();
            LoadModesFromCsv();
        }
        catch (Exception e)
        {
            // Suitable exceptions are thrown at all failure points in the loaders.
            Debug.LogError($"Failed to load mesh and materials: {e.Message}");
            // TODO: Make sure that all parameters are reset as they were before the failed attempt.
            return false;
        }
        //LoadMeshFromObj();
        //LoadMaterialsFromCsv();
        //LoadIndexingFromMtx();
        //LoadModesFromCsv();

        if (RACManager.racManager == null)
            Debug.LogError("Unable to locate RACManager instance: failed to set frequency bands related to the loaded mesh.");
        else
            RACManager.racManager.SetFrequencyBands(frequencies);

        return true;
    }

    // Called by the editor to apply a new selection.
    public bool __EditorAssignSelection(string subfolder)
    {
        // Prevent operations during play mode
        if (Application.isPlaying)
        {
            Debug.LogError("Cannot assign selection during play mode.");
            loadedAndReady = false;
            return false;
        }

        selectedSceneFolder = subfolder;

        if (LoadAllMeshData())
        {
            loadedAndReady = true;
            return true;
        }
        else
        {
            loadedAndReady = false;
            return false;
        }
    }

    public string GetSelectedPath()
    {
        return UnityPath("Assets", "Resources", "PythonExports", selectedSceneFolder);
    }

    public string GetSelectedSubfolder()
    {
        return selectedSceneFolder;
    }

    private void LoadMeshFromObj()
    {
        if (string.IsNullOrEmpty(selectedSceneFolder))
            throw new InvalidOperationException("Tried to load mesh, but the selected subfolder string is null or empty.");

        // Clean up previous mesh if it exists
        if (meshGameObject != null)
#if UNITY_EDITOR
            DestroyImmediate(meshGameObject);
#else
            Destroy(meshGameObject);
#endif

#if UNITY_EDITOR
        string importPath = UnityPath("Assets", "Resources", "PythonExports", selectedSceneFolder, "mesh.obj");
        string prefabPath = UnityPath("Assets", "Resources", "ProcessedPrefabs", selectedSceneFolder + ".prefab");
        RecursiveMKDir(prefabPath);

        // Ensure the model importer has Read/Write enabled so meshes are readable at runtime.
        // Also, ensure the original material data is respected.
        ModelImporter imp = AssetImporter.GetAtPath(importPath) as ModelImporter;
        if (imp)
        {
            imp.isReadable = true;

            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;    // for OBJ/MTL
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;              // avoid scattering .mat files
            imp.materialName = ModelImporterMaterialName.BasedOnMaterialName;           // keep material names exactly
            imp.materialSearch = ModelImporterMaterialSearch.Local;                     // don’t “find” random matches elsewhere

            imp.SaveAndReimport();
        }
        else
            throw new InvalidDataException($"Failed to import OBJ at path:\n{importPath}");

        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(importPath);

        if (!src)
            throw new InvalidDataException($"Failed to load OBJ at path:\n{importPath}");

        // Instantiate the loaded mesh as a GameObject.
        meshGameObject = (GameObject)PrefabUtility.InstantiatePrefab(src);

        // Rotate and mirror the mesh to match Unity's coordinate system.
        meshGameObject.transform.localRotation = Quaternion.Euler(-90, 180, 0);

        // Add a collider to the meshGameObject.
        foreach (MeshFilter mf in meshGameObject.GetComponentsInChildren<MeshFilter>())
        {
            MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }

        // Save the processed mesh as a prefab, to be loaded during play mode.
        PrefabUtility.SaveAsPrefabAsset(meshGameObject, prefabPath);
#else
        // When using Resources.Load, the path is relative to "Assets/Resources/", and no file extension is needed.
        string prefabPath = UnityPath("ProcessedPrefabs", sceneName, selectedSubfolder);
        GameObject prefab = Resources.Load<GameObject>(prefabPath);
        if (!prefab)
            throw new FileNotFoundException($"Runtime prefab not found:\nAssets/Resources/{prefabPath}.prefab");

        meshGameObject = Object.Instantiate(prefab);
#endif

#if UNITY_ASSERTIONS
        foreach (MeshFilter mf in meshGameObject.GetComponentsInChildren<MeshFilter>())
            Debug.Assert(mf.sharedMesh.isReadable, $"The MoD-ART mesh ({mf.sharedMesh.name}) must be readable.");
#endif

        // Set the mesh as a child of this RACMeshLoader GameObject
        meshGameObject.transform.SetParent(this.transform);

        foreach (MeshRenderer render in meshGameObject.GetComponentsInChildren<MeshRenderer>())
            render.enabled = renderAcousticMesh;
    }

    // Read the material data file
    private void LoadMaterialsFromCsv()
    {
        if (string.IsNullOrEmpty(selectedSceneFolder))
            throw new InvalidOperationException("Tried to load material data, but the selected subfolder string is null or empty.");

        // When using Resources.Load, the path is relative to "Assets/Resources/", and no file extension is needed.
        string assetPath = UnityPath("PythonExports", selectedSceneFolder, "materials");
        TextAsset csvTextAsset = Resources.Load<TextAsset>(assetPath);
        if (!csvTextAsset)
            throw new FileNotFoundException($"CSV not found:\nAssets/Resources/{assetPath}.csv");

        // Get all lines from the TextAsset.
        string[] lines = tokenizeFile(csvTextAsset.text);

        // Perform sanity checks and retrieve the array sizes.
        if (lines.Length < 3)
            throw new InvalidDataException($"materials.csv should contain at least 3 lines, but it has {lines.Length}.");
        if ((lines.Length % 2) != 1)
            throw new InvalidDataException($"materials.csv should contain an odd number of lines, but it has {lines.Length}.");
        numMaterials = (lines.Length - 1) / 2;

        string[] tokens = tokenizeLine(lines[0]);
        if (tokens.Length < 2)
            throw new InvalidDataException($"The first line of materials.csv should have at least two elements, but it has {tokens.Length}.");
        numFreqBands = tokens.Length - 1;
        if (tokens[0] != "Frequencies")
            throw new InvalidDataException($"The first word on the first line of materials.csv should be \"Frequencies\", but it is {tokens[0]}.");

        // The first line contains the frequency band centers.
        frequencies = new float[numFreqBands];
        for (int i = 1; i < numFreqBands + 1; i++)
            frequencies[i-1] = ParseF(tokens[i]);

        // Assert that `frequencies` forms a contiguous range of octave bands.
        // Start by checking the validity of the top band.
        bool validTop = false;
        for (float f = 32e3f; f > 15; f /= 2)
        {
            if (Mathf.Approximately(frequencies[numFreqBands - 1], f))
                validTop = true;
        }
        if (!validTop)
            throw new InvalidDataException($"Invalid octave bands in materials.csv: {frequencies[numFreqBands - 1]} is not a valid octave band.");
        // Next, iteratively check lower bands for contiguity.
        for (int i = numFreqBands - 2; i >= 0; --i)
        {
            if (!Mathf.Approximately(frequencies[i], frequencies[i+1] / 2))
                throw new InvalidDataException($"Invalid octave bands in materials.csv: {frequencies[i]} is not an octave below {frequencies[i+1]}.");
        }

        // Following lines contain material data.
        materialNames = new string[numMaterials];
        absorptions = new float[numMaterials, numFreqBands];
        scatterings = new float[numMaterials, numFreqBands];
        for (int i = 0; i < numMaterials; i++)
        {
            // Read the absorption coefficients (and retrieve the material name).
            tokens = tokenizeLine(lines[(i * 2) + 1]);
            if (tokens.Length != numFreqBands + 1)
                throw new InvalidDataException($"Each line of materials.csv should have the same number of elements as the first ({numFreqBands + 1}), but line {(i * 2) + 2} has {tokens.Length}.");

            materialNames[i] = tokens[0];
            for (int j = 1; j < numFreqBands + 1; j++)
                absorptions[i, j-1] = ParseF(tokens[j]);

            // Read the scattering coefficients (and cross-check the material name).
            tokens = tokenizeLine(lines[(i * 2) + 2]);
            if (tokens.Length != numFreqBands + 1)
                throw new InvalidDataException($"Each line of materials.csv should have the same number of elements as the first ({numFreqBands + 1}), but line {(i * 2) + 3} has {tokens.Length}.");
            if (tokens[0] != materialNames[i])
                throw new InvalidDataException($"Each scattering coefficient line of materials.csv should have the same material name as the preceding absorption coefficient line, but line {(i * 2) + 3} does not ({tokens[0]} != {materialNames[i]}).");

            for (int j = 1; j < numFreqBands + 1; j++)
                scatterings[i, j-1] = ParseF(tokens[j]);
        }
    }

    // Read the path indexing data file
    private void LoadIndexingFromMtx()
    {
        if (string.IsNullOrEmpty(selectedSceneFolder))
            throw new InvalidOperationException("Tried to load indexing data, but the selected subfolder string is null or empty.");

        // When using Resources.Load, the path is relative to "Assets/Resources/", and no file extension is needed.
        // Note that `.mtx` is not a valid extension for TextAsset, so we need a special importer.
        // https://discussions.unity.com/t/loading-a-file-with-a-custom-extension-as-a-textasset/731294/5
        string assetPath = UnityPath("PythonExports", selectedSceneFolder, "path_indexing");
        TextAsset mtxTextAsset = Resources.Load<TextAsset>(assetPath);
        if (!mtxTextAsset)
            throw new FileNotFoundException($"MTX not found:\nAssets/Resources/{assetPath}.mtx");

        // Get all lines from the TextAsset.
        string[] lines = tokenizeFile(mtxTextAsset.text);

        int numNonZero;
        pathIndexing = MtxParser.toArray(lines, out numNonZero);

        if (pathIndexing.Rank != 2)
            throw new InvalidDataException($"The matrix in path_indexing.mtx should be square, but it has {pathIndexing.Rank} dimensions.");
        if (pathIndexing.GetLength(0) != pathIndexing.GetLength(1))
            throw new InvalidDataException($"The matrix in path_indexing.mtx should be square, but it has shape {pathIndexing.GetLength(0)}x{pathIndexing.GetLength(1)}.");

        numNodes = pathIndexing.GetLength(0);
        numPaths = numNonZero;

        // Assert that the nonzero elements of pathIndexing range from 1 to numPaths, then translate to 0-indexing (-1 denotes "no path").
        for (int i=0; i<numNodes; i++)
        {
            for (int j=0; j<numNodes; j++)
            {
                if (pathIndexing[i, j] < 0 || pathIndexing[i, j] > numPaths)
                    throw new InvalidDataException($"Invalid path_indexing.mtx entry at ({i},{j}): {pathIndexing[i, j]}. Expected range [0, {numPaths}].");
                pathIndexing[i, j] -= 1;
            }
        }
    }

    // Read the mode data file
    private void LoadModesFromCsv()
    {
        if (string.IsNullOrEmpty(selectedSceneFolder))
            throw new InvalidOperationException("Tried to load mode data, but the selected subfolder string is null or empty.");
        if (numPaths < 0)
            throw new InvalidOperationException("LoadModesFromCsv() should only be called after LoadIndexingFromMtx().");

        // When using Resources.Load, the path is relative to "Assets/Resources/", and no file extension is needed.
        string assetPath = UnityPath("PythonExports", selectedSceneFolder, "MoD-ART");
        TextAsset csvTextAsset = Resources.Load<TextAsset>(assetPath);
        if (!csvTextAsset)
            throw new FileNotFoundException($"CSV not found:\nAssets/Resources/{assetPath}.csv");

        // Get all lines from the TextAsset.
        string[] lines = tokenizeFile(csvTextAsset.text);

        // Perform sanity checks and retrieve the array sizes.
        if (lines.Length < 3)
            throw new InvalidDataException($"MoD-ART.csv should contain at least 3 lines, but it has {lines.Length}.");
        if ((lines.Length % 3) != 0)
            throw new InvalidDataException($"MoD-ART.csv should contain a number of lines divisible by 3, but it has {lines.Length}.");
        numFDNs = lines.Length / 3;

        bandIdxs = new int[numFDNs];
        T60s = new float[numFDNs];
        rightVecs = new float[numFDNs, numPaths];
        leftVecs = new float[numFDNs, numPaths];

        // Lines contain the modal data, in groups of three.
        string[] tokens;
        for (int i = 0; i < numFDNs; i++)
        {
            // Read the octave band index and T60.
            tokens = tokenizeLine(lines[i * 3]);
            if (tokens.Length != 2)
                throw new InvalidDataException($"Line {(i * 3) + 1} of MoD-ART.csv should contain 2 elements, but it has {tokens.Length}.");
            bandIdxs[i] = ParseI(tokens[0]) - 1; // N.B. Translate to 0-indexing
            T60s[i] = ParseF(tokens[1]);

            // Read the right eigenvector.
            tokens = tokenizeLine(lines[(i * 3) + 1]);
            if (tokens.Length != numPaths)
                throw new InvalidDataException($"Line {(i * 3) + 2} of MoD-ART.csv should contain {numPaths} elements, but it has {tokens.Length}.");
            for (int j = 0; j < numPaths; j++)
                rightVecs[i, j] = ParseF(tokens[j]);

            // Read the left eigenvector.
            tokens = tokenizeLine(lines[(i * 3) + 2]);
            if (tokens.Length != numPaths)
                throw new InvalidDataException($"Line {(i * 3) + 3} of MoD-ART.csv should contain {numPaths} elements, but it has {tokens.Length}.");
            for (int j = 0; j < numPaths; j++)
                leftVecs[i, j] = ParseF(tokens[j]);
        }
    }

    private int BestBandMatch(float targetBand, float[] referenceBands)
    {
        if (targetBand < referenceBands[0])
            return 0;
        else if (targetBand > referenceBands[referenceBands.Length - 1])
            return referenceBands.Length - 1;
        else
        {
            float diff;
            int closestIndex = 0;
            float smallestDiff = Mathf.Abs(targetBand - referenceBands[0]);

            for (int j = 1; j < referenceBands.Length; j++)
            {
                diff = Mathf.Abs(targetBand - referenceBands[j]);
                if (diff < smallestDiff)
                {
                    closestIndex = j;
                    smallestDiff = diff;
                }
            }

            return closestIndex;
        }
    }
    private int BestBandMatch(float targetBand, List<float> referenceBands)
    {
        if (targetBand < referenceBands[0])
            return 0;
        else if (targetBand > referenceBands[referenceBands.Count - 1])
            return referenceBands.Count - 1;
        else
        {
            int closestIndex = 0;
            float smallestDiff = Mathf.Abs(targetBand - referenceBands[0]);
            float diff = smallestDiff;

            for (int j = 1; j < referenceBands.Count; j++)
            {
                diff = Mathf.Abs(targetBand - referenceBands[j]);
                if (diff < smallestDiff)
                {
                    closestIndex = j;
                    smallestDiff = diff;
                }
            }

            return closestIndex;
        }
    }

    private float[] ResizeCoeffs(List<float> targetFreqs, float[] inputFreqs, float[] inputCoeffs)
    {
        float[] resizedCoeffs = new float[targetFreqs.Count];

        if (inputFreqs.Length != inputCoeffs.Length || inputFreqs.Length < 1)
        {
            Debug.LogError("The arguments of ResizeCoeffs must have the same size and contain at least one element each.");
            return resizedCoeffs;
        }

        for (int i = 0; i < targetFreqs.Count; i++)
            resizedCoeffs[i] = inputCoeffs[BestBandMatch(targetFreqs[i], inputFreqs)];

        return resizedCoeffs;
    }

    private void SendWallsToRAC(List<float> targetFreqs)
    {
        // Some "buffer" variables which will hold temporary values during the loop
        int[] flattenedVertexTriplets;                  // Indices of the vertices forming all triangles in a submesh (flattened Nx3 array)
        float[] absBuffer = new float[numFreqBands];    // Absorption coeffs of one material
        float[] absResized;                             // Absorption coeffs of one material, resized to the expected frequency bands
        Vector3[] vertsBuffer = new Vector3[3];         // 3D coordinates of three vertices forming a triangle

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

                string patchMatString = mr.sharedMaterials[s].name;

                var matMatch = System.Text.RegularExpressions.Regex.Match(patchMatString, @"Patch_(\d+)_Mat_(.+)");
                if (!matMatch.Success)
                {
                    Debug.LogError($"Patch ignored: failed to regex-parse material string: \"{patchMatString}\"" +
                                    "\nExpected sub-string format: \"Patch_<integer>_Mat_<string>\"");
                    continue;
                }

                int nodeIndex = int.Parse(matMatch.Groups[1].Value) - 1; // N.B. Translate to 0-indexing
                string materialName = matMatch.Groups[2].Value;
                int materialIndex = Array.IndexOf(materialNames, materialName);

                if (materialIndex == -1)
                {
                    Debug.LogError($"Patch ignored: material \"{materialName}\" not found in materials.csv.");
                    continue;
                }

                for (int j = 0; j < numFreqBands; j++)
                    absBuffer[j] = absorptions[materialIndex, j];

                absResized = ResizeCoeffs(targetFreqs, frequencies, absBuffer);
                RACManager.UpdateMaterial(nodeIndex, ref absResized);

                flattenedVertexTriplets = mesh.GetIndices(s);
                for (int i = 0; i < flattenedVertexTriplets.Length; i += 3)
                {
                    vertsBuffer[0] = meshTransform.TransformPoint(allVerts[flattenedVertexTriplets[i]]);
                    vertsBuffer[1] = meshTransform.TransformPoint(allVerts[flattenedVertexTriplets[i+1]]);
                    vertsBuffer[2] = meshTransform.TransformPoint(allVerts[flattenedVertexTriplets[i+2]]);

                    RACManager.InitWall(ref vertsBuffer, nodeIndex);
                }
            }
        }
    }
}

