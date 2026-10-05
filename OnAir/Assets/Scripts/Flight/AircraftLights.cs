using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    // Small emissive lenses: no real-time light sources or shadow maps.
    public sealed class AircraftLights:MonoBehaviour
    {
        FlightView view;Material red,green,white;Renderer leftFlash,rightFlash,beacon;float clock;
        public bool FlashOn{get;private set;}
        public float PulsePhase=>clock%1.35f;
        void Start()
        {
            view=GetComponent<FlightView>();if(!view||!view.visual)return;
            red=LensMaterial(new Color(1,.04f,.015f));green=LensMaterial(new Color(.02f,1,.18f));white=LensMaterial(Color.white);
            var rig=view.Rig;
            if(rig)
            {
                Lens("Left red position",rig.leftNavigation.localPosition,.22f,red);
                Lens("Right green position",rig.rightNavigation.localPosition,.22f,green);
                Lens("Tail position",rig.tailNavigation.localPosition,.20f,white);
                leftFlash=Lens("Left white strobe",rig.leftStrobe.localPosition,.38f,white);
                rightFlash=Lens("Right white strobe",rig.rightStrobe.localPosition,.38f,white);
                beacon=Lens("Red beacon",rig.beacon.localPosition,.28f,red);Tick(0);return;
            }
            Lens("Left red position",new Vector3(-1.79f,.035f,-.05f),.065f,red);
            Lens("Right green position",new Vector3(1.79f,.035f,-.05f),.065f,green);
            Lens("Tail position",new Vector3(0,.09f,-1.39f),.045f,white);
            leftFlash=Lens("Left white strobe",new Vector3(-1.82f,.045f,-.17f),.11f,white);
            rightFlash=Lens("Right white strobe",new Vector3(1.82f,.045f,-.17f),.11f,white);Tick(0);
        }
        Material LensMaterial(Color color)
        {
            // Reuse the model's referenced shader so player builds cannot strip an unreferenced Shader.Find target.
            var m=new Material(view.visual.GetComponentInChildren<MeshRenderer>().sharedMaterial);m.SetColor("_BaseColor",Color.black);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2);return m;
        }
        Renderer Lens(string label,Vector3 position,float size,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name=label;obj.transform.SetParent(view.visual,false);obj.transform.localPosition=position;obj.transform.localScale=Vector3.one*size;
            Destroy(obj.GetComponent<Collider>());var r=obj.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        void Update(){if(view&&view.flight&&view.flight.journey&&!view.flight.journey.context.SimulationPaused)Tick(Time.deltaTime);}
        public void Tick(float seconds){clock+=Mathf.Max(0,seconds);float phase=clock%1.35f;FlashOn=phase<.07f||(phase>.17f&&phase<.24f);if(leftFlash)leftFlash.enabled=FlashOn;if(rightFlash)rightFlash.enabled=FlashOn;if(beacon)beacon.enabled=phase>.6f&&phase<.76f;}
        void OnDestroy(){if(red)Destroy(red);if(green)Destroy(green);if(white)Destroy(white);}
    }
}
