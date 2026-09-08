using UnityEngine;
namespace OnAir
{
    [DefaultExecutionOrder(200)]
    public sealed class FlightView : MonoBehaviour
    {
        public FlightController flight;
        public Transform visual;
        public Camera sceneCamera;
        public bool useLowPolyModel = true;
        public GameObject modelPrefab;
        Transform propeller;
        bool modelLoaded;
        void Awake()
        {
            if(!useLowPolyModel)return;
            var model=modelPrefab;
            if(!model)return;
            if(visual&&visual!=transform)visual.gameObject.SetActive(false);
            visual=Instantiate(model,transform,false).transform;
            visual.name="Low-poly aircraft";
            propeller=visual.Find("Propeller");modelLoaded=true;
        }
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(!flight||!visual||!sceneCamera)return;
            if(modelLoaded)
            {
                visual.rotation=Quaternion.LookRotation(flight.Heading,Vector3.up)*Quaternion.Euler(0,0,-flight.Bank);
                if(propeller&&!(flight.journey&&flight.journey.context&&flight.journey.context.paused))
                    propeller.Rotate(0,0,1200*Time.deltaTime,Space.Self);
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
