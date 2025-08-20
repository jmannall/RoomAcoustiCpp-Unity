#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Mesh;

[CustomEditor(typeof(RACMeshLoader))]
public class RACMeshLoaderEditor: Editor
{
    private char sep = Path.DirectorySeparatorChar;
    private string MeshesRoot = "Assets/MOD-ART/Meshes";

    private string[] optionNames = new string[0];
    private int selectedIndex = -1;

    // TODO: Add RACmaterials component, using existing RACmaterials

    public override void OnInspectorGUI()
    {
        RACMeshLoader loader = (RACMeshLoader)target;

        if (!AssetDatabase.IsValidFolder(MeshesRoot))
        {
            EditorGUILayout.HelpBox(
                $"Options root folder not found:\n{MeshesRoot}\n\n" +
                "Create it or update MeshesRoot constant.", MessageType.Error);
            return;
        }

        // Enumerate immediate subfolders
        var subs = AssetDatabase.GetSubFolders(MeshesRoot);
        optionNames = subs
            .Select(s => s.Substring(MeshesRoot.Length).TrimStart('/'))
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => n)
            .ToArray();
        // TODO: Smarter sorting (number-aware)

        if (optionNames.Length == 0)
        {
            EditorGUILayout.HelpBox($"No subfolders found under {MeshesRoot}.", MessageType.Info);
            return;
        }

        // One-time init: align UI index with the loader's current selection
        if (!loader.IsInitialised())
        {
            var current = loader.GetCurrentSelection();
            // If the component has no selection yet, default to 0 (first option)
            selectedIndex = Mathf.Max(0, System.Array.IndexOf(optionNames, current));
            // If the component has no selection yet, apply the default option
            if (string.IsNullOrEmpty(current))
                ApplySelection(loader, optionNames[selectedIndex]);
        }

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup("Mesh version", selectedIndex, optionNames);
        if (EditorGUI.EndChangeCheck())
        {
            selectedIndex = newIndex;
            ApplySelection(loader, optionNames[selectedIndex]);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Reload", "Rescan the root meshes folder and update the dropdown menu.")))
            {
                AssetDatabase.Refresh();
                Repaint();
            }

            if (GUILayout.Button(new GUIContent("Rebuild", "Clear the existing mesh and re-instantiate it based on the current selection.")))
            {
                loader.ClearChildren();
                loader.SpawnMesh();
            }
        }

        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }

    private void ApplySelection(RACMeshLoader loader, string optionName)
    {
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

        Undo.RecordObject(loader, "Change Option");
        loader.__EditorAssignSelection(MeshesRoot, optionName, meshGO);
        EditorUtility.SetDirty(loader);
    }
}
#endif
