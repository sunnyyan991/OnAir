using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    // Object pixels move continuously with their original 3D footprint. Original
    // geometry remains the shadow/clearance source; the sprite writes per-pixel depth.
    public sealed class PixelObjectView:MonoBehaviour
    {
        public PixelObjectArt art;
        public MeshRenderer presentation{get;private set;}
        public MeshRenderer[] originalFacades{get;private set;}
        MeshRenderer[] originals;
        ShadowCastingMode[] shadows;
        bool[] originalEnabled;
        bool pixelsVisible=true;
        public string FallbackReason{get;private set;}
        public bool PixelsVisible=>presentation&&presentation.enabled;
        public void Configure()
        {
            if(presentation||!art)return;
            if(!CanUseArt(out string reason)){FallbackReason=reason;Debug.LogWarning(name+": using original model: "+reason,this);return;}
            int direction=((Mathf.RoundToInt(transform.eulerAngles.y/90)%4)+4)%4;
            originals=GetComponentsInChildren<MeshRenderer>();shadows=new ShadowCastingMode[originals.Length];originalEnabled=new bool[originals.Length];
            for(int i=0;i<originals.Length;i++){shadows[i]=originals[i].shadowCastingMode;originalEnabled[i]=originals[i].enabled;originals[i].shadowCastingMode=ShadowCastingMode.ShadowsOnly;}
            var go=new GameObject("Object pixel artwork");go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=art.meshes[direction];
            presentation=go.AddComponent<MeshRenderer>();presentation.sharedMaterial=art.materials[direction];presentation.shadowCastingMode=ShadowCastingMode.Off;
            var view=GetComponent<BuildingView>();
            if(view){originalFacades=view.facades;view.facades=new[]{presentation};view.windows=new[]{presentation};}
            PixelObjectCameraPolicy.Register(this);
        }
        public bool CanUseArt(out string reason)
        {
            reason=null;
            if(!art||art.meshes==null||art.materials==null||art.meshes.Length!=4||art.materials.Length!=4){reason="incomplete artwork";return false;}
            for(int i=0;i<4;i++)if(!art.meshes[i]||!art.materials[i]){reason="missing direction "+i;return false;}
            var scale=transform.lossyScale;
            if(scale.x<=0||Mathf.Abs(scale.x-scale.y)>.001f||Mathf.Abs(scale.x-scale.z)>.001f){reason="non-uniform or mirrored scale";return false;}
            float yaw=Mathf.Round(transform.eulerAngles.y/90)*90;
            if(Quaternion.Angle(transform.rotation,Quaternion.Euler(0,yaw,0))>.05f){reason="requires an upright 90-degree orientation";return false;}
            return true;
        }
        public void ShowPixels(bool show)
        {
            if(show==pixelsVisible||!presentation)return;
            pixelsVisible=show;presentation.enabled=show;
            for(int i=0;i<originals.Length;i++)if(originals[i]){originals[i].enabled=originalEnabled[i];originals[i].shadowCastingMode=show?ShadowCastingMode.ShadowsOnly:shadows[i];}
        }
        void OnDestroy()=>PixelObjectCameraPolicy.Unregister(this);
    }
}
