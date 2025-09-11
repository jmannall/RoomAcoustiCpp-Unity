using UnityEngine;
public class CanvasProjector : MonoBehaviour {
    public Canvas canvas;
    public Transform target;
    public bool maintainOffset = false;

    private Vector3 offset = Vector3.zero;
    private Vector3 screenPos;
    private Vector2 movePos;

    void Start()
    {
        if (maintainOffset)
            offset = transform.position - worldToUISpace();
    }

    void Update() { transform.position = worldToUISpace() + offset; }

    // https://stackoverflow.com/a/45047232
    public Vector3 worldToUISpace()
    {
        screenPos = Camera.main.WorldToScreenPoint(target.position);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPos, canvas.worldCamera, out movePos);

        return canvas.transform.TransformPoint(movePos);
    }
}
