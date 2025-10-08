using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

enum FrequencyBand
{
    [InspectorName("125Hz")] Hz125 = 125,
    [InspectorName("250Hz")] Hz250 = 250,
    [InspectorName("500Hz")] Hz500 = 500,
    [InspectorName("1kHz")] kHz1 = 1000,
    [InspectorName("2kHz")] kHz2 = 2000,
    [InspectorName("4kHz")] kHz4 = 4000,
    [InspectorName("8kHz")] kHz8 = 8000,
    [InspectorName("16kHz")] kHz16 = 16000,
}

public class IRPlotter : MonoBehaviour
{
#if RAC_Debug
    [SerializeField, Range(0.1f, 5f)]
    private float durationInSeconds = 1f;
    [SerializeField, Range(10, 1000)]
    private int numPlotPoints = 100;
    [SerializeField]
    private FrequencyBand plottedOctaveBand = FrequencyBand.kHz1;
    private int plottedOctaveBandIdx;

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

        AddAxisLabels();

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

    public void RegisterSlopes(List<float> bandFreqs, List<int> idxs, List<float> T60s)
    {
        slopeBandIdxs = idxs;
        slopeT60s = T60s;

        // Set plottedOctaveBandIdx to the index of the closest match in bandFreqs
        if ((float)plottedOctaveBand < bandFreqs[0])
            plottedOctaveBandIdx = 0;
        else if ((float)plottedOctaveBand > bandFreqs[bandFreqs.Count - 1])
            plottedOctaveBandIdx = bandFreqs.Count - 1;
        else
        {
            plottedOctaveBandIdx = 0;
            float smallestDiff = Mathf.Abs((float)plottedOctaveBand - bandFreqs[0]);
            float diff = smallestDiff;

            for (int j = 1; j < bandFreqs.Count; j++)
            {
                diff = Mathf.Abs((float)plottedOctaveBand - bandFreqs[j]);
                if (diff < smallestDiff)
                {
                    plottedOctaveBandIdx = j;
                    smallestDiff = diff;
                }
            }
        }

        sourceResidues = new();
        listenerResidues = new();
        foreach (int i in idxs)
        {
            sourceResidues.Add(new());
            listenerResidues.Add(new());
        }
    }

