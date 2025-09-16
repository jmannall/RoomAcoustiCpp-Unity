#if UNITY_EDITOR
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RACMeshLoader))]
public class RACMeshLoaderEditor: Editor
{
    private int selectedIndex = -1;
    private string[] subfolderOptions = new string[0];

    void OnValidate()
    {
        AssetDatabase.Refresh();
        LoadSubfolderOptions();
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        RACMeshLoader racMeshLoader = (RACMeshLoader)target;

        if (subfolderOptions.Length == 0)
        {
            bool success = LoadSubfolderOptions();
            if (!success)
                return;
        }

        // If required, align UI index with the loader's current selection
        bool needsUpdate = alignSelection(racMeshLoader.GetSelectedSubfolder());
        if (needsUpdate)
            ApplySelection(racMeshLoader);

        // Show different UI in play or edit mode
        if (Application.isPlaying)
        {
            // During play mode: show read-only selection and disabled button
            EditorGUILayout.LabelField("Mesh version", subfolderOptions[selectedIndex]);
            
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button(new GUIContent("Reload from disk", "Disabled during play mode"));
            EditorGUI.EndDisabledGroup();
        }
        else
        {
            // During edit mode: show interactable drop-down menu and button
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Mesh version", selectedIndex, subfolderOptions);
            if (EditorGUI.EndChangeCheck())
            {
                selectedIndex = newIndex;
                ApplySelection(racMeshLoader);
            }

            if (GUILayout.Button(new GUIContent("Reload from disk", "Rescan the root meshes folder and update the dropdown menu.")))
            {
                AssetDatabase.Refresh();
                LoadSubfolderOptions();
                ApplySelection(racMeshLoader);
                Repaint();
            }
        }

        // TODO: Should Update() be called earlier? Later?
        // TODO: Is there a way to make "renderAcousticMesh" have an effect in the editor view?
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }

    private void ApplySelection(RACMeshLoader racMeshLoader)
    {
        // Prevent operations during play mode
        if (Application.isPlaying)
        {
            Debug.LogError("Cannot change mesh selection during play mode.");
            return;
        }

        Undo.RecordObject(racMeshLoader, "Change Option");
        bool success = racMeshLoader.__EditorAssignSelection(subfolderOptions[selectedIndex]);
        // TODO: if (!success) do something smart about it
    }

    // Returns true if the load was successful.
    private bool LoadSubfolderOptions()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/PythonExports"))
        {
            Debug.LogError("Mesh root folder \"Assets/Resources/PythonExports\" not found.");
            return false;
        }

        if (AssetDatabase.GetSubFolders("Assets/Resources/PythonExports").Length == 0)
        {
            Debug.LogError("Mesh root folder \"Assets/Resources/PythonExports\" has no subfolders.");
            return false;
        }

        // Enumerate immediate subfolders
        // N.B.: Before sorting, regex pads any integer in the string
        //       with leading zeros (up to 10 digits),
        //       to achieve smart alphanumerical sorting
        subfolderOptions = AssetDatabase.GetSubFolders("Assets/Resources/PythonExports")
            .Select(s => s.Substring(31))
            .OrderBy(n => Regex.Replace(n, @"\d+", match => match.Value.PadLeft(10, '0')))
            .ToArray();

        return true;
    }

    // Returns true if the object's selection should be updated.
    private bool alignSelection(string current)
    {
        //TODO: Improve the logic of this method. It is redundant.
        if (Application.isPlaying)
        {
            // Cannot realign the selection during play mode, even if it's incorrect.
            // This block always returns false, but it may log error messages if necessary.
            if (string.IsNullOrEmpty(current))
                Debug.LogError("Non-initialized RACMeshLoader selection during play mode.");
            else
            {
                // Editor was reset; reconstruct selectedIndex from the persistent selectedSubfolder
                selectedIndex = System.Array.IndexOf(subfolderOptions, current);
                if (selectedIndex < 0)
                    Debug.LogError("RACMeshLoader selection during play mode does not match any known option.");
            }
            return false;
        }

        if (System.Array.IndexOf(subfolderOptions, current) < 0)
        {
            // If the selection does not match any member of the list
            // (e.g., disk contents have changed but the class kept its state),
            // default to the first option
            Debug.LogWarning("RACMeshLoader selection does not match any known option.");
            selectedIndex = 0;
            return true;
        }

        if (selectedIndex < 0 || selectedIndex >= subfolderOptions.Length)
        {
            // If the selected index is not initialized (or out of bounds, somehow) set it back.
            if (string.IsNullOrEmpty(current))
            {
                // If the component has no selection, default to the first option
                selectedIndex = 0;
                return true;
            }
            else
            {
                // If the component has a selection, reconstruct selectedIndex from it
                selectedIndex = System.Array.IndexOf(subfolderOptions, current);
                if (selectedIndex < 0)
                {
                    Debug.LogWarning("RACMeshLoader selection during play mode does not match any known option.");
                    selectedIndex = 0;
                    return true;
                }
            }
        }

        if (selectedIndex != System.Array.IndexOf(subfolderOptions, current))
            return true;
        else
            return false;
    }

    void OnUndoRedoPerformed()
    {
        // Resynchronize UI with component state after undo/redo
        selectedIndex = -1; // Force resync on next GUI call
        Repaint();
    }

    void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    }
}
#endif
