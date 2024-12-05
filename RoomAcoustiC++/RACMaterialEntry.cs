
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RACMaterialEntry", menuName = "RoomAcoustiC++/MaterialEntry", order = 1)]
public class RACMaterialEntry : ScriptableObject
{
    // Visible Unity material
    [Tooltip("The Unity material attached to these acoustic properties.")]
    public Material material;

    #region Parameters

    //////////////////// Parameters ////////////////////

    enum AbsorptionType
    {

        Default,
        FullAbsorption,
        Custom,

        // Harris:Handbook of Acoustical Measurements and Noise Control, McGraw Hill 1991
        [InspectorName("Harris 1991/Basalt/15mm")]
        Basalt15,
        [InspectorName("Harris 1991/Basalt/15mm Fine")]
        Basalt15Fine,
        [InspectorName("Harris 1991/Basalt/30mm")]
        Basalt30,
        [InspectorName("Harris 1991/Basalt/46mm")]
        Basalt46,
        [InspectorName("Harris 1991/Brick/Exposed")]
        Brick,
        [InspectorName("Harris 1991/Brick/Painted")]
        BrickPainted,
        [InspectorName("Harris 1991/Concrete Block/Exposed")]
        ConcreteBlock,
        [InspectorName("Harris 1991/Concrete Block/Painted")]
        ConcreteBlockPainted,
        [InspectorName("Harris 1991/Floor/Concrete")]
        ConcreteFloor,
        [InspectorName("Harris 1991/Marble")]
        Marble,
        [InspectorName("Harris 1991/Heavy Carpet/On Concrete")]
        CarpetHeavyOnConcrete,
        [InspectorName("Harris 1991/Heavy Carpet/On Foam Rubber")]
        CarpetHeavyOnFoamRubber,
        [InspectorName("Harris 1991/Heavy Carpet/Latex Backing On Foam Rubber")]
        CarpetHeavyLatexBackingOnFoamRubber,
        [InspectorName("Harris 1991/Curtains/Light")]
        CurtainsLight,
        [InspectorName("Harris 1991/Curtains/Medium")]
        CurtainsMedium,
        [InspectorName("Harris 1991/Curtains/Heavy")]
        CurtainsHeavy,
        [InspectorName("Harris 1991/Glass/Heavy Plate")]
        GlassHeavyPlate,
        [InspectorName("Harris 1991/Glass/Window")]
        GlassWindow,
        [InspectorName("Harris 1991/Gypsum Board")]
        GypsumBoard,
        [InspectorName("Harris 1991/Plaster/On Brick")]
        PlasterOnBrick,
        [InspectorName("Harris 1991/Plaster/On Lath")]
        PlasterOnLath,
        [InspectorName("Harris 1991/Floor/Wood")]
        WoodFloor,
        [InspectorName("Harris 1991/Floor/Wood Parquet On Concrete")]
        WoodParquetOnConcrete,
        [InspectorName("Harris 1991/Floor/Linoleum On Concrete")]
        LinoleumOnConcrete,
        [InspectorName("Harris 1991/Water Surface")]
        WaterSurface,

        // C.M.Harris, Noise Control in Buildings, McGraw Hill 1994
        [InspectorName("Harris 1994/Carpet/24mm Wool Loop")]
        WoolLoopCarpet24,
        [InspectorName("Harris 1994/Carpet/64mm Wool Loop")]
        WoolLoopCarpet64,
        [InspectorName("Harris 1994/Carpet/95mm Wool Loop")]
        WoolLoopCarpet95,
        [InspectorName("Harris 1994/Carpet/7mm Loop Pile")]
        LoopPileCarpet7,
        [InspectorName("Harris 1994/Carpet/7mm Loop Pile On Pad")]
        LoopPileCarpet7OnPad,
        [InspectorName("Harris 1994/Carpet/14mm Loop Pile On Pad")]
        LoopPileCarpet14OnPad,
        [InspectorName("Harris 1994/Acoustical Plaster")]
        AcousticalPlaster,

