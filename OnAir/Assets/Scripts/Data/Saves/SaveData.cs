using System;
using UnityEngine;

namespace OnAir
{
    // Plain data only: no scene instances, coroutines, materials or asset references.
    [Serializable] public sealed class JourneySaveData
    {
        public int schemaVersion = SaveMigration.CurrentVersion;
        public int generatorVersion = ContinuousWorldPlan.GeneratorVersion;
        public string configuration, journeyId, savedAtUtc, biomeName;
        public JourneyState journey;
        public FlightState flight;
        public ClimateState climate;
        public WorldState world;
    }

    [Serializable] public sealed class JourneyState
    {
        public int seed, randomState;
        public bool continuousWorld;
        public double totalMeters, segmentMeters;
        public LegState[] legs;
    }
    [Serializable] public sealed class LegState
    {
        public int index, seed;
        public string terrainId;
        public Weather entryWeather;
        public DayPeriod entryPeriod;
    }
    [Serializable] public sealed class FlightState
    {
        public float flightTime, routeSlope, speedMetersPerSecond;
    }
    [Serializable] public sealed class ClimateState
    {
        public Weather weather;
        public DayPeriod period;
        public bool paused, automaticWeather, automaticDaylight;
        public float solarPhase, weatherRemaining;
    }
    [Serializable] public sealed class WorldState
    {
        public PopulationState[] population = Array.Empty<PopulationState>();
        public HighwayState highways = new HighwayState();
        public TerrainStreamer.LandscapeMemory[] memories = Array.Empty<TerrainStreamer.LandscapeMemory>();
    }
    [Serializable] public sealed class PopulationState
    {
        public Vector2Int coordinates;
        public PlacementState[] buildings;
        public Rect[] roads;
    }
    [Serializable] public sealed class PlacementState
    {
        public string buildingId;
        public Rect bounds;
        public int rotation;
        public bool tint;
        public Color color;
    }
    [Serializable] public sealed class HighwayState
    {
        public RouteState[] routes = Array.Empty<RouteState>();
        public Vector2Int[] attempted = Array.Empty<Vector2Int>();
        public bool seen, hasNextSeedZ;
        public float nextSeedZ;
        public int networksCreated;
    }
    [Serializable] public sealed class RouteState
    {
        public Vector2[] points;
        public float[] supports;
        public Rect[] occupied;
        public Vector3[] exits;
        public bool noiseBarrier, upperTier, independentLayer, ramp, ascending;
        public float heading, turnAngle, turnRadius, mergeDistance;
        public int previous = -1, next = -1, parent = -1, overflownBuildings;
    }
    [Serializable] public sealed class SaveSettings
    {
        public int schemaVersion = SaveMigration.CurrentVersion;
        public bool panelCollapsed, worldDetails;
    }
    [Serializable] public sealed class SaveEnvelope
    {
        public string checksum, payload;
    }
}
