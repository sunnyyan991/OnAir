using UnityEngine;
namespace OnAir {
 // Static authoring mask: no real-time Lights, camera or Update per billboard.
 public sealed class BillboardLighting:MonoBehaviour {
  public BillboardContent content;
  public string slotId;
  public float width,height;
  public bool selfIlluminated;
  public float Evaluate(Vector3 local){
   if(selfIlluminated)return 1;
   float illumination=.18f;
   for(int lamp=0;lamp<3;lamp++){
    float x=(lamp-1)*width*.32f;
    float down=height*.5f-local.y;
    float radius=width*.14f+down*.22f;
    float cone=Mathf.Exp(-Mathf.Pow((local.x-x)/Mathf.Max(.25f,radius),2));
    illumination=Mathf.Max(illumination,.18f+.82f*cone*Mathf.Lerp(1,.48f,Mathf.Clamp01(down/height)));
   }
   return Mathf.Clamp01(illumination);
  }
 }
}

