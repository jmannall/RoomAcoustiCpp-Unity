
using System;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;

public class RACObject : MonoBehaviour
{
    /// <summary>
    /// Unity defines the normal according to vertices. Vertex order per face is defined using the right hand rule relative to the normal.
    /// </summary>

    #region Parameters

    //////////////////// Parameters ////////////////////

    private Mesh mesh;
    private RACMaterial material;

    [SerializeField]
    private List<RACSubMesh> subMeshes = new List<RACSubMesh>();

    private MeshFilter savedMeshFilter;
    private MeshFilter currentMeshFilter;

    #endregion

    #region Unity Functions

    //////////////////// Unity Functions ////////////////////

    void Awake()
    {
        currentMeshFilter = GetComponent<MeshFilter>();
        savedMeshFilter = currentMeshFilter;
        mesh = currentMeshFilter.mesh;

        material = GetComponent<RACMaterial>();

        Transform parentTransform = transform;
        while (!material && parentTransform.parent)
        {
            material = parentTransform.GetComponentInParent<RACMaterial>();
            parentTransform = parentTransform.parent;
        }
        if (!material)
            material = gameObject.AddComponent<RACMaterial>();

        for (int i = 0; i < mesh.subMeshCount; i++)
            InitSubMesh(i);

        transform.hasChanged = false;
    }

    private void Update()
    {
        currentMeshFilter = GetComponent<MeshFilter>();
        if (transform.hasChanged || !ReferenceEquals(savedMeshFilter, currentMeshFilter))
        {
            savedMeshFilter = currentMeshFilter;
            mesh = currentMeshFilter.mesh;

            if (mesh.subMeshCount > subMeshes.Count)
            {
                // init new sub meshes
                for (int i = subMeshes.Count; i < mesh.subMeshCount; i++)
                    InitSubMesh(i);
            }
            else if (mesh.subMeshCount < subMeshes.Count) // remove extra sub meshes
                subMeshes.RemoveRange(mesh.subMeshCount, subMeshes.Count - mesh.subMeshCount);
            

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                SubMeshDescriptor subMesh = mesh.GetSubMesh(i);
                subMeshes[i].Update(ref mesh, ref subMesh, transform);
            }
            
            transform.hasChanged = false;
            RACMesh.racMesh.HasChanged();
        }
    }

    private void InitSubMesh(int i)
    {
        SubMeshDescriptor subMesh = mesh.GetSubMesh(i);
        RACMaterialEntry materialEntry = material.FindMatch(GetComponent<MeshRenderer>().sharedMaterials[i]);
        subMeshes.Add(new RACSubMesh(ref mesh, ref subMesh, transform, materialEntry.GetAbsorption()));
    }

    private void OnDestroy()
    {
        Remove();
    }

    #endregion

    #region Functions

    //////////////////// Functions ////////////////////

    private void Remove()
    {
        foreach (RACSubMesh subMesh in subMeshes)
        {
            if (subMesh != null)
                subMesh.Remove();
        }
        subMeshes.Clear();
    }

    public void UpdateAbsorption(float skew)
    {
        for (int i = 0; i < subMeshes.Count; i++)
        {
            RACMaterialEntry materialEntry = material.FindMatch(GetComponent<MeshRenderer>().sharedMaterials[i]);
            float[] absorption = materialEntry.GetAbsorption();
            if (skew < 0.0f)
            {
                absorption = absorption.Select(value => Mathf.Lerp(0.01f, value, 1 + skew)).ToArray();
                // abs[0] = Mathf.Lerp(0.01f, abs[0], 1 + skew);
            }
            else
            {
                absorption = absorption.Select(value => Mathf.Lerp(value, 1.0f, skew)).ToArray();
                // abs[0] = Mathf.Lerp(abs[0], 1.0f, skew);
            }
            subMeshes[i].UpdateAbsorption(ref absorption);
        }
    }

    #endregion

    #region RACSubMesh

    [Serializable]
    private class RACSubMesh
    {
        // Constructor

        public RACSubMesh(ref Mesh mesh, ref SubMeshDescriptor subMesh, Transform transform, float[] abs)
        {
            mesh.name = "RACSubMesh";
            int numWalls = subMesh.indexCount / 3;

            absorption = abs;
            for (int i = 0; i < numWalls; i++)
                Init(subMesh.indexStart + i * 3, ref mesh, ref transform);
        }

        // Destructor

        ~RACSubMesh() { Remove(); }

        // Functions

        public int GetId() { return walls[0].GetId(); }

        public void UpdateAbsorption(ref float[] abs)
        {
            absorption = abs;
            foreach (RACWall wall in walls)
                wall.UpdateAbsorption(ref abs);
        }

        private void Init(int i, ref Mesh mesh, ref Transform transform)
        {
            vertices[0] = mesh.vertices[mesh.triangles[i]];
            vertices[1] = mesh.vertices[mesh.triangles[i + 1]];
            vertices[2] = mesh.vertices[mesh.triangles[i + 2]];
            transform.TransformPoints(vertices);

            walls.Add(new RACWall(ref vertices, ref absorption));
        }

        public void Update(ref Mesh mesh, ref SubMeshDescriptor subMesh, Transform transform)
        {
            Profiler.BeginSample("Update SubMesh");
            int numWalls = subMesh.indexCount / 3;

            if (numWalls > walls.Count)
            {
                for (int i = walls.Count; i < numWalls; i++)
                    Init(subMesh.indexStart + i * 3, ref mesh, ref transform);
            }
            else if (numWalls < walls.Count)
                walls.RemoveRange(numWalls, walls.Count - numWalls);

            for (int i = 0; i < numWalls; i++)
            {
                int idx = subMesh.indexStart + i * 3;
                vertices[0] = mesh.vertices[mesh.triangles[idx]];
                vertices[1] = mesh.vertices[mesh.triangles[idx + 1]];
                vertices[2] = mesh.vertices[mesh.triangles[idx + 2]];
                transform.TransformPoints(vertices);

                walls[i].UpdateWall(ref vertices);
            }
        }

        public void Remove()
        {
            foreach (RACWall wall in walls)
                wall.Remove();
            walls.Clear();
        }

        // Parameters

        [SerializeField]
        private List<RACWall> walls = new List<RACWall>();
        private Vector3[] vertices = new Vector3[3];

        [SerializeField]
        private float[] absorption;
    }

    #endregion

    #region RACWall

    //////////////////// RACWall ////////////////////

    [Serializable]
    private class RACWall
    {
        // Constructors

        public RACWall() { id = -1; }

        public RACWall(ref Vector3[] vertices, ref float[] absorption)
        {
            id = RACManager.InitWall(ref vertices, ref absorption);
        }

        // Destructor

        ~RACWall() { Remove(); }

        // Functions

        public int GetId() { return id; }

        public void UpdateAbsorption(ref float[] absorption)
        {
            if (id > -1)
            {
                RACManager.UpdateWallAbsorption(id, ref absorption);
            }
        }

        public void UpdateWall(ref Vector3[] vertices)
        {
            if (id > -1)
            {
                RACManager.UpdateWall(id, ref vertices);
            }
        }

        public void Remove()
        {
            if (id > -1)
            {
                RACManager.RemoveWall(id);
                id = -1;
            }
        }

        // Parameters

        [SerializeField]
        private int id = -1;
    }

    #endregion
}