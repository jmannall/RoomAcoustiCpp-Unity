/*
 * Code adapted from Templar2020/Wander.cs
 * https://gist.github.com/Templar2020/8e4f5296de96d8ccf03263bf1a9f277f
 * as well as Unity docs for NavMesh.SamplePosition
 * https://docs.unity3d.com/6000.2/Documentation/ScriptReference/AI.NavMesh.SamplePosition.html
 */
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Wander : MonoBehaviour
{
    [SerializeField]
    public float wanderRadius = 25;
    [SerializeField]
    public float loiterTimerMax = 30;

    [SerializeField, Range(1, 100)]
    public int numAttempts = 30;
    [SerializeField, Range(0, 2)]
    public float minEdgeClearance = 1;

    private NavMeshAgent agent;
    private float timer;
    private float loiterTimer;
    private bool destinationReached;

    void OnEnable()
    {
        agent = GetComponent<NavMeshAgent>();

        Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, agent.areaMask);
        agent.SetDestination(newPos);

        destinationReached = false;
        loiterTimer = 0;
        timer = 0;
    }

    void Update()
    {
        // Do nothing if the agent is currently disabled.
        if (!agent.enabled || agent.isStopped)
            return;
        // Do nothing if wandering is disabled altogether.
        if (UIManager.uiManager != null && !UIManager.uiManager.IsWanderingActive())
            return;

        // Sometimes the agents get disconnected from the NavMesh...
        if (!agent.isOnNavMesh)
        {
            // Something's broken; fix it.
            NavMeshHit hit;
            if (NavMesh.SamplePosition(this.transform.position, out hit, 10, agent.areaMask))
            {
                if (!agent.Warp(hit.position))
                    Debug.LogError("Failed to warp agent to a valid position.");
            }
            else
                Debug.LogError("Failed to find a valid position for the agent.");
        }

        // After some drag-and-drop movements, the agent may be confused.
        if (!agent.pathPending && !agent.hasPath)
        {
            destinationReached = true;
            loiterTimer = 0;
        }

        if (destinationReached)
        {
            // The agent is at a destination.

            if (loiterTimer == 0)
            {
                // If loiterTimer == 0, the destination has just been reached:
                // choose an amount of time to loiter and start waiting.
                loiterTimer = Random.value * loiterTimerMax;
                timer = 0;
            }

            timer += Time.deltaTime;

            if (timer >= loiterTimer)
            {
                // After loitering long enough, pick a new destination.
                Vector3 newPos = RandomNavSphere(transform.position, wanderRadius + 1, agent.areaMask);
                agent.SetDestination(newPos);

                destinationReached = false;
            }
        }
        else
        {
            if (agent.remainingDistance <= 0.1)
            {
                // If the agent is free to move and is near its destination, prepare to start loitering.
                destinationReached = true;
                loiterTimer = 0;
            }
        }
    }

    public Vector3 RandomNavSphere(Vector3 origin, float dist, int areaMask)
    {
        Vector3 returnedPosition = origin;
        for (int i = 0; i < numAttempts; i++)
        {
            Vector3 randomPoint = origin + Random.insideUnitSphere * dist;
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, dist, areaMask))
            {
                // This position is valid, so save it, but...
                returnedPosition = hit.position;
                // ...try to find one farther from an edge if possible.
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, areaMask))
                {
                    // If it's already far from any edge, stop searching.
                    if (edgeHit.distance >= minEdgeClearance)
                        return returnedPosition;
                    // Otherwise, try a different point (at the next loop).
                }
            }
        }
        Debug.LogWarning("Failed to locate a valid destination.");
        return returnedPosition;
    }
}