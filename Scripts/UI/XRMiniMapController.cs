using Oculus.Interaction.Body.Input;
using System.ComponentModel;
using TMPro;
using UnityEditor;
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
    // global singleton
    public static XRMinimapController xrMinimapController = null;

    [SerializeField]
    private GameObject plotObject;
    [SerializeField]
    private GameObject minimapObject;

    [SerializeField]
    public InputActionAsset inputActions;
    private InputActionMap uiActionMap;

    [Range(1, 3)]
    public int maxReflOrder = 3;
    private int currentReflOrder = 2; // Note: default init value used if earlyReflectionsStartActive.

    private InputAction togglePlot;
    private InputAction toggleMinimap;
    private InputAction toggleWandering;
    private InputAction increaseEarlyReflections;
    private InputAction decreaseEarlyReflections;
    private InputAction muteSelectedSource;
    private InputAction quitApplication;

    [SerializeField]
    public bool plotStartsActive = false;
    [SerializeField]
    public bool minimapStartsActive = true;
    [SerializeField]
    public bool wanderingStartsActive = true;
    [SerializeField]
    public bool earlyReflectionsStartActive = true;

    [SerializeField]
    private TextMeshProUGUI wanderingText;
    [SerializeField]
    private TextMeshProUGUI earlyReflectionsText;

    [SerializeField]
    private TextMeshProUGUI forbiddenDragText;
    private Animator forbiddenDragAnimator;
    private void Emphasize() => forbiddenDragAnimator.SetTrigger("Emphasize");

    private bool wanderingIsActive = true;

    private RACAudioSource[] racSources;
    private int selectedSourceIdx = -1;

    [SerializeField]
    [Tooltip("By default, only sound sources with an associated NavMeshAgent can be moved. Ticking this allows moving any sound source.")]
    public bool allowMovingNonAgents = false;

    private void Awake()
    {
        if (xrMinimapController == null)
            xrMinimapController = this;
        else
            Debug.AssertFormat(xrMinimapController == this, "More than one instance of the XRMinimapController created! Singleton violated.");

        uiActionMap = inputActions.FindActionMap("UI");
        uiActionMap.Enable();

        if (forbiddenDragText != null)
            forbiddenDragAnimator = forbiddenDragText.GetComponent<Animator>();
        else
            forbiddenDragAnimator = null;
    }

    private void Start()
    {
        togglePlot = uiActionMap["TogglePlot"];
        togglePlot.performed += context => TogglePlot();

        toggleMinimap = uiActionMap["ToggleMinimap"];
        toggleMinimap.performed += context => ToggleMinimap();

        toggleWandering = uiActionMap["ToggleWandering"];
        toggleWandering.performed += context => ToggleWandering();

        increaseEarlyReflections = uiActionMap["IncreaseEarlyReflections"];
        increaseEarlyReflections.performed += context => IncreaseEarlyReflections();

        decreaseEarlyReflections = uiActionMap["DecreaseEarlyReflections"];
        decreaseEarlyReflections.performed += context => DecreaseEarlyReflections();

        muteSelectedSource = uiActionMap["MuteSelectedSource"];
        muteSelectedSource.performed += context => MuteSelectedSource();

        quitApplication = uiActionMap["QuitApplication"];
        quitApplication.performed += context => QuitApplication();

        racSources = FindObjectsByType<RACAudioSource>(FindObjectsSortMode.None);

        if (racSources.Length > 0)
            selectedSourceIdx = 0;

        if (plotObject == null)
            Debug.LogWarning("Plot object not provided.");
        else
        {
            if (plotStartsActive != plotObject.activeSelf)
                TogglePlot();
        }

        if (minimapObject == null)
            Debug.LogWarning("Minimap object not provided.");
        else
        {
            if (minimapStartsActive != minimapObject.activeSelf)
                ToggleMinimap();
        }

        if (wanderingStartsActive)
        {
            wanderingIsActive = true;
            EnableWandering();
        }
        else
        {
            wanderingIsActive = false;
            DisableWandering();
        }

        if (earlyReflectionsStartActive)
            SetEarlyReflections(currentReflOrder); // Note: this uses the default init value defined above.
        else
            SetEarlyReflections(0);

        RefreshLabelStyles();
    }

    public void TogglePlot()
    {
        if (plotObject != null)
            plotObject.SetActive(!plotObject.activeSelf);
    }

    public void ToggleMinimap() {
        // TODO: If far from user and/or out of sight, move in front of user before reactivating?
        if (minimapObject != null)
            minimapObject.SetActive(!minimapObject.activeSelf);
    }

    void ToggleWandering()
    {
        if (wanderingIsActive)
            DisableWandering();
        else
            EnableWandering();
    }

    void DisableWandering()
    {
        foreach (RACAudioSource source in racSources)
        {
            NavMeshAgent sourceAgent = source.GetComponent<NavMeshAgent>();
            if (sourceAgent != null)
                sourceAgent.isStopped = true;
        }
        if (wanderingText != null)
            wanderingText.text = "Let sources\nwander around";

        wanderingIsActive = false;
    }

    void EnableWandering()
    {
        foreach (RACAudioSource source in racSources)
        {
            NavMeshAgent sourceAgent = source.GetComponent<NavMeshAgent>();
            if (sourceAgent != null)
                sourceAgent.isStopped = false;
        }
        if (wanderingText != null)
            wanderingText.text = "Stop sources\nfrom wandering";

        wanderingIsActive = true;
    }

    public bool IsWanderingActive() { return wanderingIsActive; }

    void IncreaseEarlyReflections() { SetEarlyReflections(currentReflOrder + 1); }
    void DecreaseEarlyReflections() { SetEarlyReflections(currentReflOrder - 1); }

    void SetEarlyReflections(int order)
    {
        if (order < 0 || order > maxReflOrder)
            return;

        if (order == 0)
            RACManager.UpdateEarlyConfig(RACManager.EarlyConfig.NoReflections);
        else
            RACManager.UpdateEarlyConfig(RACManager.EarlyConfig.Default(order));

        switch (order)
        {
            // These are calibrated for the museum scene (approx.)
            default:
            case 0:
                RACManager.UpdateMoDARTDelay(0.0f);
                break;
            case 1:
                RACManager.UpdateMoDARTDelay(0.025f);
                break;
            case 2:
                RACManager.UpdateMoDARTDelay(0.05f);
                break;
            case 3:
                RACManager.UpdateMoDARTDelay(0.075f);
                break;
        }
        if (earlyReflectionsText != null)
        {
            earlyReflectionsText.text = "Early reflections:\norder " + order.ToString();
            if (order == maxReflOrder)
                earlyReflectionsText.text += " (max)";
        }

        currentReflOrder = order;
    }

    public void MuteSelectedSource()
    {
        if (selectedSourceIdx < 0)
            return;
        racSources[selectedSourceIdx].MuteUnmute();
        RefreshLabelStyles();
    }

    void QuitApplication()
    {
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
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
                // If the source DOESN'T have a NavMeshAgent, and its movement is forbidden, emphasize the warning text.
                else if (!allowMovingNonAgents && forbiddenDragAnimator != null)
                    Emphasize();
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
