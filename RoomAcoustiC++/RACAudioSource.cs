
using UnityEngine;
using System;

[AddComponentMenu("RoomAcoustiC++/Audio Source")]
[RequireComponent(typeof(AudioSource))]

public class RACAudioSource : MonoBehaviour
{

    private AudioSource source;

    #region Parameters

    //////////////////// Parameters ////////////////////

    // Inspector variables

    [SerializeField]
    [Tooltip("The AudioClip asset played by the RACAudioSource.")]
    private AudioClip clip;

    [SerializeField, Range(-60.0f, 24.0f)]
    [Tooltip("Control the overall gain of the source.")]
    private float gain = 0.0f;

    [SerializeField]
    [Tooltip("Play the sound when the component loads.")]
    private bool playOnAwake = false;

    [SerializeField]
    [Tooltip("Set the source to loop. If loop points are defined in the clip, these will be respected.")]
    private bool loop = false;

    [SerializeField]
    [Tooltip("Send the audio clip to channel 3.")]
    private bool arSend = false;

    [SerializeField, HideInInspector]
    private RACManager.SourceDirectivity directivity = RACManager.SourceDirectivity.Omni;

    // DSP Parameters
    private float[] input;

    private int numFrames;
    private float linGain = 1.0f;

    private bool isRunning = false;
    private bool isPlaying = false;
    // private int id = -1;
    public int id { get; private set; } = -1;

    #endregion

    #region Unity Functions

    //////////////////// Unity Functions ////////////////////

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (clip != null)
            source.clip = clip;
        source.playOnAwake = playOnAwake;
        source.loop = loop;
        source.bypassEffects = false;
        source.bypassReverbZones = true;
        source.spatialBlend = 0.0f;
        source.panStereo = 0.0f;
    }

    void Start()
    {
        AudioConfiguration config = AudioSettings.GetConfiguration();
        numFrames = config.dspBufferSize;

        RACManager.racManager.enableAudioProcessing += InitSource;
        RACManager.racManager.disableAudioProcessing += RemoveSource;

        if (playOnAwake)
            Play();

        input = new float[numFrames];
    }

    void Update()
    {
        if (id < 0)
            return;

        RACManager.UpdateSource(id, transform.position, transform.rotation);
        linGain = UpdateLinearGain();

        isPlaying = source.isPlaying;
    }

    private void OnDestroy()
    {
        RemoveSource();
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (RACManager.racManager.isRunning)
        {
            if (isRunning && isPlaying)
            {
                // Copy every other sample to mono buffer
                for (int i = 0, j = 0; i < numFrames; i++, j+=channels)
                    input[i] = linGain * data[j];
                RACManager.SubmitAudio(id, ref input);

                // Overwrite data
                Array.Fill(data, 0.0f);
                if (arSend && channels > 2)
                {
                    for (int i = 0, j = 2; i < numFrames; i++, j += channels)
                        data[j] = input[i];
                }
                return;
            }
        }
        // Overwrite data
        Array.Fill(data, 0.0f);
    }

    #endregion

    #region Functions

    //////////////////// Functions ////////////////////

    private void InitSource()
    {
        if (id >= 0)
            return;
        id = RACManager.InitSource();

        if (id < 0)
        {
            Debug.LogError("Source failed to initialise");
            return;
        }
        Debug.Log("Source successfully initialised");

        UpdateDirectivity();
        isRunning = true;
    }

    private void RemoveSource()
    {
        if (id < 0)
            return;

        isRunning = false;
        RACManager.RemoveSource(id);
        id = -1;
    }

    public void RestartSource()
    {
        RemoveSource();       
        InitSource();
    }

    private float UpdateLinearGain()
    {
        return Mathf.Pow(10, gain / 20.0f);
    }

    public void UpdateGain(float gain) { this.gain = gain; }

    public void SetClip(AudioClip clip)
    {
        bool wasPlaying = source.isPlaying;
        this.clip = clip;
        source.clip = clip;
        if (wasPlaying)
            source.Play();
    }

    public void PlayPause()
    {
        Debug.Log("Play Pause");
        if (source.isPlaying)
            Pause();
        else
            Play();
    }

    public void Pause()
    {
        if (source.isPlaying)
            source.Pause();
    }

    public void Play()
    {
        if (!isRunning)
            InitSource();
        if (!source.isPlaying)
            source.Play();
    }

    public void PlayOneShot(AudioClip clip)
    {
        if (!isRunning)
            InitSource();
        if (clip != null)
            source.PlayOneShot(clip);
    }

    public void Stop() { RemoveSource(); source.Stop(); }

    public void PlayScheduled(double time)
    {
        if (!isRunning)
            InitSource();
        if (!source.isPlaying)
            source.PlayScheduled(time);
    }

    public void SetLoop(bool doLoop) { loop = doLoop; source.loop = doLoop; }

    public void SetPlayOnAwake(bool play)
    {
        playOnAwake = play;
        source.playOnAwake = play;
    }

    public bool IsPlaying()
    {
        return source.isPlaying;
    }

    public void UpdateDirectivity()
    {
        RACManager.UpdateSourceDirectivity(id, directivity);
    }

    #endregion

}
