#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RACMeshLoader))]
public class RACMeshLoaderEditor: Editor
{
    private char sep = Path.DirectorySeparatorChar;
    private string MeshesRoot = "Assets/MOD-ART/Meshes";

    private int selectedIndex = -1;
    private string[] optionNames = new string[0];

    // TODO: Add RACmaterials component, using existing RACmaterials

    public override void OnInspectorGUI()
    {
        RACMeshLoader racMeshLoader = (RACMeshLoader)target;

        if (!AssetDatabase.IsValidFolder(MeshesRoot))
        {
            EditorGUILayout.HelpBox(
                $"Options root folder not found:\n{MeshesRoot}\n\n" +
                "Create it or update MeshesRoot constant.", MessageType.Error);
            return;
        }

        // One-time init: check folder structure
        if (optionNames.Length == 0)
        {
            // Enumerate immediate subfolders
            // B.B.: The regex adds leading zeros to any numbers in the string before sorting, to achieve smart alphanumerical sorting
            optionNames = AssetDatabase.GetSubFolders(MeshesRoot)
                .Select(s => s.Substring(MeshesRoot.Length).TrimStart('/'))
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => Regex.Replace(n, @"\d+", match => match.Value.PadLeft(10, '0')))
                .ToArray();

            if (optionNames.Length == 0)
            {
                EditorGUILayout.HelpBox($"No subfolders found under root:\n{MeshesRoot}", MessageType.Info);
                return;
            }
        }

        // One-time init: align UI index with the loader's current selection (only in edit mode)
        var current = racMeshLoader.GetCurrentSelection();
        if (Application.isPlaying)
        {
            // Cannot realign the selection during play mode
            if (string.IsNullOrEmpty(current))
            {
                EditorGUILayout.HelpBox("Misalignment between RACMeshLoader/Editor selection during play mode.", MessageType.Error);
                return;
            }
            else if (selectedIndex < 0)
            {
                // Editor was reset; reconstruct selectedIndex from the persistent selectedSubfolder
                selectedIndex = System.Array.IndexOf(optionNames, current);
            }
        }
        else if (selectedIndex < 0 || selectedIndex >= optionNames.Length)
        {
            // If the component has no selection (or a bad index, somehow), default to the first option
            if (string.IsNullOrEmpty(current))
                selectedIndex = 0;
            else // Reconstruct selectedIndex from the persistent selectedSubfolder
                selectedIndex = System.Array.IndexOf(optionNames, current);

            ApplySelection(racMeshLoader, optionNames[selectedIndex]);
        }

        // Show different UI based on play mode
        if (Application.isPlaying)
        {
            // During play mode: show read-only selection
            EditorGUILayout.LabelField("Mesh version", optionNames[selectedIndex]);
            
            // Show disabled buttons
            EditorGUI.BeginDisabledGroup(true);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Button(new GUIContent("Reload", "Disabled during play mode"));
                GUILayout.Button(new GUIContent("Rebuild", "Disabled during play mode"));
            }
            EditorGUI.EndDisabledGroup();
        }
        else
        {
            // During edit mode: show interactive controls
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Mesh version", selectedIndex, optionNames);
            if (EditorGUI.EndChangeCheck())
            {
                selectedIndex = newIndex;
                ApplySelection(racMeshLoader, optionNames[selectedIndex]);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Reload", "Rescan the root meshes folder and update the dropdown menu.")))
                {
                    AssetDatabase.Refresh();
                    optionNames = new string[0];// Reset to force re-sync on next GUI call
                    selectedIndex = -1;         // Reset to force re-sync on next GUI call
                    Repaint();                  // Ask for GUI call to perform re-sync
                }

                if (GUILayout.Button(new GUIContent("Rebuild", "Clear the existing mesh and re-instantiate it based on the current selection.")))
                {
                    racMeshLoader.ClearChildren();
                    racMeshLoader.SpawnMesh();
                }
            }
        }

        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }

    private void ApplySelection(RACMeshLoader racMeshLoader, string optionName)
    {
        // Prevent operations during play mode
        if (Application.isPlaying)
        {
            Debug.LogWarning("Cannot change mesh selection during play mode.");
            return;
        }

        string optionPath = $"{MeshesRoot}/{optionName}";

        // TODO: Load materials from file

        // Load /mesh.obj as a model GameObject
        GameObject meshGO = AssetDatabase.LoadAssetAtPath<GameObject>($"{optionPath}/mesh.obj");

        if (meshGO == null)
        {
            EditorUtility.DisplayDialog(
                "Missing mesh.obj",
                $"Expected model at:\n{MeshesRoot}/mesh.obj\n\n" +
                "Create the file or fix the folder name.",
                "OK"
            );
            return; // keep previous assignment; do NOT clear children
        }

        Undo.RecordObject(racMeshLoader, "Change Option");
        racMeshLoader.__EditorAssignSelection(MeshesRoot, optionName, meshGO);
        EditorUtility.SetDirty(racMeshLoader);
    }
}
#endif
