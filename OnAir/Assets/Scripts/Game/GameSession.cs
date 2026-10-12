using UnityEngine;
namespace OnAir
{
    public sealed class GameSession : MonoBehaviour
    {
        public Weather weather = Weather.Clear;
        public DayPeriod period = DayPeriod.Day;
        public bool paused;
        [System.NonSerialized] public bool menuPaused;
        public bool SimulationPaused => paused || menuPaused;
        public bool automaticWeather=true,automaticDaylight=true;
        [Min(30)] public float daySeconds=600,nightSeconds=300;
        [Range(0,1)] public float solarPhase=.25f;
        // Automatic weather rules per climate zone, loaded from climate.csv by GameEntry.
        [System.NonSerialized] public System.Collections.Generic.Dictionary<string,ClimateRule[]> climate;
        // Weather that the current zone disallows ends within this many seconds.
        const float ZoneExitSeconds=8;
        string climateZone;
        public string ClimateZone=>climateZone;
        System.Random weatherRandom;float weatherRemaining;
        public float WeatherRemaining=>weatherRemaining;
        public event System.Action PlayerChanged;
        public ClimateState CaptureState()=>new ClimateState{weather=weather,period=period,paused=paused,automaticWeather=automaticWeather,automaticDaylight=automaticDaylight,solarPhase=solarPhase,weatherRemaining=Mathf.Max(.001f,weatherRemaining)};
        public void RestoreState(ClimateState saved)
        {
            weather=saved.weather;period=saved.period;paused=saved.paused;automaticWeather=saved.automaticWeather;automaticDaylight=saved.automaticDaylight;solarPhase=saved.solarPhase;
            weatherRandom=new System.Random(System.Environment.TickCount^0x572391);weatherRemaining=saved.weatherRemaining;EnvironmentChanged?.Invoke();
        }
        public void InitializeClimate(int seed){weatherRandom=new System.Random(seed^0x572391);ScheduleWeather();}
        ClimateRule[] ZoneRules()=>climate!=null&&climate.TryGetValue(climateZone??nameof(ContinuousWorldPlan.EcologyZone.City),out var rules)?rules:System.Array.Empty<ClimateRule>();
        public bool ZoneAllows(Weather value)=>System.Array.Exists(ZoneRules(),r=>r.weather==value&&r.weight>0);
        public void SetClimateZone(string zone){climateZone=zone;}
        void ScheduleWeather()
        {
            int index=System.Array.FindIndex(ZoneRules(),r=>r.weather==weather&&r.weight>0);
            if(index<0){weatherRemaining=ZoneExitSeconds;return;}
            var rule=ZoneRules()[index];weatherRemaining=Mathf.Lerp(rule.minSeconds,rule.maxSeconds,(float)weatherRandom.NextDouble());
        }
        void Update()=>TickClimate(Time.deltaTime);
        public void TickClimate(float seconds)
        {
            if(SimulationPaused||seconds<=0)return;if(weatherRandom==null)InitializeClimate(System.Environment.TickCount);
            if(automaticWeather)
            {
                // Entering a zone that disallows the current weather shortens it instead of switching instantly.
                if(climateZone!=null&&!ZoneAllows(weather))weatherRemaining=Mathf.Min(weatherRemaining,ZoneExitSeconds);
                weatherRemaining-=seconds;
                while(weatherRemaining<=0)
                {
                    var rules=ZoneRules();float overdue=weatherRemaining,total=0;foreach(var r in rules)if(r.weather!=weather)total+=Mathf.Max(0,r.weight);
                    if(total<=0){ScheduleWeather();weatherRemaining=Mathf.Max(1,weatherRemaining);break;}
                    double ticket=weatherRandom.NextDouble()*total;foreach(var r in rules)if(r.weather!=weather&&r.weight>0){ticket-=r.weight;if(ticket<0){weather=r.weather;break;}}
                    ScheduleWeather();weatherRemaining=Mathf.Max(1,weatherRemaining)+overdue;EnvironmentChanged?.Invoke();
                }
            }
            if(automaticDaylight)
            {
                float day=Mathf.Max(30,daySeconds),night=Mathf.Max(30,nightSeconds);
                float elapsed=solarPhase<.5f?solarPhase*2*day:day+(solarPhase-.5f)*2*night;
                elapsed=Mathf.Repeat(elapsed+seconds,day+night);solarPhase=elapsed<day?elapsed/day*.5f:.5f+(elapsed-day)/night*.5f;
                period=PeriodAt(solarPhase);
            }
        }
        public event System.Action EnvironmentChanged;
        static DayPeriod PeriodAt(float phase)=>phase<.08f?DayPeriod.Dawn:phase<.42f?DayPeriod.Day:phase<.5f?DayPeriod.Dusk:phase<.57f||phase>.94f?DayPeriod.BlueHour:DayPeriod.Night;
        public void SetSolarPhase(float phase)
        {
            solarPhase=Mathf.Repeat(phase,1);automaticDaylight=true;period=PeriodAt(solarPhase);
            EnvironmentChanged?.Invoke();
        }
        public void SetWeather(Weather value){weather=value;if(weatherRandom!=null)ScheduleWeather();EnvironmentChanged?.Invoke();PlayerChanged?.Invoke();}
        public void SetPeriod(DayPeriod value){automaticDaylight=false;period=value;solarPhase=value==DayPeriod.Dawn?.055f:value==DayPeriod.Day?.25f:value==DayPeriod.Dusk?.455f:value==DayPeriod.BlueHour?.535f:.75f;EnvironmentChanged?.Invoke();PlayerChanged?.Invoke();}
        public void CyclePeriod(){if(automaticDaylight){SetPeriod(DayPeriod.Dawn);return;}if(period==DayPeriod.Night){automaticDaylight=true;return;}SetPeriod(period==DayPeriod.Dawn?DayPeriod.Day:period==DayPeriod.Day?DayPeriod.Dusk:period==DayPeriod.Dusk?DayPeriod.BlueHour:DayPeriod.Night);}
        public void TogglePause(){paused=!paused;PlayerChanged?.Invoke();}
    }
}
