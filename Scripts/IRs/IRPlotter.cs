using System.Collections.Generic;
using UnityEngine;

public class IRPlotter : MonoBehaviour
{
#if RAC_Debug
    [SerializeField, Range(0f, 10f)]
    private float duration = 1f;
    [SerializeField, Range(10, 1000)]
    private int numSamples = 100;

    private static IRPlotter irPlotter;

    private List<float> sourceResidues = new();
    private List<float> listenerResidues = new();

    // If isSource, channelIndex contains the source ID, otherwise, channelIndex contains the reverb direction index
    static void OnResidueCallback(float residue, bool isSource, int channelIndex, int slopeIndex)
    {
        List<float> myResidues;
        if (isSource)
            myResidues = irPlotter.sourceResidues;
        else
            myResidues = irPlotter.listenerResidues;

        if (slopeIndex == 0)
        {
            while (channelIndex >= myResidues.Count)
                myResidues.Add(0f);
            myResidues[channelIndex] = residue;
        }
    }

    void OnValidate()
    {
        DebugCPP.RegisterResidueCallback(OnResidueCallback);

        if (irPlotter == null)
            irPlotter = this;
        else
            Debug.AssertFormat(irPlotter == this, "More than one instance of the IRPlotter created! Singleton violated.");
    }

    private void OnDisable()
    {
        DebugCPP.UnregisterResidueCallback();
    }

    void Start()
    {
        GameObject child = new GameObject("Line 1", typeof(RectTransform));
        LinePlot lp = child.AddComponent<LinePlot>();
        child.transform.SetParent(this.transform, false);

        float x, y;
        for (int i = 0; i <= numSamples; ++i)
        {
            x = (float)i / (float)numSamples;
            y = x * x;
            lp.points.Add(new Vector2(x, y));
        }
    }

    void Update()
    {
        //Debug.Log(
        //    "sourceResidues: " + string.Join(", ", sourceResidues) + ";" +
        //    " listenerResidues: " + string.Join(", ", listenerResidues));

        float combinedResidue;
        foreach (float sourceResidue in sourceResidues)
        {
            combinedResidue = 0f;
            foreach (float listenerResidue in listenerResidues)
                combinedResidue += listenerResidue;
            combinedResidue *= sourceResidue;

            //Debug.Log("combinedResidue: " + combinedResidue.ToString());
        }
    }
#endif
}