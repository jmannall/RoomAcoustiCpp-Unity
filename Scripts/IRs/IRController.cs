using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class IRController : MonoBehaviour
{
    [SerializeField]
    private float spacing = 1.0f;

    private float cubeSize = 0.1f;

    private Dictionary<int, Vector3> listeners;
    int activeID = -1;

    [SerializeField, Range(0.0f, 10.0f)]
    private float impulseResponseLength = 1.0f;

    private float[] inputBuffer;
    private float[] outputBuffer;
    int numBuffers;

    private static bool doIRs = false;

    [SerializeField]
    private string runName = "Run1";

    private string filePath;
    private string areaName = "";

    [SerializeField]
    private RACAudioSource source;

    [SerializeField]
    private List<RACManager.IEMConfig> configs;

    [SerializeField]
    private List<RACManager.SpatMode> spatModes;
    bool recordMono = false;

    private List<Transform> transforms = new List<Transform>();

    private IEnumerator transformEnumerator;
    private IEnumerator spatModeEnumerator;
    private IEnumerator configEnumerator;
    private IEnumerator listenerEnumerator;

    bool nextConfig = true;
    bool nextSpatMode = true;
    bool nextTransform = true;

    StreamWriter streamWriter;

    static bool lateReverbCompleted = false;
    static int iemCounter = 0;
    static void OnIEMCompleted(int id)
    {
        if (instance == null)
            return;
        if (instance.source == null)
            return;
        if (id == -1)
            lateReverbCompleted = true;
        else if (id == instance.source.id)
            iemCounter++;
    }

    private static IRController instance;

    // Start is called before the first frame update
    private void Awake()
    {
        DebugCPP.RegisterIEMCallback(OnIEMCompleted);
        instance = this;

        listeners = new Dictionary<int, Vector3>();

        int numFrames = AudioSettings.GetConfiguration().dspBufferSize;
        inputBuffer = new float[numFrames];
        outputBuffer = new float[2 * numFrames];

        numBuffers = Mathf.CeilToInt(impulseResponseLength * AudioSettings.outputSampleRate / numFrames);

        filePath = Application.dataPath + "/ImpulseResponses/" + SceneController.currentScene + "/" + runName;
        if (!Directory.Exists(filePath))
            Directory.CreateDirectory(filePath);

        cubeSize = Mathf.Min(spacing / 2.0f, cubeSize);
    }

    private void Start()
    {
        RACManager.DisableAudioProcessing();

        UpdateStreamWriter("Data");
        streamWriter.WriteLine(AudioSettings.outputSampleRate.ToString());

        Transform[] transformStore = GetComponentsInChildren<Transform>();

        for (int i = 1; i < transformStore.Length; i++)
            transforms.Add(transformStore[i]);

        transformEnumerator = ProcessTransforms();
        spatModeEnumerator = ProcessSpatModes();
        configEnumerator = ProcessConfigs();
        listenerEnumerator = ProcessListeners();
    }

    private void Update()
    {
        if (!doIRs)
            return;

        if (nextTransform)
        {
            if (!transformEnumerator.MoveNext())
            {
                doIRs = false;
                transformEnumerator = ProcessTransforms();
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

        if (!listenerEnumerator.MoveNext())
        {
            nextConfig = true;
            listenerEnumerator = ProcessListeners();
            Debug.Log("Next config");
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
            yield return null; // Pause and resume in the next frame
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
           
            UpdateStreamWriter(areaName + "_" + spatMode.ToString());
            RACManager.UpdateSpatialisationMode(spatMode);
            yield return null; // Pause and resume in the next frame
        }
    }

    // Enumerator for the config foreach loop
    private IEnumerator ProcessConfigs()
    {
        foreach (var config in configs)
        {
            RACManager.UpdateIEMConfig(config);
            yield return null; // Pause and resume in the next frame
        }
    }

    // Enumerator for the listener foreach loop
    private IEnumerator ProcessListeners()
    {
        foreach (var listener in listeners)
        {
            activeID = listener.Key;
            RACManager.UpdateListener(listener.Value, Quaternion.identity);
            int count = 0;
            source.RestartSource();
            lateReverbCompleted = false;
            iemCounter = 0;
            while (!lateReverbCompleted || iemCounter < 2)
            {
                count++;
                yield return null;
            }
            // Debug.Log("Time for IEM: " + count.ToString() + " frames");
            RACManager.SubmitAudio(source.id, ref inputBuffer);
            inputBuffer[0] = 1.0f;
            ProcessAudioBuffer();
            inputBuffer[0] = 0.0f;
            for (int i = 1; i < numBuffers; i++)
                ProcessAudioBuffer();
            streamWriter.Write("0, 0\n");
            streamWriter.Flush();
            if (!doIRs) 
                yield break;
            RACManager.ResetFDN();
            yield return null; // Pause and resume in the next frame
        }
        activeID = -1;
    }

    public void StartIRRun()
    {
        doIRs = true;
        Debug.Log("Start IR Runs: " + doIRs);
    }

    public bool IsRunning() { return doIRs; }

    public void EndRun()
    {
        doIRs = false;
        Debug.Log("End IR Run Early");
    }

    void LocateListenerPositions(Transform transform)
    {
        Debug.Log("Locate Listener Positions");

        UpdateStreamWriter(areaName + "_Positions");

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

        Vector3 currentPosition = new Vector3(0.0f, 1.6f, 0.0f);

        listeners.Clear();
        int k = 0;
        foreach (float x in xPositions)
        {
            currentPosition.x = x;
            foreach (float y in yPositions)
            {
                currentPosition.z = y;
                listeners.Add(k++, currentPosition);
                streamWriter.WriteLine(currentPosition.x + ", " + currentPosition.y + ", " + currentPosition.z);
                streamWriter.Flush();
            }
        }
    }

    void ProcessAudioBuffer()
    {
        RACManager.SubmitAudio(source.id, ref inputBuffer);
        bool success = RACManager.ProcessOutput();
        if (success)
            RACManager.GetOutputBuffer(ref outputBuffer);

        if (recordMono)
        {
            for (int i = 0; i < outputBuffer.Length; i += 2)
                streamWriter.Write(outputBuffer[i].ToString() + ", ");
        }
        else
        {
            foreach (float sample in outputBuffer)
                streamWriter.Write(sample.ToString() + ", ");
        }
    }

    private void OnDrawGizmos()
    {
        if (!doIRs)
            return;

        Vector3 cubeDimensions = cubeSize * Vector3.one;
        Gizmos.color = Color.yellow;

        foreach (var source in listeners)
            Gizmos.DrawCube(source.Value, cubeDimensions);

        if (activeID < 0)
            return;
        Gizmos.color = Color.red;
        Gizmos.DrawCube(listeners.ElementAt(activeID).Value, cubeDimensions);
    }
}
