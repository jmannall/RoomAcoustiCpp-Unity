#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(PathWalker))]
public class PathWalkerEditor : Editor
{
    private PathWalker pathWalker;
    private SerializedProperty drawETAs;
    [SerializeField, HideInInspector]
    private int numChildren;

    void OnEnable()
    {
        pathWalker = target as PathWalker;
        drawETAs = serializedObject.FindProperty("drawETAs");
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.PropertyField(drawETAs, new GUIContent("Draw ETA labels"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Remove children"))
            pathWalker.RemoveChildren();
        if (GUILayout.Button("Refresh children"))
            pathWalker.GatherChildren();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add children"))
        {
            pathWalker.AddChildren(numChildren);
            numChildren = 0;
        }
        numChildren = EditorGUILayout.IntField(numChildren);

        EditorGUILayout.EndHorizontal();

        DrawDefaultInspector();
    }
}
#endif
