using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class MinimapClickHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField]
    private XRMinimapController minimapController;

    public void OnPointerClick(PointerEventData eventData)
    {
        RectTransform minimapRect = GetComponent<RectTransform>();
        if (minimapRect == null)
            return;

        Camera cam;
        if (eventData.pressEventCamera != null)
            cam = eventData.pressEventCamera;
        else
            cam = eventData.enterEventCamera;
        if (cam == null)
            return;

        Vector2 localCursor;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            minimapRect, eventData.position, cam, out localCursor))
            return;

        Vector2 pixelCoords = new Vector2(
            localCursor.x - minimapRect.rect.xMin,
            localCursor.y - minimapRect.rect.yMin
        );

        Debug.Log($"MinimapClickHandler detected Canvas click at {pixelCoords}.");

        minimapController.RegisterClick(pixelCoords);
    }
}
