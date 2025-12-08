using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(DebugCPP))]

public class DebugCPPEditor : Editor
{
#if RAC_Debug && UNITY_EDITOR
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
#else    
    public override VisualElement CreateInspectorGUI()
    {
        // Create a new VisualElement to be the root of our Inspector UI.
        VisualElement myInspector = new VisualElement();
        InspectorElement.FillDefaultInspector(myInspector, serializedObject, this);

        // https://discussions.unity.com/t/what-happened-to-wordwrap/814214/5
        myInspector.Add(new Label("Enable RAC_Debug on the RACManager script to enable gizmo debugging.") { style = { whiteSpace = WhiteSpace.Normal } });

        return myInspector;
    }
#endif
}