    private void AddAxisLabels()
    {
        // Add octave band label.
        GameObject tempGameObject = new GameObject("Octave band label", typeof(RectTransform));

        TextMeshProUGUI tempText = tempGameObject.AddComponent<TextMeshProUGUI>();
        if ((int)plottedOctaveBand < 1000)
            tempText.text = $"EDC ({(int)plottedOctaveBand}Hz octave band)";
        else
            tempText.text = $"EDC ({(int)plottedOctaveBand/1000}kHz octave band)";
        tempText.alignment = TextAlignmentOptions.CaplineJustified;
        tempText.textWrappingMode = TextWrappingModes.NoWrap;
        tempText.fontSize = 0.05f;
        tempText.color = new Color(0f, 0f, 0f);

        ContentSizeFitter fitter = tempGameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform tempTranform = tempGameObject.GetComponent<RectTransform>();
        tempTranform.SetParent(this.transform, false);
        tempTranform.pivot = new Vector2(0.5f, 1f);
        tempTranform.anchorMin = new Vector2(0.5f, 0.5f);
        tempTranform.anchorMax = new Vector2(0.5f, 0.5f);
        tempTranform.anchoredPosition = new Vector2(0.5f, 1f);

        int numYticks = 0;
        // Add the Y axis labels. -80f and 10f are the maximum extents of lowerLimit and upperLimit.
        for (float yTick = -80f; yTick <= 10f; yTick += 10f)
        {
            float alignment = (yTick - lowerLimit) / (upperLimit - lowerLimit);
            if ((alignment <= 0) || (alignment >= 1))
                continue;

            tempGameObject = new GameObject($"Y tick {yTick}dB", typeof(RectTransform));

            tempText = tempGameObject.AddComponent<TextMeshProUGUI>();
            tempText.text = $"{yTick}dB";
            tempText.alignment = TextAlignmentOptions.MidlineLeft;
            tempText.textWrappingMode = TextWrappingModes.NoWrap;
            tempText.fontSize = 0.05f;
            tempText.color = new Color(0f, 0f, 0f);

            fitter = tempGameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tempTranform = tempGameObject.GetComponent<RectTransform>();
            tempTranform.SetParent(this.transform, false);
            tempTranform.pivot = new Vector2(0f, 0.5f);
            tempTranform.anchorMin = new Vector2(0.5f, 0.5f);
            tempTranform.anchorMax = new Vector2(0.5f, 0.5f);
            tempTranform.anchoredPosition = new Vector2(0f, alignment);

            numYticks++;
        }

        if (numYticks < 3)
        {
            // The labels are sparse: add more at multiples of 5.
            for (float yTick = -75f; yTick <= 10f; yTick += 10f)
            {
                float alignment = (yTick - lowerLimit) / (upperLimit - lowerLimit);
                if ((alignment <= 0) || (alignment >= 1))
                    continue;

                tempGameObject = new GameObject($"Y tick {yTick}dB", typeof(RectTransform));

                tempText = tempGameObject.AddComponent<TextMeshProUGUI>();
                tempText.text = $"{yTick}dB";
                tempText.alignment = TextAlignmentOptions.MidlineLeft;
                tempText.textWrappingMode = TextWrappingModes.NoWrap;
                tempText.fontSize = 0.05f;
                tempText.color = new Color(0f, 0f, 0f);

                fitter = tempGameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                tempTranform = tempGameObject.GetComponent<RectTransform>();
                tempTranform.SetParent(this.transform, false);
                tempTranform.pivot = new Vector2(0f, 0.5f);
                tempTranform.anchorMin = new Vector2(0.5f, 0.5f);
                tempTranform.anchorMax = new Vector2(0.5f, 0.5f);
                tempTranform.anchoredPosition = new Vector2(0f, alignment);

                numYticks++;
            }
        }

        // Add the X axis labels.
        float xTickStep;
        if (durationInSeconds <= 0.3f)
            xTickStep = 0.05f;
        else if (durationInSeconds <= 0.5f)
            xTickStep = 0.1f;
        else if (durationInSeconds <= 1f)
            xTickStep = 0.25f;
        else if (durationInSeconds <= 3f)
            xTickStep = 0.5f;
        else
            xTickStep = 1f;

        for (float xTick = xTickStep; xTick < durationInSeconds; xTick += xTickStep)
        {
            float alignment = xTick / durationInSeconds;
            if (alignment >= 1)
                continue;

            tempGameObject = new GameObject($"X tick {xTick}s", typeof(RectTransform));

            tempText = tempGameObject.AddComponent<TextMeshProUGUI>();
            tempText.text = $"{xTick}s";
            tempText.alignment = TextAlignmentOptions.BaselineJustified;
            tempText.textWrappingMode = TextWrappingModes.NoWrap;
            tempText.fontSize = 0.05f;
            tempText.color = new Color(0f, 0f, 0f);

            fitter = tempGameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tempTranform = tempGameObject.GetComponent<RectTransform>();
            tempTranform.SetParent(this.transform, false);
            tempTranform.pivot = new Vector2(0.5f, 0f);
            tempTranform.anchorMin = new Vector2(0.5f, 0.5f);
            tempTranform.anchorMax = new Vector2(0.5f, 0.5f);
            tempTranform.anchoredPosition = new Vector2(alignment, 0f);
        }
    }

    private void AddNewPlot()
    {
        GameObject child = new GameObject($"Line {myPlots.Count + 1}", typeof(RectTransform));

        RectTransform tempTranform = (RectTransform)child.transform;
        tempTranform.SetParent(this.transform, false);
        tempTranform.sizeDelta = new Vector2(1f, 1f);

        LinePlot lp = child.AddComponent<LinePlot>();
        lp.color = Palettes.OkabeIto[myPlots.Count % Palettes.OkabeIto.Count];
        foreach (float x in xAxis)
            lp.points.Add(new Vector2(x, 0f));

        myPlots.Add(lp);
    }
#endif
}
