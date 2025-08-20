using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; // for ModelImporter
#endif

[AddComponentMenu("RoomAcoustiC++/MeshLoader")]
public class RACMeshLoader : MonoBehaviour
{
    // global singleton
    public static RACMeshLoader racMeshLoader = null;

    private char sep = Path.DirectorySeparatorChar;

    [SerializeField, Tooltip("Disable the mesh renderers of the acoustic mesh. Use if a separate mesh is being used for visuals.")]
    private bool disableMeshRenderers;

    // Serialized to ensure persistence when switching between edit and play mode, but hidden from the GUI
    [SerializeField, HideInInspector]
    private string foldersRoot = "";
    [SerializeField, HideInInspector]
    private string selectedSubfolder = "";
    [SerializeField, HideInInspector]
    private GameObject meshObject;

    private MeshFilter[] meshes;
    private RACObject[] objects;

    private void Awake()
    {
        Debug.AssertFormat(racMeshLoader == null, "More than one instance of the RACMeshLoader created! Singleton violated.");
        racMeshLoader = this;

        // Only spawn mesh geometry if we have a mesh object but no children
        // This prevents respawning during edit-to-play transitions
        if (meshObject != null && transform.childCount == 0)
        {
            SpawnMesh();
        }
    }

    private void Start()
    {
        // Ensure we have a spawned mesh - this handles cases where Awake didn't spawn
        if (meshObject != null && transform.childCount == 0)
        {
            SpawnMesh();
        }

        meshes = GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mesh in meshes)
        {
            // Only add RACObject if it doesn't already exist
            if (mesh.gameObject.GetComponent<RACObject>() == null)
                mesh.gameObject.AddComponent<RACObject>();
        }
        objects = GetComponentsInChildren<RACObject>();

        UpdateMeshRenderers();

        Debug.Log("Number of objects: " + objects.Length);

        // TODO: Add a [SerializeField] TextAsset dataCsv; have the editor assign it alongside the mesh. Here, pass the data directly, not the path. It will be more robust at runtime.
        string ravesPath = foldersRoot.Replace('/', sep) + sep + selectedSubfolder + sep;
        RACManager.InitRAVES(ravesPath);
        RACManager.UpdatePlanesAndEdges();
    }

    private void UpdateMeshRenderers()
    {
        MeshRenderer[] renders = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer render in renders)
            render.enabled = !disableMeshRenderers;
    }

    // Called by the editor to apply a new selection.
    public void __EditorAssignSelection(string root, string subfolder, GameObject mesh)
    {
        // Prevent operations during play mode
        if (Application.isPlaying)
        {
            Debug.LogWarning("Cannot assign selection during play mode.");
            return;
        }

        foldersRoot = root;
        selectedSubfolder = subfolder;
        meshObject = mesh;

        ClearChildren();
        SpawnMesh();
    }

    public string GetCurrentSelection()
    {
        return selectedSubfolder;
    }

    // Utility: clear all children
    public void ClearChildren()
    {
        // Prevent clearing children during play mode
        if (Application.isPlaying)
        {
            Debug.LogWarning("ClearChildren() called during play mode - ignoring to prevent object destruction");
            return;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
#if UNITY_EDITOR
            // Use appropriate destruction method based on play state
            if (!Application.isPlaying) 
                DestroyImmediate(child.gameObject);  // Edit Mode: immediate destruction
            else 
                Destroy(child.gameObject);           // Play Mode: deferred destruction
#else
            Destroy(child.gameObject);               // Runtime: always deferred
#endif
        }
    }

    // Utility: spawn the mesh under this object
    public GameObject SpawnMesh()
    {
        if (meshObject == null) return null;
        
        // Ensure the original mesh asset is readable before spawning
#if UNITY_EDITOR
        EnsureMeshIsReadable(meshObject);
#endif
        
        var go = Instantiate(meshObject, transform);
        go.name = meshObject.name;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        return go;
    }

#if UNITY_EDITOR
    private void EnsureMeshIsReadable(GameObject go)
    {
        meshes = go.GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mesh in meshes)
        {
            if (mesh.sharedMesh != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(mesh.sharedMesh);
                
                if (!string.IsNullOrEmpty(assetPath))
                {
                    ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                    
                    if (importer != null && !importer.isReadable)
                    {
                        Debug.Log($"Making mesh readable: {assetPath}");
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                }
            }
        }
    }
#endif
}
