using System;
using UnityEngine;
namespace OnAir
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameEntry : MonoBehaviour
    {
        public GameSession session;
        public JourneyController journey;
        public FlightController flight;
        public CameraFollow cameraFollow;
        public TerrainStreamer world;
        public EnvironmentController environment;
        public WeatherParticles weather;
        public FlightPanel panel;
        public BiomeNavigator biomeNavigator;
        public Camera sceneCamera;
        public Light sun;
        public TextAsset buildingsTable,terrainsTable,weatherTable;
        public BuildingCatalog buildings;
        public TerrainCatalog terrains;
        public Shader weatherShader;
        public bool randomizeOnStart=true;
        public FrameRateDisplay frameRate;
        public AircraftCabinView cabin;
        public bool enableExperimentalWingView=false;
        public LayeredPixelClouds clouds;
        [Min(5)] public float autoSaveIntervalSeconds=30;
        public SaveController saves{get;private set;}
        [Range(1,8)] public int saveSlotCount=3;
        public int titleBackgroundSeed=2409;
        [Min(10)] public float preparationTimeoutSeconds=60;
        public MenuController menu{get;private set;}
        public DisplayController display{get;private set;}
        string loadedTerrains;
        void Awake()
        {
            if(!session||!journey||!flight||!cameraFollow||!world||!environment||!weather||!panel||!sceneCamera||!sun||!buildings||!terrains||!weatherShader)
                throw new InvalidOperationException("GameEntry: incomplete scene/resource references.");
            loadedTerrains=TableSource.Read(terrainsTable);
            var definitions=terrains.Resolve(TerrainTable.Parse(loadedTerrains));
            world.buildings=new BuildingData(TableSource.Read(buildingsTable),buildings);
            WeatherFxTable.Parse(TableSource.Read(weatherTable));
            var arguments=System.Environment.GetCommandLineArgs();
            journey.context=session;journey.terrains=definitions;
            if(journey.continuousWorld){sceneCamera.orthographicSize=68;sceneCamera.farClipPlane=1000;cameraFollow.offset*=2.5f;}
            flight.journey=journey;cameraFollow.target=flight;
            // World travel follows +Z; retain the authored camera and pixel-art view.
            flight.routeSlope=0;
            flight.ConfigureClearance(world.buildings);
            var aircraft=flight.GetComponent<FlightView>().modelPrefab;if(aircraft)flight.ConfigureAircraft(aircraft.GetComponent<AircraftRig>());
            flight.swayAmplitude=3;flight.swayFrequency=.09f;flight.bobAmplitude=.3f;
            flight.GetComponent<FlightView>().sceneCamera=sceneCamera;
            if(!flight.GetComponent<AircraftLights>())flight.gameObject.AddComponent<AircraftLights>();
            if(enableExperimentalWingView){cabin=flight.gameObject.AddComponent<AircraftCabinView>();cabin.view=flight.GetComponent<FlightView>();cabin.sceneCamera=sceneCamera;}
            else RenderSettings.fog=false;
            clouds=gameObject.AddComponent<LayeredPixelClouds>();clouds.game=this;
            world.wingView=cabin;world.journey=journey;world.flight=flight;world.sceneCamera=sceneCamera;world.environment=environment;environment.journey=journey;environment.world=world;environment.sceneCamera=sceneCamera;environment.sun=sun;
            weather.context=session;weather.sceneCamera=sceneCamera;weather.table=weatherTable;weather.shader=weatherShader;
            biomeNavigator=gameObject.AddComponent<BiomeNavigator>();biomeNavigator.game=this;
            panel.context=session;panel.journey=journey;panel.terrainWorld=world;panel.flight=flight;panel.weatherFx=weather;panel.game=this;
            frameRate=gameObject.AddComponent<FrameRateDisplay>();
            var traffic=gameObject.AddComponent<CityTraffic>();traffic.game=this;
            session.menuPaused=true;
            journey.seed=titleBackgroundSeed;
            saves=gameObject.AddComponent<SaveController>();saves.Initialize(this,false,arguments,true);
            display=gameObject.AddComponent<DisplayController>();display.Initialize(saves.RootDirectory,saves.SavingEnabled);
            menu=gameObject.AddComponent<MenuController>();menu.Initialize(this);
            var menuView=gameObject.AddComponent<MenuView>();menuView.game=this;
            flight.Tick(0);cameraFollow.Follow(0);environment.Refresh(0,true);
        }
        public bool ReloadTables()
        {
            try
            {
                string terrainText=TableSource.Read(terrainsTable);
                var nextTerrains=terrains.Resolve(TerrainTable.Parse(terrainText));
                var nextBuildings=new BuildingData(TableSource.Read(buildingsTable),buildings);
                WeatherFxTable.Parse(TableSource.Read(weatherTable));
                if(!weather.ReloadTable())return false;
                saves.BeforeContentChange();
                world.buildings=nextBuildings;
                flight.ConfigureClearance(nextBuildings);flight.Tick(0);cameraFollow.Follow(0);
                if(loadedTerrains!=terrainText){journey.ReplaceTerrains(nextTerrains);loadedTerrains=terrainText;}
                world.RebuildAll();saves.AfterContentChange();return true;
            }
            catch(Exception e){Debug.LogWarning("Tables unchanged: "+e.Message,this);return false;}
        }
    }
}
