using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class MiniMapController : MonoBehaviour, IPointerClickHandler
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

    private RACAudioSource[] racSources;

    void Start()
    {
        mapRectTransform = GetComponent<RectTransform>();

        if (GetComponentInParent<Canvas>() != null)
            mapEventCamera = GetComponentInParent<Canvas>().worldCamera;

        racSources = FindObjectsByType<RACAudioSource>(FindObjectsSortMode.None);
    }

    void Update()
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapRectTransform, Input.mousePosition, mapEventCamera, out screenCursorPos))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(mapRectTransform,
                Input.mousePosition, mapEventCamera))
                return;

            screenCursorPos.x -= mapRectTransform.rect.x;
            screenCursorPos.y -= mapRectTransform.rect.y;

            worldCursorPos = screenCursorPos / pixelsPerMeter;

            //Debug.Log($"Pointer: {worldCursorPos} ({Input.GetMouseButton(0)})");
        }
        else
            return;
        
        if (!Input.GetMouseButton(0))
        {
            float minSourceDist = float.MaxValue;
            int selectedSourceIdx = -1;
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
                return;

            string sourceName = racSources[selectedSourceIdx].gameObject.name;
            Debug.Log($"Closest source: {sourceName}");
        }
        else
            Debug.Log("Clicking...");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRectTransform,
            eventData.pressPosition, eventData.pressEventCamera, out screenCursorPos))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(mapRectTransform,
                Input.mousePosition, eventData.pressEventCamera))
                return;

            screenCursorPos.x -= mapRectTransform.rect.x;
            screenCursorPos.y -= mapRectTransform.rect.y;

            worldCursorPos = screenCursorPos / pixelsPerMeter;

            Debug.Log($"Clicked position: {worldCursorPos}");
        }
    }
}
