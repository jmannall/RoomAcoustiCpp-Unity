
using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using System.Collections.Generic;

[AddComponentMenu("RoomAcoustiC++/Audio Manager")]
[RequireComponent(typeof(AudioSource))]

public class RACManager : MonoBehaviour
{
    // global singleton
    public static RACManager racManager = null;

    #region Plugin Interface

    //////////////////// Plugin interface ////////////////////

#if (UNITY_EDITOR)
    private const string DLLNAME = "RoomAcoustiCpp_x64";
#elif (UNITY_ANDROID)
    private const string DLLNAME = "libRoomAcoustiCpp";
#elif (UNITY_STANDALONE_WIN)
    private const string DLLNAME = "RoomAcoustiCpp_x64";
#else
    private const string DLLNAME = " ";
#endif

    // Load and Destroy

    [DllImport(DLLNAME)]
    private static extern bool RACInit(int fs, int numFrames, int numFDNChannels, float lerpFactor, float Q, float[] fBands, int numBands);

    [DllImport(DLLNAME)]
    private static extern void RACExit();

    [DllImport(DLLNAME)]
    private static extern bool RACLoadSpatialisationFiles(int hrtfResampling, string[] filePaths);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateSpatialisationMode(int id);

    // Image Source Model

    [DllImport(DLLNAME)]
    private static extern void RACUpdateIEMConfig(int order, int dir, bool refl, int diff, int refDiff, bool rev, float edgeLen);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateReverbTimeModel(int model);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateFDNModel(int id);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateDiffractionModel(int id);

    // Reverb

    [DllImport(DLLNAME)]
    private static extern void RACUpdateRoom(float volume, float[] dim, int numDimensions);

    [DllImport(DLLNAME)]
    private static extern void RACResetFDN();

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

    // Wall

    [DllImport(DLLNAME)]
    private static extern int RACInitWall(float[] vData, float[] absorption);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateWall(int id, float[] vData);

    [DllImport(DLLNAME)]
    private static extern int RACUpdateWallAbsorption(int id, float[] absorption);

    [DllImport(DLLNAME)]
    private static extern void RACRemoveWall(int id);

    [DllImport(DLLNAME)]
    private static extern void RACUpdatePlanesAndEdges();

    // Audio

    [DllImport(DLLNAME)]
    private static extern void RACSubmitAudio(int id, float[] input);

    [DllImport(DLLNAME)]
    private static extern bool RACProcessOutput();

    [DllImport(DLLNAME)]
    private static extern void RACGetOutputBuffer(ref IntPtr buffer);

    #endregion

    #region Parameters

    //////////////////// Parameters ////////////////////

    // File paths
    private string hrtfFile = " ";
    private string nearFieldFile = " ";
    private string ildFile = " ";
    private string resourcePath;

    public enum SpatMode { None, Performance, Quality }
    public enum ReverbTime { Sabine, Eyring }
    public enum FDNMatrix { Householder, RandomOrthogonal }
    public enum DiffractionModel { Attenuate, LowPass, UDFA, UDFAI, NNBest, NNSmall, UTD, BTM }
    public enum SourceDirectivity { Omni, Cardioid }
    public enum DirectSound { None, Check, AlwaysOn }
    public enum DiffractionSound { None, ShadowZone, AllZones }
    public enum OctaveBand { Third, Octave }

    [Serializable]
    public struct IEMConfig
    {
        [Range(0, 6)]
        [Tooltip("Set the maximum reflection/diffraction order.")]
        public int order;
        [Tooltip("None (no direct sound), Check (direct sound if source visible), Always On (no visibility check).")]
        public DirectSound direct;
        [Tooltip("Toggle the early reflections.")]
        public bool reflection;
        [Tooltip("Toggle diffraction of the direct sound. None (no diffracted sound), Shadow Zone (include only shadowed diffraction), All Zones (include specular diffraction).")]
        public DiffractionSound diffraction;
        [Tooltip("Toggle diffraction of the reflected sound. None (no diffracted sound), Shadow Zone (include only shadowed diffraction), All Zones (include specular diffraction).")]
        public DiffractionSound reflectionDiffraction;
        [Tooltip("Toggle the late reverberation.")]
        public bool lateReverb;

        [Range(0, 4)]
        [Tooltip("Set a minimum edge length threshold for diffraction modelling.")]
        public float minimumEdgeLength;

        public IEMConfig(int order, DirectSound direct, bool reflection, DiffractionSound diffraction, DiffractionSound reflectionDiffraction, bool lateReverb, float minimumEdgeLength)
        {
            this.order = order;
            this.direct = direct;
            this.reflection = reflection;
            this.diffraction = diffraction;
            this.reflectionDiffraction = reflectionDiffraction;
            this.lateReverb = lateReverb;
            this.minimumEdgeLength = minimumEdgeLength;
        }

