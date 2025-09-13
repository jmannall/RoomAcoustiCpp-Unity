#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(IRController))]
public class IRControllerEditor : Editor
{
#if RAC_Debug
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
#else    
    public override VisualElement CreateInspectorGUI()
    {
        // Create a new VisualElement to be the root of our Inspector UI.
        VisualElement myInspector = new VisualElement();
        InspectorElement.FillDefaultInspector(myInspector, serializedObject, this);

        myInspector.Add(new Label("Enable RAC_Debug on the RACManager script."));

        return myInspector;
    }
#endif
}
#endif
