using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OnAir
{
    public sealed class SaveCompatibilityException : Exception
    {
        public SaveCompatibilityException(string message) : base(message) { }
    }
    public static class SaveMigration
    {
        public const int CurrentVersion = 1;
        // Bump this when authored layout inputs change without a generator/table change.
        public const int ContentVersion = 1;
        public static string ConfigurationKey(GameEntry game)
        {
            var text = new StringBuilder(ContentVersion.ToString(CultureInfo.InvariantCulture));
            text.Append(':').Append(game.journey.continuousWorld);
            text.Append(':').Append(game.session.daySeconds.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(game.session.nightSeconds.ToString("R",CultureInfo.InvariantCulture));
            foreach(var rule in game.session.weatherRules)text.Append(':').Append((int)rule.weather).Append(':').Append(rule.weight.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(rule.minSeconds.ToString("R",CultureInfo.InvariantCulture)).Append(':').Append(rule.maxSeconds.ToString("R",CultureInfo.InvariantCulture));
            foreach (var table in new[] { game.buildingsTable, game.terrainsTable, game.weatherTable })
                text.Append('\n').Append(TableSource.Read(table).Replace("\r\n", "\n"));
            foreach (var pair in game.world.buildings.Prefabs.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var footprint = pair.Value.GetComponent<BuildingFootprint>();
                text.Append('\n').Append(pair.Key).Append(':').Append(pair.Value.name).Append(':');
                if (footprint) text.Append(footprint.size.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(footprint.size.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(footprint.height.ToString("R", CultureInfo.InvariantCulture));
            }
            foreach (var terrain in game.journey.terrains)
            {
                var kit = terrain.profile.kit;
                text.Append('\n').Append(terrain.id).Append(':').Append(kit.name).Append(':')
                    .Append(kit.buildingGapModules).Append(':').Append(kit.streetSetbackModules).Append(':')
                    .Append(kit.emptyPlotModules).Append(':').Append(kit.emptyPlotChance.ToString("R", CultureInfo.InvariantCulture));
                foreach (var color in kit.facadePalette) text.Append(':').Append(ColorUtility.ToHtmlStringRGBA(color));
                foreach (var asset in kit.greenSpaceAssets) text.Append(':').Append(asset ? asset.name : "missing");
            }
            return SaveFileStore.Hash(text.ToString());
        }
        public static JourneySaveData ReadJourney(string text, string configuration, TerrainDefinition[] terrains, BuildingData buildings)
        {
            JourneySaveData data;
            try { data = JsonUtility.FromJson<JourneySaveData>(SaveFileStore.Unpack(text)); }
            catch (ArgumentException error) { throw new FormatException("Journey JSON is invalid.", error); }
            Require(data != null, "Missing journey.");
            CheckVersion(data.schemaVersion);
            if (data.generatorVersion != ContinuousWorldPlan.GeneratorVersion || data.configuration != configuration)
                throw new SaveCompatibilityException("Saved world rules differ from the current content.");
            Require(Guid.TryParse(data.journeyId, out _) && DateTimeOffset.TryParse(data.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _), "Missing save identity/time.");
            var journey = data.journey;
            Require(journey != null && journey.seed >= 0 && journey.randomState != 0, "Invalid world seed/random state.");
            Require(Finite(journey.totalMeters) && journey.totalMeters >= 0 && Finite(journey.segmentMeters) && journey.segmentMeters >= 0, "Invalid distance.");
            Require(journey.legs != null && journey.legs.Length == 3, "Invalid journey lookahead.");
            for (int i = 0; i < journey.legs.Length; i++)
            {
                var leg = journey.legs[i];
                Require(leg != null && leg.index >= 0 && leg.index < 10000000 && (i == 0 || leg.index == journey.legs[i-1].index+1), "Invalid leg index.");
                Require(terrains.Any(t => t.id == leg.terrainId) && Enum.IsDefined(typeof(Weather), leg.entryWeather) && Enum.IsDefined(typeof(DayPeriod), leg.entryPeriod), "Missing terrain or invalid climate.");
            }
            Require(journey.segmentMeters < terrains.First(t => t.id == journey.legs[0].terrainId).distanceMeters, "Leg progress outside range.");
            Require(journey.totalMeters >= journey.segmentMeters, "Invalid accumulated distance.");
            var flight = data.flight;
            Require(flight != null && Finite(flight.flightTime) && flight.flightTime >= 0 && Finite(flight.routeSlope) && Mathf.Abs(flight.routeSlope) <= 10 && Finite(flight.speedMetersPerSecond) && flight.speedMetersPerSecond >= 0, "Invalid flight state.");
            var climate = data.climate;
            Require(climate != null && Enum.IsDefined(typeof(Weather), climate.weather) && Enum.IsDefined(typeof(DayPeriod), climate.period), "Invalid climate.");
            Require(Finite(climate.solarPhase) && climate.solarPhase >= 0 && climate.solarPhase < 1 && Finite(climate.weatherRemaining) && climate.weatherRemaining > 0, "Invalid climate progress.");
            var world = data.world;
            Require(world != null && world.population != null && world.population.Length <= 256 && world.highways != null && world.memories != null && world.memories.Length <= 4096, "Invalid world snapshot.");
            var keys = new HashSet<Vector2Int>();
            foreach (var region in world.population)
            {
                Require(region != null && keys.Add(region.coordinates) && region.buildings != null && region.buildings.Length <= 4096 && region.roads != null && region.roads.Length <= 4096, "Invalid population region.");
                foreach (var item in region.buildings)
                {
                    Require(item != null && buildings.TryResolveSaveId(item.buildingId, out _) && Valid(item.bounds), "Missing saved building.");
                    Require(item.rotation >= 0 && item.rotation < 360 && item.rotation % 90 == 0 && Finite(item.color.r) && Finite(item.color.g) && Finite(item.color.b) && Finite(item.color.a), "Invalid building appearance.");
                }
                foreach (var road in region.roads) Require(Valid(road), "Invalid road bounds.");
            }
            var highways = world.highways;
            Require(highways.routes != null && highways.routes.Length <= 256 && highways.attempted != null && highways.attempted.Length <= 2048 && highways.networksCreated >= 0, "Invalid highway snapshot.");
            Require(!highways.hasNextSeedZ || Finite(highways.nextSeedZ), "Invalid highway position.");
            foreach (var route in highways.routes)
            {
                Require(route != null && route.points != null && route.points.Length >= 2 && route.points.Length <= 16384 && route.supports != null && route.occupied != null && route.exits != null, "Invalid highway geometry.");
                Require(route.supports.Length <= 16384 && route.occupied.Length <= 16384 && route.exits.Length <= 16384, "Highway arrays outside limits.");
                Require(Finite(route.heading) && Finite(route.turnAngle) && Finite(route.turnRadius) && route.turnRadius >= 0 && Finite(route.mergeDistance) && route.mergeDistance >= 0, "Invalid highway dimensions.");
                Require(route.independentLayer && route.previous >= -1 && route.previous < highways.routes.Length && route.next >= -1 && route.next < highways.routes.Length && route.parent >= -1 && route.parent < highways.routes.Length, "Invalid highway connections.");
                float length = 0;
                for (int i=0;i<route.points.Length;i++)
                {
                    Require(Finite(route.points[i].x) && Finite(route.points[i].y), "Invalid highway point.");
                    if(i>0) length += Vector2.Distance(route.points[i-1],route.points[i]);
                }
                Require(Finite(length) && length > 0, "Empty highway.");
                foreach (float support in route.supports) Require(Finite(support) && support >= 0 && support <= length+.1f, "Invalid highway support.");
                foreach (var bounds in route.occupied) Require(Valid(bounds), "Invalid highway reservation.");
                foreach (var exit in route.exits) Require(Finite(exit.x) && Finite(exit.y) && Finite(exit.z), "Invalid highway exit.");
            }
            foreach(var memory in world.memories)
                Require(memory.seed == journey.seed && memory.version == data.generatorVersion && memory.slice >= 0 && Finite(memory.solarPhase) && memory.solarPhase >= 0 && memory.solarPhase < 1 && Finite(memory.routeSlope), "Invalid landscape history.");
            return data;
        }
        public static SaveSettings ReadSettings(string text)
        {
            SaveSettings data;
            try { data = JsonUtility.FromJson<SaveSettings>(SaveFileStore.Unpack(text)); }
            catch (ArgumentException error) { throw new FormatException("Settings JSON is invalid.", error); }
            Require(data != null, "Missing settings."); CheckVersion(data.schemaVersion); return data;
        }
        static void CheckVersion(int version)
        {
            if (version > CurrentVersion) throw new SaveCompatibilityException("Save was written by a newer format.");
            // There is no older released format yet. Add explicit migrations here when V2 ships.
            Require(version == CurrentVersion, "Unknown save format.");
        }
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool Valid(Rect value) => Finite(value.x) && Finite(value.y) && Finite(value.width) && Finite(value.height) && value.width > 0 && value.height > 0;
        static void Require(bool valid, string message) { if (!valid) throw new FormatException(message); }
    }
}
