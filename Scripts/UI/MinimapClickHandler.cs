using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class MinimapClickHandler : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum MapEventType { Click, RightClick, BeginDrag, Drag, EndDrag }

    [SerializeField]
    [Tooltip("Width of the minimap (including left and right margins), in meters.")]
    private float widthInMeters;
    [SerializeField]
    [Tooltip("Height of the minimap (including top and bottom margins), in meters.")]
    private float heightInMeters;
    [SerializeField]
    [Tooltip("Margin below the minimap, in meters.")]
    private float bottomMarginInMeters;
    [SerializeField]
    [Tooltip("Margin to the left of the minimap, in meters.")]
    private float leftMarginInMeters;
    [SerializeField]
    [Tooltip("Margin above the minimap, in meters.")]
    private float topMarginInMeters;
    [SerializeField]
    [Tooltip("Margin to the right of the minimap, in meters.")]
    private float rightMarginInMeters;

    [SerializeField]
    private TextMeshProUGUI debugText;

    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button == PointerEventData.InputButton.Right)
            HandleEvent(eventData, MapEventType.RightClick);
        else
            HandleEvent(eventData, MapEventType.Click);
    }

    public void OnBeginDrag(PointerEventData eventData) { HandleEvent(eventData, MapEventType.BeginDrag); }

    public void OnDrag(PointerEventData eventData) { HandleEvent(eventData, MapEventType.Drag); }

    public void OnEndDrag(PointerEventData eventData) { HandleEvent(eventData, MapEventType.EndDrag); }

    private void HandleEvent(PointerEventData eventData, MapEventType type)
    {
        Vector2 worldCoords = GetWorldCoordinates(eventData);

        if (debugText != null)
        {
            string text;
            switch (type)
            {
                case MapEventType.Click:
                    text = "New OnPointerClick event (left).";
                    break;
                case MapEventType.RightClick:
                    text = "New OnPointerClick event (right).";
                    break;
                case MapEventType.BeginDrag:
                    text = "New OnPointerClick event.";
                    break;
                case MapEventType.Drag:
                    text = "New OnPointerClick event.";
                    break;
                case MapEventType.EndDrag:
                    text = "New OnPointerClick event.";
                    break;
                default:
                    text = "New event (unrecognized type).";
                    break;
            }
            text += BuildDebugMessage(eventData);
            text += "\nrectCoords " + worldCoords.ToString();

            debugText.text = text;
            //Debug.Log(text);
        }

        // If a Drag event is going out of bounds, count it as an EndDrag event instead.
        // This will result in the source being dropped at the position of the latest valid Drag event.
        if (type == MapEventType.Drag &&
            (worldCoords.x < 0 || worldCoords.x > widthInMeters - (leftMarginInMeters + rightMarginInMeters) ||
             worldCoords.y < 0 || worldCoords.y > heightInMeters - (bottomMarginInMeters + topMarginInMeters)))
            type = MapEventType.EndDrag;

        if (MinimapController.minimapController != null)
            MinimapController.minimapController.RegisterEvent(eventData, type, worldCoords);
        else if (XRMinimapController.xrMinimapController != null)
            XRMinimapController.xrMinimapController.RegisterEvent(eventData, type, worldCoords);
    }

    // Returns Vector2.positiveInfinity if the coordinates are bad for any reason.
    private Vector2 GetWorldCoordinates(PointerEventData eventData)
    {
        RectTransform minimapRect = GetComponent<RectTransform>();
        if (minimapRect == null)
            return Vector2.positiveInfinity;

        Vector2 rectCoords;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            minimapRect, eventData.position, null, out rectCoords))
            return Vector2.positiveInfinity;

        rectCoords.x -= minimapRect.rect.x;
        rectCoords.y -= minimapRect.rect.y;
        rectCoords.x /= minimapRect.rect.size.x;
        rectCoords.y /= minimapRect.rect.size.y;

        Vector2 worldCoords;
        worldCoords.x = rectCoords.x * widthInMeters - leftMarginInMeters;
        worldCoords.y = rectCoords.y * heightInMeters - bottomMarginInMeters;

        return worldCoords;
    }

    private string BuildDebugMessage(PointerEventData eventData)
    {
        string text = "";
        try
        {
            text += "\neventData.button " + eventData.button.ToString();
        }
        catch (Exception e)
        {
            text += "\neventData.button " + e.ToString();
        }
        try
        {
            text += "\neventData.dragging " + eventData.dragging.ToString();
        }
        catch (Exception e)
        {
            text += "\neventData.dragging " + e.ToString();
        }
        try
        {
            text += "\neventData.pointerDrag " + eventData.pointerDrag.ToString();
        }
        catch (Exception e)
        {
            text += "\neventData.pointerDrag " + e.ToString();
        }
        try
        {
            text += "\neventData.pointerPress " + eventData.pointerPress.ToString();
        }
        catch (Exception e)
        {
            text += "\neventData.pointerPress " + e.ToString();
        }
        try
        {
            text += "\neventData.position " + eventData.position.ToString();
        }
        catch (Exception e)
        {
            text += "\neventData.position " + e.ToString();
        }
        try
        {
            text += "\neventData.pressPosition " + eventData.pressPosition.ToString();
        }
        catch (Exception e)
        {
            text += "\neventData.pressPosition " + e.ToString();
        }
        return text;
    }
}
