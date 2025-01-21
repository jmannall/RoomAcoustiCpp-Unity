
// Thanks to: https://stackoverflow.com/questions/43732825/use-debug-log-from-c

using AOT;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

[AddComponentMenu("RoomAcoustiC++/Debug C++")]
public class DebugCPP : MonoBehaviour
{
    // global singleton
    public static DebugCPP debug = null;

    static string debug_string = " ";
    private static Dictionary<string, List<Vector3>> pathDictionary = new Dictionary<string, List<Vector3>>();

    public Transform sourcePosition;
    public Transform listenerPosition;

    // Use this for initialization
    void Awake()
    {
        Debug.AssertFormat(debug == null, "More than one instance of the DebugCPP created! Singleton violated.");
        debug = this;

        RegisterDebugCallback(OnDebugCallback);
        RegisterPathCallback(OnPathCallback);
    }

    private void OnDisable()
    {
        UnregisterDebugCallback();
        UnregisterPathCallback();
    }

    private void OnDestroy()
    {
        pathDictionary.Clear();
    }

    #region DLL Interface
#if (UNITY_EDITOR)
    private const string DLLNAME = "RoomAcoustiCpp_x64";
#elif (UNITY_ANDROID)
        private const string DLLNAME = "libRoomAcoustiCpp";
#elif (UNITY_STANDALONE_WIN)
        private const string DLLNAME = "RoomAcoustiCpp_x64";
#else
    private const string DLLNAME = " ";
#endif
    #endregion

    //------------------------------------------------------------------------------------------------
    [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
    static extern void RegisterDebugCallback(debugCallback cb);

    [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
    static extern void RegisterPathCallback(pathCallback cb);

    [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void RegisterIEMCallback(iemCallback cb);

    [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
    static extern void UnregisterDebugCallback();

    [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
    static extern void UnregisterPathCallback();

    [DllImport(DLLNAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void UnregisterIEMCallback();

    //Create string param callback delegate
    delegate void debugCallback(IntPtr request, int colour, int size);

    delegate void pathCallback(IntPtr key, IntPtr intersections, int keySize, int intersectionsSize);

    public delegate void iemCallback(int id);

    enum Colour { red, green, blue, black, white, yellow, orange };
    [MonoPInvokeCallback(typeof(debugCallback))]
    static void OnDebugCallback(IntPtr request, int colour, int size)
    {
        //Ptr to string
        debug_string = Marshal.PtrToStringAnsi(request, size);

        //Add Specified Color
        debug_string =
            String.Format("{0}{1}{2}{3}{4}",
            "<color=",
            ((Colour)colour).ToString(),
            ">",
            debug_string,
            "</color>"
            );

        Debug.Log(debug_string);
    }

    static void OnPathCallback(IntPtr key, IntPtr intersections, int keySize, int intersectionsSize)
    {
        // Convert the IntPtr key to a string
        string keyString = Marshal.PtrToStringAnsi(key, keySize);

        if (intersectionsSize == 0)
        {
            pathDictionary.Remove(keyString);
            return;
        }

        // Convert the IntPtr intersections to a float array
        int floatArraySize = intersectionsSize * 3; // Each Vector3 has 3 floats
        float[] floatArray = new float[floatArraySize];
        Marshal.Copy(intersections, floatArray, 0, floatArraySize);

        // Create a list of Vector3 from the float array
        List<Vector3> vectors = new List<Vector3>();
        for (int i = 0; i < floatArray.Length; i += 3)
            vectors.Add(new Vector3(floatArray[i], floatArray[i + 1], floatArray[i + 2]));

        pathDictionary[keyString] = vectors;
    }

    private void OnDrawGizmos()
    {
        // Create a GUIStyle with a larger font size
        GUIStyle style = new GUIStyle();
        style.fontSize = 32; // Adjust this value for a larger or smaller font
        style.normal.textColor = Color.white; // Text color

        foreach (var path in pathDictionary)
        {
            if (path.Key.Contains('_'))
            {
                Gizmos.color = Color.magenta;
                Vector3 endPoint = path.Value[0] + path.Value[1].normalized;
                Gizmos.DrawRay(path.Value[0], path.Value[1].normalized);
                Gizmos.DrawWireCube(endPoint, new Vector3(0.1f, 0.1f, 0.1f));

                // Extract the number after the underscore in path.Key
                string[] parts = path.Key.Split('_');
                if (parts.Length > 1 && int.TryParse(parts[1], out int number))
                {
                    number++;
                    // Display the number as a label at the cube's position
                    Handles.Label(endPoint, number.ToString(), style);
                }
                continue;
            }

            if (path.Key.Contains('r'))
            {
                if (path.Key.Contains('d'))
                    Gizmos.color = Color.yellow;
                else
                    Gizmos.color = Color.red;
            }
            else
            {
                Gizmos.color = Color.green;
            }
            Gizmos.DrawLine(sourcePosition.position, path.Value[0]);
            Gizmos.DrawLineStrip(path.Value.ToArray(), false);
            Gizmos.DrawLine(listenerPosition.position, path.Value[path.Value.Count - 2]);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(path.Value[path.Value.Count - 1], new Vector3(0.1f, 0.1f, 0.1f));
            Handles.Label(path.Value[path.Value.Count - 1], path.Key, style);
        }
    }
}