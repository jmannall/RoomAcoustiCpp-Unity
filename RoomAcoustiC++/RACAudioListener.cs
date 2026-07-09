
using UnityEngine;

[AddComponentMenu("RoomAcoustiC++/RAC Audio Listener")]
[RequireComponent(typeof(AudioListener))]

public class RACAudioListener : MonoBehaviour
{
    // singleton
    public static RACAudioListener racAudioListener = null;

    #region Unity Functions

    //////////////////// Unity Functions ////////////////////

    void Awake()
    {
        if (racAudioListener == null)
            racAudioListener = this;
        else
            Debug.AssertFormat(racAudioListener == this, "More than one instance of the RACAudioListener created! Singleton violated.");
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
