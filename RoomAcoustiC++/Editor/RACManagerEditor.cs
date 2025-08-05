
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[AddComponentMenu("RoomAcoustiC++/Editor/AudioManager")]
[CustomEditor(typeof(RACManager))]

public class RACManagerEditor : Editor
{
    private string[] pluginOptions = new string[] { "RAC_Default", "RAC_Debug", "RAC_Profile", "RAC_ProfileDetailed" };
    private int selectedIndex = 0;

    private SerializedProperty lerpFactor, frequencyBands, hrtfResamplingStep, numReverbSources, fdnMatrix, selectedHRTF, customHRTFFile, selectedHeadphoneEQ, customHeadphoneEQFile, iemConfig, spatialisationMode, diffractionModel, reverbTimeModel, T60;

    private void OnEnable()
    {
        // Link the SerializedProperty to the serialized field in the target class
        lerpFactor = serializedObject.FindProperty("lerpFactor");
        frequencyBands = serializedObject.FindProperty("frequencyBands");
        hrtfResamplingStep = serializedObject.FindProperty("hrtfResamplingStep");
        numReverbSources = serializedObject.FindProperty("numReverbSources");
        fdnMatrix = serializedObject.FindProperty("fdnMatrix");
        selectedHRTF = serializedObject.FindProperty("selectedHRTF");
        customHRTFFile = serializedObject.FindProperty("customHRTFFile");
        selectedHeadphoneEQ = serializedObject.FindProperty("selectedHeadphoneEQ");
        customHeadphoneEQFile = serializedObject.FindProperty("customHeadphoneEQFile");
        iemConfig = serializedObject.FindProperty("iemConfig");
        spatialisationMode = serializedObject.FindProperty("spatialisationMode");
        diffractionModel = serializedObject.FindProperty("diffractionModel");
        reverbTimeModel = serializedObject.FindProperty("reverbTimeModel");
        T60 = serializedObject.FindProperty("T60");
    }

