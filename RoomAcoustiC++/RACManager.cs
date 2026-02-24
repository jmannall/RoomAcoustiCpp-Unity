using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;

[AddComponentMenu("RoomAcoustiC++/RAC Audio Manager")]
[RequireComponent(typeof(AudioSource))]

public class RACManager : MonoBehaviour
{
    // global singleton
    public static RACManager racManager = null;

    #region Plugin Interface

    //////////////////// Plugin interface ////////////////////

    private const string PluginName = "RoomAcoustiCpp";

#if RAC_Default
    private const string PluginType = "";
#elif RAC_Debug
    private const string PluginType = "_Debug";
#elif RAC_Profile
    private const string PluginType = "_Profile";
#elif RAC_ProfileDetailed
    private const string PluginType = "_ProfileDetailed";
#else
    private const string PluginType = "";
#endif

#if UNITY_IOS
    public const string DLLNAME = "__Internal";
#else
    public const string DLLNAME = PluginName + PluginType + "_x64";
#endif

    // Load and Destroy

    [DllImport(DLLNAME)]
    private static extern bool RACInit(int fs, int numFrames, int numReverbSources, int fdnSize, float lerpFactor, float Q, [In] float[] frequencyBands, int numFrequencyBands);

    [DllImport(DLLNAME)]
    private static extern void RACExit();

    [DllImport(DLLNAME)]
    private static extern bool RACLoadSpatialisationFiles(int hrtfResampling, string[] filePaths);

    [DllImport(DLLNAME)]
    private static extern bool RACInitEarlyReverb(bool enabled, int direct, int reflOrder, int shadowDiffOrder, int specularDiffOrder, float minEdgeLength, float maxPathLength, int diffractionId);

    [DllImport(DLLNAME)]
    private static extern bool RACInitSingleFDN(bool enabled, float volume, [In] float[] t60, int reverbFormulaId, [In] float[] dimensions, int numDimensions, int numRays, int matrixId);

    [DllImport(DLLNAME)]
    private static extern bool RACInitMoDART(bool enabled, int numRays, int matrixId, float delay, float minT60, [In] int[] indexing, [In] int[] frequencyIndexing, [In] float[] t60s, [In] float[] leftEigenvectors, [In] float[] rightEigenvectors, int numFDNs, int numNodes, int numPaths);

    [DllImport(DLLNAME)]
    private static extern void RACSetHeadphoneEQ([In] float[] leftIR, [In] float[] rightIR, int irLength);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateSpatialisationMode(int id);

    // Early reverb

    [DllImport(DLLNAME)]
    private static extern void RACEnableEarlyReverb(bool enable);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateEarlyConfig(int direct, int reflOrder, int shadowDiffOrder, int specularDiffOrder, float minEdgeLength, float maxPathLength);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateDiffractionModel(int difractionId);

    // Late reverb

    [DllImport(DLLNAME)]
    private static extern void RACEnableLateReverb(bool enable);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateLateReverbNumberOfRays(int numRays);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateLateReverbDistanceThresholds(float sourceThresh, float listenerThresh);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateSelfShadowingRadius(float radius);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateMoDARTDelay(float delay);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateMoDARTMinimumReverbTime(float T60);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateSingleFDNReverbTime([In] float[] t60);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateSingleFDNReverbTimeModel(int reverbFormulaId);

    [DllImport(DLLNAME)]
    private static extern void RACResetLateReverb();

    // Listener

    [DllImport(DLLNAME)]
    private static extern void RACUpdateListener(float posX, float posY, float posZ, float oriW, float oriX, float oriY, float oriZ);

    // Source

    [DllImport(DLLNAME)]
    private static extern int RACInitSource();

    [DllImport(DLLNAME)]
    private static extern void RACUpdateSource(int id, float posX, float posY, float posZ, float oriW, float oriX, float oriY, float oriZ);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateSourceDirectivity(int id, int directivity);

    [DllImport(DLLNAME)]
    private static extern void RACRemoveSource(int id);

    // Material

    [DllImport(DLLNAME)]
    private static extern int RACInitMaterial([In] float[] absorption);

    [DllImport(DLLNAME)]
    private static extern int RACUpdateMaterial(int id, [In] float[] absorption);

    [DllImport(DLLNAME)]
    private static extern void RACRemoveMaterial(int id);

    // Wall

    [DllImport(DLLNAME)]
    private static extern int RACInitWall([In] float[] vertices, int materialId);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateWall(int id, [In] float[] vertices);

