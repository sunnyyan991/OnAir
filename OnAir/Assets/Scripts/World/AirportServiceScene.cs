using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OnAir
{
    // The prefab owns all fixed paving, markings and buildings. This scene only
    // displays service equipment whose positions come from the visit simulation.
    public sealed class AirportServiceScene : MonoBehaviour
    {
        readonly List<Material> ownedMaterials=new List<Material>();
        readonly List<Light> workLights=new List<Light>();
        readonly List<float> workLightLevels=new List<float>();
        Transform fuel,tug,mechanic;
        ParticleSystem smoke;
        LineRenderer hose,towbar;
        Mesh cube,cylinder;
        Material steel,rubber,glass,lampMaterial;
        AirportVisitPlan.Site site;
        public void Build(AirportVisitPlan.Site value,AirportFlightLayout layout)
        {
            site=value;transform.SetLocalPositionAndRotation(site.assetPosition,site.assetRotation);
            cube=PrimitiveMesh(PrimitiveType.Cube);cylinder=PrimitiveMesh(PrimitiveType.Cylinder);
            steel=Material("Service steel",new Color(.38f,.43f,.43f));
            rubber=Material("Service tyres and hose",new Color(.12f,.16f,.16f));
            glass=Material("Service cab glass",new Color(.24f,.42f,.47f));
            fuel=Vehicle("Compact fuel bowser",SmallAirportGroundService.FuelParking,new Color(.76f,.75f,.63f),true,false);
            tug=Vehicle("Nose-coupled electric tractor",SmallAirportGroundService.TugParking,new Color(.83f,.68f,.29f),false,false);
            mechanic=Vehicle("Maintenance tool cart",SmallAirportGroundService.MechanicParking,new Color(.44f,.57f,.56f),false,true);
            lampMaterial=Material("Warm maintenance lamp",new Color(1,.84f,.57f));lampMaterial.EnableKeyword("_EMISSION");
            var centre=SmallAirportNetwork.Source(site,site.Hangar);
            foreach(float x in new[]{-6f,6f})foreach(float z in new[]{-7f,7f})
            {
                var position=centre+new Vector3(x,7.0f,z);
                Part(transform,"Maintenance ceiling lamp",position,new Vector3(1.2f,.15f,.45f),lampMaterial);
                // Cross-aim actual spotlights at the aircraft, rather than a glow
                // painted on the floor. The cooler fill preserves body detail.
                AddWorkLight("Cross-aimed bay spotlight",position,centre+Vector3.up*1.2f,LightType.Spot,110,26,new Color(1,.89f,.74f));
            }
            AddWorkLight("Hangar soft fill",centre+new Vector3(0,4.7f,0),centre,LightType.Point,14,13,new Color(.7f,.81f,1));
            hose=Line("Refuelling hose",rubber,.11f);towbar=Line("Connected nose coupling bar",steel,.11f);
            hose.gameObject.SetActive(false);towbar.gameObject.SetActive(false);
            MakeSmoke();
        }
        void AddWorkLight(string name,Vector3 position,Vector3 target,LightType type,float intensity,float range,Color color)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.transform.localPosition=position;
            obj.transform.localRotation=Quaternion.LookRotation((target-position).normalized);
            var light=obj.AddComponent<Light>();light.type=type;light.spotAngle=110;light.innerSpotAngle=65;
            light.range=range;light.color=color;light.shadows=LightShadows.None;light.intensity=0;
            workLights.Add(light);workLightLevels.Add(intensity);
        }
        Mesh PrimitiveMesh(PrimitiveType type)
        {
            var temporary=GameObject.CreatePrimitive(type);var mesh=temporary.GetComponent<MeshFilter>().sharedMesh;
            temporary.SetActive(false);if(Application.isPlaying)Destroy(temporary);else DestroyImmediate(temporary);return mesh;
        }
        Material Material(string label,Color color)
        {
            var shader=Shader.Find("OnAir/Cloud Receiving Lit");
            if(!shader)shader=Shader.Find("Universal Render Pipeline/Lit");
            var material=new Material(shader){name=label};material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.2f);ownedMaterials.Add(material);return material;
        }
        Transform Vehicle(string label,Vector3 position,Color color,bool tank,bool cart)
        {
            var root=new GameObject(label);root.transform.SetParent(transform,false);root.transform.localPosition=position+Vector3.up*.20f;root.transform.localRotation=Quaternion.LookRotation(Vector3.left);
            var body=Material(label+" paint",color);
            float width=cart?1.35f:1.6f,length=cart?2.0f:2.6f;
            Part(root.transform,"Chassis",new Vector3(0,.52f,0),new Vector3(width,.55f,length),body);
            if(!cart)
            {
                Part(root.transform,"Cab",new Vector3(0,1.08f,.65f),new Vector3(width*.9f,.68f,.85f),body);
                Part(root.transform,"Windscreen",new Vector3(0,1.14f,1.09f),new Vector3(width*.75f,.38f,.04f),glass);
                foreach(float side in new[]{-1f,1f})Part(root.transform,"Side glass",new Vector3(side*width*.455f,1.16f,.65f),new Vector3(.03f,.35f,.56f),glass);
            }
            else
            {
                Part(root.transform,"Tool chest",new Vector3(0,1.02f,-.1f),new Vector3(1.12f,.54f,1.45f),steel);
                Part(root.transform,"Chest lid",new Vector3(0,1.31f,-.1f),new Vector3(1.2f,.08f,1.53f),body);
                Part(root.transform,"Handle",new Vector3(0,.94f,.94f),new Vector3(1.05f,.09f,.09f),steel);
            }
            if(tank)
            {
                // A small bowser matched to a light aircraft, rather than a large tanker.
                Part(root.transform,"Fuel tank",new Vector3(0,1.04f,-.5f),new Vector3(1.08f,.69f,1.3f),body,cylinder,Quaternion.Euler(90,0,0));
                Part(root.transform,"Hose reel",new Vector3(.62f,.99f,-.5f),new Vector3(.12f,.37f,.37f),rubber,cylinder,Quaternion.Euler(0,0,90));
            }
            foreach(float x in new[]{-width*.48f,width*.48f})foreach(float z in new[]{-length*.32f,length*.32f})
                Part(root.transform,"Tyre",new Vector3(x,.3f,z),new Vector3(.61f,.14f,.61f),rubber,cylinder,Quaternion.Euler(0,0,90));
            Part(root.transform,"Front bumper",new Vector3(0,.5f,length*.505f),new Vector3(width,.16f,.12f),steel);
            Part(root.transform,"Rear bumper",new Vector3(0,.5f,-length*.505f),new Vector3(width,.16f,.12f),steel);
            return root.transform;
        }
        void Part(Transform root,string label,Vector3 position,Vector3 size,Material material,Mesh mesh=null,Quaternion? rotation=null)
        {
            var go=new GameObject(label);go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=size;
            if(rotation.HasValue)go.transform.localRotation=rotation.Value;
            go.AddComponent<MeshFilter>().sharedMesh=mesh?mesh:cube;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        LineRenderer Line(string label,Material material,float width)
        {
            var obj=new GameObject(label);obj.transform.SetParent(transform,false);var line=obj.AddComponent<LineRenderer>();
            line.useWorldSpace=false;line.sharedMaterial=material;line.widthMultiplier=width;line.numCapVertices=3;line.numCornerVertices=3;line.shadowCastingMode=ShadowCastingMode.Off;return line;
        }
        void MakeSmoke()
        {
            var obj=new GameObject("Brief wheel-contact puff");obj.transform.SetParent(transform,false);smoke=obj.AddComponent<ParticleSystem>();smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=smoke.main;main.loop=false;main.playOnAwake=false;main.duration=.25f;main.startLifetime=.8f;main.startSpeed=.35f;main.startSize=.6f;main.startColor=new Color(.7f,.72f,.71f,.3f);main.maxParticles=16;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=smoke.emission;emission.enabled=false;var shape=smoke.shape;shape.enabled=false;
            var color=smoke.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.3f,0),new GradientAlphaKey(0,1)});color.color=gradient;
            var material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;ownedMaterials.Add(material);obj.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
        }
        public void Touchdown(Vector3 global)
        {
            if(!smoke)return;var position=SmallAirportNetwork.Source(site,global);position.y=.3f;smoke.transform.localPosition=position;smoke.Play();smoke.Emit(10);
        }
        public void Refresh(AirportPhase phase,Vector3 aircraft,float seconds,bool paused,SmallAirportGroundService simulation=null)
        {
            if(smoke){if(paused&&!smoke.isPaused)smoke.Pause();else if(!paused&&smoke.isPaused)smoke.Play();}
            if(simulation==null){hose.gameObject.SetActive(false);towbar.gameObject.SetActive(false);return;}
            Pose(fuel,simulation.Fuel);Pose(tug,simulation.Tug);Pose(mechanic,simulation.Mechanic);
            bool connected=phase==AirportPhase.Maintenance&&simulation.FuelConnected;
            hose.gameObject.SetActive(connected);
            if(connected)
            {
                var a=simulation.Fuel.position+new Vector3(0,1.12f,-.45f);
                var b=SmallAirportNetwork.Source(site,aircraft)+new Vector3(-1,.15f,3.6f);
                hose.positionCount=4;hose.SetPositions(new[]{a,Vector3.Lerp(a,b,.35f)-Vector3.up*.6f,Vector3.Lerp(a,b,.72f)-Vector3.up*.4f,b});
            }
            bool towing=phase==AirportPhase.BackIntoHangar&&simulation.TugCoupled;
            towbar.gameObject.SetActive(towing);
            if(towing)
            {
                var a=simulation.Tug.position+simulation.Tug.heading*1.32f;a.y=.55f;
                var forward=Quaternion.Inverse(site.assetRotation)*site.HangarForward;
                var b=SmallAirportNetwork.Source(site,aircraft)+forward*4.0f;b.y=.55f;
                towbar.positionCount=2;towbar.SetPositions(new[]{a,b});
            }
        }
        static void Pose(Transform transform,AirportServiceVehicleState vehicle)
        {transform.localPosition=vehicle.position+Vector3.up*.20f;transform.localRotation=Quaternion.LookRotation(vehicle.heading);}
        public void RefreshNight(float night)
        {for(int i=0;i<workLights.Count;i++)if(workLights[i])workLights[i].intensity=night*workLightLevels[i];lampMaterial.SetColor("_EmissionColor",new Color(1,.89f,.74f)*night*1.8f);}
        void OnDestroy(){foreach(var material in ownedMaterials)if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
    }
}
