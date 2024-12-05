
using UnityEngine;

[AddComponentMenu("RoomAcoustiC++/Audio Listener")]
[RequireComponent(typeof(AudioListener))]

public class RACAudioListener : MonoBehaviour
{
    // singleton
    private static RACAudioListener racAudioListener = null;

    #region Unity Functions

    //////////////////// Unity Functions ////////////////////
    
    private void OnEnable()
    {
        Debug.AssertFormat(racAudioListener == null, "More than one instance of the RACAudioListener created! Singleton violated.");
        racAudioListener = this;

        RACManager.UpdateListener(transform.position, transform.rotation);
    }

    void Update()
    {
        RACManager.UpdateListener(transform.position, transform.rotation);
    }

    #endregion
}
