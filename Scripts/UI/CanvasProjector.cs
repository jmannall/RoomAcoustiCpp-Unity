using UnityEngine;
public class CanvasProjector : MonoBehaviour {
    public Canvas canvas;
    public Transform target;
    public bool maintainOffset = false;

    private Vector3 offset = Vector3.zero;
    private Vector3 screenPos;
    private Vector2 movePos;
    private Camera cam;

    void Start()
    {
        // Use the canvas' camera (fallback to Camera.main if none is set)
        cam = canvas.worldCamera;
        if (cam == null)
            cam = Camera.main;

        if (maintainOffset)
            offset = transform.position - worldToUISpace();
    }

    void Update() { transform.position = worldToUISpace() + offset; }

    // https://stackoverflow.com/a/45047232
    public Vector3 worldToUISpace()
    {
        // Convert world to screen using the same camera that renders the canvas
        screenPos = cam.WorldToScreenPoint(target.position);

        // Convert the screenpoint to ui rectangle local point
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPos, cam, out movePos);

        // Convert the local point to world point on the canvas plane
        return canvas.transform.TransformPoint(movePos);
    }
}
