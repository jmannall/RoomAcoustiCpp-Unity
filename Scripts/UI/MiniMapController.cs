using TMPro;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(RectTransform))]
public class MiniMapController : MonoBehaviour
{
    /*
     * Code adapted from
     * https://blog.yarsalabs.com/real-world-position-from-a-minimap-in-unity/
     */
    private Vector2 screenCursorPos;
    private Vector2 worldCursorPos;

    private Camera mapEventCamera;
    private RectTransform mapRectTransform;

    [SerializeField, Range(1, 1000)]
    public float pixelsPerMeter;
    [SerializeField, Range(0, 1000)]
    public int pixelMargin;
    [SerializeField]
    public bool allowMovingNonAgents = false;

    [SerializeField]
    private TextMeshProUGUI forbiddenDragText;
    private Animator forbiddenDragAnimator;
    private void Emphasize() => forbiddenDragAnimator.SetTrigger("Emphasize");

    private RACAudioSource[] racSources;
    private int selectedSourceIdx = -1;

    private bool previousFrameMouseLeft;
    private bool previousFrameMouseRight;

    private void Awake()
    {
        if (forbiddenDragText != null)
            forbiddenDragAnimator = forbiddenDragText.GetComponent<Animator>();
        else
            forbiddenDragAnimator = null;
    }

    void Start()
    {
        mapRectTransform = GetComponent<RectTransform>();

        if (GetComponentInParent<Canvas>() != null)
            mapEventCamera = GetComponentInParent<Canvas>().worldCamera;

        racSources = FindObjectsByType<RACAudioSource>(FindObjectsSortMode.None);

        TextMeshProUGUI[] sourceLabels;
        foreach (RACAudioSource source in racSources)
        {
            sourceLabels = source.GetComponentsInChildren<TextMeshProUGUI>();

            foreach (TextMeshProUGUI label in sourceLabels)
            {
                if (source.IsMuted())
                    label.color = Color.gray5;
                else
                    label.color = Color.white;
            }
        }

        previousFrameMouseLeft = Input.GetMouseButton(0);
        previousFrameMouseRight = Input.GetMouseButton(1);
    }

    void Update()
    {
        bool cursorInBounds = false;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapRectTransform, Input.mousePosition, mapEventCamera, out screenCursorPos))
        {
            screenCursorPos.x -= mapRectTransform.rect.x + pixelMargin;
            screenCursorPos.y -= mapRectTransform.rect.y + pixelMargin;

            worldCursorPos = screenCursorPos / pixelsPerMeter;

            if (worldCursorPos.x > 0 && worldCursorPos.x < 21
                && worldCursorPos.y > 0 && worldCursorPos.y < 26)
                cursorInBounds = true;
        }

        if (!cursorInBounds)
        {
            // If a source was being dragged, drop it and forget about it.
            if (selectedSourceIdx >= 0)
            {
                // Find the AI agent associated to the source and allow it freedom of movement.
                StopDragging(racSources[selectedSourceIdx]);

                // Forget about it.
                selectedSourceIdx = -1;
            }

            // Treat this as a button release.
            previousFrameMouseLeft = false;
            previousFrameMouseRight = false;

            // Update the labels, a source might have been de-selected.
            RefreshLabelStyles();

            // Do nothing else, because the cursor is OOB.
            return;
        }

        if (!previousFrameMouseLeft)
        {
            // At the previous frame, the mouse button was NOT down; we need to update the selected source.
            // Detect the closest source, and underline its label.
            float minSourceDist = float.MaxValue;
            for (int i = 0; i < racSources.Length; i++)
            {
                Vector2 sourcePos;
                sourcePos.x = racSources[i].transform.position.x;
                sourcePos.y = racSources[i].transform.position.z;

                float sourceDist = (worldCursorPos - sourcePos).magnitude;

                if (sourceDist > minSourceDist)
                    continue;

                minSourceDist = sourceDist;
                selectedSourceIdx = i;
            }

            if (selectedSourceIdx < 0)
            {
                Debug.LogWarning("Failed to detect closest source.");
                previousFrameMouseLeft = false;
                previousFrameMouseRight = false;
                return;
            }
        }
        else
        {
            if (selectedSourceIdx < 0)
            {
                Debug.LogError("Left mouse remains pressed since previous frame, but no source is selected.");
                previousFrameMouseLeft = false;
                previousFrameMouseRight = false;
                return;
            }

            if (!Input.GetMouseButton(0))
            {
                // At the previous frame, the mouse button WAS down, but now it's not.
                // A source was being dragged, we need to release it.

                // Find the AI agent associated to the source and allow it freedom of movement.
                StopDragging(racSources[selectedSourceIdx]);
            }
            // If the mouse button WAS down and it still is, no need to do anything here, we'll just keep dragging the selected source.
        }

        if (Input.GetMouseButton(1))
        {
            // The right mouse button is pressed.
            // If it wasn't pressed at the previous frame, toggle the selected source.
            if (!previousFrameMouseRight && selectedSourceIdx >= 0)
            {
                racSources[selectedSourceIdx].MuteUnmute();

                TextMeshProUGUI[] sourceLabels = racSources[selectedSourceIdx].GetComponentsInChildren<TextMeshProUGUI>();

                foreach (TextMeshProUGUI label in sourceLabels)
                {
                    if (racSources[selectedSourceIdx].IsMuted())
                        label.color = Color.gray5;
                    else
                        label.color = Color.white;
                }
            }

            previousFrameMouseRight = true;
        }
        else
            previousFrameMouseRight = false;

        if (Input.GetMouseButton(0))
        {
            // The source is currently being dragged. Update its position and make sure it does not move on its own.
            DragSource(racSources[selectedSourceIdx]);
            previousFrameMouseLeft = true;
        }
        else
        {
            // If the mouse button is NOT down, forget the selected source. It needs to be refreshed at the next frame.
            // By contrast, if the mouse button IS down, the selected source will be the same at the next frame.
            // N.B.: This must be the last operation in the loop, or it will mess up logic like the right clicks.
            selectedSourceIdx = -1;
            previousFrameMouseLeft = false;
        }

        RefreshLabelStyles();
    }

    void DragSource(RACAudioSource draggedSource)
    {
        // Update the position of the source being dragged.
        Vector3 draggedPosition;
        draggedPosition.x = worldCursorPos.x;
        draggedPosition.z = worldCursorPos.y;
        draggedPosition.y = racSources[selectedSourceIdx].transform.position.y;

        // Find the AI agent associated to the source being dragged, if it has one.
        NavMeshAgent draggedSourceAgent = draggedSource.GetComponent<NavMeshAgent>();

        // If the source has a NavMeshAgent, disable it during movement or it will get cranky.
        if (draggedSourceAgent != null)
            draggedSourceAgent.enabled = false;
        // If the source DOESN'T have a NavMeshAgent, and its movement is forbidden, emphasize the warning text.
        else if (!allowMovingNonAgents && forbiddenDragAnimator != null)
        {
            Emphasize();
            return;
        }

        draggedSource.transform.SetPositionAndRotation(draggedPosition, draggedSource.transform.rotation);
    }

    void StopDragging(RACAudioSource draggedSource)
    {
        // Find the AI agent associated to the source being dragged, if it has one.
        NavMeshAgent draggedSourceAgent = draggedSource.GetComponent<NavMeshAgent>();

        if (draggedSourceAgent == null)
            return;

        draggedSourceAgent.nextPosition = draggedSource.transform.position;
        draggedSourceAgent.enabled = true;
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
}
