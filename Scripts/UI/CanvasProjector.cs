using TMPro;
using UnityEngine;

public class CanvasProjector : MonoBehaviour
{
    public Canvas targetCanvas;
    public Camera targetCamera;
    public Transform targetObject;
    public float minDistance = 0.5f;
    [SerializeField, Range(0, 1)]
    public float distanceScaling;

    private TextMeshProUGUI textBox;
    private string defaultText;
    private float defaultSize;

    private float distanceFromCamera;
    private Vector3 screenSpacePos;
    RectTransform canvasRect;

    void Start()
    {
        textBox = GetComponent<TextMeshProUGUI>();
        defaultText = textBox.text;
        defaultSize = textBox.fontSize;

        canvasRect = targetCanvas.transform as RectTransform;

        if (minDistance < 0)
            minDistance = 0f;
    }

    void Update()
    {
        screenSpacePos = targetCamera.WorldToScreenPoint(targetObject.position);

        distanceFromCamera = Vector3.Dot(targetObject.position - targetCamera.transform.position, targetCamera.transform.forward);

        if (distanceFromCamera < minDistance)
        {
            textBox.text = "";
            return;
        }


        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenSpacePos, targetCanvas.worldCamera, out Vector2 localPoint))
        {
            // Check if within bounds of the canvas
            if (Mathf.Abs(localPoint.x) <= canvasRect.rect.width / 2 &&
                Mathf.Abs(localPoint.y) <= canvasRect.rect.height / 2)
            {
                transform.localPosition = localPoint;

                textBox.text = defaultText;
                textBox.fontSize = defaultSize / Mathf.Pow(distanceFromCamera, distanceScaling);
            }
            else
                textBox.text = "";
        }
        else
            textBox.text = "";
    }
}
