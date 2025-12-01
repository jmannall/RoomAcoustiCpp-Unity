using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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

    private RACAudioSource[] racSources;
    private int selectedSourceIdx = -1;

    [SerializeField]
    [Tooltip("By default, only sound sources with an associated NavMeshAgent can be moved. Ticking this allows moving any sound source.")]
    public bool allowMovingNonAgents = false;

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

        racSources = FindObjectsByType<RACAudioSource>(FindObjectsSortMode.None);

        if (racSources.Length > 0)
            selectedSourceIdx = 0;

        RefreshLabelStyles();
    }

    public void MoveMinimap() {
        minimapObject.transform.position = summoningHand.position + summonDistance * summoningHand.forward;
    }

    public void ToggleMinimap() {
        // TODO: If far from user and/or out of sight, move in front of user before reactivating?
        minimapObject.SetActive(!minimapObject.activeSelf);
    }

    public void MuteSelectedSource()
    {
        if (selectedSourceIdx < 0)
            return;
        racSources[selectedSourceIdx].MuteUnmute();
        RefreshLabelStyles();
    }

    public void RefreshLabelStyles()
    {
        if (racSources.Length <= 0)
            return;

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

    public void RegisterEvent(PointerEventData eventData, MinimapClickHandler.MapEventType type, Vector2 eventCoords)
    {
        if (racSources.Length <= 0)
            return;

        // Ignore events outside of the canvas.
        if (eventCoords == Vector2.positiveInfinity)
            return;

        switch (type)
        {
            default:
            case MinimapClickHandler.MapEventType.Click:
                // If the event is just a click, select the closest source and do nothing else.
                SelectClostestSource(eventCoords);
                break;

            case MinimapClickHandler.MapEventType.BeginDrag:
                // If the event is the start of a drag, select the closest source, and prepare to move it.
                SelectClostestSource(eventCoords);

                // Find the AI agent associated to the source being dragged, if it has one.
                NavMeshAgent draggedSourceAgent = racSources[selectedSourceIdx].GetComponent<NavMeshAgent>();
                // If the source has a NavMeshAgent, disable it during movement or it will get cranky.
                if (draggedSourceAgent != null)
                    draggedSourceAgent.enabled = false;
                break;

            case MinimapClickHandler.MapEventType.Drag:
                // Only update the source's position if it has a NavMeshAgent and/or moving non-agents is allowed.
                if (!allowMovingNonAgents && racSources[selectedSourceIdx].GetComponent<NavMeshAgent>() == null)
                    break;

                // Update the position of the source being dragged.
                Vector3 draggedPosition;
                draggedPosition.x = eventCoords.x;
                draggedPosition.z = eventCoords.y;
                draggedPosition.y = racSources[selectedSourceIdx].transform.position.y;

                racSources[selectedSourceIdx].transform.SetPositionAndRotation(draggedPosition, racSources[selectedSourceIdx].transform.rotation);

                break;
            case MinimapClickHandler.MapEventType.EndDrag:
                // Find the AI agent associated to the source being dragged, if it has one.
                NavMeshAgent selectedSourceAgent = racSources[selectedSourceIdx].GetComponent<NavMeshAgent>();
                if (selectedSourceAgent == null)
                    break;

                // If the source has an agent, give it freedom of movement again.
                selectedSourceAgent.nextPosition = racSources[selectedSourceIdx].transform.position;
                selectedSourceAgent.enabled = true;

                break;
        }

        RefreshLabelStyles();
    }

    private void SelectClostestSource(Vector2 eventCoords)
    {
        // Detect the closest source, and store its index in selectedSourceIdx.
        float minSourceDist = float.MaxValue;
        for (int i = 0; i < racSources.Length; i++)
        {
            Vector2 sourcePos;
            sourcePos.x = racSources[i].transform.position.x;
            sourcePos.y = racSources[i].transform.position.z;

            float sourceDist = (eventCoords - sourcePos).magnitude;

            if (sourceDist > minSourceDist)
                continue;

            minSourceDist = sourceDist;
            selectedSourceIdx = i;
        }
    }
}
