
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
        mGameObject.ResetCustomAbsorption();
        materialEntries = mGameObject.GetAbsorptionMap();

    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        RACMaterialEntry mGameObject = target as RACMaterialEntry;
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
            return;

        // Display the table
        for (int i = 0; i < materialEntries.Count; i++)
        {
            RACMaterialEntry.Entry entry = materialEntries[i];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(materialEntries[i].frequency.ToString("0.##"), EditorStyles.textField, GUILayout.Width(fieldWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.Space(fieldPadding);
            float a = EditorGUILayout.FloatField(materialEntries[i].absorption, GUILayout.Width(fieldWidth));
            EditorGUILayout.Space(fieldPadding);
            EditorGUILayout.EndHorizontal();

            if (a != materialEntries[i].absorption)
            {
                mGameObject.Custom();
                entry.absorption = Mathf.Clamp(a, 0.0f, 1.0f);
            }

            materialEntries[i] = entry;
        }

        if (Event.current.type == EventType.Repaint)
        {
            mGameObject.UpdateCustomAbsorption(materialEntries);
            mGameObject.SetAbsorption();
        }
        EditorUtility.SetDirty(target);
    }
}