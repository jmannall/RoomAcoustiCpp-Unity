
using UnityEngine;

[AddComponentMenu("RoomAcoustiC++/Audio Listener")]
[RequireComponent(typeof(AudioListener))]

public class RACAudioListener : MonoBehaviour
{
    // singleton
    private static RACAudioListener racAudioListener = null;

    #region Unity Functions

    //////////////////// Unity Functions ////////////////////

    private void OnValidate()
    {
        if (racAudioListener == null)
            racAudioListener = this;
        else
            Debug.AssertFormat(racAudioListener == this, "More than one instance of the RACAudioListener created! Singleton violated.");
    }

    private void Awake()
    {
        // UpdateListener();
    }

    void Update()
    {
        UpdateListener();
    }

    private void UpdateListener()
    {
        if (RACManager.racManager.isRunning)
            RACManager.UpdateListener(transform.position, transform.rotation);
    }

    #endregion
}
