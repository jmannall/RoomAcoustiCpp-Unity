using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;

// `.mtx` is not a valid extension for TextAsset, so we need a special importer.
// https://discussions.unity.com/t/loading-a-file-with-a-custom-extension-as-a-textasset/731294/5
[ScriptedImporter(1, "mtx")]
public class MtxImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        TextAsset subAsset = new TextAsset(File.ReadAllText(ctx.assetPath))
        {
            // Keep the name without extension (matches Resources.Load path)
            name = Path.GetFileNameWithoutExtension(ctx.assetPath)
        };
        ctx.AddObjectToAsset("text", subAsset);
        ctx.SetMainObject(subAsset);
    }
}
