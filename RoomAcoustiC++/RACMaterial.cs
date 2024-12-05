
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[AddComponentMenu("RoomAcoustiC++/Material")]

public class RACMaterial : MonoBehaviour
{
    [Tooltip("List of materials to be used by the acoustic model.")]
    public List<RACMaterialEntry> materials;

    public void Awake()
    {
        if (materials == null)
            materials = new List<RACMaterialEntry>();

        // Add default material to end
        materials.Add(ScriptableObject.CreateInstance<RACMaterialEntry>());
    }

    public RACMaterialEntry FindMatch(Material material)
    {
        foreach (RACMaterialEntry entry in materials)
        {
            if (entry.material == material)
                return entry;
        }

        // Return default material if no match found
        return materials.Last();
    }

    public Material FindMatch(string name)
    {
        foreach (RACMaterialEntry entry in materials)
        {
            if (entry.material.name == name)
                return entry.material;
        }

        // Return default material if no match found
        return materials.Last().material;
    }
}