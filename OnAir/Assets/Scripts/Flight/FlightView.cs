using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // A player-selectable aircraft model. Metric assets (1 unit = 1 m, wheels at
    // local y0) are shown at true size under the scaled Plane root.
    [System.Serializable]
    public sealed class AircraftModel
    {
        public string id="lowwing",displayName="LOW WING";
        public GameObject prefab;
        public bool metric;
        // Height of the flight pivot above the wheels; matches the airport ground height.
        public float pivotHeight=.9f;
    }
    [DefaultExecutionOrder(200)]
    public sealed class FlightView : MonoBehaviour
    {
        public FlightController flight;
        public Transform visual;
        public Camera sceneCamera;
        public bool useLowPolyModel = true;
        // Default model, listed first.
        public GameObject modelPrefab;
        public AircraftModel[] extraModels;
        Transform propeller;
        float airportPitch;
        bool modelLoaded;
        List<AircraftModel> models;
        public AircraftRig Rig{get;private set;}
        public AircraftModel Current{get;private set;}
        public string AircraftId=>Current!=null?Current.id:null;
        public event System.Action AircraftChanged;
        public IReadOnlyList<AircraftModel> Models
        {
            get
            {
                if(models!=null)return models;
                models=new List<AircraftModel>();
                if(modelPrefab)models.Add(new AircraftModel{prefab=modelPrefab});
                if(extraModels!=null)foreach(var m in extraModels)if(m!=null&&m.prefab&&!models.Exists(x=>x.id==m.id))models.Add(m);
                return models;
            }
        }
        void Awake()
        {
            if(useLowPolyModel&&!modelLoaded&&Models.Count>0)Load(Models[0]);
        }
        // Unknown or empty ids (older saves) use the default model.
        public void SelectAircraft(string id)
        {
            if(!useLowPolyModel||Models.Count==0)return;
            var model=Models[0];foreach(var m in Models)if(m.id==id)model=m;
            if(model!=Current)Load(model);
        }
        void Load(AircraftModel model)
        {
            // The first load hides the original sprite; later loads replace the model.
            if(modelLoaded&&visual)Destroy(visual.gameObject);
            else if(visual&&visual!=transform)visual.gameObject.SetActive(false);
            if(model.metric)
            {
                var holder=new GameObject().transform;holder.SetParent(transform,false);
                holder.localScale=Vector3.one/Mathf.Max(.001f,transform.lossyScale.x);
                Instantiate(model.prefab,holder,false).transform.localPosition=Vector3.down*model.pivotHeight;
                visual=holder;
            }
            else visual=Instantiate(model.prefab,transform,false).transform;
            visual.name="Aircraft visual";Rig=visual.GetComponent<AircraftRig>();
            propeller=visual.Find("Propeller");modelLoaded=true;Current=model;
            Refresh();AircraftChanged?.Invoke();
        }
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(!flight||!visual||!sceneCamera)return;
            if(modelLoaded)
            {
                float pitch=flight.airport&&flight.airport.Active?flight.airport.VisualPitch:0;
                airportPitch=Mathf.Lerp(airportPitch,pitch,1-Mathf.Exp(-5*Mathf.Max(0,Time.deltaTime)));
                visual.rotation=Quaternion.LookRotation(flight.Heading,Vector3.up)*Quaternion.Euler(airportPitch,0,-flight.Bank*(Rig?Rig.bankMultiplier:1));
                if(propeller&&!(flight.journey&&flight.journey.context&&flight.journey.context.SimulationPaused))
                    propeller.Rotate(0,0,1200*Time.deltaTime*flight.EngineActivity,Space.Self);
                return;
            }
            var cameraTransform=sceneCamera.transform;
            var heading=flight.Heading;
            float x=Vector3.Dot(heading,cameraTransform.right),y=Vector3.Dot(heading,cameraTransform.up);
            float angle=-Mathf.Atan2(x,y)*Mathf.Rad2Deg;
            // Sprite nose is local +Y. Project heading onto the camera plane, then bank the wings.
            visual.rotation=cameraTransform.rotation*Quaternion.Euler(0,0,angle)*Quaternion.Euler(0,flight.Bank,0);
        }
    }
}
