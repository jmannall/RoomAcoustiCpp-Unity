
// Thanks to: https://stackoverflow.com/questions/43732825/use-debug-log-from-c

using AOT;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

[AddComponentMenu("RoomAcoustiC++/Debug C++")]
public class DebugCPP : MonoBehaviour
{
    static string debug_string = " ";

    // Use this for initialization
    void Awake()
    {
        RegisterDebugCallback(OnDebugCallback);
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
    //Create string param callback delegate
    delegate void debugCallback(IntPtr request, int colour, int size);
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

        UnityEngine.Debug.Log(debug_string);
    }
}