    [DllImport(DLLNAME)]
    private static extern void RACRemoveWall(int id);

    [DllImport(DLLNAME)]
    private static extern void RACUpdatePlanesAndEdges();

    // Audio
    [DllImport(DLLNAME)]
    private static extern void RACSubmitAudio(int id, [In] float[] data);

    [DllImport(DLLNAME)]
    private static extern bool RACProcessOutput();

    [DllImport(DLLNAME)]
    private static extern void RACGetOutputBuffer([In] float[] buffer);

    [DllImport(DLLNAME)]
    private static extern void RACRecordImpulseResponse(float posX, float posY, float posZ, float oriW, float oriX, float oriY, float oriZ, [In] float[] buffer, int numSamples);

    #endregion

    public event Action enableAudioProcessing;
    public event Action disableAudioProcessing;

    #region Parameters

    //////////////////// Parameters ////////////////////

    // File paths
    private string hrtfFile = " ";
    private string nearFieldFile = " ";
    private string ildFile = " ";
    private string headphoneEQFile = " ";
    private string resourcePath;

    public enum SpatMode { None, Performance, Quality }
    public enum ReverbTime { Sabine, Eyring, Custom }
    public enum FDNMatrix { Householder, RandomOrthogonal }
    public enum LateReverbModel {
        [InspectorName("Single FDN")] SingleFDN,
        [InspectorName("MoD-ART")] MoDART
    }
    public enum DiffractionModel { Attenuate, LowPass, UDFA, UDFAI, NNBest, NNSmall, UTD, BTM }
    public enum SourceDirectivity { Omni, Subcardioid, Cardioid, Supercardioid, Hypercardioid, Bidirectional, Genelec8020c, Genelec8020cDTF, QSC_K8 }
    public enum DirectSound { None, Check, AlwaysOn }
    public enum DiffractionSound { None, ShadowZone, AllZones }

    [Serializable]
    public struct EarlyConfig
    {
        [Tooltip("Toggle the early sound components as a whole.")]
        public bool enabled;

        [Tooltip("None (no direct sound), Check (direct sound if source visible), Always On (no visibility check).")]
        public DirectSound direct;
        [Range(0, 6)]
        [Tooltip("Set the maximum number of reflections in reflection only paths.")]
        public int reflectionOrder;
        [Range(0, 6)]
        [Tooltip("Set the maximum number of reflections or diffractions in shadowed diffraction paths.")]
        public int shadowDiffractionOrder;
        [Range(0, 6)]
        [Tooltip("Set the maximum number of reflections or diffractions in specular diffraction paths.")]
        public int specularDiffractionOrder;

        [Range(0, 4)]
        [Tooltip("Set a minimum edge length threshold for diffraction modelling.")]
        public float minimumEdgeLength;

        [LogarithmicRange(0, 3, false)]
        [Tooltip("Set a maximum path length threshold for image sources.")]
        public float maximumPathLength;

        public float GetMaximumPathLength()
        {
            return Mathf.Pow(10f, maximumPathLength);
        }

        public EarlyConfig(bool enabled, DirectSound direct, int reflOrder, int diffShadowOrder, int diffSpecularOrder, float minimumEdgeLength, float maximumPathLength)
        {
            this.enabled = enabled;
            this.direct = direct;
            this.reflectionOrder = reflOrder;
            this.shadowDiffractionOrder = diffShadowOrder;
            this.specularDiffractionOrder = diffSpecularOrder;
            this.minimumEdgeLength = minimumEdgeLength;
            this.maximumPathLength = maximumPathLength;
        }

        public static EarlyConfig Default(int maxOrder) => new EarlyConfig(
            enabled: true,
            direct: DirectSound.Check,
            reflOrder: maxOrder,
            diffShadowOrder: maxOrder,
            diffSpecularOrder: 0,
            minimumEdgeLength: 0.0f,
            maximumPathLength: 3f
        );

        public static EarlyConfig NoReflections => new EarlyConfig(
            enabled: true,
            direct: DirectSound.Check,
            reflOrder: 0,
            diffShadowOrder: 1,
            diffSpecularOrder: 0,
            minimumEdgeLength: 0.0f,
            maximumPathLength: 3f
        );
    }

    [Serializable]
    public struct LateConfig
    {
        [Tooltip("Toggle the late reverberation component as a whole.")]
        public bool enabled;

        [LogarithmicRange(2, 5, true)]
        [Tooltip("Number of rays used for MoD-ART energy injection and detection.")]
        public float numRays;

