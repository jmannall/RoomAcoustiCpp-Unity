
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[AddComponentMenu("RoomAcoustiC++/RAC Mesh")]

public class RACMesh : MonoBehaviour
{

    // global singleton
    public static RACMesh racMesh = null;

    [SerializeField]
    [Tooltip("Input the main dimensions of the room in metres. Controls the length of the FDN delay lines.")]
    [Min(0.0f)]
    private List<float> roomDimensions = new List<float> { 2.0f, 3.0f, 5.0f };

    [SerializeField, HideInInspector]
    private float absorptionSkew = 0.0f;
   
    [SerializeField]
    [Tooltip("Set the volume of the room.")]
    [Min(0.0f)]
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
        if (racMesh == null)
            racMesh = this;
        else
            Debug.AssertFormat(racMesh == this, "More than one instance of the RACMesh created! Singleton violated.");
    }

    void Start()
    {
        meshes = GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mesh in meshes)
            mesh.gameObject.AddComponent<RACObject>();
        objects = GetComponentsInChildren<RACObject>();

        if (disableMeshRenderers)
            DisableMeshRenderers();

        Debug.Log("Number of meshes: " + meshes.Length);
        Debug.Log("Number of objects: " + objects.Length);

        RACManager racManagerInstance;
        if (Application.isPlaying)
            racManagerInstance = RACManager.racManager;
        else
            racManagerInstance = FindAnyObjectByType<RACManager>();

        if (racManagerInstance == null)
        {
            Debug.LogError("Unable to locate RACManager instance: failed to start RACMesh.");
            return;
        }

        if (racManagerInstance.GetLateReverbModel() == RACManager.LateReverbModel.SingleFDN)
        {
            Debug.Log("RAC mesh is initializing late reverb with a single FDN.");

            bool success = RACManager.InitSingleFDN(volume, roomDimensions.ToArray());

            if (!success)
                Debug.LogError("Failed to initialize late reverb.");
        }
        else
        {
            Debug.LogError("RAC mesh cannot initialize MoD-ART. Use RacMesh if you want basic late reverb.");
            return;
        }

        initialised = true;
        if (absorptionSkew != 0.0f)
            UpdateAbsorption();
        RACManager.UpdatePlanesAndEdges();
    }

    void LateUpdate()
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
