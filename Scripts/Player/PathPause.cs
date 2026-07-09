using UnityEngine;

public class PathPause : MonoBehaviour
{
    [Range(0, 10)]
    public double waitTime = 1.0;
    private double startTime;

    public bool clockwise = true;

    public void StartWait()
    {
        startTime = Time.time;
    }

    public bool IsWaiting()
    {
        return Time.time < startTime + waitTime;
    }
}
