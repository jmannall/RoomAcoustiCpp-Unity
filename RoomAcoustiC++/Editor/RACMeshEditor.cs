using Codice.Client.BaseCommands;
using System;
using System.Security.Policy;
using UnityEditor;
using UnityEngine;

[AddComponentMenu("RoomAcoustiC++/Editor/Mesh")]
[CustomEditor(typeof(RACMesh))]

public class RACMeshEditor : Editor
{
    private SerializedProperty absorptionSkew;

    private void OnEnable()
    {
        absorptionSkew = serializedObject.FindProperty("absorptionSkew");
    }

    public override void OnInspectorGUI()
    {
        RACMesh racMesh = (RACMesh)target;

        // Update the serialized object
        serializedObject.Update();

        bool isPlaying = EditorApplication.isPlaying;

        DrawDefaultInspector();
        GUI.changed = false;

        EditorGUILayout.Slider(absorptionSkew, -1.0f, 1.0f, new GUIContent("Absorption Skew", "Scale the material absorption."));
        serializedObject.ApplyModifiedProperties();

        if (isPlaying && GUI.changed)
            racMesh.UpdateAbsorption();
    }


    private InspectorNameAttribute GetCustomAttribute(SerializedProperty property)
    {
        var fieldInfo = serializedObject.targetObject.GetType().GetField(property.name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (fieldInfo != null)
        {
            var attributes = fieldInfo.GetCustomAttributes(typeof(InspectorNameAttribute), false);
            if (attributes.Length > 0)
            {
                return (InspectorNameAttribute)attributes[0];
            }
        }
        return null;
    }
}