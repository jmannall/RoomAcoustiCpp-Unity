using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IRController : MonoBehaviour
{
    [SerializeField]
    private float spacing = 1.0f;

    [SerializeField, Tooltip("Rotates around the y - axis")]
    private float rotationStep = 0.0f;

    private float cubeSize = 0.1f;

    [SerializeField]
    private float listenerHeight = 1.6f;

    int activeSource = -1;
    int activeListener = -1;

    [SerializeField, Range(0.0f, 10.0f)]
    private float impulseResponseLength = 1.0f;

    private float[] inputBuffer;
    private float[] outputBuffer;
    private float[] outputSignal;
    int numBuffers;
    int numSamples;

    private static bool doIRs = false;

    [SerializeField, HideInInspector]
    private string sceneName = "";

    [SerializeField]
    private string runName = "Run1";

    private string filePath;
    private string configName = "";
    private string areaName = "";
    private string spatName = "";

    [SerializeField]
    private string irFilePath;
    private float[] impulseResponse;

    [SerializeField]
    private RACAudioSource racSource;
    private Transform listenerTransform;

    [SerializeField]
    private List<Transform> listeners;

    [SerializeField]
    private List<Transform> sources;

    [SerializeField]
    private List<RACManager.IEMConfig> configs;

    [SerializeField]
    private List<RACManager.SpatMode> spatModes;
    bool recordMono = false;

    private List<Transform> transforms = new List<Transform>();

    private IEnumerator transformEnumerator;
    private IEnumerator spatModeEnumerator;
    private IEnumerator configEnumerator;
    private IEnumerator sourceEnumerator;
    private IEnumerator listenerEnumerator;

    private bool useTransforms = false;
    bool nextSource = true;
    bool nextConfig = true;
    bool nextSpatMode = true;
    bool nextTransform = true;

    StreamWriter streamWriter;
    bool expectResidues;
    string wavPath;

    static bool iemStarted = false;
    static bool iemCompleted = false;
    static bool rtmStarted = false;
    static bool rtmCompleted = false;
    static void OnIEMStarted()
    {
        iemStarted = true;
        // Reset `iemCompleted` in preparation for the next while loop.
        // N.B.: DO NOT reset `iemCompleted` outside of this function, it may cause a deadlock.
        iemCompleted = false;
    }
    static void OnIEMCompleted()
    {
        // N.B.: DO NOT reset `iemStarted` here.
        // If RTM has not yet started, while IEM has already finished,
        // setting `iemStarted = false` here would deadlock the while loop.
        iemCompleted = true;
    }
    static void OnRTMStarted()
    {
        rtmStarted = true;
        // Reset `rtmCompleted` in preparation for the next while loop.
        // N.B.: DO NOT reset `rtmCompleted` outside of this function, it may cause a deadlock.
        rtmCompleted = false;
    }
    static void OnRTMCompleted()
    {
        // N.B.: DO NOT reset `rtmStarted` here.
        // If IEM has not yet started, while RTM has already finished,
        // setting `rtmStarted = false` here would deadlock the while loop.
        rtmCompleted = true;
    }
    // If isSource, sourceIndex contains the source ID, otherwise, sourceIndex contains the reverb direction index
    static void OnResidueCallback(float residue, bool isSource, int sourceIndex, int slopeIndex)
    {
        // Write to file, provided expectResidues is true and streamWriter is available
        if (irController != null && irController.streamWriter != null && irController.expectResidues)
            irController.streamWriter.WriteLine(
                isSource.ToString() + ", " +
                sourceIndex.ToString() + ", " +
                slopeIndex.ToString() + ", " +
                residue.ToString() + ";");
        
        /*
        if (isSource)
            Debug.Log(
                "Received source residue." +
                " Source idx " + sourceIndex.ToString() + "," +
                " slope idx " + slopeIndex.ToString() + ";" +
                " Residue value: " + residue.ToString());
        else
            Debug.Log(
                "Received listener residue." +
                " Direction idx " + sourceIndex.ToString() + "," +
                " slope idx " + slopeIndex.ToString() + ";" +
                " Residue value: " + residue.ToString());
        */
    }

    private static IRController irController;

    private void OnValidate()
    {
        Debug.AssertFormat(irController == null, "More than one instance of the IRController created! Singleton violated.");
        irController = this;
    }

    // Start is called before the first frame update
    private void Awake()
    {
        DebugCPP.RegisterIEMStartCallback(OnIEMStarted);
        DebugCPP.RegisterIEMEndCallback(OnIEMCompleted);
        DebugCPP.RegisterRTMStartCallback(OnRTMStarted);
        DebugCPP.RegisterRTMEndCallback(OnRTMCompleted);
        DebugCPP.RegisterResidueCallback(OnResidueCallback);

        int numFrames = AudioSettings.GetConfiguration().dspBufferSize;
        numSamples = Mathf.CeilToInt(impulseResponseLength * AudioSettings.outputSampleRate);
        numBuffers = Mathf.CeilToInt(numSamples / numFrames);

        inputBuffer = new float[numFrames];
        outputBuffer = new float[2 * numFrames];
        outputSignal = new float[numSamples];

        UpdateSceneName();

        cubeSize = Mathf.Min(spacing / 2.0f, cubeSize);
    }

    private void Start()
    {
        UpdateSceneName();

        listenerTransform = FindAnyObjectByType<RACAudioListener>().transform;
        if (listenerTransform == null)
            Debug.LogError("RACAudioListener not found");

        if (string.IsNullOrEmpty(irFilePath))
            impulseResponse = new float[1] { 1.0f };
        else
            impulseResponse = ReadCSV(irFilePath);

        if (listeners.Count > 0)
            AddListenerRotations();
        else
        {
            useTransforms = true;
            Transform[] transformStore = GetComponentsInChildren<Transform>();
            for (int i = 1; i < transformStore.Length; i++)
                transforms.Add(transformStore[i]);
        }

        if (sources.Count == 0)
            sources.Add(racSource.transform);

        transformEnumerator = ProcessTransforms();
        spatModeEnumerator = ProcessSpatModes();
        configEnumerator = ProcessConfigs();
        sourceEnumerator = ProcessSources();
        listenerEnumerator = ProcessListeners();
    }

    private void OnDisable()
    {
        DebugCPP.UnregisterIEMStartCallback();
        DebugCPP.UnregisterIEMEndCallback();
        DebugCPP.UnregisterRTMStartCallback();
        DebugCPP.UnregisterRTMEndCallback();
        DebugCPP.UnregisterResidueCallback();
    }

    private void Update()
    {
        if (!doIRs)
        {
            if (listeners.Count > 0)
                useTransforms = false;
            return;
        }

        if (nextTransform)
        {
            if (useTransforms && !transformEnumerator.MoveNext())
            {
                transformEnumerator = ProcessTransforms();
                RACManager.UpdateImpulseResponseMode(false);
                RACManager.EnableAudioProcessing();
                doIRs = false;
                Debug.Log("All IR runs complete");
                return;
            }
        }

        nextTransform = false;

        if (nextSpatMode)
        {
            if (!spatModeEnumerator.MoveNext())
            {
                nextTransform = true;
                if (!useTransforms)
                    useTransforms = true;
                spatModeEnumerator = ProcessSpatModes();
                return;
            }
        }

        nextSpatMode = false;

        if (nextConfig)
        {
            if (!configEnumerator.MoveNext())
            {
                nextSpatMode = true;
                configEnumerator = ProcessConfigs();
                Debug.Log("Next spat mode");
                return;
            }
        }

        nextConfig = false;

        if (nextSource)
        {
            if (!sourceEnumerator.MoveNext())
            {
                nextConfig = true;
                sourceEnumerator = ProcessSources();
                Debug.Log("Next config");
                return;
            }
        }

        nextSource = false;

        if (!listenerEnumerator.MoveNext())
        {
            nextSource = true;
            listenerEnumerator = ProcessListeners();
            Debug.Log("Next source");
            return;
        }
    }

    void OnDestroy()
    {
        // TODO: Why not unregister the callbacks here?
        listeners.Clear();
        if (streamWriter != null)
            streamWriter.Close();
    }

    public void UpdateSceneName()
    {
        if ((RACMeshLoader.racMeshLoader != null) && !string.IsNullOrEmpty(RACMeshLoader.racMeshLoader.GetCurrentSelection()))
            sceneName = RACMeshLoader.racMeshLoader.GetCurrentSelection();
        else
            sceneName = SceneManager.GetActiveScene().name;

        filePath = Application.persistentDataPath + "/ImpulseResponses/" + sceneName + "/" + runName;
        if (!Directory.Exists(filePath))
            Directory.CreateDirectory(filePath);
    }

    public string GetSceneName()
    {
        return sceneName;
    }

    void UpdateStreamWriter(string fileName)
    {
        if (streamWriter != null)
            streamWriter.Close();
        FileStream file = new FileStream(filePath + "/" + fileName + ".csv", FileMode.Create);
        streamWriter = new StreamWriter(file);
    }

    // Enumerator for the transforms foreach loop
    private IEnumerator ProcessTransforms()
    {
        foreach (var transform in transforms)
        {
            areaName = transform.gameObject.name;
            LocateListenerPositions(transform);
            yield return null; // Pause and resume in the next frame
            ClearListenerPositions(transform);
        }
    }

    // Enumerator for the spat mode foreach loop
    private IEnumerator ProcessSpatModes()
    {
        foreach (var spatMode in spatModes)
        {
            switch (spatMode)
            {
                case RACManager.SpatMode.None:
                    recordMono = true;
                    break;
                default:
                    recordMono = false;
                    break;
            }

            spatName = spatMode.ToString();
            RACManager.UpdateSpatialisationMode(spatMode);
            yield return null; // Pause and resume in the next frame
        }
    }

    // Enumerator for the config foreach loop
    private IEnumerator ProcessConfigs()
    {
        int idx = 0;
        foreach (var config in configs)
        {
            RACManager.UpdateIEMConfig(config);
            configName = idx.ToString();
            idx++;
            yield return null; // Pause and resume in the next frame
        }
    }

    // Enumerator for the source foreach loop
    private IEnumerator ProcessSources()
    {
        foreach (var source in sources)
        {
            activeSource++;
            racSource.transform.position = source.position;
            racSource.transform.rotation = source.rotation;
            if (!useTransforms)
                areaName = source.gameObject.name;
            yield return null; // Pause and resume in the next frame
        }
        activeSource = -1;
    }

    // Enumerator for the listener foreach loop
    private IEnumerator ProcessListeners()
    {
        RACManager.ProcessOutput();
        foreach (var listener in listeners)
        {
            activeListener++;

            UpdateStreamWriter("Spat_" + spatName + "_Config_" + configName + "_Src_" + activeSource.ToString() + "_Lst_" + activeListener.ToString() + "_Residues.csv");
            irController.streamWriter.WriteLine("isSource, sourceIndex, slopeIndex, residue;");
            expectResidues = true;

            RACManager.UpdateListener(listener.position, listener.rotation);
            listenerTransform.position = listener.position;
            listenerTransform.rotation = listener.rotation;
            
            racSource.RestartSource();

            int countStart = 0;
            int countEnd = 0;
            iemStarted = false;
            rtmStarted = false;
            // Wait for confirmation that both IEM and RTM have begun fresh loops.
            while (!iemStarted || !rtmStarted)
            {
                countStart++;
                if (countStart > 100)
                {
                    if (!iemStarted)
                        Debug.LogError("Failed to start a fresh loop on the IEM thread.");
                    if (!rtmStarted)
                        Debug.LogError("Failed to start a fresh loop on the RTM thread.");
                    break;
                }
                yield return null;
            }
            // N.B.: `iemCompleted` and `rtmCompleted` have been reset as part of the function calls `OnIEMStarted` and `OnRTMStarted`.
            // DO NOT manually reset `iemCompleted` nor `rtmCompleted` at this point.
            // One thread may already have finished before the other one started.

            // Wait for confirmation that both IEM and RTM have finished their loops.
            while (!iemCompleted || !rtmCompleted)
            {
                countEnd++;
                if (countEnd > 100)
                {
                    if (!iemCompleted)
                        Debug.LogError("Failed to complete a fresh loop on the IEM thread.");
                    if (!rtmCompleted)
                        Debug.LogError("Failed to complete a fresh loop on the RTM thread.");
                    break;
                }
                yield return null;
            }
            //Debug.Log("Time for IEM and RTM to start fresh loops: " + countStart.ToString() + " frames");
            //Debug.Log("Time for IEM and RTM to complete fresh loops: " + countEnd.ToString() + " frames");
            //Debug.Log("Time for IEM and RTM to run fresh loops: " + (countStart + countEnd).ToString() + " frames");

            expectResidues = false;

            RACManager.SubmitAudio(racSource.id, ref inputBuffer);
            RACManager.ResetFDN();

            //RACManager.ProcessOutput();
            //bool success = RACManager.ProcessOutput();
            //Debug.Log("RACManager.ProcessOutput() returned " + success.ToString());

            // Wait for confirmation that the DSP thread has run a fresh loop, or else the FDNs might get reset after the start of the IR recording.
            // TODO: Use flags and callbacks like for the other threads.
            int countReset = 0;
            int countFrames = 0;
            while (countFrames < 100)
            {
                if (RACManager.ProcessOutput())
                {
                    RACManager.GetOutputBuffer(ref outputBuffer);

                    countReset += 1;
                    if (countReset > 3)
                        break;
                }

                countFrames++;
                if (countFrames > 99)
                {
                    Debug.LogError("Failed to reset the DSP thread.");
                    break;
                }
                yield return null;
            }
            //Debug.Log("Time to reset the DSP: " + countFrames.ToString() + " frames");

            UpdateStreamWriter("Spat_" + spatName + "_Config_" + configName + "_Src_" + activeSource.ToString() + "_Lst_" + activeListener.ToString() + "_IR.csv");

            inputBuffer[0] = 1.0f;
            ProcessAudioBuffer(0);
            inputBuffer[0] = 0.0f;
            for (int i = 1; i < numBuffers; i++)
                ProcessAudioBuffer(i);
            streamWriter.Write("0, 0\n");
            streamWriter.Flush();

            wavPath = filePath + "/Spat_" + spatName + "_Config_" + configName + "_Src_" + activeSource.ToString() + "_Lst_" + activeListener.ToString() + "_IR.wav";
            WavWriter.Save(wavPath, outputSignal, AudioSettings.outputSampleRate, channels: 2, writeFloat32: true, writeMono: recordMono);
            //Debug.Log("<color=green>WAV saved to: " + wavPath + "</color>");

            racSource.Stop();

            if (!doIRs)
                yield break;

            yield return null; // Pause and resume in the next frame
        }
        activeListener = -1;
    }

    public void StartIRRun()
    {
        RACManager.DisableAudioProcessing();
        RACManager.UpdateImpulseResponseMode(true);

        WriteRunSettings();

        doIRs = true;
        Debug.Log("Start IR Runs: " + doIRs);
    }

    public bool IsRunning() { return doIRs; }

    public void EndRun()
    {
        doIRs = false;
        RACManager.UpdateImpulseResponseMode(false);
        RACManager.EnableAudioProcessing();
        Debug.Log("End IR Run Early");
    }

    void LocateListenerPositions(Transform transform)
    {
        Debug.Log("Locate Listener Positions");

        Vector2 scale = new Vector2(transform.localScale.x, transform.localScale.y);
        Vector2 position = new Vector2(transform.localPosition.x, transform.localPosition.z);
        Vector2 corner = position - scale / 2.0f;

        Vector2 numSources = new Vector2(Mathf.Floor(scale.x / spacing) + 1.0f, Mathf.Floor(scale.y / spacing) + 1.0f);
        Vector2 offset = (scale - (numSources - Vector2.one) * spacing) / 2.0f;

        List<float> xPositions = new List<float>();
        for (int i = 0; i < numSources.x; i++)
            xPositions.Add(corner.x + offset.x + i * spacing);

        List<float> yPositions = new List<float>();
        for (int i = 0; i < numSources.y; i++)
            yPositions.Add(corner.y + offset.y + i * spacing);

        Vector3 currentPosition = new Vector3(0.0f, listenerHeight, 0.0f);

        foreach (float x in xPositions)
        {
            currentPosition.x = x;
            foreach (float y in yPositions)
            {
                GameObject emptyGO = new GameObject();
                emptyGO.transform.parent = transform;
                Transform newTransform = emptyGO.transform;
                newTransform.rotation = Quaternion.identity;

                currentPosition.z = y;
                newTransform.position = currentPosition;
                listeners.Add(newTransform);
            }
        }
    }

    void ClearListenerPositions(Transform transform)
    {
        listeners.Clear();
        if (transform.childCount > 0)
        {
            Transform[] children = transform.gameObject.GetComponentsInChildren<Transform>();
            for (int i = 1; i < children.Length; i++)
                Destroy(children[i].gameObject);
        }
    }
    
    void AddListenerRotations()
    {
        if (rotationStep == 0.0f)
            return;

        int numExtraListeners = Mathf.FloorToInt(360.0f / rotationStep);

        var store = new List<Transform>(listeners);
        listeners.Clear();
        foreach (Transform original in store)
        {
            listeners.Add(original);
            for (int j = 1; j < numExtraListeners; j++)
            {
                GameObject emptyGO = new GameObject();
                emptyGO.transform.parent = original;
                Transform newTransform = emptyGO.transform;
                Vector3 rot = original.eulerAngles;
                rot.y -= j * rotationStep;
                newTransform.position = original.position;
                newTransform.eulerAngles = rot;
                listeners.Add(newTransform);
            }
        }
    }

    void ProcessAudioBuffer(int bufferNumber)
    {
        int inputIdx = bufferNumber * inputBuffer.Length;
        int outputIdx = bufferNumber * outputBuffer.Length;

        for (int i = 0; i < Mathf.Min(inputBuffer.Length, impulseResponse.Length - inputIdx); i++)
            inputBuffer[i] = impulseResponse[inputIdx + i];

        RACManager.SubmitAudio(racSource.id, ref inputBuffer);
        bool success = RACManager.ProcessOutput();
        if (success)
            RACManager.GetOutputBuffer(ref outputBuffer);

        for (int i = 0; i < Mathf.Min(outputBuffer.Length, outputSignal.Length - outputIdx); ++i)
            outputSignal[outputIdx + i] = outputBuffer[i];

        if (recordMono)
        {
            for (int i = 0; i < outputBuffer.Length; i += 2)
                WriteSample(outputBuffer[i]);
        }
        else
        {
            foreach (float sample in outputBuffer)
                WriteSample(sample);
        }

        for (int i = 0; i < Mathf.Min(inputBuffer.Length, impulseResponse.Length - inputIdx); i++)
            inputBuffer[i] = 0.0f;
    }

    void WriteSample(float input)
    {
        streamWriter.Write(input.ToString() + ", ");
    }

    // TODO: Add summary everywhere it's useful.
    /// <summary>
    /// Write all information related to the current IR run settings.
    /// </summary>
    void WriteRunSettings()
    {
        UpdateStreamWriter("Run_settings");

        streamWriter.WriteLine("Sample rate: " + AudioSettings.outputSampleRate);

        // Write configs settings
        streamWriter.WriteLine("\nConfigurations");
        int idx = 0;
        // To extract all members of a struct, see https://stackoverflow.com/a/7613806 and https://stackoverflow.com/a/2762679
        var fields = typeof(RACManager.IEMConfig).GetFields();
        foreach (var config in configs)
        {
            streamWriter.WriteLine("\tConfig_" + idx);
            foreach (var field in fields)
                streamWriter.WriteLine("\t\t" + field.Name + " " + field.GetValue(config));
            streamWriter.Flush();
            idx++;
        }

        // Write sources settings
        streamWriter.WriteLine("\nSource positions");
        int scrIdx = 0;
        foreach (var source in sources)
        {
            streamWriter.WriteLine("\tSrc_" + scrIdx + ": " + source.position.x + ", " + source.position.y + ", " + source.position.z);
            ++scrIdx;
            // TODO: Write rotations
            streamWriter.Flush();
        }

        // Write listeners settings
        streamWriter.WriteLine("\nListener positions");
        int lstIdx = 0;
        foreach (var listener in listeners)
        {
            streamWriter.WriteLine("\tLst_" + lstIdx + ": " + listener.position.x + ", " + listener.position.y + ", " + listener.position.z);
            ++lstIdx;
            // TODO: Write rotations
            streamWriter.Flush();
        }

        // TODO: foreach (var transform in transforms)
    }

    float[] ReadCSV(string path)
    {
        // Read all lines from the CSV file
        string[] lines = File.ReadAllLines(path);

        // Split the values by commas and convert them to float
        return lines.SelectMany(line => line.Split(','))
                    .Select(float.Parse)
                    .ToArray();
    }

    private void OnDrawGizmos()
    {
        if (!doIRs)
            return;

        Vector3 cubeDimensions = cubeSize * Vector3.one;

        Gizmos.color = Color.blue;
        foreach (var source in sources)
            Gizmos.DrawCube(source.position, cubeDimensions);

        Gizmos.color = Color.yellow;
        foreach (var listener in listeners)
            Gizmos.DrawCube(listener.position, cubeDimensions);

        if (activeSource < 0)
            return;
        Gizmos.color = Color.green;
        Gizmos.DrawCube(sources.ElementAt(activeSource).position, cubeDimensions);

        if (activeListener < 0)
            return;
        Gizmos.color = Color.red;
        Gizmos.DrawCube(listeners.ElementAt(activeListener).position, cubeDimensions);
        Gizmos.color = Color.white;
        Gizmos.DrawRay(listeners.ElementAt(activeListener).position, listeners.ElementAt(activeListener).forward);
    }
}