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

    private NavMeshAgent agent;
    private float timer;
    private float loiterTimer;
    private bool destinationReached;

    void OnEnable()
    {
        agent = GetComponent<NavMeshAgent>();

        Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, NavMesh.AllAreas, numAttempts);
        agent.SetDestination(newPos);

        destinationReached = false;
        loiterTimer = 0;
        timer = 0;
    }

    void Update()
    {
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
                Vector3 newPos = RandomNavSphere(transform.position, wanderRadius + 1, NavMesh.AllAreas, numAttempts);
                agent.SetDestination(newPos);

                destinationReached = false;
            }
        }
        else
        {
            if (!agent.isStopped && agent.remainingDistance <= 0.1)
            {
                // If the agent is free to move and is near its destination, prepare to start loitering.
                destinationReached = true;
                loiterTimer = 0;
            }
        }
    }

    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask, int attempts)
    {
        for (int i = 0; i < attempts; i++)
        {
            Vector3 randomPoint = origin + Random.insideUnitSphere * dist;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, dist, layermask))
                return hit.position;
        }
        Debug.LogWarning("Failed to locate a valid destination.");
        return origin;
    }
}