        [Range(0.0f, 2.5f)]
        [Tooltip("Minimum distance that a sound source needs to move before triggering an update of late reverberation parameters, in meters.")]
        public float sourceThresh;
        [Range(0.0f, 2.5f)]
        [Tooltip("Minimum distance that the listener needs to move before triggering an update of late reverberation parameters, in meters.")]
        public float listenerThresh;

        [Range(0.0f, 1.0f)]
        [Tooltip("Radius of the sphere used to consider self-shadowing of the listener's head for late reverberation parameters, in meters.")]
        public float selfShadowRadius;

        [Range(0.0f, 0.5f)]
        [Tooltip("Delay preceding the late reverberation component, in seconds.")]
        public float delay;

        [LogarithmicRange(-2, 1, false)]
        [Tooltip("Minimum reverberation time for each mode. A higher minimum reduces the number of slopes, and hence FDNs, used to model later reverberation.")]
        public float minT60;

        public void SetNumRays(float numRays)
        {
            this.numRays = Mathf.Log10(numRays);
        }

        public int GetNumRays()
        {
            return Mathf.RoundToInt(Mathf.Pow(10f, numRays));
        }

        public void SetMinReverbTime(float minT60)
        {
            this.minT60 = Mathf.Log10(minT60);
        }

        public float GetMinReverbTime()
        {
            return Mathf.Pow(10f, minT60);
        }

        public LateConfig(bool enabled, float numRays, float sourceThresh, float listenerThresh, float selfShadowRadius, float delay, float minT60)
        {
            this.enabled = enabled;
            this.numRays = numRays;
            this.sourceThresh = sourceThresh;
            this.listenerThresh = listenerThresh;
            this.selfShadowRadius = selfShadowRadius;
            this.delay = delay;
            this.minT60 = minT60;
        }

        public static LateConfig Default => new LateConfig(
            enabled: true,
            numRays: 3f,
            sourceThresh: 0.25f,
            listenerThresh: 0.05f,
            selfShadowRadius: 0.0f,
            delay: 0f,
            minT60: 0.01f
        );
    }

    // DSP Parameters
    private int sampleRate;
    private int numFrames;
    private int numChannels = 2;

    [Header("Initial properties")]
    [SerializeField, Range(0.0f, 10.0f)]
    private float lerpFactor = 2.0f;

    [SerializeField]
    private List<float> frequencyBands = new List<float> { 250.0f, 500.0f, 1000.0f, 2000.0f, 4000.0f };

    [Range(0.1f, 2.0f)]
    private float Q = 0.98f;

    [SerializeField, Range(1, 45)]
    private int hrtfResamplingStep = 5;

    [SerializeField, Range(1, 32)]
    private int numReverbSources = 12;

    [SerializeField, Range(6, 32)]
    private int fdnSize = 12;

    [SerializeField]
    private FDNMatrix fdnMatrix = FDNMatrix.Householder;

    private float[] outputBuffer;

    private float[] vertices = new float[9];

    public bool isRunning { get; private set; }

    private static bool noHRTFFiles = true;

    [Header("Acoustic Model Configuration")]
    [SerializeField]
    private EarlyConfig earlyConfig = EarlyConfig.Default(2);
    [SerializeField]
    private LateConfig lateConfig = LateConfig.Default;

    [Header("Configurable properties")]
    [SerializeField, HideInInspector]
    private SpatMode spatialisationMode = SpatMode.Performance;

    [SerializeField, HideInInspector]
    private DiffractionModel diffractionModel = DiffractionModel.BTM;

    [SerializeField, HideInInspector]
    private LateReverbModel lateReverbModel = LateReverbModel.MoDART;

    [SerializeField, HideInInspector]
    private List<float> T60;

    [SerializeField, HideInInspector]
    private ReverbTime reverbTimeModel = ReverbTime.Sabine;

    private float[] interleavedData;

    public enum HRTFFiles
    {
        KemarHRTF,
        KemarDTF,
        Custom // This will allow users to enter a custom string
    }

    public enum HeadphoneEQFiles
    {
        None,
        Custom // This will allow users to enter a custom string
    }

    public HRTFFiles selectedHRTF;
    public string customHRTFFile;

    public HeadphoneEQFiles selectedHeadphoneEQ;
    public string customHeadphoneEQFile;
    #endregion

    #region Unity Functions

    //////////////////// Unity Functions ////////////////////

