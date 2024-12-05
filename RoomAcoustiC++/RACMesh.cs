
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("RoomAcoustiC++/Mesh")]

public class RACMesh : MonoBehaviour
{

    // global singleton
    public static RACMesh racMesh = null;

    [SerializeField]
    [Tooltip("Input the main dimensions of the room in metres. Controls the length of the FDN delay lines.")]
    private List<float> roomDimensions;

    [SerializeField, HideInInspector]
    private float absorptionSkew = 0.0f;

    [SerializeField]
    [Tooltip("Set the volume of the room.")]
    private float volume = 0.0f;

    [SerializeField]
    [Tooltip("Disable the mesh renderers of the acoustic mesh. Use if a seperate mesh is being used for visuals.")]
    private bool disableMeshRenderers;

    private MeshFilter[] meshes;
    private RACObject[] objects;

    private bool hasChanged = false;
    private bool initialised = false;

    #region Unity Functions

    //////////////////// Unity Functions ////////////////////

    void Awake()
    {
        Debug.AssertFormat(racMesh == null, "More than one instance of the RACMesh created! Singleton violated.");
        racMesh = this;
    }

    void Start()
    {
        meshes = GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mesh in meshes)
            mesh.gameObject.AddComponent<RACObject>();
        objects = GetComponentsInChildren<RACObject>();

        if (disableMeshRenderers)
            DisableMeshRenderers();

        Debug.Log("Number of objects: " + meshes.Length);

        RACManager.UpdateRoom(volume, roomDimensions.ToArray(), roomDimensions.Count);

        initialised = true;
        if (absorptionSkew != 0.0f)
            UpdateAbsorption();
    }

    private void LateUpdate()
    {
        meshes = GetComponentsInChildren<MeshFilter>();
        objects = GetComponentsInChildren<RACObject>();

        if (meshes.Length != objects.Length)
        {
            foreach (MeshFilter mesh in meshes)
            {
                if (mesh.gameObject.GetComponent<RACObject>() == null)
                    mesh.gameObject.AddComponent<RACObject>();
            }
            if (disableMeshRenderers)
                DisableMeshRenderers();
        }

        if (hasChanged)
        {
            RACManager.UpdatePlanesAndEdges();
            hasChanged = false;
        }
    }

    private void DisableMeshRenderers()
    {
        MeshRenderer[] renders = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer render in renders)
            render.enabled = false;
    }

    static public void UpdateAbsorption(float skew)
    {
        racMesh.absorptionSkew = skew;
        racMesh.UpdateAbsorption();
    }

    public void UpdateAbsorption()
    {
        if (!initialised)
            return;
        for (int i = 0; i < objects.Length; i++)
            objects[i].UpdateAbsorption(absorptionSkew);
    }

    public void HasChanged() { hasChanged = true; }

    #endregion
}
