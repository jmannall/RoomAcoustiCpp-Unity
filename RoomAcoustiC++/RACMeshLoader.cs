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

    // Data read from modal_data.csv
    [SerializeField, HideInInspector]
    private int numSlopes = -1;
    [SerializeField, HideInInspector]
    private int numFDNs = -1;
    [SerializeField, HideInInspector]
    private int[] bandIdxs;
    // TODO: Remove loading of decayRates
    [SerializeField, HideInInspector]
    private float[] decayRates;
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
        int resized_numFDNs = numSlopes * targetFreqs.Count;
        int[] resized_bandIdxs = new int[resized_numFDNs];
        float[] resized_T60s = new float[resized_numFDNs];
        float[] resized_leftVecs = new float[resized_numFDNs * numPaths];
        float[] resized_rightVecs = new float[resized_numFDNs * numPaths];
        // The internal variables' shapes are:
        // numFDNs = numSlopes * numBands;
        // bandIdxs = new int[numFDNs];
        // T60s = new float[numFDNs];
        // rightVecs = new float[numFDNs, numPaths];
        // leftVecs = new float[numFDNs, numPaths];

        // Fill out all resized variables.
        int oldBandIdx, newBandIdx;
        int numAssigned = 0;
        for (int localIdx = 0; localIdx < numFDNs; ++localIdx)
        {
            // Find the best match for this slope's frequency band among the requested bands.
            oldBandIdx = bandIdxs[localIdx];
            newBandIdx = BestBandMatch(frequencies[oldBandIdx], targetFreqs);

            // Only record this slope if its frequency band matches one of the requested bands.
            // If the user removed any of the octave bands through the GUI, some slopes will not find a match and will be ignored.
            if (Mathf.Approximately(frequencies[oldBandIdx], targetFreqs[newBandIdx]))
            {
                resized_bandIdxs[numAssigned] = newBandIdx;
                resized_T60s[numAssigned] = T60s[localIdx];
                for (int i = 0; i < numPaths; ++i)
                {
                    resized_leftVecs[numAssigned * numPaths + i] = leftVecs[localIdx, i];
                    resized_rightVecs[numAssigned * numPaths + i] = rightVecs[localIdx, i];
                }
                numAssigned++;
            }
        }

        if (numAssigned < resized_numFDNs - 1)
        {
            float targetFreq;
            // If the user added any the octave bands through the GUI, the requested bands need to be filled by replicating "edge" attributes.
            for (newBandIdx = 0; newBandIdx < targetFreqs.Count; ++newBandIdx)
            {
                targetFreq = targetFreqs[newBandIdx];

                if (targetFreq < frequencies[0])
                {
                    // An additional frequency band is lower than the lowest available band.
                    // It should be filled by replicating all of the slopes from the lowest available band.
                    for (int localIdx = 0; localIdx < numFDNs; ++localIdx)
                    {
                        if (bandIdxs[localIdx] == 0)
                        {
                            resized_bandIdxs[numAssigned] = newBandIdx;
                            resized_T60s[numAssigned] = T60s[localIdx];
                            for (int i = 0; i < numPaths; ++i)
                            {
                                resized_leftVecs[numAssigned * numPaths + i] = leftVecs[localIdx, i];
                                resized_rightVecs[numAssigned * numPaths + i] = rightVecs[localIdx, i];
                            }
                            numAssigned++;
                        }
                    }
                }
                else if (targetFreq > frequencies[numFreqBands - 1])
                {
                    // An additional frequency band is higher than the highest available band.
                    // It should be filled by replicating all of the slopes from the highest available band.
                    for (int localIdx = 0; localIdx < numFDNs; ++localIdx)
                    {
                        if (bandIdxs[localIdx] == numFreqBands - 1)
                        {
                            resized_bandIdxs[numAssigned] = newBandIdx;
                            resized_T60s[numAssigned] = T60s[localIdx];
                            for (int i = 0; i < numPaths; ++i)
                            {
                                resized_leftVecs[numAssigned * numPaths + i] = newBandIdx;
                                resized_rightVecs[numAssigned * numPaths + i] = newBandIdx;
                            }
                            numAssigned++;
                        }
                    }
                }
            }
        }

        if (numAssigned < resized_numFDNs - 1)
            Debug.LogError("The data required by InitMoDART() was not filled entirely.");

        RACManager.InitMoDART(
          flattenedPathIndexing,
          resized_bandIdxs, resized_T60s,
          resized_leftVecs, resized_rightVecs,
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

        /*
        try
        {
            LoadMeshFromObj();
            LoadMaterialsFromCsv();
            LoadIndexingFromCsv();
            LoadModesFromCsv();
        }
        catch (Exception e)
        {
            // Suitable exceptions are thrown at all failure points in the loaders.
            Debug.LogError($"Failed to load mesh and materials: {e.Message}");
            // TODO: Make sure that all parameters are reset as they were before the failed attempt.
            return false;
        }
        */
        LoadMeshFromObj();
        LoadMaterialsFromCsv();
        LoadIndexingFromCsv();
        LoadModesFromCsv();

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

    // TODO: Move generic helper functions in a separate script

    // Parser for one float vector of length M
    private float[] ParseFloatLine(string line, int M)
    {
        float[] v = new float[M];

        string[] tokens = tokenizeLine(line);

        if (tokens.Length != M)
            throw new InvalidDataException($"Expected {M} floats, got {tokens.Length}.");

        for (int i = 0; i < M; i++)
            v[i] = ParseF(tokens[i]);
        return v;
    }

    // Parser for one int vector of length M
    private int[] ParseIntLine(string line, int M)
    {
        int[] v = new int[M];

        string[] tokens = tokenizeLine(line);
        if (tokens.Length != M)
            throw new InvalidDataException($"Expected {M} ints, got {tokens.Length}.");

        for (int i = 0; i < M; i++)
            v[i] = ParseI(tokens[i]);
        return v;
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
        int[] parsedIntLine;
        float[] parsedFloatLine;

        try
        {
            parsedIntLine = ParseIntLine(lines[0], 2);
        }
        catch (Exception e)
        {
            throw new InvalidDataException($"The first line of materials.csv should have two integer tokens (number of materials and number of frequency bands).\nInstead, the line was:\n{lines[0]}\n\nSpecific message: {e.Message}");
        }
        numMaterials = parsedIntLine[0];
        numFreqBands = parsedIntLine[1];

        if (lines.Length != (2 * numMaterials + 2))
            throw new InvalidDataException($"Expected 2 * {numMaterials} + 2 = {2 * numMaterials + 2} CSV lines, got {lines.Length}.");

        // The second line contains the frequency band centers.
        frequencies = ParseFloatLine(lines[1], numFreqBands);

        // Assert that `frequencies` form a contiguous range of octave bands.
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
        int lineIndex = 2;
        absorptions = new float[numMaterials, numFreqBands];
        scatterings = new float[numMaterials, numFreqBands];
        for (int i = 0; i < numMaterials; i++)
        {
            parsedFloatLine = ParseFloatLine(lines[lineIndex++], numFreqBands);
            for (int j = 0; j < numFreqBands; j++)
                absorptions[i, j] = parsedFloatLine[j];
            parsedFloatLine = ParseFloatLine(lines[lineIndex++], numFreqBands);
            for (int j = 0; j < numFreqBands; j++)
                scatterings[i, j] = parsedFloatLine[j];
        }
    }

    // Read the path indexing data file
    private void LoadIndexingFromCsv()
    {
        if (string.IsNullOrEmpty(selectedSceneFolder))
            throw new InvalidOperationException("Tried to load indexing data, but the selected subfolder string is null or empty.");

        // When using Resources.Load, the path is relative to "Assets/Resources/", and no file extension is needed.
        string assetPath = UnityPath("PythonExports", selectedSceneFolder, "path_indexing");
        TextAsset csvTextAsset = Resources.Load<TextAsset>(assetPath);
        if (!csvTextAsset)
            throw new FileNotFoundException($"CSV not found:\nAssets/Resources/{assetPath}.csv");

        // Get all lines from the TextAsset.
        string[] lines = tokenizeFile(csvTextAsset.text);
        int[] parsedIntLine;

        try {
            parsedIntLine = ParseIntLine(lines[0], 2);
        }
        catch (Exception e)
        {
            throw new InvalidDataException($"The first line of path_indexing.csv should have two integer tokens (number of scattering nodes and number of propagation paths).\nInstead, the line was:\n{lines[0]}\n\nSpecific message: {e.Message}");
        }
        numNodes = parsedIntLine[0];
        numPaths = parsedIntLine[1];

        if (lines.Length != (numNodes + 1))
            throw new InvalidDataException($"Expected {numNodes+1} CSV lines, got {lines.Length}.");

        // Following lines contain the indexing data.
        pathIndexing = new int[numNodes, numNodes];
        for (int i = 1; i < numNodes+1; i++)
        {
            parsedIntLine = ParseIntLine(lines[i], numNodes);
            for (int j = 0; j < numNodes; j++)
            {
                if ((parsedIntLine[j] < -1) || (parsedIntLine[j] >= numPaths))
                    throw new InvalidDataException($"All path indices should be in the range [-1, numPaths={numPaths}). The value {parsedIntLine[j]} is outside of the range.");

                pathIndexing[i-1, j] = parsedIntLine[j];
            }
        }
    }

    // Read the mode data file
    private void LoadModesFromCsv()
    {
        if (string.IsNullOrEmpty(selectedSceneFolder))
            throw new InvalidOperationException("Tried to load mode data, but the selected subfolder string is null or empty.");
        if (numPaths < 0)
            throw new InvalidOperationException("LoadModesFromCsv() should only be called after LoadIndexingFromCsv().");

        // When using Resources.Load, the path is relative to "Assets/Resources/", and no file extension is needed.
        string assetPath = UnityPath("PythonExports", selectedSceneFolder, "modal_data");
        TextAsset csvTextAsset = Resources.Load<TextAsset>(assetPath);
        if (!csvTextAsset)
            throw new FileNotFoundException($"CSV not found:\nAssets/Resources/{assetPath}.csv");

        // Get all lines from the TextAsset.
        string[] lines = tokenizeFile(csvTextAsset.text);
        int[] parsedIntLine;
        float[] parsedFloatLine;

        try
        {
            parsedIntLine = ParseIntLine(lines[0], 2);
        }
        catch (Exception e)
        {
            throw new InvalidDataException($"The first line of modal_data.csv should have two integer tokens (number of slopes and number of frequency bands).\nInstead, the line was:\n{lines[0]}\n\nSpecific message: {e.Message}");
        }
        numSlopes = parsedIntLine[0];
        if (parsedIntLine[1] != numFreqBands)
            throw new InvalidDataException($"The number of frequency bands declared in modal_data.csv ({parsedIntLine[1]}) does not match the one declared in materials.csv ({numFreqBands}).");
        numFDNs = numSlopes * numFreqBands;

        if (lines.Length != (1 + numFDNs *3))
            throw new InvalidDataException($"Expected {1 + numFDNs * 3} CSV lines, got {lines.Length}.");

        bandIdxs = new int[numFDNs];
        decayRates = new float[numFDNs];
        T60s = new float[numFDNs];
        rightVecs = new float[numFDNs, numPaths];
        leftVecs = new float[numFDNs, numPaths];

        // Following lines contain the modal data, in groups of three.
        int lineIndex = 1;
        float freqFromFile;
        for (int i = 0; i < numFDNs; i++)
        {
            parsedFloatLine = ParseFloatLine(lines[lineIndex++], 3);

            freqFromFile = parsedFloatLine[0];
            T60s[i] = parsedFloatLine[1];
            decayRates[i] = parsedFloatLine[2];

            bandIdxs[i] = BestBandMatch(freqFromFile, frequencies);
            if (!Mathf.Approximately(frequencies[bandIdxs[i]], freqFromFile))
                throw new InvalidDataException($"The frequency {freqFromFile} read from modal_data.csv does not match any of the frequencies {frequencies} read from materials.csv.");

            parsedFloatLine = ParseFloatLine(lines[lineIndex++], numPaths);
            for (int j = 0; j < numPaths; j++)
                rightVecs[i, j] = parsedFloatLine[j];

            parsedFloatLine = ParseFloatLine(lines[lineIndex++], numPaths);
            for (int j = 0; j < numPaths; j++)
                leftVecs[i, j] = parsedFloatLine[j];
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

                for (int j = 0; j < numFreqBands; j++)
                    absBuffer[j] = absorptions[matIndex, j];

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

