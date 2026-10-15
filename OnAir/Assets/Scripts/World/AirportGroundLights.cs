using System.Collections.Generic;
using UnityEngine;

namespace OnAir
{
    // Only named optical groups are controlled. Paving, grass, paint and building
    // shells are never selected by a material-name or colour heuristic.
    public sealed class AirportGroundLights:MonoBehaviour
    {
        public const float NightEmission=2.2f, PapiGlideDegrees=3, PapiLateralDegrees=10;
        public const float SequenceRepeatSeconds=.5f, SequencePulseSeconds=.055f, SequenceDayEmission=7, SequenceNightEmission=3.5f;
        public static readonly string[] SteadyGroupNames={"RunwayEdge","ThresholdSouthGreen","EndSouthRed","ThresholdNorthGreen","EndNorthRed","TaxiEdgeBlue","WarmApron","WarmTerminal"};
        static readonly Color AviationWhite=new Color(1,.98f,.94f),AviationRed=new Color(1,.035f,.015f),AviationGreen=new Color(.025f,1,.16f),TaxiBlue=new Color(.025f,.18f,1);
        sealed class Lens
        {
            public Renderer renderer;
            public Color color;
            public readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        }
        sealed class Papi
        {
            public readonly List<Lens>[] lamps={new List<Lens>(),new List<Lens>(),new List<Lens>(),new List<Lens>()};
            public float sourceZ,opticalHeight;
            public bool north;
        }
        sealed class Sequence
        {
            public readonly List<List<Lens>> lamps=new List<List<Lens>>();
            public bool north;
        }
        sealed class SourceRenderer
        {
            public Renderer renderer;
            public Material[] materials;
            public bool enabled;
            public readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        }
        readonly List<Lens> steady=new List<Lens>();
        readonly List<Material> ownedMaterials=new List<Material>();
        readonly List<SourceRenderer> sources=new List<SourceRenderer>();
        Papi south,north;
        Sequence sequenceSouth,sequenceNorth;
        public int BoundRendererCount{get;private set;}
        public int PapiSouthRedCount{get;private set;}=-1;
        public int PapiNorthRedCount{get;private set;}=-1;
        public int SequenceSouthIndex{get;private set;}=-1;
        public int SequenceNorthIndex{get;private set;}=-1;
        public int SequenceSouthLampCount=>sequenceSouth?.lamps.Count??0;
        public int SequenceNorthLampCount=>sequenceNorth?.lamps.Count??0;
        public void Build(Transform airportRoot)
        {
            Release();south=north=null;PapiSouthRedCount=PapiNorthRedCount=-1;
            sequenceSouth=sequenceNorth=null;SequenceSouthIndex=SequenceNorthIndex=-1;
            steady.Clear();BoundRendererCount=0;
            var root=airportRoot?airportRoot.Find("AirfieldLights"):null;
            if(!root)return;
            foreach(string name in SteadyGroupNames)
            {
                Color? color=null;
                switch(name)
                {
                    case "RunwayEdge":color=AviationWhite;break;
                    case "ThresholdSouthGreen":case "ThresholdNorthGreen":color=AviationGreen;break;
                    case "EndSouthRed":case "EndNorthRed":color=AviationRed;break;
                    case "TaxiEdgeBlue":color=TaxiBlue;break;
                }
                bool directional=name=="ThresholdSouthGreen"||name=="ThresholdNorthGreen"||name=="EndSouthRed"||name=="EndNorthRed";
                steady.AddRange(Bind(root.Find(name),color,directional));
            }
            south=BindPapi(root.Find("PapiSouth"),airportRoot,false);
            north=BindPapi(root.Find("PapiNorth"),airportRoot,true);
            sequenceSouth=BindSequence(root.Find("ApproachSequenceSouth"),false);
            sequenceNorth=BindSequence(root.Find("ApproachSequenceNorth"),true);
        }
        List<Lens> Bind(Transform group,Color? fixedColor,bool directional=false)
        {
            var result=new List<Lens>();if(!group)return result;
            foreach(var renderer in group.GetComponentsInChildren<Renderer>(true))
            {
                var original=new SourceRenderer{renderer=renderer,materials=renderer.sharedMaterials,enabled=renderer.enabled};
                renderer.GetPropertyBlock(original.block);sources.Add(original);
                var templates=original.materials;var materials=new Material[templates.Length];Color color=Color.white;
                for(int i=0;i<templates.Length;i++)
                {
                    if(!templates[i])continue;
                    if(i==0&&templates[i].HasProperty("_BaseColor"))color=templates[i].GetColor("_BaseColor");
                    var material=new Material(templates[i]){name="Airport optical light / "+group.name};
                    material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.black);
                    // Threshold/end and PAPI optics are single forward-facing
                    // planes; the source palette is otherwise double-sided.
                    if(directional&&material.HasProperty("_Cull"))material.SetFloat("_Cull",2);
                    ownedMaterials.Add(material);materials[i]=material;
                }
                renderer.sharedMaterials=materials;
                var lens=new Lens{renderer=renderer,color=fixedColor??color};result.Add(lens);BoundRendererCount++;
                Apply(lens,lens.color,0,true);
            }
            return result;
        }
        Papi BindPapi(Transform group,Transform airportRoot,bool isNorth)
        {
            if(!group)return null;
            var papi=new Papi{north=isNorth,sourceZ=isNorth?90:-90,opticalHeight=.55f};
            bool positioned=false;
            for(int i=0;i<4;i++)
            {
                papi.lamps[i].AddRange(Bind(group.Find("Lens-"+i),AviationWhite,true));
                foreach(var lens in papi.lamps[i])
                {
                    if(!positioned)
                    {
                        var point=airportRoot.InverseTransformPoint(lens.renderer.bounds.center);
                        papi.sourceZ=point.z;papi.opticalHeight=point.y;positioned=true;
                    }
                    Apply(lens,AviationWhite,0,false);
                }
            }
            return papi;
        }
        Sequence BindSequence(Transform group,bool isNorth)
        {
            if(!group)return null;
            var sequence=new Sequence{north=isNorth};
            // Authoring order is outermost approach lamp -> runway threshold.
            // A missing group remains compatible with previously baked assets.
            for(int i=0;i<64;i++)
            {
                var lamp=group.Find("Lamp-"+i);if(!lamp)break;
                var lenses=Bind(lamp,Color.white,true);sequence.lamps.Add(lenses);
                foreach(var lens in lenses)ApplyEmission(lens,Color.white,0,false);
            }
            return sequence;
        }
        // The caller owns simulation time, including pause. The compressed field
        // adds one approach sequence; steady airport lights keep their own policy.
        public void Refresh(float night,float simulationTime,AirportVisitPlan.Site site,Vector3 aircraftWorld,AirportPhase phase)
        {
            float level=Mathf.Clamp01(night);
            foreach(var lens in steady)Apply(lens,lens.color,level,true);
            PapiSouthRedCount=RefreshPapi(south,level,site,aircraftWorld,phase);
            PapiNorthRedCount=RefreshPapi(north,level,site,aircraftWorld,phase);
            SequenceSouthIndex=RefreshSequence(sequenceSouth,level,simulationTime,site,phase);
            SequenceNorthIndex=RefreshSequence(sequenceNorth,level,simulationTime,site,phase);
        }
        int RefreshSequence(Sequence sequence,float night,float simulationTime,AirportVisitPlan.Site site,AirportPhase phase)
        {
            if(sequence==null)return -1;
            int count=sequence.lamps.Count;
            bool active=site!=null&&phase==AirportPhase.Approach&&site.reverse==sequence.north;
            int index=active&&SequenceFlash(simulationTime,count)?SequenceIndex(simulationTime,count):-1;
            float strength=Mathf.Lerp(SequenceDayEmission,SequenceNightEmission,night);
            for(int i=0;i<count;i++)foreach(var lens in sequence.lamps[i])ApplyEmission(lens,Color.white,strength,i==index);
            return index;
        }
        public static int SequenceIndex(float simulationTime,int count,float period=SequenceRepeatSeconds)
        {
            if(count<=0||period<=0||float.IsNaN(period)||float.IsInfinity(period)||float.IsNaN(simulationTime)||float.IsInfinity(simulationTime))return -1;
            return Mathf.Min(count-1,Mathf.FloorToInt(Mathf.Repeat(simulationTime,period)/(period/count)));
        }
        public static bool SequenceFlash(float simulationTime,int count,float period=SequenceRepeatSeconds)
        {
            int index=SequenceIndex(simulationTime,count,period);if(index<0)return false;
            float slot=period/count;
            // Every slot contains a dark gap; even denser authoring cannot turn
            // adjacent optics into a simultaneous all-row flash.
            return Mathf.Repeat(simulationTime,period)-index*slot<Mathf.Min(SequencePulseSeconds,slot*.8f);
        }
        int RefreshPapi(Papi papi,float night,AirportVisitPlan.Site site,Vector3 aircraftWorld,AirportPhase phase)
        {
            if(papi==null)return -1;
            var source=site==null?Vector3.zero:SmallAirportNetwork.Source(site,aircraftWorld);
            bool visible=site!=null&&phase==AirportPhase.Approach&&site.reverse==papi.north&&PapiVisible(source,papi.sourceZ,papi.north);
            int count=visible?PapiRedCount(PapiAngleDegrees(source,papi.sourceZ,papi.opticalHeight,papi.north)):-1;
            for(int i=0;i<4;i++)foreach(var lens in papi.lamps[i])Apply(lens,i<count?AviationRed:AviationWhite,night,visible);
            return count;
        }
        public static bool PapiVisible(Vector3 aircraftSource,float sourceZ,bool north)
        {
            float distance=north?aircraftSource.z-sourceZ:sourceZ-aircraftSource.z;
            return distance>1&&Mathf.Abs(aircraftSource.x)<=distance*Mathf.Tan(PapiLateralDegrees*Mathf.Deg2Rad);
        }
        public static float PapiAngleDegrees(Vector3 aircraftSource,float sourceZ,float opticalHeight,bool north)
        {
            float distance=north?aircraftSource.z-sourceZ:sourceZ-aircraftSource.z;
            return Mathf.Atan2(aircraftSource.y-opticalHeight,Mathf.Max(.001f,distance))*Mathf.Rad2Deg;
        }
        public static int PapiRedCount(float angleDegrees)
        {
            // 2°30', 2°50', 3°10', 3°30'. Near-runway optics have the
            // highest aiming angles: on path, the near pair is red.
            if(angleDegrees<2.5f)return 4;if(angleDegrees<2+50/60f)return 3;
            if(angleDegrees<3+10/60f)return 2;if(angleDegrees<3.5f)return 1;return 0;
        }
        static void Apply(Lens lens,Color color,float night,bool visible)
            =>ApplyEmission(lens,color,NightEmission*night,visible);
        static void ApplyEmission(Lens lens,Color color,float strength,bool visible)
        {
            if(!lens.renderer)return;
            lens.renderer.enabled=visible;lens.renderer.GetPropertyBlock(lens.block);
            lens.block.SetColor("_BaseColor",color);
            lens.block.SetColor("_EmissionColor",visible?color*strength:Color.black);
            lens.renderer.SetPropertyBlock(lens.block);
        }
        void Release()
        {
            foreach(var source in sources)if(source.renderer)
            {
                source.renderer.sharedMaterials=source.materials;source.renderer.enabled=source.enabled;
                source.renderer.SetPropertyBlock(source.block);
            }
            sources.Clear();
            foreach(var material in ownedMaterials)if(material)
            {if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
            ownedMaterials.Clear();
        }
        void OnDestroy()=>Release();
    }
}
