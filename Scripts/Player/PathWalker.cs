using System.Collections.Generic;
using System.Net;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public class PathWalker : MonoBehaviour
{
    public GameObject targetObject;
    [SerializeField, HideInInspector]
    private NavMeshAgent targetAgent;

    public bool switchDirection = false;
    public bool closedLoop = false;

    [SerializeField, HideInInspector]
    private bool drawETAs = true;

    [SerializeField, HideInInspector]
    private List<Transform> waypoints;
    private int idx = -1;

    [Min(0.0f)]
    public float distanceThreshold = 0.1f;

    void OnValidate()
    {
        targetAgent = targetObject.GetComponent<NavMeshAgent>();
        GatherChildren();
    }

    void Awake() { GatherChildren(); }

    void Start()
    {
        if (waypoints.Count != 0 && !targetAgent.isStopped)
            targetAgent.SetDestination(NextWaypoint());
    }

    void Update()
    {
        if (waypoints.Count != 0 && !targetAgent.isStopped && targetAgent.remainingDistance <= distanceThreshold)
            targetAgent.destination = NextWaypoint();
    }

    public List<int> GetWaypointETAs()
    {
        List<int> waypointETAs = new List<int>();
        for (int i = 0; i < waypoints.Count; ++i)
            waypointETAs.Add(Mathf.RoundToInt(i / targetAgent.speed));
        return waypointETAs;
    }

    public List<Vector3> GetAllWaypoints()
    {
        List<Vector3> waypointVecs = new List<Vector3>();
        for (int i = 0; i < waypoints.Count; ++i)
            waypointVecs.Add(waypoints[i].position);
        return waypointVecs;
    }

    public Vector3 GetCurrentWaypoint()
    {
        if (waypoints.Count == 0)
            return Vector3.zero;
        else
            return waypoints[idx].position;
    }

    private Vector3 NextWaypoint()
    {
        if (waypoints.Count == 0)
            return Vector3.zero;

        if (switchDirection)
        {
            idx--;
            if (idx < 0)
            {
                if (closedLoop)
                    idx = waypoints.Count - 1;
                else
                {
                    idx = 1;
                    switchDirection = false;
                }
            }
            return waypoints[idx].position;
        }
        else
        {
            idx++;
            if (idx >= waypoints.Count)
            {
                if (closedLoop)
                    idx = 0;
                else
                {
                    idx = waypoints.Count - 2;
                    switchDirection = true;

                }
            }
            return waypoints[idx].position;
        }
    }

    void OnDrawGizmos()
    {
        if (waypoints.Count == 0)
            return;

        float radius = 0.2f;

        // First, draw the waypoints and connections.
        Vector3 startPosition = waypoints[0].position;
        Vector3 previousPosition = startPosition;
        for (int i = 0; i < waypoints.Count; ++i)
        {
            float grayscale = (float)i / (float)waypoints.Count;
            Gizmos.color = new Color(grayscale, grayscale, grayscale);

            if (!drawETAs) // There is a bug in CompareFunction.Always; it's a headache to fix. Just avoid drawing both the spheres and labels.
                Gizmos.DrawSphere(waypoints[i].position, radius);
            Gizmos.DrawLine(previousPosition, waypoints[i].position);

            previousPosition = waypoints[i].position;
        }
        if (closedLoop)
            Gizmos.DrawLine(previousPosition, startPosition);

        // Then, draw the ETA labels, if needed.
        if (drawETAs)
        {
#if UNITY_EDITOR
            // Ensure the labels are drawn correctly in terms of occlusion
            CompareFunction defaultZorder = Handles.zTest;
            Handles.zTest = CompareFunction.Always;

            GUIStyle baseStyle = new GUIStyle(GUI.skin.box);
            baseStyle.fontSize = 15;
            baseStyle.alignment = TextAnchor.MiddleCenter;
            GUIStyle blackStyle = new GUIStyle(baseStyle);
            GUIStyle whiteStyle = new GUIStyle(baseStyle);
            blackStyle.normal.textColor = Color.black;
            whiteStyle.normal.textColor = Color.white;
            whiteStyle.fontSize = (int)(baseStyle.fontSize * 0.9);

            for (int i = 0; i < waypoints.Count; ++i)
            {
                // Display the ETA to the waypoint, given the agent's speed (assuming no obstructions)
                string labelText = Mathf.RoundToInt((float)i / targetAgent.speed).ToString();

                if (closedLoop && i == 0)
                {
                    // Include the ETA of the full loop
                    labelText += ", " + Mathf.RoundToInt((float)waypoints.Count / targetAgent.speed).ToString();
                }
                else if (!closedLoop && i != waypoints.Count - 1)
                {
                    // Include the ETA on the way back
                    int j = 2 * (waypoints.Count - 1) - i;
                    labelText += ", " + Mathf.RoundToInt((float)j / targetAgent.speed).ToString();
                }

                Handles.Label(waypoints[i].position, labelText, blackStyle);
                Handles.Label(waypoints[i].position, labelText, whiteStyle);
            }
            // Restore correct Z order
            Handles.zTest = defaultZorder;
#endif
        }
    }

    public void GatherChildren()
    {
        waypoints = new List<Transform>();

        if (this.transform.childCount == 1)
        {
            Transform child = transform.GetChild(0);
            waypoints.Add(child);

            while (child.childCount > 0)
            {
                child = child.GetChild(0);
                waypoints.Add(child);
            }
        }
        else
        {
            for (int i = 0; i < transform.childCount; i++)
                waypoints.Add(transform.GetChild(i));
        }
    }

    public void RemoveChildren()
    {
        for (int i = this.transform.childCount - 1; i >= 0; i--)
#if UNITY_EDITOR
            DestroyImmediate(this.transform.GetChild(i).gameObject);
#else
            Destroy(this.transform.GetChild(i).gameObject);
# endif

        waypoints = new List<Transform>();
    }

    public void RebaseChildren()
    {
        Debug.Log("TODO: iteratively set all childrens' children as children of self.");
    }

    public void AddChildren(int numChildren)
    {
        GameObject parent = this.gameObject;
        Vector3 offset = Vector3.zero;
        for (int i = 0; i < numChildren; ++i)
        {
            GameObject child = new GameObject("P" + i.ToString());
            child.transform.SetParent(parent.transform);
            child.transform.SetLocalPositionAndRotation(offset, new Quaternion());
            parent = child;
            offset = new Vector3(1f, 0f, 0f);
        }
    }
}