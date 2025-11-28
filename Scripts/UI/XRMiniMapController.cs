using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using static Unity.VisualScripting.Member;

public class XRMinimapController : MonoBehaviour
{
    /*
     * Code adapted from
     * https://blog.yarsalabs.com/real-world-position-from-a-minimap-in-unity/
     */

    [SerializeField]
    private GameObject minimapObject;

    [SerializeField]
    public InputActionAsset inputActions;
    private InputActionMap uiActionMap;

    [SerializeField]
    [Tooltip("Hand in front of which the minimap gets summoned.")]
    private Transform summoningHand;

    [SerializeField, Range(0.0f, 2.0f)]
    [Tooltip("Distance between the hand and the summoned minimap.")]
    private float summonDistance = 0.5f;

    private InputAction toggleMinimap;
    private InputAction muteSelectedSource;
    private InputAction dragSelectedSource;

    private RACAudioSource[] racSources;
    private int selectedSourceIdx = -1;

    [SerializeField]
    [Tooltip("By default, only sound sources with an associated NavMeshAgent can be moved. Ticking this allows moving any sound source.")]
    public bool allowMovingNonAgents = false;

    [SerializeField, Range(1, 1000)]
    [Tooltip("Scale of the minimap, in pixels per meter. Required to calibrate the position tracking.")]
    public float pixelsPerMeter;
    [SerializeField, Range(0, 1000)]
    [Tooltip("Margin to either side of the minimap, in pixels. Required to calibrate the position tracking.")]
    public int pixelMargin;

    private void Awake()
    {
        uiActionMap = inputActions.FindActionMap("UI");
        uiActionMap.Enable();

        if (summoningHand == null)
            Debug.LogError("Summoning hand transform not provided.");
        if (minimapObject == null)
            Debug.LogError("Minimap object not provided.");
    }

    private void Start()
    {
        toggleMinimap = uiActionMap["ToggleMinimap"];
        toggleMinimap.performed += context => ToggleMinimap();

        muteSelectedSource = uiActionMap["MuteSelectedSource"];
        muteSelectedSource.performed += context => MuteSelectedSource();

        dragSelectedSource = uiActionMap["DragSelectedSource"];
        dragSelectedSource.performed += context => DragSelectedSource();

        racSources = FindObjectsByType<RACAudioSource>(FindObjectsSortMode.None);

        RefreshLabelStyles();
    }

    public void ToggleMinimap()
    {
        if (!minimapObject.activeSelf)
            minimapObject.transform.position = summoningHand.position + summonDistance * summoningHand.forward;

        minimapObject.SetActive(!minimapObject.activeSelf);
    }

    public void MuteSelectedSource()
    {
        racSources[selectedSourceIdx].MuteUnmute();
        RefreshLabelStyles();
    }

    public void DragSelectedSource()
    {
    }

    public void RefreshLabelStyles()
    {
        TextMeshProUGUI[] sourceLabels;
        for (int i = 0; i < racSources.Length; i++)
        {
            sourceLabels = racSources[i].GetComponentsInChildren<TextMeshProUGUI>();

            foreach (TextMeshProUGUI label in sourceLabels)
            {
                // Muted source labels are colored in gray.
                if (racSources[i].IsMuted())
                    label.color = Color.gray5;
                else
                    label.color = Color.white;

                // All source labels are bold. Optional styles are added with a bitwise OR (it's a bit mask).
                FontStyles sourceStyle = FontStyles.Bold;
                // Immovable source labels are in italics.
                if (!allowMovingNonAgents && racSources[i].GetComponent<NavMeshAgent>() == null)
                    sourceStyle |= FontStyles.Italic;
                // The selected source is underlined.
                if (i == selectedSourceIdx)
                    sourceStyle |= FontStyles.Underline;
                // Set the style mask.
                label.fontStyle = sourceStyle;
            }
        }
    }

    public void RegisterClick(Vector2 pixelCoords)
    {
        Vector2 worldCoords = new(pixelCoords.x - pixelMargin, pixelCoords.y - pixelMargin);

        Debug.Log($"XRMinimapController detected Canvas click at {worldCoords / pixelsPerMeter}.");

        // Detect the closest source, and underline its label.
        float minSourceDist = float.MaxValue;
        for (int i = 0; i < racSources.Length; i++)
        {
            Vector2 sourcePos;
            sourcePos.x = racSources[i].transform.position.x;
            sourcePos.y = racSources[i].transform.position.z;

            float sourceDist = (worldCoords - sourcePos).magnitude;

            if (sourceDist > minSourceDist)
                continue;

            minSourceDist = sourceDist;
            selectedSourceIdx = i;
        }

        RefreshLabelStyles();
    }
}
