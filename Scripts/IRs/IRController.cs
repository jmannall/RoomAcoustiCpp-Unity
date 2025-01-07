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

    [SerializeField]
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
    private string runName = "Run1";

    private string filePath;

    [SerializeField]
    private RACAudioSource racSource;

    [SerializeField]
    private List<Transform> listeners;

    [SerializeField]
    private List<Transform> sources;

    [SerializeField]
    private List<RACManager.IEMConfig> configs;

    [SerializeField]
    private List<RACManager.SpatMode> spatModes;

    private IEnumerator spatModeEnumerator;
    private IEnumerator configEnumerator;
    private IEnumerator sourceEnumerator;
    private IEnumerator listenerEnumerator;

    bool nextSource = true;
    bool nextConfig = true;
    bool nextSpatMode = true;

    StreamWriter streamWriter;

    //private Quaternion quaternion = Quaternion.LookRotation(Vector3.right);
    //private string direction = "_x";
    private Quaternion quaternion = Quaternion.identity;
    private string direction = "_z";

    static bool lateReverbCompleted = false;
    static int iemCounter = 0;
    static void OnIEMCompleted(int id)
    {
        if (instance == null)
            return;
        if (instance.racSource == null)
            return;
        if (id == -1)
            lateReverbCompleted = true;
        else if (id == instance.racSource.id)
            iemCounter++;
    }

    private static IRController instance;

    // Start is called before the first frame update
    private void Awake()
    {
        DebugCPP.RegisterIEMCallback(OnIEMCompleted);
        instance = this;

        int numFrames = AudioSettings.GetConfiguration().dspBufferSize;
        inputBuffer = new float[numFrames];
        outputBuffer = new float[2 * numFrames];

        numBuffers = Mathf.CeilToInt(impulseResponseLength * AudioSettings.outputSampleRate / numFrames);

        filePath = Application.dataPath + "/InpulseResponses/" + SceneController.currentScene + "/" + runName;
        if (!Directory.Exists(filePath))
            Directory.CreateDirectory(filePath);
        
    }

    private void Start()
    {
        RACManager.DisableAudioProcessing();

        UpdateStreamWriter("Data");
        streamWriter.WriteLine(AudioSettings.outputSampleRate.ToString());

        if (listeners.Count == 0)
            LocateListenerPositions();
        WriteListenerPositions();

        if (sources.Count == 0)
            sources.Add(racSource.transform);

        spatModeEnumerator = ProcessSpatModes();
        configEnumerator = ProcessConfigs();
        sourceEnumerator = ProcessSources();
        listenerEnumerator = ProcessListeners();
    }

    private void OnDisable()
    {
        DebugCPP.UnregisterIEMCallback();
    }

    private void Update()
    {
        if (!doIRs)
            return;

        if (nextSpatMode)
        {
            if (!spatModeEnumerator.MoveNext())
            {
                doIRs = false;
                spatModeEnumerator = ProcessSpatModes();
                Debug.Log("All IR runs complete");
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

    // Enumerator for the spat mode foreach loop
    private IEnumerator ProcessSpatModes()
    {
        foreach (var spatMode in spatModes)
        {
            UpdateStreamWriter(spatMode.ToString() + direction);
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

    // Enumerator for the source foreach loop
    private IEnumerator ProcessSources()
    {
        foreach (var source in sources)
        {
            activeSource++;
            racSource.transform.position = source.position;
            yield return null; // Pause and resume in the next frame
        }
        activeSource = -1;
    }
    
    // Enumerator for the listener foreach loop
    private IEnumerator ProcessListeners()
    {
        foreach (var listener in listeners)
        {
            activeListener++;
            RACManager.UpdateListener(listener.position, listener.rotation);

            int count = 0;
            racSource.RestartSource();
            lateReverbCompleted = false;
            iemCounter = 0;
            while (!lateReverbCompleted || iemCounter < 2)
            {
                count++;
                yield return null;
            }
            // Debug.Log("Time for IEM: " + count.ToString() + " frames");
            RACManager.SubmitAudio(racSource.id, ref inputBuffer);
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
        activeListener = -1;
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

    void LocateListenerPositions()
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
        Transform currentTransform = transform;
        currentTransform.rotation = quaternion;

        int k = 0;
        foreach (float x in xPositions)
        {
            currentPosition.x = x;
            foreach (float y in yPositions)
            {
                currentPosition.z = y;
                currentTransform.position = currentPosition;
                listeners.Add(currentTransform);
            }
        }        
    }

    void WriteListenerPositions()
    {
        UpdateStreamWriter("Positions");

        foreach (var listener in listeners)
        {
            streamWriter.WriteLine(listener.position.x + ", " + listener.position.y + ", " + listener.position.z);
            streamWriter.Flush();
        }
    }

    void ProcessAudioBuffer()
    {
        RACManager.SubmitAudio(racSource.id, ref inputBuffer);
        bool success = RACManager.ProcessOutput();
        if (success)
            RACManager.GetOutputBuffer(ref outputBuffer);
        foreach (float sample in outputBuffer)
            streamWriter.Write(sample.ToString() + ", ");
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
    }
}
