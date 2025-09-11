using UnityEngine;
using UnityEngine.AI;

public class PathWalker : MonoBehaviour
{
    public GameObject targetObject;
    private NavMeshAgent targetAgent;

    public bool switchDirection = false;
    public bool closedLoop = false;

    private Vector3[] waypoints;
    private int idx = -1;

    [Min(0.0f)]
    public float distanceThreshold = 0.1f;

    private void Awake()
    {
        waypoints = new Vector3[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            waypoints[i] = transform.GetChild(i).position;
    }

    void Start()
    {
        targetAgent = targetObject.GetComponent<NavMeshAgent>();
        targetAgent.SetDestination(NextWaypoint());
    }

    void Update()
    {
        if (targetAgent.remainingDistance <= distanceThreshold)
            targetAgent.destination = NextWaypoint();
    }

    public Vector3 GetCurrentWaypoint() { return waypoints[idx]; }

    private Vector3 NextWaypoint()
    {
        if (switchDirection)
        {
            idx--;
            if (idx < 0)
            {
                if (closedLoop)
                    idx = waypoints.Length - 1;
                else
                {
                    idx = 0;
                    switchDirection = true;

                }
            }
            return waypoints[idx];
        }
        else
        {
            idx++;
            if (idx >= waypoints.Length)
            {
                if (closedLoop)
                    idx = 0;
                else
                {
                    idx = waypoints.Length - 1;
                    switchDirection = false;

                }
            }
            return waypoints[idx];
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 startPosition = transform.GetChild(0).position;
        Vector3 previousPosition = startPosition;
        foreach (Transform t in transform)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(t.position, 0.2f);
            Gizmos.DrawLine(previousPosition, t.position);
            previousPosition = t.position;
        }
        if (closedLoop)
            Gizmos.DrawLine(previousPosition, startPosition);
    }
}