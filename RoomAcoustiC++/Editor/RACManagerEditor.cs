
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

    private SerializedProperty lerpFactor, fBands, fLimitBand, hrtfResamplingStep, fdnMatrix, selectedHRTF, customHRTFFile, selectedHeadphoneEQ, customHeadphoneEQFile, iemConfig, spatialisationMode, diffractionModel, reverbTimeModel, T60;

    private void OnEnable()
    {
        // Link the SerializedProperty to the serialized field in the target class
        lerpFactor = serializedObject.FindProperty("lerpFactor");
        fBands = serializedObject.FindProperty("fBands");
        fLimitBand = serializedObject.FindProperty("fLimitBand");
        hrtfResamplingStep = serializedObject.FindProperty("hrtfResamplingStep");
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
        EditorGUILayout.PropertyField(fBands, new GUIContent("Frequency Bands", "Set the centre frequencies for all frequency dependent processing."), true);
        EditorGUILayout.PropertyField(fLimitBand, new GUIContent("Frequency Limit Band", "Control the size of the upper and lower bands for frequency dependent processing. If Frequency Bands are provided as octave bands, select Octave."));
        EditorGUILayout.PropertyField(hrtfResamplingStep, new GUIContent("HRTF Resampling Step", "Control the HRTF angular resolution."));
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
            if (T60.arraySize == 0)
            {
                T60.arraySize = 1;
                T60.GetArrayElementAtIndex(0).floatValue = 1.0f; // Default value if no custom T60 is set
            }
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