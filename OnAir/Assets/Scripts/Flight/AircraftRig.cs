using UnityEngine;
namespace OnAir
{
    public sealed class AircraftRig:MonoBehaviour
    {
        public string aircraftId="a330_300";
        public float length=63.67f,wingspan=60.3f,bankMultiplier=.3f;
        public Transform leftNavigation,rightNavigation,tailNavigation,leftStrobe,rightStrobe,beacon;
        public AircraftLivery[] liveries;
        public Material cabinInterior;
        public Vector3 cabinEye=new Vector3(2.35f,1.05f,-4);
        public Vector3 cabinAngles=new Vector3(7,94,0);
        public int LiveryIndex{get;private set;}
        public AircraftLivery Livery=>liveries!=null&&liveries.Length>0?liveries[LiveryIndex]:null;
        Renderer[] surfaces;MaterialPropertyBlock block;
        void Awake(){surfaces=GetComponentsInChildren<Renderer>();block=new MaterialPropertyBlock();ApplyLivery(0);}
        public void ApplyLivery(int index)
        {
            if(liveries==null||liveries.Length==0)return;LiveryIndex=(index%liveries.Length+liveries.Length)%liveries.Length;
            if(surfaces==null)surfaces=GetComponentsInChildren<Renderer>();if(block==null)block=new MaterialPropertyBlock();
            foreach(var r in surfaces)if(r&&r.sharedMaterial.HasProperty("_Accent")){r.GetPropertyBlock(block);block.SetColor("_Accent",Livery.accent);r.SetPropertyBlock(block);}
        }
    }
}
