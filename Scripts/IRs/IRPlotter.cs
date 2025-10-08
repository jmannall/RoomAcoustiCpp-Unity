using System;
using System.Collections.Generic;
using UnityEngine;

enum FrequencyBand
{
    [InspectorName("125Hz")] Hz125 = 0,
    [InspectorName("250Hz")] Hz250 = 1,
    [InspectorName("500Hz")] Hz500 = 2,
    [InspectorName("1kHz")] Hz1k = 3,
    [InspectorName("2kHz")] Hz2k = 4,
    [InspectorName("4kHz")] Hz4k = 5,
    [InspectorName("8kHz")] Hz8k = 6,
    [InspectorName("16kHz")] Hz16k = 7,
}

public class IRPlotter : MonoBehaviour
{
#if RAC_Debug
    [SerializeField, Range(0.1f, 10f)]
    private float durationInSeconds = 1f;
    [SerializeField, Range(10, 1000)]
    private int numPlotPoints = 100;
    [SerializeField]
    private FrequencyBand plottedOctaveBand = FrequencyBand.Hz1k;
    private int plottedOctaveBandIdx = (int)FrequencyBand.Hz1k;


    [SerializeField, Range(-30f, 10f)]
    private float upperLimit = -10f;
    [SerializeField, Range(-80f, -40f)]
    private float lowerLimit = -70f;

    [SerializeField]
    private bool negativeSlopes = true;
    [SerializeField]
    private bool backwardsIntegration = true;

    public static IRPlotter irPlotter;

    private List<float> xAxis;
    private List<LinePlot> myPlots;

    private List<float> bandFreqs;
    private List<int> slopeBandIdxs;
    private List<float> slopeT60s;

    private int numRegisteredSources = 0;
    private List<List<float>> sourceResidues;
    private List<List<float>> listenerResidues;

    // Lock to protect residue data races
    private readonly object residueLock = new();

    // If isSource, channelIndex contains the source ID, otherwise, channelIndex contains the reverb direction index
    static void OnResidueCallback(float residue, bool isSource, int channelIndex, int slopeIndex)
    {
        List<float> myResidues;

        lock (irPlotter.residueLock)
        {
            if (isSource)
                myResidues = irPlotter.sourceResidues[slopeIndex];
            else
                myResidues = irPlotter.listenerResidues[slopeIndex];

            if (isSource && (channelIndex >= irPlotter.numRegisteredSources))
                irPlotter.numRegisteredSources = channelIndex + 1;

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

    void OnDisable()
    {
        DebugCPP.UnregisterResidueCallback();
    }

    void Start()
    {
        xAxis = new();
        // These in-range values are the ones shown in the plot.
        for (int i = 0; i < numPlotPoints; ++i)
            xAxis.Add((float)i / (float)(numPlotPoints - 1));

        // Some out-of-range values are computed for the backwards integration.
        for (int i = numPlotPoints; i < 2 * numPlotPoints; ++i)
            xAxis.Add((float)i / (float)(numPlotPoints - 1));

        myPlots = new();
    }

    void Update()
    {
        List<float> yValues;
        float combinedResidue;
        double timeInSeconds, exponentPerSecond;

        lock (residueLock)
        {
            for (int sourceId = 0; sourceId < numRegisteredSources; ++sourceId)
            {
                while (sourceId >= myPlots.Count)
                    AddNewPlot();

                yValues = new();
                foreach (float x in xAxis)
                    yValues.Add(0f);

                for (int slopeId = 0; slopeId < slopeT60s.Count; ++slopeId)
                {
                    // Only consider slopes in the specified octave band.
                    if (slopeBandIdxs[slopeId] != plottedOctaveBandIdx)
                        continue;
                    // Skip this slope if it hasn't received its source residue yet.
                    if (sourceResidues[slopeId].Count <= sourceId)
                        continue;

                    combinedResidue = 0f;
                    foreach (float listenerResidue in listenerResidues[slopeId])
                        combinedResidue += listenerResidue;
                    combinedResidue *= sourceResidues[slopeId][sourceId];

                    if (!negativeSlopes && combinedResidue < 0f)
                        continue;

                    exponentPerSecond = -6.0 / (double)slopeT60s[slopeId];
                    for (int sampleId = 0; sampleId < xAxis.Count; ++sampleId)
                    {
                        timeInSeconds = (double)xAxis[sampleId] * (double)durationInSeconds;
                        yValues[sampleId] += combinedResidue * (float)Math.Pow(10.0, exponentPerSecond * timeInSeconds);
                    }
                }

                for (int i = 0; i < yValues.Count; ++i)
                {
                    // Drop non-positive values
                    if (yValues[i] < 1e-15f) yValues[i] = 1e-15f;
                }

                if (backwardsIntegration)
                {
                    // Plot an EDC (non-normalized backwards integration) if requested
                    for (int i = yValues.Count - 2; i >= 0; --i)
                        yValues[i] += yValues[i+1];
                }

                for (int i = 0; i < yValues.Count; ++i)
                {
                    // Convert to dB
                    yValues[i] = 10 * Mathf.Log10(yValues[i]);
                    // Rescale to plot range
                    yValues[i] = (yValues[i] - lowerLimit) / (upperLimit - lowerLimit);
                }

                myPlots[sourceId].SetPlotData(xAxis, yValues);
            }
        }
    }

    public void RegisterSlopes(List<float> freqs, List<int> idxs, List<float> T60s)
    {
        bandFreqs = freqs;
        slopeBandIdxs = idxs;
        slopeT60s = T60s;

        // TODO: Set plottedOctaveBandIdx to the index of the closest match in bandFreqs

        sourceResidues = new();
        listenerResidues = new();
        foreach (int i in idxs)
        {
            sourceResidues.Add(new());
            listenerResidues.Add(new());
        }
    }

    private void AddNewPlot()
    {
        GameObject child = new GameObject("Line 1", typeof(RectTransform));
        child.transform.SetParent(this.transform, false);

        LinePlot lp = child.AddComponent<LinePlot>();
        lp.color = Palettes.OkabeIto[myPlots.Count % Palettes.OkabeIto.Count];
        foreach (float x in xAxis)
            lp.points.Add(new Vector2(x, 0f));

        myPlots.Add(lp);
    }
#endif
}