        public static IEMConfig Default => new IEMConfig(
        order: 2,
        direct: DirectSound.Check,
        reflection: true,
        diffraction: DiffractionSound.ShadowZone,
        reflectionDiffraction: DiffractionSound.None,
        lateReverb: true,
        minimumEdgeLength: 0.0f
    );
    }

    // DSP Parameters
    private int sampleRate;
    private int numFrames;
    private int numChannels = 2;
    private int numFDNChannels = 12;

    [Header("Initial properties")]
    [SerializeField, Range(0.0f, 10.0f)]
    private float lerpFactor = 2.0f;

    [SerializeField]
    private List<float> fBands = new List<float> { 250.0f, 500.0f, 1000.0f, 2000.0f, 4000.0f };

    public List<float> fLimits { private set; get; } = new List<float>();

    [SerializeField]
    private OctaveBand fLimitBand = OctaveBand.Octave;

    [Range(0.1f, 2.0f)]
    private float Q = 0.98f;

    [SerializeField, Range(5, 45)]
    private int hrtfResamplingStep = 5;

    [SerializeField]
    private FDNMatrix fdnMatrix = FDNMatrix.Householder;

    private float[] outputBuffer;

    private float[] vData = new float[9];

    public bool isRunning { get; private set; }

    [Header("Acoustic Model Configuration")]
    [SerializeField]
    private IEMConfig iemConfig = IEMConfig.Default;

    [Header("Configurable properties")]
    [SerializeField, HideInInspector]
    private SpatMode spatialisationMode = SpatMode.Performance;

    [SerializeField, HideInInspector]
    private DiffractionModel diffractionModel = DiffractionModel.BTM;

    [SerializeField, HideInInspector]
    private ReverbTime reverbTimeModel = ReverbTime.Sabine;

    #endregion

    #region Unity Functions

    //////////////////// Unity Functions ////////////////////

    private void Awake()
    {
        Debug.AssertFormat(racManager == null, "More than one instance of the RACManager created! Singleton violated.");
        racManager = this;

        AudioConfiguration config = AudioSettings.GetConfiguration();
        numFrames = config.dspBufferSize;
        sampleRate = config.sampleRate;

        Debug.Log("Sample rate: " + sampleRate);

        outputBuffer = new float[numChannels * numFrames];

        hrtfFile = "Kemar_HRTF_ITD_48000Hz.3dti-hrtf";
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

        fBands.Sort();
        CreateFLimits();

        isRunning = RACInit(sampleRate, numFrames, numFDNChannels, lerpFactor, Q, fBands.ToArray(), fBands.Count);
        bool filesLoaded = RACLoadSpatialisationFiles(hrtfResamplingStep, filePaths);
        if (!filesLoaded)
        {
            Debug.Log("Failed to load HRTF files");
            isRunning = false;
        }
        else
            Debug.Log("HRTF files loaded");
        UpdateIEMConfig();
        UpdateSpatialisationMode();
        UpdateReverbTimeModel();
        UpdateFDNModel();
        UpdateDiffractionModel();
    }