    void Awake()
    {
        if (racManager == null)
            racManager = this;
        else
            Debug.AssertFormat(racManager == this, "More than one instance of the RACManager created! Singleton violated.");

        AudioConfiguration config = AudioSettings.GetConfiguration();
        numFrames = config.dspBufferSize;
        sampleRate = config.sampleRate;

        Debug.Log("Sample rate: " + sampleRate);

        outputBuffer = new float[numChannels * numFrames];

        switch (selectedHRTF)
        {
            case HRTFFiles.KemarHRTF:
                hrtfFile = "Kemar_HRTF_ITD_48000_3dti-hrtf.3dti-hrtf";
                break;
            case HRTFFiles.KemarDTF:
                hrtfFile = "Kemar_DTF_ITD_48000_3dti-hrtf.3dti-hrtf";
                break;
            case HRTFFiles.Custom:
                hrtfFile = customHRTFFile;
                break;
        }
        nearFieldFile = "NearFieldCompensation_ILD_48000.3dti-ild";
        ildFile = "HRTF_ILD_48000.3dti-ild";

        if (Application.platform == RuntimePlatform.Android)
        {
            DownloadFileForAndroid(hrtfFile);
            DownloadFileForAndroid(nearFieldFile);
            DownloadFileForAndroid(ildFile);

            resourcePath = Application.temporaryCachePath;
        }
        else
            resourcePath = Application.streamingAssetsPath;

        Debug.Log("Resource Path: " + resourcePath);
        char sep = Path.DirectorySeparatorChar;
        string[] filePaths = { resourcePath + sep + hrtfFile, resourcePath + sep + nearFieldFile, resourcePath + sep + ildFile };

        isRunning = RACInit(sampleRate, numFrames, numReverbSources, fdnSize, lerpFactor, Q, frequencyBands.ToArray(), frequencyBands.Count);
        bool filesLoaded = RACLoadSpatialisationFiles(hrtfResamplingStep, filePaths);
        if (!filesLoaded)
        {
            Debug.LogError("Failed to load HRTF files");
            racManager.spatialisationMode = SpatMode.None;
            noHRTFFiles = true;
        }
        else
        {
            Debug.Log("HRTF files loaded");
            noHRTFFiles = false;
        }

        LoadHeadphoneEQ();

        bool success = InitEarlyReverb();
        if (!success)
            Debug.LogError("Failed to initialize Early Reflections");
        UpdateSpatialisationMode();

        // These are not passed with the initialization call, so they should be set separately.
        UpdateLateReverbDistanceThresholds();
        UpdateSelfShadowingRadius();
    }

    void Start()
    {
#if UNITY_EDITOR
        Debug.Log("Unity Editor");
#endif

#if UNITY_ANDROID
        Debug.Log("Android");
#endif

#if UNITY_IOS
        Debug.Log("Iphone");
#endif

#if UNITY_STANDALONE_OSX
        Debug.Log("Stand Alone OSX");
#endif

#if UNITY_STANDALONE_WIN
        Debug.Log("Stand Alone Windows");
#endif
        interleavedData = new float[numReverbSources * numFrames];

        // This is used to synchronize sources which are set to "playOnAwake".
        StartCoroutine(SyncStartAllSources());
    }

    void OnDestroy()
    {
        isRunning = false;
        RACExit();
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (isRunning)
        {
            int numSamples = data.Length;

            Profiler.BeginSample("Process Audio Output");
            bool success = RACProcessOutput();
            //for (int i = 1, j = 0; i < numFDNChannels * numFrames; i += numFDNChannels, j += 2)
            //    interleavedData[i] = data[j];
            //bool success = RACProcessOutput_MOD_ART(interleavedData);
            Profiler.EndSample();

            if (success)
            {
                // fetch the output buffer from the context
                GetOutputBuffer(ref outputBuffer);

                if (channels == numChannels)
                {
                    // choose the right length in case data buffer too big
                    numSamples = (numSamples > outputBuffer.Length) ? outputBuffer.Length : numSamples;

                    // memcpy the data over
                    Array.Copy(outputBuffer, data, numSamples);
                }
                else
                {
                    // Copy stereo interleaved buffer to the first two channels of the interleaved data buffer
                    for (int i = 0; i < numFrames; i++)
                    {
                        data[i * channels] = outputBuffer[i * numChannels];     // Left channel
                        data[i * channels + 1] = outputBuffer[i * numChannels + 1]; // Right channel
                        // Fill the rest of the channels with 0
                        for (int j = numChannels; j < channels; j++)
                            data[i * channels + j] = 0.0f;
                    }
                }
            }
            else // fill output with 0
            {
                Debug.LogError("Failed to retrieve audio output buffer");
                Array.Fill(data, 0.0f);
            }
        }
    }

