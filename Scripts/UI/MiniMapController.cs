using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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

    private RACAudioSource[] racSources;
    private int selectedSourceIdx = -1;

    void Start()
    {
        mapRectTransform = GetComponent<RectTransform>();

        if (GetComponentInParent<Canvas>() != null)
            mapEventCamera = GetComponentInParent<Canvas>().worldCamera;

        racSources = FindObjectsByType<RACAudioSource>(FindObjectsSortMode.None);
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
            // The cursor is OOB. Make sure that no source label is underlined.
            TextMeshProUGUI[] sourceLabel;
            for (int i = 0; i < racSources.Length; i++)
            {
                sourceLabel = racSources[i].gameObject.GetComponentsInChildren<TextMeshProUGUI>();

                foreach (TextMeshProUGUI l in sourceLabel)
                    l.fontStyle = FontStyles.Bold;
            }
            // If a source was being dragged, forget about it.
            selectedSourceIdx = -1;
            // Stop to avoid dragging a source OOB.
            return;
        }

        if (selectedSourceIdx < 0)
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
                return;
            }

            TextMeshProUGUI[] sourceLabel;
            for (int i = 0; i < racSources.Length; i++)
            {
                sourceLabel = racSources[i].gameObject.GetComponentsInChildren<TextMeshProUGUI>();

                foreach (TextMeshProUGUI l in sourceLabel)
                {
                if (i == selectedSourceIdx)
                    l.fontStyle = FontStyles.Bold | FontStyles.Underline;
                else
                    l.fontStyle = FontStyles.Bold;
                }
            }
        }

        if (Input.GetMouseButton(0))
        {
            // If the mouse button IS down, update the position of the source being dragged.
            Vector3 draggedSourcePosition;
            draggedSourcePosition.x = worldCursorPos.x;
            draggedSourcePosition.z = worldCursorPos.y;
            draggedSourcePosition.y = racSources[selectedSourceIdx].transform.position.y;
            Quaternion draggedSourceRotation = racSources[selectedSourceIdx].transform.rotation;
            racSources[selectedSourceIdx].transform.SetPositionAndRotation(draggedSourcePosition, draggedSourceRotation);
        }
        else
        {
            // If the mouse button is NOT down, forget the selected source. It needs to be refreshed at the next frame.
            selectedSourceIdx = -1;
            // By contrast, if the mouse button IS down, the selected source will be the same at the next frame.
        }
    }
}
