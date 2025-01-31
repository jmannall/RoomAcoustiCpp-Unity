using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(IRController))]

public class IRControllerEditor : Editor
{
    private IRController irController;

    private void OnEnable()
    {
        irController = target as IRController;
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Run Impulse Responses"))
            irController.StartIRRun();

        if (GUILayout.Button("End Impulse Responses"))
            irController.EndRun();
    }
}