    #endregion

    #region Plugin Function Calls

    //////////////////// Plugin Function Calls ////////////////////

    public static bool InitEarlyReverb()
    {
        return RACInitEarlyReverb(racManager.earlyConfig.enabled, SelectDirectMode(racManager.earlyConfig.direct),
            racManager.earlyConfig.reflectionOrder, racManager.earlyConfig.shadowDiffractionOrder,
            racManager.earlyConfig.specularDiffractionOrder, racManager.earlyConfig.minimumEdgeLength,
            racManager.earlyConfig.GetMaximumPathLength(), (int)racManager.diffractionModel);
    }

    public static bool InitSingleFDN(float volume, float[] dimensions)
    {
        return RACInitSingleFDN(racManager.lateConfig.enabled, volume, racManager.T60.ToArray(),
            (int)racManager.reverbTimeModel, dimensions, dimensions.Length, racManager.lateConfig.GetNumRays(), (int)racManager.fdnMatrix);
    }

    public static bool InitMoDART(int[] indexing, int[] frequencyIndexing, float[] t60s, float[] leftEigenvectors, float[] rightEigenvectors, int numFDNs, int numNodes, int numPaths)
    {
        return RACInitMoDART(racManager.lateConfig.enabled, racManager.lateConfig.GetNumRays(), (int)racManager.fdnMatrix,
            racManager.lateConfig.delay, racManager.lateConfig.GetMinReverbTime(), indexing, frequencyIndexing, t60s,
            leftEigenvectors, rightEigenvectors, numFDNs, numNodes, numPaths);
    }

    public static void UpdateSpatialisationMode()
    {
        if (noHRTFFiles)
            racManager.spatialisationMode = SpatMode.None;

        switch (racManager.spatialisationMode)
        {
            case SpatMode.None:
                { RACUpdateSpatialisationMode(0); break; }
            case SpatMode.Performance:
                { RACUpdateSpatialisationMode(1); break; }
            case SpatMode.Quality:
                { RACUpdateSpatialisationMode(2); break; }
        }
    }

    public static void UpdateSpatialisationMode(SpatMode mode)
    {
        if (noHRTFFiles)
            return;

        racManager.spatialisationMode = mode;
        UpdateSpatialisationMode();
    }

    // Early reverb

    static int SelectDirectMode(DirectSound dir)
    {
        switch (dir)
        {
            case DirectSound.None:
                { return 0; }
            case DirectSound.Check:
                { return 1; }
            case DirectSound.AlwaysOn:
                { return 2; }
            default:
                { return 0; }
        }
    }

    public static void EnableEarlyReverb()
    {
        Profiler.BeginSample("Enable Early Reverb");
        RACEnableEarlyReverb(racManager.earlyConfig.enabled);
        Profiler.EndSample();
    }

    public static void EnableEarlyReverb(bool enable)
    {
        racManager.earlyConfig.enabled = enable;
        EnableEarlyReverb();
    }

    public static void UpdateEarlyConfig()
    {
        Profiler.BeginSample("Update early config");
        RACUpdateEarlyConfig(
            SelectDirectMode(racManager.earlyConfig.direct),
            racManager.earlyConfig.reflectionOrder, racManager.earlyConfig.shadowDiffractionOrder, racManager.earlyConfig.specularDiffractionOrder,
            racManager.earlyConfig.minimumEdgeLength, racManager.earlyConfig.GetMaximumPathLength());
        Profiler.EndSample();
    }

    public static void UpdateEarlyConfig(EarlyConfig config)
    {
        racManager.earlyConfig = config;
        UpdateEarlyConfig();
    }

    public static void UpdateDiffractionModel(DiffractionModel model)
    {
        racManager.diffractionModel = model;
        UpdateDiffractionModel();
    }

    public static void UpdateDiffractionModel()
    {
        switch (racManager.diffractionModel)
        {
            case DiffractionModel.Attenuate:
                { RACUpdateDiffractionModel(0); break; }
            case DiffractionModel.LowPass:
                { RACUpdateDiffractionModel(1); break; }
            case DiffractionModel.UDFA:
                { RACUpdateDiffractionModel(2); break; }
            case DiffractionModel.UDFAI:
                { RACUpdateDiffractionModel(3); break; }
            case DiffractionModel.NNBest:
                { RACUpdateDiffractionModel(4); break; }
            case DiffractionModel.NNSmall:
                { RACUpdateDiffractionModel(5); break; }
            case DiffractionModel.UTD:
                { RACUpdateDiffractionModel(6); break; }
            case DiffractionModel.BTM:
                { RACUpdateDiffractionModel(7); break; }
        }
    }

