using UnityEngine;

public class LogarithmicRangeAttribute : PropertyAttribute
{
    public readonly int min;
    public readonly int max;

    public readonly bool displayInt;

    public LogarithmicRangeAttribute(int min, int max, bool displayInt)
    {
        this.min = min;
        this.max = max;
        this.displayInt = displayInt;
    }
}