        // JCW https://www.acoustic-supplies.com/absorption-coefficient-chart/
        [InspectorName("JCW/Concrete")]
        Concrete,
        [InspectorName("JCW/Wooden Door")]
        WoodenDoor,
        [InspectorName("JCW/Glass/Small Pane")]
        GlassSmallPane,
        [InspectorName("JCW/Glass/Large Pane")]
        GlassLargePane,
        [InspectorName("JCW/Plasterboard/12mm On Studs")]
        Plasterboard12OnStuds,
        [InspectorName("JCW/Plywood/3mm Over 32mm Airspace")]
        Plywood3OverAirspace32,
        [InspectorName("JCW/Plywood/3mm Over 57mm Airspace")]
        Plywood3OverAirspace57,
        [InspectorName("JCW/Plywood/5mm Over 50mm Airspace")]
        Plywood5OverAirspace50,
        [InspectorName("JCW/Plywood/6mm")]
        Plywood6,
        [InspectorName("JCW/Plywood/10mm")]
        Plywood10,
        [InspectorName("JCW/Plywood/19mm")]
        Plywood19,
        [InspectorName("JCW/Ceiling/Suspended Plasterboard Grid")]
        PlasterboardCeilingSuspendedGrid,
        [InspectorName("JCW/Ceiling/Wooden Tongue Groove")]
        WoodenTougueGrooveCeiling,
        [InspectorName("JCW/Ceiling/Metal Deck 25mm Batts")]
        MetalDeck25Batts,
        [InspectorName("JCW/Ceiling/Metal Deck 75mm Batts")]
        MetalDeck75Batts,

        // JASA 104 (6) Dec 1998 Beranek / Hidaka
        [InspectorName("JASA Beranek 1998/Wooden Boards/On Concrete")]
        WoodBoardsOnConcrete,
        [InspectorName("JASA Beranek 1998/Concrete Block/Plastered")]
        ConcreteBlockPlastered,
        [InspectorName("JASA Beranek 1998/Carpet/Thin On Concrete")]
        CarpetThinOnConcrete,
        [InspectorName("JASA Beranek 1998/Gypsum Plasterboard")]
        GypsumPlasterBoard,
        [InspectorName("JASA Beranek 1998/Ceiling/Plaster 30mm")]
        PlasterCeiling30,
        [InspectorName("JASA Beranek 1998/Ceiling/Plaster 60mm")]
        PlasterCeiling60,
        [InspectorName("JASA Beranek 1998/Ceiling/Wood 28mm")]
        WoodCeiling28,
        [InspectorName("JASA Beranek 1998/Side Walls/Wood 12mm")]
        WoodSideWalls12,
        [InspectorName("JASA Beranek 1998/Side Walls/Wood 20mm")]
        WoodSidewalls20,
        [InspectorName("JASA Beranek 1998/Floor/Wood 33mm On Sleepers Over Concrete")]
        WoodFloor33OnSleepersOverConcrete,
        [InspectorName("JASA Beranek 1998/Floor/Wood 27mm Over Airspace")]
        WoodFloor27OverAirspace,
        [InspectorName("JASA Beranek 1998/Floor/Wood 19mm Over Fibreglass On Concrete")]
        Wood19OverFibreglassOnConcrete,
        [InspectorName("JASA Beranek 1998/Audience/Heavy Upholstery/Occupied")]
        AudienceHeavyOccupied,
        [InspectorName("JASA Beranek 1998/Audience/Medium Upholstery/Occupied")]
        AudienceMediumOccupied,
        [InspectorName("JASA Beranek 1998/Audience/Light Upholstery/Occupied")]
        AudienceLightOccupied,
        [InspectorName("JASA Beranek 1998/Audience/Heavy Upholstery/Unoccupied")]
        AudienceHeavyUnoccupied,
        [InspectorName("JASA Beranek 1998/Audience/Medium Upholstery/Unoccupied")]
        AudienceMediumUnoccupied,
        [InspectorName("JASA Beranek 1998/Audience/Light Upholstery/Unoccupied")]
        AudienceLightUnoccupied,

        // Acoustic Properties of Absorbent Asphalts: Amelia Trematerra and Ilaria Lombardi 2017 IOP Conf. Ser.: Mater. Sci. Eng. 225012081
        [InspectorName("IOP Trematerra 2017/Asphalt/Dry")]
        DryAsphalt,
        [InspectorName("IOP Trematerra 2017/Asphalt/Wet")]
        WetAsphalt,
        [InspectorName("IOP Trematerra 2017/Asphalt/Dirty")]
        DirtyAsphalt
    };

    [Serializable]
    public struct Entry
    {
        public float frequency;
        public float absorption;

        public Entry(float f, float a)
        {
            frequency = Mathf.Clamp(f, 20.0f, 20000.0f);
            absorption = Mathf.Clamp(a, 0.0f, 1.0f);
        }
    }

    [HideInInspector, SerializeField]
    private List<Entry> mAbsorptionMap;

    // Inspector variables
    [SerializeField]
    [Tooltip("Select material absorption.")]
    AbsorptionType absorption = AbsorptionType.Default;

    #endregion

    #region Unity Functions

