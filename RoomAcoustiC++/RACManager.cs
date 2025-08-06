
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
#if RAC_Default
    private const string DLLNAME = "RoomAcoustiCpp_x64";
# elif RAC_Debug
    private const string DLLNAME = "RoomAcoustiCpp_Debug_x64";
# elif RAC_Profile
    private const string DLLNAME = "RoomAcoustiCpp_Profile_x64";
# elif RAC_ProfileDetailed
    private const string DLLNAME = "RoomAcoustiCpp_ProfileDetailed_x64";
#else
    private const string DLLNAME = "RoomAcoustiCpp_x64";
#endif
#elif (UNITY_ANDROID)
    private const string DLLNAME = "libRoomAcoustiCpp";
#elif (UNITY_STANDALONE_WIN)
    private const string DLLNAME = "RoomAcoustiCpp_x64";
#else
    private const string DLLNAME = " ";
#endif

    // Load and Destroy

    [DllImport(DLLNAME)]
    private static extern bool RACInit(int fs, int numFrames, int numReverbSources, float lerpFactor, float Q, [In] float[] frequencyBands, int numFrequencyBands);

    [DllImport(DLLNAME)]
    private static extern void RACExit();

    [DllImport(DLLNAME)]
    private static extern bool RACLoadSpatialisationFiles(int hrtfResampling, string[] filePaths);

    [DllImport(DLLNAME)]
    private static extern void RACSetHeadphoneEQ([In] float[] leftIR, [In] float[] rightIR, int irLength);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateSpatialisationMode(int id);

    // Image Source Model

    [DllImport(DLLNAME)]
    private static extern void RACUpdateIEMConfig(int direct, int reflOrder, int shadowDiffOrder, int specularDiffOrder, bool lateReverb, float minEdgeLength);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateReverbTime([In] float[] T60);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateReverbTimeModel(int id);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateDiffractionModel(int id);

    // Reverb

    [DllImport(DLLNAME)]
    private static extern bool RACInitLateReverb(float volume, [In] float[] dimensions, int numDimensions, int id);

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
    private static extern int RACInitWall([In] float[] vertices, [In] float[] absorption);

    [DllImport(DLLNAME)]
    private static extern void RACUpdateWall(int id, [In] float[] vertices);

    [DllImport(DLLNAME)]
    private static extern int RACUpdateWallAbsorption(int id, [In] float[] absorption);

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
    private static extern void RACUpdateImpulseResponseMode(bool mode);

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
    public enum SourceDirectivity { Omni, Subcardioid, Cardioid, Supercardioid, Hypercardioid, Bidirectional, Genelec8020c, Genelec8020cDTF, QSC_K8 }
    public enum DirectSound { None, Check, AlwaysOn }
    public enum DiffractionSound { None, ShadowZone, AllZones }

    [Serializable]
    public struct IEMConfig
    {
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

    [Header("Initial properties")]
    [SerializeField, Range(0.0f, 10.0f)]
    private float lerpFactor = 2.0f;

    [SerializeField]
    public List<float> frequencyBands = new List<float> { 250.0f, 500.0f, 1000.0f, 2000.0f, 4000.0f };

    [Range(0.1f, 2.0f)]
    private float Q = 0.98f;

    [SerializeField, Range(1, 45)]
    private int hrtfResamplingStep = 5;

    [SerializeField, Range(0, 32)]
    private int numReverbSources = 12;

    [SerializeField]
    private FDNMatrix fdnMatrix = FDNMatrix.Householder;

    private float[] outputBuffer;

    private float[] vertices = new float[9];

    public bool isRunning { get; private set; }

    private static bool noHRTFFiles = true;

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

        isRunning = RACInit(sampleRate, numFrames, numReverbSources, lerpFactor, Q, frequencyBands.ToArray(), frequencyBands.Count);
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
        if (reverbTimeModel == ReverbTime.Custom)
            UpdateReverbTime();
        else
            UpdateReverbTimeModel();
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
        interleavedData = new float[numReverbSources * numFrames];
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

    public static void UpdateReverbTime()
    {
        if (racManager.T60.Count < racManager.frequencyBands.Count)
        {
            int oldSize = racManager.T60.Count;
            for (int i = oldSize; i < racManager.frequencyBands.Count; i++)
                racManager.T60.Add(1.0f); // Default value for new elements
        }
        else if (racManager.T60.Count > racManager.frequencyBands.Count)
            racManager.T60.RemoveRange(racManager.frequencyBands.Count, racManager.T60.Count - racManager.frequencyBands.Count);

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

    public static void UpdateIEMConfig()
    {
        int direct = SelectDirectMode(racManager.iemConfig.direct);

        Profiler.BeginSample("Update IEM");
        RACUpdateIEMConfig(direct, racManager.iemConfig.reflectionOrder, racManager.iemConfig.shadowDiffractionOrder, racManager.iemConfig.specularDiffractionOrder, racManager.iemConfig.lateReverb, racManager.iemConfig.minimumEdgeLength);
        Profiler.EndSample();
    }

    public static void UpdateIEMConfig(IEMConfig config)
    {
        racManager.iemConfig = config;
        UpdateIEMConfig();
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

    public static void InitLateReverb(float volume, float[] dimensions)
    {
        Profiler.BeginSample("Set FDN");
        RACInitLateReverb(volume, dimensions, dimensions.Length, (int)racManager.fdnMatrix);
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

    public static int InitWall(ref Vector3[] vertices, ref float[] absorption)
    {
        if (vertices.Length != 3)
        {
            Debug.LogError("Wall must have 3 vertices");
            return -1;
        }

        UpdateVData(ref vertices);

        Profiler.BeginSample("Init Wall");
        int id = RACInitWall(racManager.vertices, absorption);
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
        Profiler.BeginSample("Get Output");
        RACGetOutputBuffer(buffer);
        Profiler.EndSample();
    }

    public static void UpdateImpulseResponseMode(bool mode)
    {
        RACUpdateImpulseResponseMode(mode);
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
}
