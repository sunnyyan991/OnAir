using UnityEngine;
namespace OnAir
{
    // Owns the character's world position. Journey owns route selection; the camera only observes.
    [DefaultExecutionOrder(-100)]
    public sealed class FlightController : MonoBehaviour
    {
        public JourneyController journey;
        [Min(0)] public float speedMetersPerSecond=12;
        [Min(1)] public float altitude=19;
        [Min(0)] public float swayAmplitude=1.2f;
        [Min(.01f)] public float swayFrequency=.32f;
        [Min(0)] public float bobAmplitude=.2f;
        public bool autoCircle=true;
        [Min(1)] public float firstCircleAfter=10;
        [Min(1)] public float circleInterval=22;
        [Min(1)] public float circleRadius=4.5f;
        public bool IsCircling {get;private set;}
        public Vector3 Heading {get;private set;}=Vector3.forward;
        public float Bank {get;private set;}
        public float CircleProgress=>circleAngle/(Mathf.PI*2);
        float cruiseTime,flightTime,untilCircle,circleAngle,activeRadius;
        int originLeg,teleport=-1,turnSign=1;
        Vector3 circleStart,circleForward,circleRight;
        float WorldSpeed=>Mathf.Max(0,speedMetersPerSecond)*TerrainStreamer.SegmentLength/journey.Current.terrain.distanceMeters;

        void Start()=>Synchronize();
        void Update()=>Tick(Time.deltaTime);
        void Synchronize()
        {
            journey.Initialize();
            if(teleport!=journey.TeleportRevision)
            {
                teleport=journey.TeleportRevision;originLeg=journey.Current.index;
                cruiseTime=flightTime=0;untilCircle=firstCircleAfter;circleAngle=0;Bank=0;turnSign=1;IsCircling=false;
                CruisePose();
            }
            int delta=journey.Current.index-originLeg;
            if(delta!=0)
            {
                var shift=Vector3.back*(delta*TerrainStreamer.SegmentLength);
                transform.position+=shift;circleStart+=shift;originLeg=journey.Current.index;
            }
        }
        public void Tick(float seconds)
        {
            if(!journey)return;
            Synchronize();
            if(!IsCircling)CruisePose();
            if(journey.context.paused||seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds)||WorldSpeed<=0)return;
            if(IsCircling)
            {
                float rate=WorldSpeed/activeRadius;
                float used=Mathf.Min(seconds,(Mathf.PI*2-circleAngle)/rate);
                circleAngle=Mathf.Min(Mathf.PI*2,circleAngle+used*rate);flightTime+=used;
                var p=circleStart+circleRight*(activeRadius*(1-Mathf.Cos(circleAngle)))+circleForward*(activeRadius*Mathf.Sin(circleAngle));
                Heading=(circleRight*Mathf.Sin(circleAngle)+circleForward*Mathf.Cos(circleAngle)).normalized;
                ApplyPose(p,turnSign*27,used);
                if(circleAngle>=Mathf.PI*2-.00001f)
                {
                    IsCircling=false;untilCircle=circleInterval;turnSign=-turnSign;CruisePose();
                    Cruise(seconds-used);
                }
            }
            else
            {
                Cruise(seconds);
                if(autoCircle&&untilCircle<=0)BeginCircle();
            }
        }
        void Cruise(float seconds)
        {
            if(seconds<=0)return;
            cruiseTime+=seconds;flightTime+=seconds;untilCircle-=seconds;
            journey.Advance(seconds*speedMetersPerSecond);Synchronize();CruisePose();
            // Gentle wing banking follows the bend in the cruising path.
            float bank=-Mathf.Sin(cruiseTime*swayFrequency)*7;
            Bank=Mathf.Lerp(Bank,bank,1-Mathf.Exp(-3*seconds));
        }
        void CruisePose()
        {
            float phase=cruiseTime*swayFrequency;
            var p=new Vector3(Mathf.Sin(phase)*swayAmplitude,0,journey.Progress*TerrainStreamer.SegmentLength);
            Heading=new Vector3(Mathf.Cos(phase)*swayAmplitude*swayFrequency,0,Mathf.Max(.01f,WorldSpeed)).normalized;
            ApplyPose(p,Bank,0);
        }
        void ApplyPose(Vector3 position,float bank,float seconds)
        {
            position.y=altitude+Mathf.Sin(flightTime*1.6f)*bobAmplitude;
            transform.SetPositionAndRotation(position,Quaternion.LookRotation(Heading,Vector3.up));
            Bank=Mathf.Lerp(Bank,bank,1-Mathf.Exp(-3*seconds));
        }
        public bool BeginCircle()
        {
            if(!journey)return false;
            Synchronize();
            if(IsCircling||journey.context.paused||WorldSpeed<=0)return false;
            circleStart=transform.position;circleForward=Heading;
            circleRight=Vector3.Cross(Vector3.up,circleForward)*turnSign;
            activeRadius=Mathf.Max(1,circleRadius);circleAngle=0;IsCircling=true;
            return true;
        }
    }
}