    // Late reverb

    public static void EnableLateReverb()
    {
        Profiler.BeginSample("Enable Late Reverb");
        RACEnableLateReverb(racManager.lateConfig.enabled);
        Profiler.EndSample();
    }

    public static void EnableLateReverb(bool enable)
    {
        racManager.lateConfig.enabled = enable;
        EnableLateReverb();
    }

    public static void UpdateLateReverbNumberOfRays()
    {
        Profiler.BeginSample("Update number of rays");
        RACUpdateLateReverbNumberOfRays(racManager.lateConfig.GetNumRays());
        Profiler.EndSample();
    }

    public static void UpdateLateReverbNumberOfRays(float numRays)
    {
        racManager.lateConfig.SetNumRays(numRays);
        UpdateLateReverbNumberOfRays();
    }

    public static void UpdateLateReverbDistanceThresholds()
    {
        Profiler.BeginSample("Update distance thresholds for late reverb updates");
        RACUpdateLateReverbDistanceThresholds(racManager.lateConfig.sourceThresh, racManager.lateConfig.listenerThresh);
        Profiler.EndSample();
    }

    public static void UpdateLateReverbDistanceThresholds(float sourceThresh, float listenerThresh)
    {
        racManager.lateConfig.sourceThresh = sourceThresh;
        racManager.lateConfig.listenerThresh = listenerThresh;
        UpdateLateReverbDistanceThresholds();
    }

    public static void UpdateSelfShadowingRadius()
    {
        Profiler.BeginSample("Update self-shadowing radius");
        RACUpdateSelfShadowingRadius(racManager.lateConfig.selfShadowRadius);
        Profiler.EndSample();
    }

    public static void UpdateSelfShadowingRadius(float radius)
    {
        racManager.lateConfig.selfShadowRadius = radius;
        UpdateSelfShadowingRadius();
    }

    public static void UpdateMoDARTDelay()
    {
        Profiler.BeginSample("Update MoDART delay");
        RACUpdateMoDARTDelay(racManager.lateConfig.delay);
        Profiler.EndSample();
    }

    public static void UpdateMoDARTDelay(float delay)
    {
        racManager.lateConfig.delay = delay;
        UpdateMoDARTDelay();
    }

    public static void UpdateMoDARTMinimumReverbTime()
    {
        Profiler.BeginSample("Update minimum reverb time");
        RACUpdateMoDARTMinimumReverbTime(racManager.lateConfig.GetMinReverbTime());
        Profiler.EndSample();
    }

    public static void UpdateMoDARTMinimumReverbTime(float minT60)
    {
        racManager.lateConfig.SetMinReverbTime(minT60);
        UpdateMoDARTMinimumReverbTime();
    }

    public static void UpdateSingleFDNReverbTime()
    {
        if (racManager.T60.Count < racManager.frequencyBands.Count)
        {
            int oldSize = racManager.T60.Count;
            for (int i = oldSize; i < racManager.frequencyBands.Count; i++)
                racManager.T60.Add(1.0f); // Default value for new elements
        }
        else if (racManager.T60.Count > racManager.frequencyBands.Count)
            racManager.T60.RemoveRange(racManager.frequencyBands.Count, racManager.T60.Count - racManager.frequencyBands.Count);

        RACUpdateSingleFDNReverbTime(racManager.T60.ToArray());
    }

    public static void UpdateSingleFDNReverbTime(List<float> newT60)
    {
        racManager.T60 = newT60;
        UpdateSingleFDNReverbTime();
    }

    public static void UpdateSingleFDNReverbTimeModel()
    {
        switch (racManager.reverbTimeModel)
        {
            case ReverbTime.Sabine:
                { RACUpdateSingleFDNReverbTimeModel(0); break; }
            case ReverbTime.Eyring:
                { RACUpdateSingleFDNReverbTimeModel(1); break; }
            case ReverbTime.Custom:
                { RACUpdateSingleFDNReverbTimeModel(2); break; }
        }
    }

    public static void UpdateSingleFDNReverbTimeModel(ReverbTime model)
    {
        racManager.reverbTimeModel = model;
        UpdateSingleFDNReverbTimeModel();
    }

    public static void ResetLateReverb()
    {
        Profiler.BeginSample("Reset FDN");
        RACResetLateReverb();
        Profiler.EndSample();
    }

