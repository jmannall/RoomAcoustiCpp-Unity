using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; // for EditorApplication.delayCall
#endif

[ExecuteAlways] // Run in edit mode too, for disabling the mesh render
[AddComponentMenu("RoomAcoustiC++/MeshLoader")]

public class RACMeshLoader : MonoBehaviour
{
    // global singleton
    public static RACMeshLoader racMeshSingleton = null;

    private char sep = Path.DirectorySeparatorChar;

    [SerializeField, Tooltip("Disable the mesh renderers of the acoustic mesh. Use if a separate mesh is being used for visuals.")]
    private bool disableMeshRenderers;

    private string foldersRoot = "";
    private string selectedSubfolder = "";

    private GameObject meshObject;

    private MeshFilter[] meshes;
    private RACObject[] objects;
    private bool initialised = false;

    private void Awake()
    {
        Debug.AssertFormat(racMeshSingleton == null, "More than one instance of the RACMeshLoader created! Singleton violated.");
        racMeshSingleton = this;

        // Ensure the right child is present when entering Play Mode (or at runtime).
        if (meshObject != null)
        {
            ClearChildren();
            SpawnMesh();
        }

        UpdateMeshRenderers();
    }

    private void OnEnable()
    {
        // scene reloads, domain reloads, etc.
        UpdateMeshRenderers();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // Property changed in Inspector (edit mode) -> update safely after GUI cycle
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                // The component might have been deleted or the scene closed before the callback fires.
                if (this == null) return;

                UpdateMeshRenderers();
            };
            return;
        }
#endif
        UpdateMeshRenderers();
    }

    private void Start()
    {
        meshes = GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mesh in meshes)
            mesh.gameObject.AddComponent<RACObject>();
        objects = GetComponentsInChildren<RACObject>();

        UpdateMeshRenderers();
    }

    private void UpdateMeshRenderers()
    {
        MeshRenderer[] renders = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer render in renders)
            render.enabled = !disableMeshRenderers;
    }

    private void InitRAVES()
    {
        Debug.Log("Number of objects: " + meshes.Length);

        // TODO: Add a [SerializeField] TextAsset dataCsv; have the editor assign it alongside the mesh. Here, pass the data directly, not the path. It will be more robust at runtime.
        RACManager.InitRAVES($"{foldersRoot.Replace('/', sep)}" + sep + $"{selectedSubfolder}");

        RACManager.UpdatePlanesAndEdges();

        initialised = true;
    }

    // Called by the editor to apply a new selection.
    public void __EditorAssignSelection(string root, string subfolder, GameObject mesh)
    {
        foldersRoot = root;
        selectedSubfolder = subfolder;
        meshObject = mesh;

        ClearChildren();
        SpawnMesh();

        InitRAVES();
    }

    public string GetCurrentSelection()
    {
        return selectedSubfolder;
    }

    public bool IsInitialised()
    {
        return initialised;
    }

    // Utility: clear all children
    public void ClearChildren()
    {
        initialised = false;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child.gameObject);
            else Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }

    // Utility: spawn the mesh under this object
    public GameObject SpawnMesh()
    {
        if (meshObject == null) return null;
        var go = Instantiate(meshObject, transform);
        go.name = meshObject.name;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        UpdateMeshRenderers();
        return go;
    }
}
