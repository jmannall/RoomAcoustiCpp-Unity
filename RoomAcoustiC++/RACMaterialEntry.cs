
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
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

    [HideInInspector, SerializeField]
    private float[] customAbsorptions = new float[10] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
    private List<float> customFrequencies = new List<float> { 31.25f, 67.5f, 125.0f, 250.0f, 500.0f, 1e3f, 2e3f, 4e3f, 8e3f, 16e3f };

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

    public void SetAbsorption()
    {
        switch (absorption)
        {
            case AbsorptionType.Basalt15:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.23f, 0.43f, 0.37f, 0.58f, 0.62f);
                break;
            case AbsorptionType.Basalt30:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.58f, 0.48f, 0.54f, 0.73f, 0.63f);
                break;
            case AbsorptionType.Basalt46:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.53f, 0.64f, 0.84f, 0.91f, 0.63f);
                break;
            case AbsorptionType.Basalt15Fine:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.64f, 0.7f, 0.79f, 0.88f, 0.72f);
                break;
            case AbsorptionType.Brick:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.03f, 0.03f, 0.04f, 0.05f, 0.07f);
                break;
            case AbsorptionType.BrickPainted:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.01f, 0.02f, 0.02f, 0.02f, 0.03f);
                break;
            case AbsorptionType.ConcreteBlock:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.44f, 0.31f, 0.29f, 0.39f, 0.25f);
                break;
            case AbsorptionType.ConcreteBlockPainted:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.05f, 0.06f, 0.07f, 0.09f, 0.08f);
                break;
            case AbsorptionType.ConcreteFloor:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.01f, 0.02f, 0.02f, 0.02f, 0.02f);
                break;
            case AbsorptionType.Marble:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.01f, 0.01f, 0.01f, 0.02f, 0.02f);
                break;
            case AbsorptionType.LinoleumOnConcrete:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.03f, 0.03f, 0.03f, 0.03f, 0.02f);
                break;
            case AbsorptionType.WoodBoardsOnConcrete:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.08f, 0.07f, 0.06f, 0.06f, 0.06f);
                break;
            case AbsorptionType.ConcreteBlockPlastered:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.05f, 0.05f, 0.04f, 0.04f, 0.04f);
                break;
            case AbsorptionType.CarpetThinOnConcrete:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.04f, 0.08f, 0.2f, 0.35f, 0.4f);
                break;
            case AbsorptionType.CarpetHeavyOnConcrete:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.06f, 0.14f, 0.37f, 0.6f, 0.65f);
                break;
            case AbsorptionType.CarpetHeavyOnFoamRubber:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.24f, 0.57f, 0.69f, 0.71f, 0.73f);
                break;
            case AbsorptionType.CarpetHeavyLatexBackingOnFoamRubber:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.27f, 0.39f, 0.34f, 0.48f, 0.63f);
                break;
            case AbsorptionType.CurtainsLight:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.04f, 0.11f, 0.17f, 0.24f, 0.35f);
                break;
            case AbsorptionType.CurtainsMedium:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.31f, 0.49f, 0.75f, 0.7f, 0.6f);
                break;
            case AbsorptionType.CurtainsHeavy:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.35f, 0.55f, 0.72f, 0.7f, 0.65f);
                break;
            case AbsorptionType.GlassHeavyPlate:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.06f, 0.04f, 0.03f, 0.02f, 0.02f);
                break;
            case AbsorptionType.GlassWindow:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.25f, 0.18f, 0.12f, 0.07f, 0.04f);
                break;
            case AbsorptionType.GypsumBoard:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.1f, 0.05f, 0.04f, 0.07f, 0.09f);
                break;
            case AbsorptionType.GypsumPlasterBoard:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.12f, 0.1f, 0.08f, 0.07f, 0.06f);
                break;
            case AbsorptionType.PlasterCeiling30:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.12f, 0.08f, 0.06f, 0.06f, 0.04f);
                break;
            case AbsorptionType.PlasterCeiling60:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.08f, 0.05f, 0.04f, 0.03f, 0.02f);
                break;
            case AbsorptionType.PlasterOnBrick:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.02f, 0.02f, 0.03f, 0.04f, 0.05f);
                break;
            case AbsorptionType.PlasterOnLath:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.1f, 0.06f, 0.05f, 0.04f, 0.03f);
                break;
            case AbsorptionType.WoodFloor:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.11f, 0.1f, 0.07f, 0.06f, 0.07f);
                break;
            case AbsorptionType.WoodParquetOnConcrete:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.04f, 0.07f, 0.06f, 0.06f, 0.07f);
                break;
            case AbsorptionType.WoodCeiling28:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.14f, 0.1f, 0.08f, 0.07f, 0.06f);
                break;
            case AbsorptionType.WoodSideWalls12:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.22f, 0.19f, 0.13f, 0.08f, 0.06f);
                break;
            case AbsorptionType.WoodSidewalls20:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.18f, 0.11f, 0.08f, 0.07f, 0.06f);
                break;
            case AbsorptionType.WoodFloor33OnSleepersOverConcrete:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.06f, 0.05f, 0.05f, 0.05f, 0.04f);
                break;
            case AbsorptionType.WoodFloor27OverAirspace:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.07f, 0.06f, 0.06f, 0.06f, 0.06f);
                break;
            case AbsorptionType.Wood19OverFibreglassOnConcrete:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.15f, 0.08f, 0.05f, 0.05f, 0.05f);
                break;
            case AbsorptionType.AudienceHeavyOccupied:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.76f, 0.81f, 0.84f, 0.84f, 0.81f);
                break;
            case AbsorptionType.AudienceMediumOccupied:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.62f, 0.68f, 0.7f, 0.68f, 0.66f);
                break;
            case AbsorptionType.AudienceLightOccupied:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.47f, 0.57f, 0.62f, 0.62f, 0.6f);
                break;
            case AbsorptionType.AudienceHeavyUnoccupied:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.76f, 0.81f, 0.84f, 0.84f, 0.81f);
                break;
            case AbsorptionType.AudienceMediumUnoccupied:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.62f, 0.68f, 0.7f, 0.68f, 0.66f);
                break;
            case AbsorptionType.AudienceLightUnoccupied:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.47f, 0.57f, 0.62f, 0.62f, 0.6f);
                break;
            case AbsorptionType.WaterSurface:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.01f, 0.01f, 0.02f, 0.02f, 0.03f);
                break;
            case AbsorptionType.WoolLoopCarpet24:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.16f, 0.11f, 0.3f, 0.5f, 0.47f);
                break;
            case AbsorptionType.WoolLoopCarpet64:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.17f, 0.12f, 0.32f, 0.52f, 0.57f);
                break;
            case AbsorptionType.WoolLoopCarpet95:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.18f, 0.21f, 0.5f, 0.63f, 0.83f);
                break;
            case AbsorptionType.LoopPileCarpet7:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.08f, 0.17f, 0.33f, 0.59f, 0.75f);
                break;
            case AbsorptionType.LoopPileCarpet7OnPad:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.19f, 0.35f, 0.79f, 0.69f, 0.79f);
                break;
            case AbsorptionType.LoopPileCarpet14OnPad:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.25f, 0.55f, 0.7f, 0.62f, 0.84f);
                break;
            case AbsorptionType.Concrete:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.02f, 0.04f, 0.06f, 0.08f, 0.1f);
                break;
            case AbsorptionType.WoodenDoor:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.07f, 0.05f, 0.04f, 0.04f, 0.04f);
                break;
            case AbsorptionType.GlassLargePane:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.06f, 0.04f, 0.03f, 0.02f, 0.02f);
                break;
            case AbsorptionType.GlassSmallPane:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.04f, 0.04f, 0.03f, 0.02f, 0.02f);
                break;
            case AbsorptionType.Plasterboard12OnStuds:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.1f, 0.06f, 0.05f, 0.04f, 0.04f);
                break;
            case AbsorptionType.Plywood3OverAirspace32:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.25f, 0.12f, 0.08f, 0.08f, 0.08f);
                break;
            case AbsorptionType.Plywood3OverAirspace57:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.2f, 0.1f, 0.1f, 0.08f, 0.08f);
                break;
            case AbsorptionType.Plywood5OverAirspace50:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.24f, 0.17f, 0.1f, 0.08f, 0.05f);
                break;
            case AbsorptionType.Plywood6:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.25f, 0.15f, 0.1f, 0.1f, 0.1f);
                break;
            case AbsorptionType.Plywood10:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.22f, 0.17f, 0.09f, 0.1f, 0.11f);
                break;
            case AbsorptionType.Plywood19:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.18f, 0.15f, 0.12f, 0.1f, 0.1f);
                break;
            case AbsorptionType.PlasterboardCeilingSuspendedGrid:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.11f, 0.04f, 0.04f, 0.07f, 0.08f);
                break;
            case AbsorptionType.WoodenTougueGrooveCeiling:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.19f, 0.14f, 0.08f, 0.13f, 0.1f);
                break;
            case AbsorptionType.MetalDeck25Batts:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.69f, 0.99f, 0.88f, 0.52f, 0.27f);
                break;
            case AbsorptionType.MetalDeck75Batts:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.99f, 0.99f, 0.89f, 0.52f, 0.31f);
                break;
            case AbsorptionType.DryAsphalt:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.2f, 0.38f, 0.19f, 0.46f, 0.46f);
                break;
            case AbsorptionType.WetAsphalt:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.2f, 0.38f, 0.16f, 0.66f, 0.66f);
                break;
            case AbsorptionType.DirtyAsphalt:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.2f, 0.5f, 0.19f, 0.35f, 0.35f);
                break;
            case AbsorptionType.AcousticalPlaster:
                SetAbsorption(new List<float> { 250.0f, 500.0f, 1e3f, 2e3f, 4e3f }, 0.35f, 0.64f, 0.73f, 0.73f, 0.77f);
                break;
            case AbsorptionType.FullAbsorption:
                SetAbsorption(new List<float> { 250.0f }, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
                break;
            case AbsorptionType.Custom:
                UpdateAbsorption(customFrequencies, customAbsorptions);
                break;
            default:
                SetAbsorption(new List<float> { 250.0f }, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f);
                break;
        }
    }
    public void SetAbsorption(List<Entry> a) { mAbsorptionMap = a; }

    private void SetAbsorption(List<float> refFreqs, params float[] absorptions)
    {
        UpdateAbsorption(refFreqs, absorptions);
        UpdateCustomAbsorption(mAbsorptionMap);
    }

    public void ResetCustomAbsorption()
    {
        if (customAbsorptions.Length != 10)
            customAbsorptions = new float[10] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
    }

    private void UpdateAbsorption(List<float> refFreqs, params float[] absorptions)
    {
        List<float> freqs = null;
        if (Application.isPlaying && RACManager.racManager != null)
            freqs = RACManager.racManager.frequencyBands;
        else
        {
#if UNITY_EDITOR
            var racManagerInstance = UnityEngine.Object.FindAnyObjectByType<RACManager>();
            if (racManagerInstance != null)
                freqs = racManagerInstance.frequencyBands;
#endif
        }

        if (freqs == null)
            return;

        mAbsorptionMap = new List<Entry>();

        int maxIndex = absorptions.Length - 1;

        for (int i = 0; i < freqs.Count; i++)
        {
            float freq = freqs[i];
            float absorption;
            if (freq < refFreqs[0])
                absorption = absorptions[0];
            else if (freq > refFreqs[refFreqs.Count - 1])
                absorption = absorptions[maxIndex];
            else
            {
                int closestIndex = 0;
                float smallestDiff = Mathf.Abs(freq - refFreqs[0]);

                for (int j = 1; j < refFreqs.Count; j++)
                {
                    float diff = Mathf.Abs(freq - refFreqs[j]);
                    if (diff < smallestDiff)
                    {
                        smallestDiff = diff;
                        closestIndex = j;
                    }
                }

                closestIndex = Mathf.Clamp(closestIndex, 0, maxIndex);
                absorption = absorptions[closestIndex];
            }

            mAbsorptionMap.Add(new Entry(freq, absorption));
        }
    }

    public void Custom() { absorption = AbsorptionType.Custom; }

    public void Default() { absorption = AbsorptionType.Default; SetAbsorption(); }

    public void UpdateCustomAbsorption(List<Entry> currentValues)
    {
        if (currentValues == null)
            return;

        int count = currentValues.Count;

        for (int i = 0; i < count; i++)
        {
            float targetFreq = currentValues[i].frequency;
            float absorptionValue = currentValues[i].absorption;

            // Find closest index in customFrequencies
            int closestIndex = -1;
            float minDiff = float.MaxValue;

            for (int j = 0; j < customFrequencies.Count; j++)
            {
                float diff = Mathf.Abs(customFrequencies[j] - targetFreq);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestIndex = j;
                }
            }

            // Update absorption value at closest index
            if (closestIndex >= 0 && closestIndex < customAbsorptions.Length)
                customAbsorptions[closestIndex] = absorptionValue;
        }
    }

    public float[] GetAbsorption()
    {
        float[] absorption = new float[mAbsorptionMap.Count];

        for (int i = 0; i < absorption.Length; i++)
            absorption[i] = mAbsorptionMap[i].absorption;
        return absorption;
    }

    public List<Entry> GetAbsorptionMap() { return mAbsorptionMap; }

    #endregion

}
