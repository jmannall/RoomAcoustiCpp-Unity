
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("RoomAcoustiC++/Editor/MaterialEntry")]
[CustomEditor(typeof(RACMaterialEntry)), CanEditMultipleObjects]

public class RACMaterialEntryEditor : Editor
{
    float buttonWidth = 20; // Width for the buttons
    float fieldPadding = 15; // Padding for spacing between fields

    List<RACMaterialEntry.Entry> materialEntries;

    public void OnEnable()
    {
        RACMaterialEntry mGameObject = target as RACMaterialEntry;
        materialEntries = mGameObject.GetAbsorptionMap();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        RACMaterialEntry mGameObject = target as RACMaterialEntry;
        if (mGameObject.SetAbsorption())
            mGameObject.SetAbsorption(materialEntries);
        materialEntries = mGameObject.GetAbsorptionMap();

        // Get the current width of the Inspector window
        float viewWidth = EditorGUIUtility.currentViewWidth;

        // Set a fixed width for the labels or buttons
        float labelWidth = EditorGUIUtility.labelWidth; // Width for the label headers
        float fieldWidth = (viewWidth - 4 * fieldPadding - buttonWidth) / 2; // Adjust dynamically


        // Display header for the table
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent("Frequency", "Define centre frequencies"), GUILayout.Width(fieldWidth + fieldPadding + 2));
        // EditorGUILayout.Space();
        EditorGUILayout.LabelField(new GUIContent("Absorption", "Set material absorption"), GUILayout.Width(fieldWidth));
        EditorGUILayout.EndHorizontal();

        if (materialEntries == null)
            materialEntries = new List<RACMaterialEntry.Entry>();

        // Display the table
        for (int i = 0; i < materialEntries.Count; i++)
        {
            RACMaterialEntry.Entry entry = materialEntries[i];
            EditorGUILayout.BeginHorizontal();
            float f = EditorGUILayout.FloatField(materialEntries[i].frequency, GUILayout.Width(fieldWidth));
            EditorGUILayout.Space(fieldPadding);
            float a = EditorGUILayout.FloatField(materialEntries[i].absorption, GUILayout.Width(fieldWidth));
            EditorGUILayout.Space(fieldPadding);

            if (f != materialEntries[i].frequency)
            {
                mGameObject.Custom();
                entry.frequency = Mathf.Clamp(f, 20.0f, 20000.0f);
            }

            if (a != materialEntries[i].absorption)
            {
                mGameObject.Custom();
                entry.absorption = Mathf.Clamp(a, 0.0f, 1.0f);
            }

            materialEntries[i] = entry;

            // Add a button to remove this entry
            if (GUILayout.Button("-", GUILayout.Width(buttonWidth)))
            {
                mGameObject.Custom();
                materialEntries.RemoveAt(i);
            }
            EditorGUILayout.EndHorizontal();
        }

        // Add a button to add a new element
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Add", GUILayout.Width(3 * buttonWidth)))
        {
            mGameObject.Custom();
            materialEntries.Add(new RACMaterialEntry.Entry());
        }
        GUILayout.EndHorizontal();

        EditorUtility.SetDirty(target);
    }
}