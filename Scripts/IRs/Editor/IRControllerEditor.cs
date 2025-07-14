using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(IRController))]

public class IRControllerEditor : Editor
{
#if RAC_Debug && UNITY_EDITOR
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
