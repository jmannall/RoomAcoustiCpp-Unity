using TMPro;
using UnityEngine;

public class CanvasProjector : MonoBehaviour {
    public Canvas targetCanvas;
    public Camera targetCamera;
    public Transform targetObject;
    public float minDistance = 0.1f;
    [SerializeField, Range(0, 1)]
    public float distanceScaling;

    private TextMeshProUGUI textBox;
    private Vector3 screenPos;
    private Vector2 localPos;
    private float distanceFromCanvas;
    private string defaultText;
    private float defaultSize;

    void Start()
    {
        textBox = gameObject.GetComponent<TextMeshProUGUI>();
        defaultText = textBox.text;
        defaultSize = textBox.fontSize;
        if (minDistance < 0)
            minDistance = 0f;
    }

    void Update()
    {
        worldToUISpace();
        if (distanceFromCanvas > minDistance)
        {
            textBox.text = defaultText;
            textBox.fontSize = defaultSize / Mathf.Pow(distanceFromCanvas, distanceScaling);
            transform.position = screenPos;
        }
        else
            textBox.text = "";
    }

    // https://stackoverflow.com/a/45047232
    public void worldToUISpace()
    {
        screenPos = targetCamera.WorldToScreenPoint(targetObject.position);
        // If the target is on the wrong side of the canvas, return false
        distanceFromCanvas = screenPos.z;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.transform as RectTransform,
            screenPos, targetCanvas.worldCamera, out localPos);

        screenPos = targetCanvas.transform.TransformPoint(localPos);
    }
}
