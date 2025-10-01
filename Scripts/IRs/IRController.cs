using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.LightTransport;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using static Unity.VisualScripting.Member;

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

    private static bool doIRs = false;

    [SerializeField, HideInInspector]
    private string sceneName = "";

    [SerializeField]
    private string runName = "Run1";

    private string filePath;
    private string earlyConfigName = "";
    private string lateConfigName = "";
    private string spatName = "";
    private string srcName = "";
    private string lstName = "";

    [SerializeField]
    private RACAudioSource racSource;
    private Transform sourceTransform;
    private Transform listenerTransform;

    [SerializeField]
    private List<Transform> listeners;

    [SerializeField]
    private List<Transform> sources;

    [SerializeField]
    private List<RACManager.EarlyConfig> earlyConfigs;

    [SerializeField]
    private List<RACManager.LateConfig> lateConfigs;

    [SerializeField]
    private List<RACManager.SpatMode> spatModes;
    bool recordMono = false;

    private List<Transform> transforms = new List<Transform>();

    private IEnumerator transformEnumerator;
    private IEnumerator spatModeEnumerator;
    private IEnumerator earlyConfigEnumerator;
    private IEnumerator lateConfigEnumerator;
    private IEnumerator sourceEnumerator;
    private IEnumerator listenerEnumerator;

    private bool useTransforms = false;
    bool nextSource = true;
    bool nextEarlyConfig = true;
    bool nextLateConfig = true;
    bool nextSpatMode = true;
    bool nextTransform = true;

    StreamWriter streamWriter;

    private static IRController irController;

    void OnValidate()
    {
        if (irController == null)
            irController = this;
        else
            Debug.AssertFormat(irController == this, "More than one instance of the IRController created! Singleton violated.");
    }

    // Start is called before the first frame update
    void Awake()
    {
        UpdateSceneName();

        cubeSize = Mathf.Min(spacing / 2.0f, cubeSize);
    }

    void Start()
    {
        UpdateSceneName();

        listenerTransform = FindAnyObjectByType<RACAudioListener>().transform;
        if (listenerTransform == null)
            Debug.LogError("RACAudioListener not found");

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
        sourceTransform = sources[0].transform;

        transformEnumerator = ProcessTransforms();
        spatModeEnumerator = ProcessSpatModes();
        earlyConfigEnumerator = ProcessEarlyConfigs();
        lateConfigEnumerator = ProcessLateConfigs();
        sourceEnumerator = ProcessSources();
        listenerEnumerator = ProcessListeners();
    }

    void Update()
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

        if (nextEarlyConfig)
        {
            if (!earlyConfigEnumerator.MoveNext())
            {
                nextSpatMode = true;
                earlyConfigEnumerator = ProcessEarlyConfigs();
                Debug.Log("Next spat mode");
                return;
            }
        }

        nextEarlyConfig = false;

        if (nextLateConfig)
        {
            if (!lateConfigEnumerator.MoveNext())
            {
                nextEarlyConfig = true;
                lateConfigEnumerator = ProcessLateConfigs();
                Debug.Log("Next early config");
                return;
            }
        }

        nextLateConfig = false;

        if (nextSource)
        {
            if (!sourceEnumerator.MoveNext())
            {
                nextLateConfig = true;
                sourceEnumerator = ProcessSources();
                Debug.Log("Next late config");
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
        if ((RACMeshLoader.racMeshLoader != null) && !string.IsNullOrEmpty(RACMeshLoader.racMeshLoader.GetSelectedSubfolder()))
            sceneName = RACMeshLoader.racMeshLoader.GetSelectedSubfolder();
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
        FileStream file = new FileStream(filePath + "/" + fileName, FileMode.Create);
        streamWriter = new StreamWriter(file);
    }

    // Enumerator for the transforms foreach loop
    private IEnumerator ProcessTransforms()
    {
        foreach (var transform in transforms)
        {
            srcName = transform.gameObject.name;
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
            if (spatName == "None")
                spatName = "No";
            RACManager.UpdateSpatialisationMode(spatMode);
            yield return null; // Pause and resume in the next frame
        }
    }

    // Enumerator for the earlyConfig foreach loop
    private IEnumerator ProcessEarlyConfigs()
    {
        int idx = 0;
        foreach (var config in earlyConfigs)
        {
            RACManager.UpdateEarlyConfig(config);
            earlyConfigName = idx.ToString();
            idx++;
            yield return null; // Pause and resume in the next frame
        }
    }

    // Enumerator for the earlyConfig foreach loop
    private IEnumerator ProcessLateConfigs()
    {
        int idx = 0;
        foreach (var config in lateConfigs)
        {
            // RACManager.UpdateMoDARTLateConfig(config);
            lateConfigName = idx.ToString();
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
            if (!useTransforms)
                srcName = source.gameObject.name;
            sourceTransform = source.transform;
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
            lstName = listener.gameObject.name;

            string currentSetup = spatName + " spatialization, Early config " + earlyConfigName + ", Late config " + lateConfigName + ", " + srcName + ", " + lstName;

            RACManager.UpdateListener(listener.position, listener.rotation);
            listenerTransform.position = listener.position;
            listenerTransform.rotation = listener.rotation;
            
            int numSamples = Mathf.CeilToInt(impulseResponseLength * AudioSettings.outputSampleRate);
            float[] recordedIR = new float[numSamples];

            RACManager.RecordImpulseResponse(sourceTransform.position, sourceTransform.rotation, ref recordedIR);

            WavWriter.Save(filePath + "/" + currentSetup + ".wav", recordedIR, AudioSettings.outputSampleRate, channels: 2, writeFloat32: true, writeMono: recordMono);
            //WavWriter.Save(filePath + "/Echogram " + currentSetup + ".wav", recordedIR, AudioSettings.outputSampleRate, channels: 2, writeFloat32: true, writeMono: recordMono, echogram: true);
            //Debug.Log("<color=green>WAV saved to: " + wavPath + "</color>");

            if (!doIRs)
                yield break;

            yield return null; // Pause and resume in the next frame
        }
        activeListener = -1;
    }

    public void StartIRRun()
    {
        RACManager.DisableAudioProcessing();

        WriteRunSettings();

        doIRs = true;
        Debug.Log("Start IR Runs: " + doIRs);
    }

    public bool IsRunning() { return doIRs; }

    public void EndRun()
    {
        doIRs = false;
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

    void WriteRunSettings()
    {
        UpdateStreamWriter("Run_settings.txt");

        streamWriter.WriteLine("Sample rate: " + AudioSettings.outputSampleRate);

        // Write configs settings
        // To extract all members of a struct, see https://stackoverflow.com/a/7613806 and https://stackoverflow.com/a/2762679
        streamWriter.WriteLine("\nEarly configurations");
        int idx = 0;
        var fields = typeof(RACManager.EarlyConfig).GetFields();
        foreach (var config in earlyConfigs)
        {
            streamWriter.WriteLine("\t" + idx);
            if (config.enabled)
            {
                foreach (var field in fields)
                    streamWriter.WriteLine("\t\t" + field.Name + " " + field.GetValue(config));
            }
            else
                streamWriter.WriteLine("\t\tenabled false");
            streamWriter.Flush();
            idx++;
        }
        streamWriter.WriteLine("\nLate configurations");
        idx = 0;
        fields = typeof(RACManager.LateConfig).GetFields();
        foreach (var config in lateConfigs)
        {
            streamWriter.WriteLine("\t" + idx);
            if (config.enabled)
            {
                foreach (var field in fields)
                    streamWriter.WriteLine("\t\t" + field.Name + " " + field.GetValue(config));
            }
            else
                streamWriter.WriteLine("\t\tenabled False");
            streamWriter.Flush();
            idx++;
        }

        // TODO: Write grid parameters if using transforms

        // Write sources settings
        streamWriter.WriteLine("\nSource positions");
        int scrIdx = 0;
        foreach (var source in sources)
        {
            streamWriter.WriteLine("\t" + source.gameObject.name + ": " + source.position.x + ", " + source.position.y + ", " + source.position.z);
            ++scrIdx;
            // TODO: Write rotations
            streamWriter.Flush();
        }

        // Write listeners settings
        streamWriter.WriteLine("\nListener positions");
        int lstIdx = 0;
        foreach (var listener in listeners)
        {
            streamWriter.WriteLine("\t" + listener.gameObject.name + ": " + listener.position.x + ", " + listener.position.y + ", " + listener.position.z);
            ++lstIdx;
            // TODO: Write rotations
            streamWriter.Flush();
        }

        // TODO: foreach (var transform in transforms)
    }

    void OnDrawGizmos()
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