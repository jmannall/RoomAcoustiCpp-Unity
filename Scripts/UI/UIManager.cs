using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    // global singleton
    public static UIManager uiManager = null;

    [Range(1, 3)]
    public int maxReflOrder = 2;
    private int currentReflOrder = 2; // Note: default init value used if earlyReflectionsStartActive.

    private bool wanderingIsActive = true;

    private RACAudioSource[] racSources;

    [SerializeField]
    private GameObject plotObject;
    [SerializeField]
    private GameObject minimapObject;
    [SerializeField]
    private GameObject userObject;

    [SerializeField]
    private TextMeshProUGUI wanderingText;
    [SerializeField]
    private TextMeshProUGUI earlyReflectionsText;

    public InputActionAsset inputActions;

    private InputActionMap uiActionMap;

    private InputAction togglePlot;
    private InputAction toggleMinimap;
    private InputAction toggleWandering;
    private InputAction increaseEarlyReflections;
    private InputAction decreaseEarlyReflections;
    private InputAction quitApplication;

    [SerializeField]
    public bool plotStartsActive = false;
    [SerializeField]
    public bool minimapStartsActive = false;
    [SerializeField]
    public bool wanderingStartsActive = true;
    [SerializeField]
    public bool earlyReflectionsStartActive = true;

    private void Awake()
    {
        if (uiManager == null)
            uiManager = this;
        else
            Debug.AssertFormat(uiManager == this, "More than one instance of the UIManager created! Singleton violated.");

        uiActionMap = inputActions.FindActionMap("UI");
        uiActionMap.Enable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
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

        quitApplication = uiActionMap["QuitApplication"];
        quitApplication.performed += context => QuitApplication();

        racSources = FindObjectsByType<RACAudioSource>(FindObjectsSortMode.None);

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

        UpdateUserCamera();
    }

    public void TogglePlot()
    {
        if (plotObject != null)
            plotObject.SetActive(!plotObject.activeSelf);
    }

    public void ToggleMinimap()
    {
        // TODO: If far from user and/or out of sight, move in front of user before reactivating?
        if (minimapObject != null)
            minimapObject.SetActive(!minimapObject.activeSelf);

        UpdateUserCamera();
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
            wanderingText.text = "Let sources wander around (Escape)";

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
            wanderingText.text = "Stop sources from wandering (Escape)";

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
            earlyReflectionsText.text = "Current early reflection order: " + order.ToString();

        currentReflOrder = order;
    }

    void QuitApplication()
    {
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }

    void UpdateUserCamera()
    {
        if (minimapObject != null && userObject != null)
        {
            PlayerCamera cam = userObject.GetComponent<PlayerCamera>();
            if (cam != null)
                cam.SetControl(minimapObject.activeSelf);
        }
    }
}
