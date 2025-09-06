#if UNITY_EDITOR
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RACMeshLoader))]
public class RACMeshLoaderEditor: Editor
{
    private int selectedIndex = -1;
    private string[] optionNames = new string[0];

    public void OnValidate()
    {
        AssetDatabase.Refresh();
        LoadOptions((RACMeshLoader)target);
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        RACMeshLoader racMeshLoader = (RACMeshLoader)target;

        if (optionNames.Length == 0)
        {
            bool success = LoadOptions(racMeshLoader);
            if (!success)
                return;
        }

        // If required, align UI index with the loader's current selection
        bool needsUpdate = alignSelection(racMeshLoader.GetCurrentSelection());
        if (needsUpdate)
            ApplySelection(racMeshLoader);

        // Show different UI in play or edit mode
        if (Application.isPlaying)
        {
            // During play mode: show read-only selection and disabled button
            EditorGUILayout.LabelField("Mesh version", optionNames[selectedIndex]);
            
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button(new GUIContent("Reload from disk", "Disabled during play mode"));
            EditorGUI.EndDisabledGroup();
        }
        else
        {
            // During edit mode: show interactable drop-down menu and button
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Mesh version", selectedIndex, optionNames);
            if (EditorGUI.EndChangeCheck())
            {
                selectedIndex = newIndex;
                ApplySelection(racMeshLoader);
            }

            if (GUILayout.Button(new GUIContent("Reload from disk", "Rescan the root meshes folder and update the dropdown menu.")))
            {
                AssetDatabase.Refresh();
                LoadOptions(racMeshLoader);
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
        bool success = racMeshLoader.__EditorAssignSelection(optionNames[selectedIndex]);
        // TODO: if (!success) do something smart about it
    }

    // Returns true if the load was successful.
    private bool LoadOptions(RACMeshLoader racMeshLoader)
    {
        if (!AssetDatabase.IsValidFolder(racMeshLoader.GetRootFolder()))
        {
            Debug.LogError(
                $"Options root folder not found:\n{racMeshLoader.GetRootFolder()}\n\n" +
                "Create it or update MeshesRoot constant.");
            return false;
        }

        // Enumerate immediate subfolders
        // N.B.: Before sorting, regex pads any integer in the string
        //       with leading zeros (up to 10 digits),
        //       to achieve smart alphanumerical sorting
        optionNames = AssetDatabase.GetSubFolders(racMeshLoader.GetRootFolder())
            .Select(s => s.Substring(racMeshLoader.GetRootFolder().Length).TrimStart('/'))
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => Regex.Replace(n, @"\d+", match => match.Value.PadLeft(10, '0')))
            .ToArray();

        if (optionNames.Length == 0)
        {
            Debug.LogError($"No subfolders found under root:\n{racMeshLoader.GetRootFolder()}");
            return false;
        }

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
                selectedIndex = System.Array.IndexOf(optionNames, current);
                if (selectedIndex < 0)
                    Debug.LogError("RACMeshLoader selection during play mode does not match any known option.");
            }
            return false;
        }

        if (System.Array.IndexOf(optionNames, current) < 0)
        {
            // If the selection does not match any member of the list
            // (e.g., disk contents have changed but the class kept its state),
            // default to the first option
            Debug.LogWarning("RACMeshLoader selection does not match any known option.");
            selectedIndex = 0;
            return true;
        }

        if (selectedIndex < 0 || selectedIndex >= optionNames.Length)
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
                selectedIndex = System.Array.IndexOf(optionNames, current);
                if (selectedIndex < 0)
                {
                    Debug.LogWarning("RACMeshLoader selection during play mode does not match any known option.");
                    selectedIndex = 0;
                    return true;
                }
            }
        }

        if (selectedIndex != System.Array.IndexOf(optionNames, current))
            return true;
        else
            return false;
    }

    private void OnUndoRedoPerformed()
    {
        // Resynchronize UI with component state after undo/redo
        selectedIndex = -1; // Force resync on next GUI call
        Repaint();
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    }
}
#endif
