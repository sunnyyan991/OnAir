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
        public Camera sceneCamera;
        public Light sun;
        public TextAsset buildingsTable,terrainsTable,weatherTable;
        public BuildingCatalog buildings;
        public TerrainCatalog terrains;
        public Shader weatherShader;
        string loadedTerrains;
        void Awake()
        {
            if(!session||!journey||!flight||!cameraFollow||!world||!environment||!weather||!panel||!sceneCamera||!sun||!buildings||!terrains||!weatherShader)
                throw new InvalidOperationException("GameEntry: incomplete scene/resource references.");
            loadedTerrains=TableSource.Read(terrainsTable);
            var definitions=terrains.Resolve(TerrainTable.Parse(loadedTerrains));
            world.buildings=new BuildingData(TableSource.Read(buildingsTable),buildings);
            WeatherFxTable.Parse(TableSource.Read(weatherTable));
            journey.context=session;journey.terrains=definitions;journey.Initialize();
            flight.journey=journey;cameraFollow.target=flight;
            flight.GetComponent<FlightView>().sceneCamera=sceneCamera;
            world.journey=journey;environment.journey=journey;environment.world=world;environment.sceneCamera=sceneCamera;environment.sun=sun;
            weather.context=session;weather.sceneCamera=sceneCamera;weather.table=weatherTable;weather.shader=weatherShader;
            panel.context=session;panel.journey=journey;panel.terrainWorld=world;panel.flight=flight;panel.weatherFx=weather;panel.game=this;
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
                world.buildings=nextBuildings;
                if(loadedTerrains!=terrainText){journey.ReplaceTerrains(nextTerrains);loadedTerrains=terrainText;}
                world.RebuildAll();return true;
            }
            catch(Exception e){Debug.LogWarning("Tables unchanged: "+e.Message,this);return false;}
        }
    }
}
