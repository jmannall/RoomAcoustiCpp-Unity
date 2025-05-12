
using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using System.Collections.Generic;
using System.Collections;

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
    private static extern void RACUpdateIEMConfig(int dir, int refl, int diffShadow, int diffSpecular, bool rev, float edgeLen);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateReverbTime(float[] T60);

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
    private static extern void RACSetHeadphoneEQ(float[] leftIR, float[] rightIR, int irLength);

    [DllImport(DLLNAME)]
    private static extern void RACSubmitAudio(int id, float[] input);

    [DllImport(DLLNAME)]
    private static extern bool RACProcessOutput();

    [DllImport(DLLNAME)]
    private static extern bool RACProcessOutput_MOD_ART(float[] input);

    [DllImport(DLLNAME)]
    private static extern void RACGetOutputBuffer(ref IntPtr buffer);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateImpulseResponseMode(float lerpFactor, bool mode);

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
    public enum DiffractionModel { Attenuate, LowPass, UDFA, UDFAI, NNBest, NNSmall, UTD, BTM }
    public enum SourceDirectivity { Omni, Subcardioid, Cardioid, Supercardioid, Hypercardioid, Bidirectional, Genelec, GenelecDTF }
    public enum DirectSound { None, Check, AlwaysOn }
    public enum DiffractionSound { None, ShadowZone, AllZones }
    public enum OctaveBand { Third, Octave }

    [Serializable]
    public struct IEMConfig
    {
        [Tooltip("None (no direct sound), Check (direct sound if source visible), Always On (no visibility check).")]
        public DirectSound direct;
        [Range(0, 6)]
        [Tooltip("Set the maximum number of reflections in reflection only paths.")]
        public int reflectionOrder;
        [Range(0, 6)]
        [Tooltip("Set the maximum number of reflections or diffractions shadowed diffraction paths.")]
        public int shadowDiffractionOrder;
        [Range(0, 6)]
        [Tooltip("Set the maximum number of reflections or diffractions specular diffraction paths.")]
        public int specularDiffractionOrder;
        [Tooltip("Toggle the late reverberation.")]
        public bool lateReverb;

        [Range(0, 4)]
        [Tooltip("Set a minimum edge length threshold for diffraction modelling.")]
        public float minimumEdgeLength;

        public IEMConfig(DirectSound direct, int reflOrder, int diffShadowOrder, int diffSpecularOrder, bool lateReverb, float minimumEdgeLength)
        {
            this.direct = direct;
            this.reflectionOrder = reflOrder;
            this.shadowDiffractionOrder = diffShadowOrder;
            this.specularDiffractionOrder = diffSpecularOrder;
            this.lateReverb = lateReverb;
            this.minimumEdgeLength = minimumEdgeLength;
        }

        public static IEMConfig Default => new IEMConfig(
        direct: DirectSound.Check,
        reflOrder: 2,
        diffShadowOrder: 1,
        diffSpecularOrder: 0,
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

    [SerializeField, Range(1, 45)]
    private int hrtfResamplingStep = 5;

    [SerializeField]
    private FDNMatrix fdnMatrix = FDNMatrix.Householder;

    private float[] outputBuffer;

    private float[] vData = new float[9];

    public bool isRunning { get; private set; }

    private static bool noHRTFFiles = false;

    [Header("Acoustic Model Configuration")]
    [SerializeField]
    private IEMConfig iemConfig = IEMConfig.Default;

    [Header("Configurable properties")]
    [SerializeField, HideInInspector]
    private SpatMode spatialisationMode = SpatMode.Performance;

    [SerializeField, HideInInspector]
    private DiffractionModel diffractionModel = DiffractionModel.BTM;

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

    private void Awake()
    {
        Debug.AssertFormat(racManager == null, "More than one instance of the RACManager created! Singleton violated.");
        racManager = this;

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

        fBands.Sort();
        CreateFLimits();

        isRunning = RACInit(sampleRate, numFrames, numFDNChannels, lerpFactor, Q, fBands.ToArray(), fBands.Count);
        bool filesLoaded = RACLoadSpatialisationFiles(hrtfResamplingStep, filePaths);
        if (!filesLoaded)
        {
            Debug.LogError("Failed to load HRTF files");
            racManager.spatialisationMode = SpatMode.None;
            noHRTFFiles = true;
        }
        else
            Debug.Log("HRTF files loaded");

        LoadHeadphoneEQ();

        UpdateIEMConfig();
        UpdateSpatialisationMode();
        UpdateReverbTimeModel();
        if (reverbTimeModel == ReverbTime.Custom)
            UpdateReverbTime();
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
        interleavedData = new float[numFDNChannels * numFrames];
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
                Array.Fill(data, 0.0f);
        }
    }

    #endregion

    #region Plugin Function Calls

    //////////////////// Plugin Function Calls ////////////////////

    public static void UpdateSpatialisationMode()
    {
        if (noHRTFFiles)
        {
            racManager.spatialisationMode = SpatMode.None;
            return;
        }

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

    public static void UpdateReverbTime()
    {
        RACUpdateReverbTime(racManager.T60.ToArray());
    }

    public static void UpdateReverbTimeModel()
    {
        switch (racManager.reverbTimeModel)
        {
            case ReverbTime.Sabine:
                { RACUpdateReverbTimeModel(0); break; }
            case ReverbTime.Eyring:
                { RACUpdateReverbTimeModel(1); break; }
            case ReverbTime.Custom:
                { RACUpdateReverbTimeModel(2); break; }
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
        RACUpdateIEMConfig(direct, racManager.iemConfig.reflectionOrder, racManager.iemConfig.shadowDiffractionOrder, racManager.iemConfig.specularDiffractionOrder, racManager.iemConfig.lateReverb, racManager.iemConfig.minimumEdgeLength);
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
            case SourceDirectivity.Genelec: 
                { RACUpdateSourceDirectivity(id, 6); break; }
            case SourceDirectivity.GenelecDTF:
                { RACUpdateSourceDirectivity(id, 7); break; }
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

    public static int InitWall(ref Vector3[] vertices, ref float[] absorption)
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

    public static void UpdateWall(int id, ref Vector3[] vertices)
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
        // fetch the buffer
        IntPtr result = IntPtr.Zero;

        Profiler.BeginSample("Get Output");
        RACGetOutputBuffer(ref result);

        // copy the buffer as a float array
        Marshal.Copy(result, buffer, 0, racManager.numChannels * racManager.numFrames);
        Profiler.EndSample();
    }

    public static void UpdateImpulseResponseMode(bool mode)
    {
        RACUpdateImpulseResponseMode(racManager.lerpFactor, mode);
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
}
