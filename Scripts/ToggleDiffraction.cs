
using UnityEngine;
using UnityEngine.InputSystem;
using static Unity.VisualScripting.Member;

public class ToggleDiffraction : MonoBehaviour
{
    RACManager.IEMConfig iemConfig;

    public InputActionAsset inputActions;

    private InputActionMap controlActionMap;
    private InputAction toggleDiff;
    private InputAction toggleRefDiff;

    private void Awake()
    {
        controlActionMap = inputActions.FindActionMap("Control");
        controlActionMap.Enable();
    }
    // Start is called before the first frame update
    void Start()
    {

        toggleDiff = controlActionMap["ToggleDiff"];
        toggleRefDiff = controlActionMap["ToggleRefDiff"];

        iemConfig = new RACManager.IEMConfig();
        iemConfig.order = 3;
        iemConfig.direct = RACManager.DirectSound.Check;
        iemConfig.reflection = true;
        iemConfig.diffraction = RACManager.DiffractionSound.AllZones;
        iemConfig.reflectionDiffraction = RACManager.DiffractionSound.ShadowZone;
        iemConfig.lateReverb = true;
        iemConfig.minimumEdgeLength = 0.0f;
    }

    // Update is called once per frame
    void Update()
    {
        if (toggleDiff.triggered)
            ToggleDiff();

        if(toggleRefDiff.triggered)
            ToggleRefDiff();
    }

    void ToggleDiff()
    {
        if (iemConfig.diffraction == RACManager.DiffractionSound.AllZones)
            iemConfig.diffraction = RACManager.DiffractionSound.None;
        else
            iemConfig.diffraction = RACManager.DiffractionSound.AllZones;
        RACManager.UpdateIEMConfig(iemConfig);
    }

    void ToggleRefDiff()
    {
        if (iemConfig.reflectionDiffraction == RACManager.DiffractionSound.ShadowZone)
            iemConfig.reflectionDiffraction = RACManager.DiffractionSound.None;
        else
            iemConfig.reflectionDiffraction = RACManager.DiffractionSound.ShadowZone;
        RACManager.UpdateIEMConfig(iemConfig);
    }
}
