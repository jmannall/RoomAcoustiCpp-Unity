using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IRController : MonoBehaviour
{
#if RAC_Debug && UNITY_EDITOR

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
    int numBuffers;

    private static bool doIRs = false;

    [SerializeField]
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

    [SerializeField]
    private bool doEchogram = false;

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

    private static IRController irController;

    // Start is called before the first frame update
    private void Awake()
    {
        DebugCPP.RegisterIEMStartCallback(OnIEMStarted);
        DebugCPP.RegisterIEMEndCallback(OnIEMCompleted);
        DebugCPP.RegisterRTMStartCallback(OnRTMStarted);
        DebugCPP.RegisterRTMEndCallback(OnRTMCompleted);

        Debug.AssertFormat(irController == null, "More than one instance of the IRController created! Singleton violated.");
        irController = this;

        int numFrames = AudioSettings.GetConfiguration().dspBufferSize;
        inputBuffer = new float[numFrames];
        outputBuffer = new float[2 * numFrames];

        numBuffers = Mathf.CeilToInt(impulseResponseLength * AudioSettings.outputSampleRate / numFrames);

        if (sceneName == "")
            sceneName = SceneManager.GetActiveScene().name;

        filePath = Application.persistentDataPath + "/ImpulseResponses/" + sceneName + "/" + runName;
        if (!Directory.Exists(filePath))
            Directory.CreateDirectory(filePath);

        cubeSize = Mathf.Min(spacing / 2.0f, cubeSize);
    }

    private void Start()
    {
        listenerTransform = FindAnyObjectByType<RACAudioListener>().transform;
        if (listenerTransform == null)
            Debug.LogError("RACAudioListener not found");

        if (irFilePath == "")
            impulseResponse = new float[1] { 1.0f };
        else
            impulseResponse = ReadCSV(irFilePath);

        UpdateStreamWriter("Data");
        streamWriter.WriteLine(AudioSettings.outputSampleRate.ToString());

        if (listeners.Count > 0)
        {
            WriteListenerPositions();
            AddListenerRotations();
        }
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
        listeners.Clear();
        if (streamWriter != null)
            streamWriter.Close();
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
            WriteListenerPositions();
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
            UpdateStreamWriter(areaName + "_" + spatName + "_" + configName);
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
            Debug.Log("Time for IEM and RTM to run fresh loops: " + (countStart + countEnd).ToString() + " frames");

            RACManager.SubmitAudio(racSource.id, ref inputBuffer);
            RACManager.ResetFDN();
            RACManager.ProcessOutput();
            //bool success = RACManager.ProcessOutput();
            //Debug.Log("RACManager.ProcessOutput() returned " + success.ToString());

            // TODO: Wait for confirmation that the DSP thread has run a fresh loop, or else the FDNs might get reset after the start of the IR recording.

            inputBuffer[0] = 1.0f;
            ProcessAudioBuffer(0);
            inputBuffer[0] = 0.0f;
            for (int i = 1; i < numBuffers; i++)
                ProcessAudioBuffer(i);
            streamWriter.Write("0, 0\n");
            streamWriter.Flush();
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

    void WriteListenerPositions()
    {
        UpdateStreamWriter(areaName + "_Positions");

        foreach (var listener in listeners)
        {
            streamWriter.WriteLine(listener.position.x + ", " + listener.position.y + ", " + listener.position.z);
            streamWriter.Flush();
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
        int idx = bufferNumber * inputBuffer.Length;
        for (int i = 0; i < Mathf.Min(inputBuffer.Length, impulseResponse.Length - idx); i++)
            inputBuffer[i] = impulseResponse[idx + i];

        RACManager.SubmitAudio(racSource.id, ref inputBuffer);
        bool success = RACManager.ProcessOutput();
        if (success)
            RACManager.GetOutputBuffer(ref outputBuffer);

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

        for (int i = 0; i < Mathf.Min(inputBuffer.Length, impulseResponse.Length - idx); i++)
            inputBuffer[i] = 0.0f;
    }

    void WriteSample(float input)
    {
        if (doEchogram)
            streamWriter.Write(Mathf.Log10(input * input + 1e-30f).ToString() + ", ");
        else
            streamWriter.Write(input.ToString() + ", ");
    }

    /* Write all information related to the current IR run settings.
     */
    void WriteRunSettings()
    {
        UpdateStreamWriter("Run_settings");

        streamWriter.Write("Echogram mode: " + doEchogram.ToString());

        // Write configs settings
        streamWriter.WriteLine("\nConfigurations");
        int idx = 0;
        // To extract all members of a struct, see https://stackoverflow.com/a/7613806 and https://stackoverflow.com/a/2762679
        var fields = typeof(RACManager.IEMConfig).GetFields();
        foreach (var config in configs)
        {
            streamWriter.WriteLine("\tConfig_index " + idx.ToString());
            foreach (var field in fields)
                streamWriter.WriteLine("\t\t" + field.Name + " " + field.GetValue(config).ToString());
            streamWriter.Flush();
            idx++;
        }

        // Write spatModes settings
        streamWriter.WriteLine("\nSpatialisation modes");
        foreach (var spatMode in spatModes)
        {
            streamWriter.WriteLine("\t" + spatMode.ToString());
            streamWriter.Flush();
        }

        // Write listeners settings
        streamWriter.WriteLine("\nListener positions");
        foreach (var listener in listeners)
        {
            streamWriter.WriteLine("\t" + listener.position.x + ", " + listener.position.y + ", " + listener.position.z);
            // TODO: Write rotations
            streamWriter.Flush();
        }

        // Write sources settings
        streamWriter.WriteLine("\nSource positions");
        foreach (var source in sources)
        {
            streamWriter.WriteLine("\t" + source.position.x + ", " + source.position.y + ", " + source.position.z);
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
#endif
}