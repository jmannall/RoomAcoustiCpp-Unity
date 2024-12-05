
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

            normals.Add(Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[0]).normalized);
            walls.Add(new RACWall(normals.Last(), ref vertices, ref absorption));
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

                normals[i] = Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[0]).normalized;
                walls[i].UpdateWall(normals[i], ref vertices);
            }
        }

        public void Remove()
        {
            foreach (RACWall wall in walls)
                wall.Remove();
            walls.Clear();
            normals.Clear();
        }

        // Parameters

        [SerializeField]
        private List<RACWall> walls = new List<RACWall>();
        private List<Vector3> normals = new List<Vector3>();
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

        public RACWall(Vector3 normal, ref Vector3[] vertices, ref float[] absorption)
        {
            id = RACManager.InitWall(normal, ref vertices, ref absorption);
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

        public void UpdateWall(Vector3 normal, ref Vector3[] vertices)
        {
            if (id > -1)
            {
                RACManager.UpdateWall(id, normal, ref vertices);
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

    #region Commented Code
    //////////////////// Objects ////////////////////

    //private class Object
    //{
    //    public Object() { }

    //    public void Remove()
    //    {
    //        for (int i = 0; i < walls.Length; i++)
    //        {
    //            walls[i].Remove();
    //        }
    //    }

    //    public void RemoveWall(int i)
    //    {
    //        walls[i].Remove();
    //    }

    //    public virtual int GetId() { return -1; }

    //    public virtual void UpdateObject(ref Mesh mesh, Transform transform) { return; }

    //    public virtual void UpdateObject(ref Mesh mesh, ref SubMeshDescriptor subMesh, Transform transform) { return; }


    //    ~Object()
    //    {
    //        Remove();
    //    }

    //    protected RACWall[] walls;
    //    protected Vector3[] normals;
    //    protected Vector3[] vertices;
    //}

    //private class Quad : Object
    //{
    //    public Quad(ref Mesh mesh, Transform transform, ref float[] absorption)
    //    {
    //        normals = new Vector3[1];
    //        normals[0] = transform.TransformDirection(mesh.normals[0]);

    //        vertices = new Vector3[4];
    //        vertices[0] = mesh.vertices[0];
    //        vertices[1] = mesh.vertices[2];
    //        vertices[2] = mesh.vertices[3];
    //        vertices[3] = mesh.vertices[1];
    //        transform.TransformPoints(vertices);

    //        walls = new RACWall[1];
    //        walls[0] = new RACWall(normals[0], ref vertices, ref absorption);
    //    }

    //    public override int GetId() { return walls[0].GetId(); }

    //    public override void UpdateObject(ref Mesh mesh, Transform transform)
    //    {
    //        Profiler.BeginSample("Update Quad");
    //        normals[0] = transform.TransformDirection(mesh.normals[0]);

    //        vertices[0] = mesh.vertices[0];
    //        vertices[1] = mesh.vertices[2];
    //        vertices[2] = mesh.vertices[3];
    //        vertices[3] = mesh.vertices[1];
    //        transform.TransformPoints(vertices);

    //        walls[0].UpdateWall(normals[0], ref vertices);
    //    }
    //}

    //private class Plane : Object
    //{
    //    public Plane(ref Mesh mesh, Transform transform, ref float[] absorption)
    //    {
    //        normals = new Vector3[1];
    //        normals[0] = transform.TransformDirection(mesh.normals[0]);

    //        vertices = new Vector3[4];
    //        vertices[0] = mesh.vertices[0];
    //        vertices[1] = mesh.vertices[110];
    //        vertices[2] = mesh.vertices[120];
    //        vertices[3] = mesh.vertices[10];
    //        transform.TransformPoints(vertices);

    //        walls = new RACWall[1];
    //        walls[0] = new RACWall(normals[0], ref vertices, ref absorption);
    //    }

    //    public override int GetId() { return walls[0].GetId(); }

    //    public override void UpdateObject(ref Mesh mesh, Transform transform)
    //    {
    //        normals[0] = transform.TransformDirection(mesh.normals[0]);

    //        vertices[0] = mesh.vertices[0];
    //        vertices[1] = mesh.vertices[110];
    //        vertices[2] = mesh.vertices[120];
    //        vertices[3] = mesh.vertices[10];
    //        transform.TransformPoints(vertices);

    //        walls[0].UpdateWall(normals[0], ref vertices);
    //    }
    //}

    //private class Cube : Object
    //{
    //    public Cube(ref Mesh mesh, Transform transform, ref float[] absorption)
    //    {
    //        normals = new Vector3[6];
    //        normals[0] = mesh.normals[0];
    //        normals[1] = mesh.normals[4];
    //        normals[2] = mesh.normals[6];
    //        normals[3] = mesh.normals[12];
    //        normals[4] = mesh.normals[16];
    //        normals[5] = mesh.normals[20];
    //        transform.TransformDirections(normals);

    //        vertices = new Vector3[4];
    //        vertices[0] = mesh.vertices[0];
    //        vertices[1] = mesh.vertices[2];
    //        vertices[2] = mesh.vertices[3];
    //        vertices[3] = mesh.vertices[1];
    //        transform.TransformPoints(vertices);
    //        walls[0] = new RACWall(normals[0], ref vertices, ref absorption);

    //        vertices[0] = mesh.vertices[8];
    //        vertices[1] = mesh.vertices[4];
    //        vertices[2] = mesh.vertices[5];
    //        vertices[3] = mesh.vertices[9];
    //        transform.TransformPoints(vertices);
    //        walls[1] = new RACWall(normals[1], ref vertices, ref absorption);

    //        vertices[0] = mesh.vertices[10];
    //        vertices[1] = mesh.vertices[6];
    //        vertices[2] = mesh.vertices[7];
    //        vertices[3] = mesh.vertices[11];
    //        transform.TransformPoints(vertices);
    //        walls[2] = new RACWall(normals[2], ref vertices, ref absorption);

    //        mesh.vertices[12..16].CopyTo(vertices, 0); // vertices[0..4] = mesh.vertices[12..16]
    //        transform.TransformPoints(vertices);
    //        walls[3] = new RACWall(normals[3], ref vertices, ref absorption);

    //        mesh.vertices[16..20].CopyTo(vertices, 0); // vertices[0..4] = mesh.vertices[16..20]
    //        transform.TransformPoints(vertices);
    //        walls[4] = new RACWall(normals[4], ref vertices, ref absorption);

    //        mesh.vertices[20..24].CopyTo(vertices, 0); // vertices[0..4] = mesh.vertices[20..24]
    //        transform.TransformPoints(vertices);
    //        walls[5] = new RACWall(normals[5], ref vertices, ref absorption);
    //    }

    //    public override void UpdateObject(ref Mesh mesh, Transform transform)
    //    {
    //        normals[0] = mesh.normals[0];
    //        normals[1] = mesh.normals[4];
    //        normals[2] = mesh.normals[6];
    //        normals[3] = mesh.normals[12];
    //        normals[4] = mesh.normals[16];
    //        normals[5] = mesh.normals[20];
    //        transform.TransformDirections(normals);

    //        vertices[0] = mesh.vertices[0];
    //        vertices[1] = mesh.vertices[2];
    //        vertices[2] = mesh.vertices[3];
    //        vertices[3] = mesh.vertices[1];
    //        transform.TransformPoints(vertices);
    //        walls[0].UpdateWall(normals[0], ref vertices);

    //        vertices[0] = mesh.vertices[8];
    //        vertices[1] = mesh.vertices[4];
    //        vertices[2] = mesh.vertices[5];
    //        vertices[3] = mesh.vertices[9];
    //        transform.TransformPoints(vertices);
    //        walls[1].UpdateWall(normals[0], ref vertices);

    //        vertices[0] = mesh.vertices[10];
    //        vertices[1] = mesh.vertices[6];
    //        vertices[2] = mesh.vertices[7];
    //        vertices[3] = mesh.vertices[11];
    //        transform.TransformPoints(vertices);
    //        walls[2].UpdateWall(normals[0], ref vertices);

    //        mesh.vertices[12..16].CopyTo(vertices, 0); // vertices[0..4] = mesh.vertices[12..16]
    //        transform.TransformPoints(vertices);
    //        walls[3].UpdateWall(normals[0], ref vertices);

    //        mesh.vertices[16..20].CopyTo(vertices, 0); // vertices[0..4] = mesh.vertices[16..20]
    //        transform.TransformPoints(vertices);
    //        walls[4].UpdateWall(normals[0], ref vertices);

    //        mesh.vertices[20..24].CopyTo(vertices, 0); // vertices[0..4] = mesh.vertices[20..24]
    //        transform.TransformPoints(vertices);
    //        walls[5].UpdateWall(normals[0], ref vertices);
    //    }
    //}
    #endregion
}