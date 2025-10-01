#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(IRController))]
public class IRControllerEditor : Editor
{
    private IRController irController;

    void OnEnable()
    {
        irController = target as IRController;
    }

    public override void OnInspectorGUI()
    {
        // Read-only text field reflecting the output folder
        irController.UpdateSceneName();
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("Export subfolder:", irController.GetSceneName());
        EditorGUI.EndDisabledGroup();

        DrawDefaultInspector();

        if (GUILayout.Button("Run Impulse Responses"))
            irController.StartIRRun();

        if (GUILayout.Button("End Impulse Responses"))
            irController.EndRun();
    }
}
#endif
