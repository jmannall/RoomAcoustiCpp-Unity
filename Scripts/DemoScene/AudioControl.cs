using UnityEngine;
using UnityEngine.InputSystem;

public class AudioControl : MonoBehaviour
{

    [SerializeField]
    private RACAudioSource source;

    [HideInInspector]
    public InputActionMap audioActionMap;
    private InputAction playPause;

    private void Awake()
    {
        audioActionMap = GetComponent<PlayerController>().inputActions.FindActionMap("Audio");
        audioActionMap.Enable();
    }

    // Start is called before the first frame update
    void Start()
    {
        playPause = audioActionMap["PlayPause"];
    }

    // Update is called once per frame
    void Update()
    {
        if (playPause.triggered)
            source.PlayPause();
    }
}
