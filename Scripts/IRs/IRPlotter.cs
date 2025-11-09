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
    [SerializeField, Range(0.1f, 10f)]
    private float lineThickness = 5f;
    [SerializeField, Range(0.1f, 5f)]
    private float durationInSeconds = 1f;
    [SerializeField, Range(10, 1000)]
    private int numPlotPoints = 100;
    [SerializeField]
    private FrequencyBand plottedOctaveBand = FrequencyBand.kHz1;
    private int plottedOctaveBandIdx;

    [SerializeField, Range(-50f, 50f)]
    private float upperLimit = -10f;
    [SerializeField, Range(-100f, 0f)]
    private float lowerLimit = -70f;

    [SerializeField]
    private bool negativeSlopes = true;
    [SerializeField]
    private bool backwardsIntegration = true;

    public static IRPlotter irPlotter;

    private Rect plotExtent;
    private List<float> xAxis;
    private List<LinePlot> myPlots;

    private List<int> slopeBandIdxs = null;
    private List<float> slopeT60s = null;

    private int numRegisteredSources = 0;
    private List<List<float>> sourceResidues;
    private List<List<float>> listenerResidues;
    private bool allSourcesRegistered = false;
    // The sources are sorted in alphabetical order.
    // This translates from RACAudioSource.id to the alphabetical index.
    private int[] sourceIdToIndex;

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

            while (channelIndex >= myResidues.Count)
                myResidues.Add(0f);
            myResidues[channelIndex] = residue;
        }
    }

    void OnValidate()
    {
        //DebugCPP.RegisterResidueCallback(OnResidueCallback);

        //if (irPlotter == null)
        //    irPlotter = this;
        //else
        //    Debug.AssertFormat(irPlotter == this, "More than one instance of the IRPlotter created! Singleton violated.");
    }

    void OnDisable()
    {
        //DebugCPP.UnregisterResidueCallback();
    }

    private void Awake()
    {
        DebugCPP.RegisterResidueCallback(OnResidueCallback);

        if (irPlotter == null)
            irPlotter = this;
        else
            Debug.AssertFormat(irPlotter == this, "More than one instance of the IRPlotter created! Singleton violated.");
    }

    private void OnDestroy()
    {
        DebugCPP.UnregisterResidueCallback();
    }

    void Start()
    {
        plotExtent = GetComponent<RectTransform>().rect;

        xAxis = new();
        // These in-range values are the ones shown in the plot.
        for (int i = 0; i < numPlotPoints; ++i)
            xAxis.Add(plotExtent.width * (float)i / (float)(numPlotPoints - 1));

        // Some out-of-range values are computed for the backwards integration.
        // Note that these are x > plotExtent.width, i.e., outside of the element's bounds.
        for (int i = numPlotPoints; i < 2 * numPlotPoints; ++i)
            xAxis.Add(plotExtent.width * (float)i / (float)(numPlotPoints - 1));

        AddAxisLabels();

        myPlots = new();

        allSourcesRegistered = false;
    }

    void Update()
    {
        if (upperLimit <= lowerLimit)
            upperLimit = lowerLimit + 1;

        if (!allSourcesRegistered)
        {
            RACAudioSource[] racSources = FindObjectsByType<RACAudioSource>(FindObjectsSortMode.None);
            foreach (RACAudioSource thisSource in racSources)
            {
                // An ID of -1 indicates the source has not been initialized yet.
                if (thisSource.id < 0)
                {
                    Debug.LogWarning("Waiting for all sources to be initialized.");
                    return;
                }
            }
            // If the loop ended, all sources have been initialized.
            allSourcesRegistered = true;
            numRegisteredSources = racSources.Length;

            PopulatePlots(racSources);
        }

        if (slopeT60s == null)
        {
            Debug.LogError("Slopes have not been initialized!");
            return;
        }

        List<float> yValues;
        float combinedResidue;
        double timeInSeconds, exponentPerSecond;

        lock (residueLock)
        {
            for (int sourceId = 0; sourceId < numRegisteredSources; ++sourceId)
            {
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

                    // Skip this slope if the residue is zero, avoid useless computations.
                    if (combinedResidue == 0f)
                        continue;

                    if (!negativeSlopes && combinedResidue < 0f)
                        continue;

                    exponentPerSecond = -6.0 / (double)slopeT60s[slopeId];
                    for (int sampleId = 0; sampleId < xAxis.Count; ++sampleId)
                    {
                        timeInSeconds = (double)xAxis[sampleId] * (double)durationInSeconds / (double)plotExtent.width;
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
                        yValues[i] += yValues[i + 1];
                }

                for (int i = 0; i < yValues.Count; ++i)
                {
                    // Convert to dB
                    yValues[i] = 10 * Mathf.Log10(yValues[i]);
                    // Rescale to plot range
                    yValues[i] = plotExtent.height * (yValues[i] - lowerLimit) / (upperLimit - lowerLimit);
                }

                myPlots[sourceIdToIndex[sourceId]].SetPlotData(xAxis, yValues);
                myPlots[sourceIdToIndex[sourceId]].thickness = lineThickness;
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
            tempText.text = $"Energy decay curves ({(int)plottedOctaveBand}Hz octave band)";
        else
            tempText.text = $"Energy decay curves ({(int)plottedOctaveBand/1000}kHz octave band)";
        tempText.alignment = TextAlignmentOptions.CaplineJustified;
        tempText.textWrappingMode = TextWrappingModes.NoWrap;
        //tempText.margin = new Vector4(5f, 5f, 5f, 5f);
        tempText.fontSize = 30f;
        tempText.color = Color.black;

        ContentSizeFitter fitter = tempGameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform tempTranform = tempGameObject.GetComponent<RectTransform>();
        tempTranform.SetParent(this.transform, false);
        tempTranform.pivot = new Vector2(0.5f, 1.2f);
        tempTranform.anchorMin = new Vector2(0.5f, 1f);
        tempTranform.anchorMax = new Vector2(0.5f, 1f);
        tempTranform.anchoredPosition = Vector2.zero;

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
            tempText.margin = new Vector4(5f, 5f, 5f, 5f);
            tempText.fontSize = 20f;
            tempText.color = Color.black;

            fitter = tempGameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tempTranform = tempGameObject.GetComponent<RectTransform>();
            tempTranform.SetParent(this.transform, false);
            tempTranform.pivot = new Vector2(0f, 0.5f);
            tempTranform.anchorMin = new Vector2(0f, alignment);
            tempTranform.anchorMax = new Vector2(0f, alignment);
            tempTranform.anchoredPosition = Vector2.zero;

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
                tempText.margin = new Vector4(5f, 5f, 5f, 5f);
                tempText.fontSize = 20f;
                tempText.color = Color.black;

                fitter = tempGameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                tempTranform = tempGameObject.GetComponent<RectTransform>();
                tempTranform.SetParent(this.transform, false);
                tempTranform.pivot = new Vector2(0f, 0.5f);
                tempTranform.anchorMin = new Vector2(0f, alignment);
                tempTranform.anchorMax = new Vector2(0f, alignment);
                tempTranform.anchoredPosition = Vector2.zero;

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
                break;

            tempGameObject = new GameObject($"X tick {xTick}s", typeof(RectTransform));

            tempText = tempGameObject.AddComponent<TextMeshProUGUI>();
            tempText.text = $"{xTick}s";
            tempText.alignment = TextAlignmentOptions.BaselineJustified;
            tempText.textWrappingMode = TextWrappingModes.NoWrap;
            tempText.margin = new Vector4(5f, 5f, 5f, 5f);
            tempText.fontSize = 20f;
            tempText.color = Color.black;

            fitter = tempGameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tempTranform = tempGameObject.GetComponent<RectTransform>();
            tempTranform.SetParent(this.transform, false);
            tempTranform.pivot = new Vector2(0.5f, 0f);
            tempTranform.anchorMin = new Vector2(alignment, 0f);
            tempTranform.anchorMax = new Vector2(alignment, 0f);
            tempTranform.anchoredPosition = Vector2.zero;
        }
    }

    private LinePlot NewPlot()
    {
        GameObject child = new GameObject($"Line {myPlots.Count + 1}", typeof(RectTransform));

        RectTransform tempTranform = child.GetComponent<RectTransform>();
        tempTranform.SetParent(this.transform, false);
        tempTranform.pivot = Vector2.zero;
        tempTranform.anchorMin = Vector2.zero;
        tempTranform.anchorMax = Vector2.zero;
        tempTranform.anchoredPosition = Vector2.zero;

        LinePlot lp = child.AddComponent<LinePlot>();
        foreach (float x in xAxis)
            lp.points.Add(new Vector2(x, 0f));

        return lp;
    }

    private void PopulatePlots(RACAudioSource[] racSources)
    {
        // Sort the sources alphabetically.
        List<RACAudioSource> sourcesList = new List<RACAudioSource>(racSources);
        sourcesList.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
        racSources = sourcesList.ToArray();
        // Build mapping from source ID to alphabetical index.
        sourceIdToIndex = new int[numRegisteredSources];
        for (int i = 0; i < racSources.Length; i++)
            sourceIdToIndex[racSources[i].id] = i;

        // This needs to be done in a separate loop from all of the following,
        // because the order of source objects does not match their indices.
        foreach (RACAudioSource thisSource in racSources)
            myPlots.Add(NewPlot());

        GameObject legend = this.transform.parent.Find("Legend").gameObject;
        if (legend == null)
            Debug.LogError("IRPlotter could not find a sibling named \"Legend\".");

        Transform[] legendColumns = null;
        // Note: this is a "floored" integer division.
        int numPaletteLoops = 1 + ((racSources.Length - 1) / Palettes.OkabeIto.Count);
        if (legend != null)
        {
            // Create as many columns as there are palette loops.
            legendColumns = new Transform[numPaletteLoops];
            for (int i = 0; i < numPaletteLoops; ++i)
            {
                GameObject column = new GameObject($"Legend column {i + 1}", typeof(RectTransform));
                column.transform.SetParent(legend.transform, false);

                // Stack entries in each column vertically.
                VerticalLayoutGroup vlg = column.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = false;
                vlg.childForceExpandHeight = false;

                // Add a title to the column, specifying line style. Easier than dashed legend entry icons.
                GameObject columnTitle = new GameObject($"Legend column {i + 1} title", typeof(RectTransform));
                columnTitle.transform.SetParent(column.transform, false);

                TextMeshProUGUI labelText = columnTitle.AddComponent<TextMeshProUGUI>();
                labelText.alignment = TextAlignmentOptions.TopLeft;
                labelText.textWrappingMode = TextWrappingModes.NoWrap;
                labelText.fontSize = 20f;
                labelText.color = Color.black;
                if (i == 0)
                    labelText.text = "Solid line";
                else
                    labelText.text = $"Dash spacing {i}";
                // Make sure all titles have the same height.
                LayoutElement le = columnTitle.AddComponent<LayoutElement>();
                le.minHeight = 24f;

                legendColumns[i] = column.transform;
            }
        }

        foreach (RACAudioSource thisSource in racSources)
        {
            int legendIdx = sourceIdToIndex[thisSource.id];

            string sourceName = thisSource.gameObject.name;
            Color sourceColor = Palettes.OkabeIto[legendIdx % Palettes.OkabeIto.Count];

            // Set the plot line to the correct color.
            myPlots[legendIdx].color = sourceColor;
            // Note: this is a "floored" integer division.
            int paletteLoop = legendIdx / Palettes.OkabeIto.Count;
            // 1, 2, 4, ... (integer power of 2 https://stackoverflow.com/a/31176751)
            myPlots[legendIdx].dashStride = 1 << paletteLoop;
            // 1, 1, 2, 4, ...
            if (paletteLoop == 0)
                myPlots[legendIdx].dashLength = 1;
            else
                myPlots[legendIdx].dashLength = 1 << (paletteLoop-1);

            // Add a legend label matching the source object's name.
            if (legend != null)
            {
                GameObject legendEntry = new GameObject($"{sourceName} legend entry", typeof(RectTransform));
                // Assign to the appropriate legend column (even if there is only 1, because it holds the vertical layout group).
                legendEntry.transform.SetParent(legendColumns[paletteLoop], false);

                HorizontalLayoutGroup row = legendEntry.AddComponent<HorizontalLayoutGroup>();
                row.childAlignment = TextAnchor.MiddleLeft;
                row.childControlWidth = true;
                row.childControlHeight = true;
                row.childForceExpandWidth = false;
                row.childForceExpandHeight = false;
                row.spacing = 6f;
                row.padding = new RectOffset(6, 6, 6, 6);

                GameObject entryIcon = new GameObject($"{sourceName} legend icon", typeof(RectTransform));
                entryIcon.transform.SetParent(legendEntry.transform, false);

                Image iconImage = entryIcon.AddComponent<Image>();
                iconImage.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
                iconImage.color = sourceColor;

                // Lock the icon to a square of the desired size (otherwise its parent HorizontalLayoutGroup will stretch it)
                LayoutElement iconLE = entryIcon.AddComponent<LayoutElement>();
                iconLE.preferredWidth = 10f;
                iconLE.preferredHeight = 10f;
                iconLE.minWidth = 10f;
                iconLE.minHeight = 10f;

                GameObject entryLabel = new GameObject($"{sourceName} legend label", typeof(RectTransform));
                entryLabel.transform.SetParent(legendEntry.transform, false);

                TextMeshProUGUI labelText = entryLabel.AddComponent<TextMeshProUGUI>();
                labelText.text = sourceName;
                labelText.alignment = TextAlignmentOptions.MidlineLeft;
                labelText.textWrappingMode = TextWrappingModes.NoWrap;
                //labelText.margin = new Vector4(0.01f, 0.005f, 0.01f, 0.005f);
                labelText.fontSize = 20f;
                labelText.color = Color.black;
            }

            // Ensure that the source's color matches the line's color.
            foreach (MeshRenderer meshRenderer in thisSource.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (Material material in meshRenderer.materials)
                {
                    material.SetColor("_BaseColor", sourceColor);
                }
            }
        }
    }
#endif
}