    //////////////////// Unity Functions ////////////////////

    void Awake()
    {
        SetAbsorption();
    }

    #endregion

    #region Functions

    //////////////////// Functions ////////////////////

    public bool SetAbsorption()
    {
        switch (absorption)
        {
            case AbsorptionType.Basalt15:
                SetAbsorption(0.23f, 0.43f, 0.37f, 0.58f, 0.62f);
                break;
            case AbsorptionType.Basalt30:
                SetAbsorption(0.58f, 0.48f, 0.54f, 0.73f, 0.63f);
                break;
            case AbsorptionType.Basalt46:
                SetAbsorption(0.53f, 0.64f, 0.84f, 0.91f, 0.63f);
                break;
            case AbsorptionType.Basalt15Fine:
                SetAbsorption(0.64f, 0.7f, 0.79f, 0.88f, 0.72f);
                break;
            case AbsorptionType.Brick:
                SetAbsorption(0.03f, 0.03f, 0.04f, 0.05f, 0.07f);
                break;
            case AbsorptionType.BrickPainted:
                SetAbsorption(0.01f, 0.02f, 0.02f, 0.02f, 0.03f);
                break;
            case AbsorptionType.ConcreteBlock:
                SetAbsorption(0.44f, 0.31f, 0.29f, 0.39f, 0.25f);
                break;
            case AbsorptionType.ConcreteBlockPainted:
                SetAbsorption(0.05f, 0.06f, 0.07f, 0.09f, 0.08f);
                break;
            case AbsorptionType.ConcreteFloor:
                SetAbsorption(0.01f, 0.02f, 0.02f, 0.02f, 0.02f);
                break;
            case AbsorptionType.Marble:
                SetAbsorption(0.01f, 0.01f, 0.01f, 0.02f, 0.02f);
                break;
            case AbsorptionType.LinoleumOnConcrete:
                SetAbsorption(0.03f, 0.03f, 0.03f, 0.03f, 0.02f);
                break;
            case AbsorptionType.WoodBoardsOnConcrete:
                SetAbsorption(0.08f, 0.07f, 0.06f, 0.06f, 0.06f);
                break;
            case AbsorptionType.ConcreteBlockPlastered:
                SetAbsorption(0.05f, 0.05f, 0.04f, 0.04f, 0.04f);
                break;
            case AbsorptionType.CarpetThinOnConcrete:
                SetAbsorption(0.04f, 0.08f, 0.2f, 0.35f, 0.4f);
                break;
            case AbsorptionType.CarpetHeavyOnConcrete:
                SetAbsorption(0.06f, 0.14f, 0.37f, 0.6f, 0.65f);
                break;
            case AbsorptionType.CarpetHeavyOnFoamRubber:
                SetAbsorption(0.24f, 0.57f, 0.69f, 0.71f, 0.73f);
                break;
            case AbsorptionType.CarpetHeavyLatexBackingOnFoamRubber:
                SetAbsorption(0.27f, 0.39f, 0.34f, 0.48f, 0.63f);
                break;
            case AbsorptionType.CurtainsLight:
                SetAbsorption(0.04f, 0.11f, 0.17f, 0.24f, 0.35f);
                break;
            case AbsorptionType.CurtainsMedium:
                SetAbsorption(0.31f, 0.49f, 0.75f, 0.7f, 0.6f);
                break;
            case AbsorptionType.CurtainsHeavy:
                SetAbsorption(0.35f, 0.55f, 0.72f, 0.7f, 0.65f);
                break;
            case AbsorptionType.GlassHeavyPlate:
                SetAbsorption(0.06f, 0.04f, 0.03f, 0.02f, 0.02f);
                break;
            case AbsorptionType.GlassWindow:
                SetAbsorption(0.25f, 0.18f, 0.12f, 0.07f, 0.04f);
                break;
            case AbsorptionType.GypsumBoard:
                SetAbsorption(0.1f, 0.05f, 0.04f, 0.07f, 0.09f);
                break;
            case AbsorptionType.GypsumPlasterBoard:
                SetAbsorption(0.12f, 0.1f, 0.08f, 0.07f, 0.06f);
                break;
            case AbsorptionType.PlasterCeiling30:
                SetAbsorption(0.12f, 0.08f, 0.06f, 0.06f, 0.04f);
                break;
            case AbsorptionType.PlasterCeiling60:
                SetAbsorption(0.08f, 0.05f, 0.04f, 0.03f, 0.02f);
                break;
            case AbsorptionType.PlasterOnBrick:
                SetAbsorption(0.02f, 0.02f, 0.03f, 0.04f, 0.05f);
                break;
            case AbsorptionType.PlasterOnLath:
                SetAbsorption(0.1f, 0.06f, 0.05f, 0.04f, 0.03f);
                break;
            case AbsorptionType.WoodFloor:
                SetAbsorption(0.11f, 0.1f, 0.07f, 0.06f, 0.07f);
                break;
            case AbsorptionType.WoodParquetOnConcrete:
                SetAbsorption(0.04f, 0.07f, 0.06f, 0.06f, 0.07f);
                break;
            case AbsorptionType.WoodCeiling28:
                SetAbsorption(0.14f, 0.1f, 0.08f, 0.07f, 0.06f);
                break;
            case AbsorptionType.WoodSideWalls12:
                SetAbsorption(0.22f, 0.19f, 0.13f, 0.08f, 0.06f);
                break;
            case AbsorptionType.WoodSidewalls20:
                SetAbsorption(0.18f, 0.11f, 0.08f, 0.07f, 0.06f);
                break;
            case AbsorptionType.WoodFloor33OnSleepersOverConcrete:
                SetAbsorption(0.06f, 0.05f, 0.05f, 0.05f, 0.04f);
                break;
            case AbsorptionType.WoodFloor27OverAirspace:
                SetAbsorption(0.07f, 0.06f, 0.06f, 0.06f, 0.06f);
                break;
            case AbsorptionType.Wood19OverFibreglassOnConcrete:
                SetAbsorption(0.15f, 0.08f, 0.05f, 0.05f, 0.05f);
                break;
            case AbsorptionType.AudienceHeavyOccupied:
                SetAbsorption(0.76f, 0.81f, 0.84f, 0.84f, 0.81f);
                break;
            case AbsorptionType.AudienceMediumOccupied:
                SetAbsorption(0.62f, 0.68f, 0.7f, 0.68f, 0.66f);
                break;
            case AbsorptionType.AudienceLightOccupied:
                SetAbsorption(0.47f, 0.57f, 0.62f, 0.62f, 0.6f);
                break;
            case AbsorptionType.AudienceHeavyUnoccupied:
                SetAbsorption(0.76f, 0.81f, 0.84f, 0.84f, 0.81f);
                break;
            case AbsorptionType.AudienceMediumUnoccupied:
                SetAbsorption(0.62f, 0.68f, 0.7f, 0.68f, 0.66f);
                break;
            case AbsorptionType.AudienceLightUnoccupied:
                SetAbsorption(0.47f, 0.57f, 0.62f, 0.62f, 0.6f);
                break;
            case AbsorptionType.WaterSurface:
                SetAbsorption(0.01f, 0.01f, 0.02f, 0.02f, 0.03f);
                break;
            case AbsorptionType.WoolLoopCarpet24:
                SetAbsorption(0.16f, 0.11f, 0.3f, 0.5f, 0.47f);
                break;
            case AbsorptionType.WoolLoopCarpet64:
                SetAbsorption(0.17f, 0.12f, 0.32f, 0.52f, 0.57f);
                break;
            case AbsorptionType.WoolLoopCarpet95:
                SetAbsorption(0.18f, 0.21f, 0.5f, 0.63f, 0.83f);
                break;
            case AbsorptionType.LoopPileCarpet7:
                SetAbsorption(0.08f, 0.17f, 0.33f, 0.59f, 0.75f);
                break;
            case AbsorptionType.LoopPileCarpet7OnPad:
                SetAbsorption(0.19f, 0.35f, 0.79f, 0.69f, 0.79f);
                break;
            case AbsorptionType.LoopPileCarpet14OnPad:
                SetAbsorption(0.25f, 0.55f, 0.7f, 0.62f, 0.84f);
                break;
            case AbsorptionType.Concrete:
                SetAbsorption(0.02f, 0.04f, 0.06f, 0.08f, 0.1f);
                break;
            case AbsorptionType.WoodenDoor:
                SetAbsorption(0.07f, 0.05f, 0.04f, 0.04f, 0.04f);
                break;
            case AbsorptionType.GlassLargePane:
                SetAbsorption(0.06f, 0.04f, 0.03f, 0.02f, 0.02f);
                break;
            case AbsorptionType.GlassSmallPane:
                SetAbsorption(0.04f, 0.04f, 0.03f, 0.02f, 0.02f);
                break;
            case AbsorptionType.Plasterboard12OnStuds:
                SetAbsorption(0.1f, 0.06f, 0.05f, 0.04f, 0.04f);
                break;
            case AbsorptionType.Plywood3OverAirspace32:
                SetAbsorption(0.25f, 0.12f, 0.08f, 0.08f, 0.08f);
                break;
            case AbsorptionType.Plywood3OverAirspace57:
                SetAbsorption(0.2f, 0.1f, 0.1f, 0.08f, 0.08f);
                break;
            case AbsorptionType.Plywood5OverAirspace50:
                SetAbsorption(0.24f, 0.17f, 0.1f, 0.08f, 0.05f);
                break;
            case AbsorptionType.Plywood6:
                SetAbsorption(0.25f, 0.15f, 0.1f, 0.1f, 0.1f);
                break;
            case AbsorptionType.Plywood10:
                SetAbsorption(0.22f, 0.17f, 0.09f, 0.1f, 0.11f);
                break;
            case AbsorptionType.Plywood19:
                SetAbsorption(0.18f, 0.15f, 0.12f, 0.1f, 0.1f);
                break;
            case AbsorptionType.PlasterboardCeilingSuspendedGrid:
                SetAbsorption(0.11f, 0.04f, 0.04f, 0.07f, 0.08f);
                break;
            case AbsorptionType.WoodenTougueGrooveCeiling:
                SetAbsorption(0.19f, 0.14f, 0.08f, 0.13f, 0.1f);
                break;
            case AbsorptionType.MetalDeck25Batts:
                SetAbsorption(0.69f, 0.99f, 0.88f, 0.52f, 0.27f);
                break;
            case AbsorptionType.MetalDeck75Batts:
                SetAbsorption(0.99f, 0.99f, 0.89f, 0.52f, 0.31f);
                break;
            case AbsorptionType.DryAsphalt:
                SetAbsorption(0.2f, 0.38f, 0.19f, 0.46f, 0.46f);
                break;
            case AbsorptionType.WetAsphalt:
                SetAbsorption(0.2f, 0.38f, 0.16f, 0.66f, 0.66f);
                break;
            case AbsorptionType.DirtyAsphalt:
                SetAbsorption(0.2f, 0.5f, 0.19f, 0.35f, 0.35f);
                break;
            case AbsorptionType.AcousticalPlaster:
                SetAbsorption(0.35f, 0.64f, 0.73f, 0.73f, 0.77f);
                break;
            case AbsorptionType.FullAbsorption:
                SetAbsorption(1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
                break;
            case AbsorptionType.Custom:
                return true;
            default:
                SetAbsorption(0.1f, 0.1f, 0.1f, 0.1f, 0.1f);
                break;
        }
        return false;
        // UpdateSliders();
    }
    public void SetAbsorption(List<Entry> a) { mAbsorptionMap = a; }

    private void SetAbsorption(float a0, float a1, float a2, float a3, float a4)
    {
        mAbsorptionMap = new List<Entry>
        {
            { new Entry(250, a0) },
            { new Entry(500, a1) },
            { new Entry(1000, a2) },
            { new Entry(2000, a3) },
            { new Entry(4000, a4) }
        };
    }

    public void Custom() { absorption = AbsorptionType.Custom; }

    public void Default() { absorption = AbsorptionType.Default; SetAbsorption(); }

    public float[] GetAbsorption()
    {
        List<Entry> toReturn = mAbsorptionMap;

        bool firstEntry = true;
        int count = 0;
        int last = 0;
        int j;

        float[] absorption = new float[RACManager.racManager.fLimits.Count - 1];
        for (j = 0 ; j < absorption.Length; j++)
        {
            absorption[j] = 0.0f;
            for (int i = 0; i < toReturn.Count; i++)
            {
                if (RACManager.racManager.fLimits[j] < toReturn[i].frequency && toReturn[i].frequency < RACManager.racManager.fLimits[j + 1])
                {
                    absorption[j] += toReturn[i].absorption;
                    count++;
                }
            }

            if (count > 0)
            {
                absorption[j] /= count;
                if (firstEntry)
                {
                    // Extend the first value to the beginning
                    for (int k = 0; k < j; k++)
                        absorption[k] = absorption[j];
                    firstEntry = false;
                }
                else if (last < j - 1)
                {
                    int total = j - last;
                    // Interpolate missing values
                    for (int k = last + 1; k < j; k++)
                        absorption[k] = ((j - k) * absorption[last] + (k - last) * absorption[j]) / total;
                }
                count = 0;
                last = j;
            }
        }

        // Extend the last value to the end
        for (int k = last; k < j; k++)
            absorption[k] = absorption[last];

        return absorption;
    }

    public List<Entry> GetAbsorptionMap() { return mAbsorptionMap; }

    #endregion

}