    // Listener

    public static void UpdateListener(Vector3 position, Quaternion orientation)
    {
        Profiler.BeginSample("Update Listener");
        RACUpdateListener(position.x, position.y, position.z, orientation.w, orientation.x, orientation.y, orientation.z);
        Profiler.EndSample();
    }

    // Source

    public static int InitSource()
    {
        Profiler.BeginSample("Init Source");
        return RACInitSource();
    }

    public static void UpdateSource(int id, Vector3 position, Quaternion orientation)
    {
        Profiler.BeginSample("Update Source");
        RACUpdateSource(id, position.x, position.y, position.z, orientation.w, orientation.x, orientation.y, orientation.z);
        Profiler.EndSample();
    }

    public static void UpdateSourceDirectivity(int id, SourceDirectivity directivity)
    {
        switch (directivity)
        {
            case SourceDirectivity.Omni:
                { RACUpdateSourceDirectivity(id, 0); break; }
            case SourceDirectivity.Subcardioid:
                { RACUpdateSourceDirectivity(id, 1); break; }
            case SourceDirectivity.Cardioid:
                { RACUpdateSourceDirectivity(id, 2); break; }
            case SourceDirectivity.Supercardioid:
                { RACUpdateSourceDirectivity(id, 3); break; }
            case SourceDirectivity.Hypercardioid:
                { RACUpdateSourceDirectivity(id, 4); break; }
            case SourceDirectivity.Bidirectional:
                { RACUpdateSourceDirectivity(id, 5); break; }
            case SourceDirectivity.Genelec8020c: 
                { RACUpdateSourceDirectivity(id, 6); break; }
            case SourceDirectivity.Genelec8020cDTF:
                { RACUpdateSourceDirectivity(id, 7); break; }
            case SourceDirectivity.QSC_K8:
                { RACUpdateSourceDirectivity(id, 8); break; }
        }
    }

    public static void RemoveSource(int id)
    {
        Profiler.BeginSample("Remove Source");
        RACRemoveSource(id);
        Profiler.EndSample();
    }

    // Material

    public static int InitMaterial(ref float[] absorption)
    {
        Profiler.BeginSample("Init Material");
        int id = RACInitMaterial(absorption);
        Profiler.EndSample();
        return id;
    }

    public static void UpdateMaterial(int id, ref float[] absorption)
    {
        Profiler.BeginSample("Update Material");
        RACUpdateMaterial(id, absorption);
        Profiler.EndSample();
    }

    public static void RemoveMaterial(int id)
    {
        Profiler.BeginSample("Remove Material");
        RACRemoveMaterial(id);
        Profiler.EndSample();
    }

    // Wall

    public static void UpdateVData(ref Vector3[] vertices)
    {
        racManager.vertices[0] = vertices[0].x;
        racManager.vertices[1] = vertices[0].y;
        racManager.vertices[2] = vertices[0].z;
        racManager.vertices[3] = vertices[1].x;
        racManager.vertices[4] = vertices[1].y;
        racManager.vertices[5] = vertices[1].z;
        racManager.vertices[6] = vertices[2].x;
        racManager.vertices[7] = vertices[2].y;
        racManager.vertices[8] = vertices[2].z;
    }

    public static int InitWall(ref Vector3[] vertices, int materialId)
    {
        if (vertices.Length != 3)
        {
            Debug.LogError("Wall must have 3 vertices");
            return -1;
        }

        UpdateVData(ref vertices);

        Profiler.BeginSample("Init Wall");
        int id = RACInitWall(racManager.vertices, materialId);
        Profiler.EndSample();
        return id;
    }

    public static void UpdateWall(int id, ref Vector3[] vertices)
    {
        if (vertices.Length != 3)
        {
            Debug.LogError("Wall must have 3 vertices");
            return;
        }

        UpdateVData(ref vertices);

        Profiler.BeginSample("Update Wall");
        RACUpdateWall(id, racManager.vertices);
        Profiler.EndSample();
    }

    public static void RemoveWall(int id)
    {
        Profiler.BeginSample("Remove Wall");
        RACRemoveWall(id);
        Profiler.EndSample();
    }

    public static void UpdatePlanesAndEdges()
    {
        Profiler.BeginSample("Update Planes and Edges");
        RACUpdatePlanesAndEdges();
        Profiler.EndSample();
    }

    // Audio

    public static void SetHeadphoneEQ(ref float[] leftIR, ref float[] rightIR)
    {
        RACSetHeadphoneEQ(leftIR, rightIR, leftIR.Length);
    }