    public override void OnInspectorGUI()
    {
        // Update the serialized object
        serializedObject.Update();

        bool isPlaying = EditorApplication.isPlaying;
        if (isPlaying)
            GUI.enabled = false;

        selectedIndex = EditorGUILayout.Popup("Select Plugin", selectedIndex, pluginOptions);

        if (GUILayout.Button("Apply Plugin"))
        {
            string selectedDefine = pluginOptions[selectedIndex];

            // Use NamedBuildTarget for modern Unity
            var buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

            // Get current defines
            string defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);

            // Filter out old plugin defines
            var newDefinesList = defines.Split(';')
                .Where(d => !d.StartsWith("RAC_"))
                .ToList();

            // Add new selected define
            newDefinesList.Add(selectedDefine);
            string newDefines = string.Join(";", newDefinesList);

            // Apply new define symbols
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, newDefines);
        }

        EditorGUILayout.PropertyField(lerpFactor, new GUIContent("Lerp Factor", "Control the speed at which DSP parameters are interpolated."));

        // EditorGUILayout.PropertyField(frequencyBands, new GUIContent("Frequency Bands", "Set the centre frequencies for all frequency dependent processing."), true);

        // EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent("Frequency Bands: ", "Set the centre frequencies for all frequency dependent processing."));

        // Build a text list of frequencies
        string frequencyList = " | ";
        for (int i = 0; i < frequencyBands.arraySize; i++)
        {
            SerializedProperty freq = frequencyBands.GetArrayElementAtIndex(i);
            float oldValue = freq.floatValue;

            // Optional: snap and clamp
            float snapped = SnapToOctave(oldValue, 250.0f);
            if (!Mathf.Approximately(snapped, oldValue))
            {
                if (snapped <= 20000.0f && snapped >= 20.0f)
                    freq.floatValue = snapped;
            }

            frequencyList += oldValue.ToString("0.##") + " | ";
        }

        // Display as read-only text (styled like a text field, but not editable)
        EditorGUILayout.LabelField(frequencyList, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Add to Start", "Insert a lower octave at the beginning")))
        {
            float firstFreq = frequencyBands.arraySize > 0 ? frequencyBands.GetArrayElementAtIndex(0).floatValue : 250f;
            float newFreq = firstFreq * 0.5f;
            if (newFreq >= 20.0f)
            {
                frequencyBands.InsertArrayElementAtIndex(0);
                frequencyBands.GetArrayElementAtIndex(0).floatValue = newFreq;
            }
        }

        if (GUILayout.Button(new GUIContent("Add to End", "Insert a higher octave at the end")))
        {
            float lastFreq = frequencyBands.arraySize > 0 ? frequencyBands.GetArrayElementAtIndex(frequencyBands.arraySize - 1).floatValue : 250f;
            float newFreq = lastFreq * 2.0f;
            if (newFreq <= 20e3)
            {
                frequencyBands.InsertArrayElementAtIndex(frequencyBands.arraySize);
                frequencyBands.GetArrayElementAtIndex(frequencyBands.arraySize - 1).floatValue = newFreq;
            }
        }

        if (GUILayout.Button("Remove First Band") && frequencyBands.arraySize > 0)
            frequencyBands.DeleteArrayElementAtIndex(0);

        if (GUILayout.Button("Remove Last Band") && frequencyBands.arraySize > 0)
            frequencyBands.DeleteArrayElementAtIndex(frequencyBands.arraySize - 1);

        EditorGUILayout.EndHorizontal();

        

        EditorGUILayout.Separator();

        EditorGUILayout.PropertyField(hrtfResamplingStep, new GUIContent("HRTF Resampling Step", "Control the HRTF angular resolution."));
        EditorGUILayout.PropertyField(numReverbSources, new GUIContent("Reverb Sources", "Control the number of reverb sources used for late reverberation spatialisation."));
        EditorGUILayout.PropertyField(fdnMatrix, new GUIContent("FDN Matrix", "Select the design of the FDN feedback matrix."));

        EditorGUILayout.PropertyField(selectedHRTF, new GUIContent("HRTF File", "Select HRTF File."));

        if (selectedHRTF.enumValueIndex == (int)RACManager.HRTFFiles.Custom)
            EditorGUILayout.PropertyField(customHRTFFile, new GUIContent("Enter HRTF file here:"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.PropertyField(selectedHeadphoneEQ, new GUIContent("Headphone EQ File", "Select Headphone EQ File."));

        GUI.enabled = true;
        if (selectedHeadphoneEQ.enumValueIndex == (int)RACManager.HeadphoneEQFiles.Custom)
        {
            EditorGUILayout.PropertyField(customHeadphoneEQFile, new GUIContent("Enter Headphone EQ file:"));
            serializedObject.ApplyModifiedProperties();
            if (isPlaying && GUILayout.Button("Update Headphone EQ"))
                RACManager.LoadHeadphoneEQ();
        }
        serializedObject.ApplyModifiedProperties();
        //GUI.enabled = true;
        GUI.changed = false;

        EditorGUILayout.PropertyField(iemConfig, new GUIContent("Image Edge Model", "Control the acoustic components modelled by the image edge model."), true);
        serializedObject.ApplyModifiedProperties();

        if (isPlaying && GUI.changed)
        {
            RACManager.UpdateIEMConfig();
            GUI.changed = false;
        }

        EditorGUILayout.PropertyField(spatialisationMode, new GUIContent("Spatialisation Mode", "Select from None (mono), Performance (inter-aural level differences) or Quality (binaural)."));
        serializedObject.ApplyModifiedProperties();

        if (isPlaying && GUI.changed)
        {
            RACManager.UpdateSpatialisationMode();
            GUI.changed = false;
        }

        EditorGUILayout.PropertyField(diffractionModel, new GUIContent("Diffraction Model", "Select the diffraction model used for audio processing."));
        serializedObject.ApplyModifiedProperties();

        if (isPlaying && GUI.changed)
        {
            RACManager.UpdateDiffractionModel();
            GUI.changed = false;
        }

        EditorGUILayout.PropertyField(reverbTimeModel, new GUIContent("Reverberation Time", "Select the formula used to calculate the reverberation time"));

        bool isCustom = reverbTimeModel.enumValueIndex == (int)RACManager.ReverbTime.Custom;
        if (isCustom)
        {
            if (T60.arraySize < frequencyBands.arraySize)
            {
                int oldSize = T60.arraySize;
                T60.arraySize = frequencyBands.arraySize;
                for (int i = oldSize; i < T60.arraySize; i++)
                    T60.GetArrayElementAtIndex(i).floatValue = 1.0f; // Default value for new elements
            }
            else if (T60.arraySize > frequencyBands.arraySize)
                T60.arraySize = frequencyBands.arraySize; // Resize to match frequency bands

            EditorGUILayout.PropertyField(T60, new GUIContent("T60", "Enter custom T60"));
        }
        serializedObject.ApplyModifiedProperties();
        if (isPlaying && GUI.changed)
        {
            if (isCustom)
                RACManager.UpdateReverbTime();
            else
                RACManager.UpdateReverbTimeModel();
            GUI.changed = false;
        }        
    }

    private float SnapToOctave(float value, float reference)
    {
        // Calculate nearest power of 2 multiplier from reference
        float ratio = value / reference;
        float power = Mathf.Round(Mathf.Log(ratio, 2));
        return reference * Mathf.Pow(2, power);
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