    private void Start()
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
    }

    private void OnDestroy()
    {
        isRunning = false;
        RACExit();
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (isRunning)
        {
            int numSamples = data.Length;

            Profiler.BeginSample("Process Audio Output");
            bool success = RACProcessOutput();
            Profiler.EndSample();

            if (success)
            {
                // fetch the output buffer from the context
                GetOutputBuffer(ref outputBuffer);

                // choose the right length in case data buffer too big
                numSamples = (numSamples > outputBuffer.Length) ? outputBuffer.Length : numSamples;

                // memcpy the data over
                Array.Copy(outputBuffer, data, numSamples);
            }
            else
            {
                // fill output with 0
                for (int i = 0; i < numSamples; ++i)
                    data[i] = 0.0f;
            }
        }
    }

    #endregion

    #region Plugin Function Calls

    //////////////////// Plugin Function Calls ////////////////////

    public static void UpdateSpatialisationMode()
    {
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
        racManager.spatialisationMode = mode;
        UpdateSpatialisationMode();
    }

    public static void UpdateReverbTimeModel()
    {
        switch (racManager.reverbTimeModel)
        {
            case ReverbTime.Sabine:
                { RACUpdateReverbTimeModel(0); break; }
            case ReverbTime.Eyring:
                { RACUpdateReverbTimeModel(1); break; }
        }
    }

    public static void UpdateReverbTimeModel(ReverbTime model)
    {
        racManager.reverbTimeModel = model;
        UpdateReverbTimeModel();
    }

    // IEM Config

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

    static int SelectDiffractionMode(DiffractionSound diff)
    {
        switch (diff)
        {
            case DiffractionSound.None:
                { return 0; }
            case DiffractionSound.ShadowZone:
                { return 1; }
            case DiffractionSound.AllZones:
                { return 2; }
            default:
                { return 0; }
        }
    }

    public static void UpdateIEMConfig()
    {
        Profiler.BeginSample("Update IEM");
        int direct = SelectDirectMode(racManager.iemConfig.direct);
        int diffraction = SelectDiffractionMode(racManager.iemConfig.diffraction);
        int reflectionDiffraction = SelectDiffractionMode(racManager.iemConfig.reflectionDiffraction);
        RACUpdateIEMConfig(racManager.iemConfig.order, direct, racManager.iemConfig.reflection, diffraction, reflectionDiffraction, racManager.iemConfig.lateReverb, racManager.iemConfig.minimumEdgeLength);
        Profiler.EndSample();
    }

    public static void UpdateIEMConfig(IEMConfig config)
    {
        racManager.iemConfig = config;
        UpdateIEMConfig();
    }

    private static void UpdateFDNModel()
    {
        switch (racManager.fdnMatrix)
        {
            case FDNMatrix.Householder:
                { RACUpdateFDNModel(0); break; }
            case FDNMatrix.RandomOrthogonal:
                { RACUpdateFDNModel(1); break; }
        }
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

    // Reverb

    public static void UpdateRoom(float volume, float[] dim, int numDimensions)
    {
        Profiler.BeginSample("Set FDN");
        RACUpdateRoom(volume, dim, numDimensions);
        Profiler.EndSample();
    }

    public static void ResetFDN()
    {
        Profiler.BeginSample("Reset FDN");
        RACResetFDN();
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
            case SourceDirectivity.Cardioid:
                { RACUpdateSourceDirectivity(id, 1); break; }
        }
    }

    public static void RemoveSource(int id)
    {
        Profiler.BeginSample("Remove Source");
        RACRemoveSource(id);
        Profiler.EndSample();
    }

    // Wall

    public static void UpdateVData(ref Vector3[] vertices)
    {
        racManager.vData[0] = vertices[0].x;
        racManager.vData[1] = vertices[0].y;
        racManager.vData[2] = vertices[0].z;
        racManager.vData[3] = vertices[1].x;
        racManager.vData[4] = vertices[1].y;
        racManager.vData[5] = vertices[1].z;
        racManager.vData[6] = vertices[2].x;
        racManager.vData[7] = vertices[2].y;
        racManager.vData[8] = vertices[2].z;
    }

    public static int InitWall(Vector3 normal, ref Vector3[] vertices, ref float[] absorption)
    {
        if (vertices.Length != 3)
        {
            Debug.LogError("Wall must have 3 vertices");
            return -1;
        }

        UpdateVData(ref vertices);

        Profiler.BeginSample("Init Wall");
        int id = RACInitWall(racManager.vData, absorption);
        Profiler.EndSample();
        return id;
    }

    public static void UpdateWall(int id, Vector3 normal, ref Vector3[] vertices)
    {
        if (vertices.Length != 3)
        {
            Debug.LogError("Wall must have 3 vertices");
            return;
        }

        UpdateVData(ref vertices);

        Profiler.BeginSample("Update Wall");
        RACUpdateWall(id, racManager.vData);
        Profiler.EndSample();
    }

    public static void UpdateWallAbsorption(int id, ref float[] absorption)
    {
        Profiler.BeginSample("Update Wall Absorption");
        RACUpdateWallAbsorption(id, absorption);
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
        // fetch the buffer
        IntPtr result = IntPtr.Zero;

        Profiler.BeginSample("Get Output");
        RACGetOutputBuffer(ref result);

        // copy the buffer as a float array
        Marshal.Copy(result, buffer, 0, racManager.numChannels * racManager.numFrames);
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

    private void CreateFLimits()
    {
        if (fLimitBand == OctaveBand.Octave)
            fLimits.Add(fBands[0] * Mathf.Pow(2, -0.5f));
        else
            fLimits.Add(fBands[0] * Mathf.Pow(2, -(1.0f / 6.0f)));

        for (int i = 0; i < fBands.Count - 1; ++i)
            fLimits.Add(Mathf.Sqrt(fBands[i] * fBands[i + 1]));

        if (fLimitBand == OctaveBand.Octave)
            fLimits.Add(fBands[fBands.Count - 1] * Mathf.Pow(2, 0.5f));
        else
            fLimits.Add(fBands[fBands.Count - 1] * Mathf.Pow(2, 1.0f / 6.0f));
    }

    public static void DisableAudioProcessing() { racManager.isRunning = false; }

    public static void EnableAudioProcessing() { racManager.isRunning = true; }
}