    public static void SubmitAudio(int id, ref float[] input)
    {
        Profiler.BeginSample("Submit Audio");
        RACSubmitAudio(id, input);
        Profiler.EndSample();
    }

    public static bool ProcessOutput()
    {
        Profiler.BeginSample("Process Audio");
        return RACProcessOutput();
    }

    public static void GetOutputBuffer(ref float[] buffer)
    {
        Profiler.BeginSample("Get Output");
        RACGetOutputBuffer(buffer);
        Profiler.EndSample();
    }

    public static void RecordImpulseResponse(Vector3 position, Quaternion orientation, ref float[] buffer)
    {
        Profiler.BeginSample("Record IR");
        RACRecordImpulseResponse(position.x, position.y, position.z, orientation.w, orientation.x, orientation.y, orientation.z, buffer, buffer.Length);
        Profiler.EndSample();
    }
    #endregion

    #region Download Functions

    void DownloadFileForAndroid(string fileName)
    {
        string url = Path.Combine(Application.streamingAssetsPath, fileName);
        string savePath = Path.Combine(Application.temporaryCachePath, fileName);
        //Create Directory if it does not exist
        if (!Directory.Exists(Path.GetDirectoryName(savePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
        }

        UnityWebRequest webRequest = new UnityWebRequest(url);
        webRequest.method = UnityWebRequest.kHttpVerbGET;
        DownloadHandlerFile downloadHandler = new DownloadHandlerFile(savePath);
        downloadHandler.removeFileOnAbort = true;
        webRequest.downloadHandler = downloadHandler;
        webRequest.SendWebRequest();

        while (!webRequest.isDone)
            Debug.Log("Waiting to download " + fileName);


        if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            Debug.Log(webRequest.error);
        else
            Debug.Log("Download saved to: " + savePath.Replace("/", "\\") + "\r\n" + webRequest.error);
    }

    #endregion

    public static void DisableAudioProcessing() { racManager.disableAudioProcessing?.Invoke(); racManager.isRunning = false; }

    public static void EnableAudioProcessing() { racManager.enableAudioProcessing?.Invoke(); racManager.isRunning = true; }

    public static void LoadHeadphoneEQ()
    {
        switch (racManager.selectedHeadphoneEQ)
        {
            case HeadphoneEQFiles.None:
                return;
            case HeadphoneEQFiles.Custom:
                racManager.headphoneEQFile = racManager.customHeadphoneEQFile;
                break;
        }

        char sep = Path.DirectorySeparatorChar;
            
        if (Application.platform == RuntimePlatform.Android)
            racManager.DownloadFileForAndroid(racManager.headphoneEQFile);

        string headphoneEQPath = racManager.resourcePath + sep + racManager.headphoneEQFile;
        if (File.Exists(headphoneEQPath))
        {
            Debug.Log("Headphone EQ file loaded");

            using (BinaryReader reader = new BinaryReader(File.Open(headphoneEQPath, FileMode.Open)))
            {
                int irLength = reader.ReadInt32();
                float[] leftIR = new float[irLength];
                float[] rightIR = new float[irLength];
                for (int i = 0; i < irLength; i++)
                {
                    leftIR[i] = reader.ReadSingle();
                    rightIR[i] = reader.ReadSingle();
                }
                SetHeadphoneEQ(ref leftIR, ref rightIR);
            }
        }
        else
            Debug.LogError("Headphone EQ file not found");
    }

    public LateReverbModel GetLateReverbModel() { return lateReverbModel; }

    public List<float> GetFrequencyBands() { return frequencyBands; }

    public void SetFrequencyBands(List<float> newFrequencyBands) { frequencyBands = newFrequencyBands; }
    public void SetFrequencyBands(float[] newFrequencyBands) { frequencyBands = new List<float>(newFrequencyBands); }

    // https://docs.unity3d.com/6000.3/Documentation/Manual/Coroutines.html
    private System.Collections.IEnumerator SyncStartAllSources()
    {
        // This "yield" skips a frame, ensuring that "Start()" has been called
        //  on all loaded objects in the scene.
        yield return null;

        RACAudioSource[] sources = FindObjectsByType<RACAudioSource>(FindObjectsSortMode.None);

        // Add a 2.5 second safeguard. Gives all RACSources time to initialize before playing.
        double t = AudioSettings.dspTime + 2.5;

        foreach (RACAudioSource s in sources)
            if (s.WantsToPlayOnAwake)
                s.PlayScheduled(t);
    }
}
