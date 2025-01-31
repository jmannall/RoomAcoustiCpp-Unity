using UnityEditor;
using UnityEngine;

[AddComponentMenu("RoomAcoustiC++/Editor/AudioSource")]
[CustomEditor(typeof(RACAudioSource))]

public class RACAudioSourceEditor : Editor
{
    private SerializedProperty directivity;

    private void OnEnable()
    {
        directivity = serializedObject.FindProperty("directivity");
    }
    public override void OnInspectorGUI()
    {
        bool isPlaying = EditorApplication.isPlaying;

        RACAudioSource source = (RACAudioSource)target;

        DrawDefaultInspector();

        serializedObject.Update();

        EditorGUILayout.PropertyField(directivity, new GUIContent("Directivity", "Set the directivity of the source."));
        serializedObject.ApplyModifiedProperties();

        if (isPlaying && GUI.changed)
        {
            source.UpdateDirectivity();
            GUI.changed = false;
        }
    }
}
