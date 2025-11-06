using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class Billboard : MonoBehaviour
{
    public Transform POV;

    void Update()
    {
        // https://discussions.unity.com/t/please-help-me-with-script-making-ui-canvas-look-towards-camera/586675/4
        transform.LookAt(transform.position + POV.rotation * Vector3.forward, POV.rotation * Vector3.up);
    }
}
