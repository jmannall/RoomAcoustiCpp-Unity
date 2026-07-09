using UnityEngine;
public class WireframeCam : MonoBehaviour {
  void OnPreRender()  { GL.wireframe = true;  }
  void OnPostRender() { GL.wireframe = false; }
}
