using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using System.Collections.Generic;
namespace OnAir {
 [DefaultExecutionOrder(400)] public sealed class LayeredPixelClouds:MonoBehaviour {
  public const float FadeSeconds=30f;
  public const float ExistingLayerRise=50f,ThirdLayerAircraftOffset=10f;
  public const int ThirdLayerQuota=2,RuntimeGroupCount=16;
  static readonly int[] ThirdLayerShapes={6,9,10,11}; // M01, M04, M05, M06 in the approved catalogue.
  public GameEntry game;public int VisibleClouds{get;private set;}public int PoolCount=>groups==null?0:groups.Length;
  public Vector3 LayerHeights{get;private set;}public int VisiblePresenceChanges{get;private set;}public int OffscreenPopulationChanges{get;private set;}
  public float PixelsPerMetre=>catalog?catalog.pixelsPerMetre/2:0;
  sealed class Part {public Transform t;public Renderer r;public Material material;public CloudCatalog.Shape shape;}
  sealed class Group {public string id;public int layer,index;public Vector2 origin,size;public float speed;public int passage;public Transform root;public Part[] parts;public bool present;public bool fading,arriving,reserved;public float fade=1,fadeInSeconds=FadeSeconds;public Group next,target;public Vector2 screen;}
  CloudCatalog catalog;Group[] groups;Mesh quad;int seed;bool ready,populated;System.Random random,thirdLayerRandom;
  readonly List<Group> retiring=new List<Group>();CloudShadowOverlay shadowOverlay;
  string reviewId;Vector2 reviewPoint;
  System.Random thunderRandom;Part flashPart;Vector2 flashUV;float flashAge=10,thunderWait;bool wasStorm;
  public int ThunderEvents{get;private set;}
  public float ThunderStrength{get;private set;}
  public float ThunderWait=>thunderWait;
  public void ReviewSolo(string id,Vector2 viewport){reviewId=id;reviewPoint=viewport;}
  public void ReviewNormal(){reviewId=null;}
  void Start(){catalog=Resources.Load<CloudCatalog>("PixelClouds/Catalog");var source=Resources.Load<Material>("PixelClouds/Cloud");if(!catalog||catalog.shapes.Length!=12||!source){Debug.LogError("Approved cloud catalog missing");enabled=false;return;}
   quad=new Mesh{name="Shared approved-cloud quad"};quad.vertices=new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(.5f,.5f),new Vector3(-.5f,.5f)};quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};quad.triangles=new[]{0,2,1,0,3,2};quad.RecalculateBounds();
   seed=game.journey.seed;var rng=new System.Random(seed^724831);random=new System.Random(seed^192401);thirdLayerRandom=new System.Random(seed^364927);thunderRandom=new System.Random(seed^871293);groups=new Group[RuntimeGroupCount];
   for(int n=0;n<groups.Length;n++){bool third=n>=12;int shape=third?ThirdLayerShapes[n-12]:n;var initialRandom=third?thirdLayerRandom:rng;var g=new Group{id=(third?"T3-":"")+catalog.shapes[shape].id,index=n,layer=third?2:n<6?0:1,origin=new Vector2((float)initialRandom.NextDouble(),(float)initialRandom.NextDouble()),speed=Mathf.Lerp(.90f,1.10f,(float)initialRandom.NextDouble())};var root=new GameObject("Cloud group "+g.id);root.transform.SetParent(transform,false);g.root=root.transform;var parts=new List<Part>();
    AddPart(g,shape,Vector2.zero,source,parts);
    // Large cloud banks extend the approved shapes with additional pieces at the
    // SAME texels/metre. A single global scale applies to every sprite and bank piece.
    if(n==4){AddPart(g,5,new Vector2(-65,-18),source,parts);AddPart(g,1,new Vector2(65,-15),source,parts);AddPart(g,2,new Vector2(-30,30),source,parts);AddPart(g,4,new Vector2(40,28),source,parts);}
    if(n==5){for(int row=-1;row<=1;row++)for(int col=0;col<4;col++)AddPart(g,(col+row+3)%3==0?4:(col+row+3)%3==1?5:1,new Vector2((col-1.5f)*65,row*33),source,parts);}
    g.parts=parts.ToArray();Vector2 lo=new Vector2(float.MaxValue,float.MaxValue),hi=-lo;foreach(var p in parts){var pos=(Vector2)p.t.localPosition;var half=p.shape.Size(PixelsPerMetre)*.5f;lo=Vector2.Min(lo,pos-half);hi=Vector2.Max(hi,pos+half);}var centre=(lo+hi)*.5f;foreach(var p in parts)p.t.localPosition-=(Vector3)centre;g.size=hi-lo;g.present=false;g.fading=false;g.fade=1;groups[n]=g;
   }
   shadowOverlay=new CloudShadowOverlay();RenderPipelineManager.beginCameraRendering+=BeginCloudShadows;ready=true;
  }
  void AddPart(Group g,int shape,Vector2 offset,Material source,List<Part> list){var s=catalog.shapes[shape];var o=new GameObject("Pixel cloud "+g.id+" / "+s.id+" / "+list.Count,typeof(MeshFilter),typeof(MeshRenderer));o.transform.SetParent(g.root,false);o.transform.localPosition=offset*2;var size=s.Size(PixelsPerMetre);o.transform.localScale=new Vector3(size.x,size.y,1);o.GetComponent<MeshFilter>().sharedMesh=quad;var r=o.GetComponent<MeshRenderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;var mat=new Material(source);mat.mainTexture=catalog.atlas;mat.SetVector("_CloudRect",new Vector4(s.uv.x,s.uv.y,s.uv.width,s.uv.height));mat.SetVector("_CloudGrid",new Vector4(s.grid.x,s.grid.y,0,0));r.sharedMaterial=mat;list.Add(new Part{t=o.transform,r=r,material=mat,shape=s});}
  static int Family(Group g)=>g.layer==2?3:g.index<4?0:g.index<6?1:2;
  System.Random PopulationRandom(int family)=>family==3?thirdLayerRandom:random;
  static float CloudSpeed(Group g)=>(g.layer==0?4.5f:5.75f)*g.speed;
  int ActiveCount(int family){int count=0;foreach(var g in groups)if(g.present&&Family(g)==family)count++;return count;}
  static float ExitSeconds(Group g,Vector2 drift,float width,float height){
   float speed=CloudSpeed(g);
   float x=Mathf.Abs(drift.x)<.0001f?float.PositiveInfinity:(width*.5f+g.size.x*.5f+2-g.screen.x*Mathf.Sign(drift.x))/(speed*Mathf.Abs(drift.x));
   float y=Mathf.Abs(drift.y)<.0001f?float.PositiveInfinity:(height*.5f+g.size.y*.5f+2-g.screen.y*Mathf.Sign(drift.y))/(speed*Mathf.Abs(drift.y));
   return Mathf.Max(0,Mathf.Min(x,y));
  }
  void PrepareNext(Group source,Vector2 drift,float width,float height){
   // Prepare outside the active quota before this cloud retires. The old Incoming
   // position remains the arrival anchor, so steady cloud cadence is unchanged.
   int family=Family(source);if(ActiveCount(family)>Target(family))return;
   Group chosen=null;int eligible=0;
   foreach(var g in groups)if((!g.present||g==source)&&!g.reserved&&Family(g)==family){if(PopulationRandom(family).Next(++eligible)==0)chosen=g;}
   if(chosen==null)return;
   var warm=Copy(chosen);warm.target=chosen;warm.arriving=true;warm.fade=0;warm.passage=chosen.passage+1;
   warm.fadeInSeconds=Mathf.Max(.1f,Mathf.Min(FadeSeconds,ExitSeconds(source,drift,width,height)));
   warm.screen=Incoming(warm,drift,width,height)-drift*CloudSpeed(warm)*warm.fadeInSeconds;
   chosen.reserved=true;source.next=warm;
  }
  void PromoteNext(Group source){
   var warm=source.next;if(warm==null)return;source.next=null;
   var target=warm.target;target.reserved=false;
   if(ActiveCount(Family(source))>=Target(Family(source))){warm.fading=true;warm.arriving=false;retiring.Add(warm);return;}
   Adopt(target,warm);
  }
  void Adopt(Group target,Group warm){
   Release(target);target.root=warm.root;target.parts=warm.parts;target.screen=warm.screen;target.fade=warm.fade;
   target.fadeInSeconds=warm.fadeInSeconds;target.arriving=warm.arriving;target.present=true;target.fading=false;target.passage=warm.passage;
  }
  Group Copy(Group source){
   var root=Instantiate(source.root.gameObject,transform).transform;var renderers=root.GetComponentsInChildren<Renderer>();
   var copy=new Group{id=source.id,layer=source.layer,index=source.index,size=source.size,speed=source.speed,screen=source.screen,root=root,present=true,fade=source.fade,parts=new Part[source.parts.Length]};
   for(int n=0;n<copy.parts.Length;n++){var mat=new Material(source.parts[n].material);renderers[n].sharedMaterial=mat;copy.parts[n]=new Part{t=renderers[n].transform,r=renderers[n],material=mat,shape=source.parts[n].shape};}
   return copy;
  }
  int Target(int family){
   // Independent near-aircraft allowance: weather never borrows or expands these slots.
   if(family==3)return ThirdLayerQuota;
   switch(game.session.weather){
    case Weather.Clear:return family==1?0:1;
    case Weather.Cloudy:return family==0?3:family==1?0:2;
    case Weather.Rain:return family==0?4:family==1?1:2;
    default:return family==0?4:family==1?2:3;
   }
  }
  void Populate(Vector2 drift,float w,float h,bool initial){
   for(int family=0;family<4;family++){
    int count=0;foreach(var g in groups)if(g.present&&Family(g)==family)count++;
    while(count<Target(family)){
     // Reservoir sampling avoids selecting the first few shapes every time.
     Group chosen=null;int eligible=0;
     foreach(var g in groups)if(!g.present&&Family(g)==family){eligible++;if(PopulationRandom(family).Next(eligible)==0)chosen=g;}
     if(chosen==null)break;
     // A weather increase can claim an already-preparing cloud without resetting
     // its opacity or position, and without reducing the requested activity quota.
     if(chosen.reserved){foreach(var source in groups)if(source.next!=null&&source.next.target==chosen){var warm=source.next;source.next=null;chosen.reserved=false;Adopt(chosen,warm);break;}count++;continue;}
     chosen.present=true;chosen.fading=false;chosen.arriving=!initial;chosen.fade=initial?1:0;chosen.fadeInSeconds=FadeSeconds;
     chosen.screen=initial?new Vector2((chosen.origin.x-.5f)*w,(chosen.origin.y-.5f)*h):Incoming(chosen,drift,w,h);
     if(!initial){chosen.screen-=drift*CloudSpeed(chosen)*FadeSeconds;OffscreenPopulationChanges++;}
     count++;
    }
   }
  }
  void LateUpdate(){Advance(game.session.SimulationPaused?0:Time.deltaTime);}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  public void ReviewAdvance(float seconds){Advance(seconds);}
#endif
  void Advance(float dt){if(!ready)return;var camera=game.sceneCamera;
   var current=game.flight.transform.position;
   float h=camera.orthographicSize*2,w=h*camera.aspect;var route=new Vector3(game.flight.routeSlope,0,1).normalized;
   var drift=-new Vector2(Vector3.Dot(route,camera.transform.right),Vector3.Dot(route,camera.transform.up)).normalized;
   Shader.SetGlobalVector("_CloudViewForward",camera.transform.forward);
   Shader.SetGlobalColor("_CloudLightTint",game.environment.sun.color);
   float phase=game.session.solarPhase;
   float sunset=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.445f,.48f,phase))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.50f,.535f,phase)));
   Shader.SetGlobalFloat("_CloudSunsetGlow",sunset);
   // Flight transform is the 65m camera anchor; the aircraft visual actually flies at 130m.
   float aircraftY=AircraftHeightPreview.Enabled?AircraftHeightPreview.AbsoluteHeight:current.y;
   LayerHeights=new Vector3(current.y-18+ExistingLayerRise,current.y+9+ExistingLayerRise,aircraftY+ThirdLayerAircraftOffset);VisibleClouds=0;
   if(!populated){Populate(drift,w,h,true);populated=true;}
   foreach(var g in groups)if(g.next!=null){var warm=g.next;float step=Mathf.Min(dt,(1-warm.fade)*warm.fadeInSeconds);warm.screen+=drift*CloudSpeed(warm)*step;warm.fade=Mathf.MoveTowards(warm.fade,1,dt/warm.fadeInSeconds);warm.arriving=warm.fade<1;}
   foreach(var g in groups){
    if(!g.present)continue;
    g.screen+=drift*((g.layer==0?4.5f:5.75f)*g.speed*dt);
    if(g.arriving){g.fade=Mathf.MoveTowards(g.fade,1,dt/g.fadeInSeconds);g.arriving=g.fade<1;}
    if(!g.arriving&&g.next==null&&ExitSeconds(g,drift,w,h)<=FadeSeconds)PrepareNext(g,drift,w,h);
    bool outside=Mathf.Abs(g.screen.x)>w*.5f+g.size.x*.5f+2||Mathf.Abs(g.screen.y)>h*.5f+g.size.y*.5f+2;
    if(!g.fading&&outside&&((g.screen.x>w*.5f+g.size.x*.5f+2&&drift.x>0)||(g.screen.x<-w*.5f-g.size.x*.5f-2&&drift.x<0)||(g.screen.y>h*.5f+g.size.y*.5f+2&&drift.y>0)||(g.screen.y<-h*.5f-g.size.y*.5f-2&&drift.y<0))){Retire(g);g.present=false;g.passage++;PromoteNext(g);OffscreenPopulationChanges++;}
    if(g.fading){g.fade=Mathf.MoveTowards(g.fade,0,dt/FadeSeconds);if(g.fade<=0){g.fading=false;g.present=false;OffscreenPopulationChanges++;}}
   }
   for(int n=retiring.Count-1;n>=0;n--){var g=retiring[n];g.screen+=drift*((g.layer==0?4.5f:5.75f)*g.speed*dt);g.fade=Mathf.MoveTowards(g.fade,0,dt/FadeSeconds);if(g.fade<=0){Release(g);retiring.RemoveAt(n);}}
   Populate(drift,w,h,false);
   AdvanceThunder(dt,w,h);
   shadowOverlay.count=0;
   foreach(var g in RenderGroups()){
    float sx=g.screen.x,sy=g.screen.y;
    bool outside=Mathf.Abs(sx)>w*.5f+g.size.x*.5f+2||Mathf.Abs(sy)>h*.5f+g.size.y*.5f+2;
    bool show=g.present;if(reviewId!=null){show=g.id==reviewId;if(show){sx=(reviewPoint.x-.5f)*w;sy=(reviewPoint.y-.5f)*h;outside=false;}}
    float y=LayerHeights[g.layer]-(g.layer==2?0:g.index*.10f);var ray=camera.ViewportPointToRay(new Vector3(.5f+sx/w,.5f+sy/h,0));g.root.position=ray.GetPoint((y-ray.origin.y)/ray.direction.y);g.root.rotation=camera.transform.rotation;
    for(int p=0;p<g.parts.Length;p++){g.parts[p].r.enabled=show&&camera.enabled;g.parts[p].material.SetFloat("_CloudPlaneY",y+p*.01f);g.parts[p].material.SetFloat("_CloudFade",show?g.fade:0);
     var part=g.parts[p];part.material.SetVector("_CloudFlash",part==flashPart?new Vector4(flashUV.x,flashUV.y,0,ThunderStrength):Vector4.zero);var bounds=new Bounds();bool first=true;
     for(int cy=-1;cy<=1;cy+=2)for(int cx=-1;cx<=1;cx+=2){var v=part.t.TransformPoint(new Vector3(cx*.5f,cy*.5f,0));v+=camera.transform.forward*((y+p*.01f-v.y)/camera.transform.forward.y);if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}
     // One shadow path across birth, full opacity and retirement. No caster
     // handoff at fade=1, and no second cloud shadow over the same receiver.
     bounds.Expand(.2f);part.r.bounds=bounds;part.r.shadowCastingMode=ShadowCastingMode.Off;
     if(show&&shadowOverlay.count<128){int n=shadowOverlay.count++;var centre=part.t.position;centre+=camera.transform.forward*((y+p*.01f-centre.y)/camera.transform.forward.y);shadowOverlay.centres[n]=centre;var size=part.shape.Size(PixelsPerMetre);shadowOverlay.sizes[n]=new Vector4(size.x,size.y,g.fade,g.layer);var rect=part.shape.uv;shadowOverlay.rects[n]=new Vector4(rect.x,rect.y,rect.width,rect.height);shadowOverlay.grids[n]=new Vector4(part.shape.grid.x,part.shape.grid.y,0,0);}
    }if(show&&!outside)VisibleClouds++;
   }
  }
  void AdvanceThunder(float dt,float width,float height){
   bool storm=game.session.weather==Weather.HeavyRain;
   if(!storm){wasStorm=false;flashPart=null;flashAge=10;ThunderStrength=0;return;}
   if(!wasStorm){wasStorm=true;thunderWait=Mathf.Lerp(12,25,(float)thunderRandom.NextDouble());}
   if(dt>0){flashAge+=dt;thunderWait-=dt;}
   if(thunderWait<=0){
    Part chosen=null;Vector2 centre=Vector2.zero;int candidates=0;
    foreach(var g in groups){if(!g.present)continue;foreach(var part in g.parts){
     var uv=new Vector2(Mathf.Lerp(.35f,.65f,(float)thunderRandom.NextDouble()),Mathf.Lerp(.32f,.62f,(float)thunderRandom.NextDouble()));
     var size=part.shape.Size(PixelsPerMetre);var screen=g.screen+(Vector2)part.t.localPosition+new Vector2((uv.x-.5f)*size.x,(uv.y-.5f)*size.y);
     if(Mathf.Abs(screen.x)>width*.43f||Mathf.Abs(screen.y)>height*.43f)continue;
     var rect=part.shape.uv;if(catalog.atlas.GetPixelBilinear(rect.x+uv.x*rect.width,rect.y+uv.y*rect.height).a<.8f)continue;
     if(thunderRandom.Next(++candidates)==0){chosen=part;centre=uv;}
    }}
    if(chosen!=null){flashPart=chosen;flashUV=centre;flashAge=0;ThunderEvents++;thunderWait=Mathf.Lerp(25,55,(float)thunderRandom.NextDouble());}
    else thunderWait=3;
   }
   ThunderStrength=flashPart!=null?.16f*(Mathf.Exp(-Mathf.Pow((flashAge-.13f)/.065f,2))+.55f*Mathf.Exp(-Mathf.Pow((flashAge-.40f)/.10f,2))):0;
   if(flashAge>.8f){flashPart=null;ThunderStrength=0;}
  }
  Vector2 Incoming(Group g,Vector2 direction,float width,float height){
   // Sample the full crosswind extent of the camera, including both corners.
   // A different lane on each passage avoids permanent empty diagonal wedges.
   float extent=(Mathf.Abs(direction.y)*width+Mathf.Abs(direction.x)*height)*.5f;
   float lane=Mathf.Repeat(g.index*.381966f+g.passage*.618034f+(float)PopulationRandom(Family(g)).NextDouble()*.22f,1);
   float lateral=(lane*2-1)*extent;
   var offset=new Vector2(-direction.y,direction.x)*lateral;
   float x=(width*.5f+g.size.x*.5f+8+Mathf.Sign(direction.x)*offset.x)/Mathf.Max(.0001f,Mathf.Abs(direction.x));
   float y=(height*.5f+g.size.y*.5f+8+Mathf.Sign(direction.y)*offset.y)/Mathf.Max(.0001f,Mathf.Abs(direction.y));
   return offset-direction*Mathf.Min(x,y);
  }
  // Small diagnostic sample of actual alpha, not a misleading bounding-box count.
  public float ReviewCoverage()=>ReviewCoverage(new Rect(0,0,1,1));
  public float ReviewCoverage(Rect viewport){if(!ready)return 0;var camera=game.sceneCamera;float h=camera.orthographicSize*2,w=h*camera.aspect;int covered=0;
   for(int y=0;y<18;y++)for(int x=0;x<32;x++){var point=new Vector2((viewport.x+(x+.5f)/32*viewport.width-.5f)*w,(viewport.y+(y+.5f)/18*viewport.height-.5f)*h);bool hit=false;
    foreach(var g in groups){if(!g.present)continue;foreach(var p in g.parts){var size=p.shape.Size(PixelsPerMetre);var q=point-g.screen-(Vector2)p.t.localPosition;var uv=new Vector2(q.x/size.x+.5f,q.y/size.y+.5f);if(uv.x<0||uv.x>1||uv.y<0||uv.y>1)continue;var rect=p.shape.uv;if(catalog.atlas.GetPixelBilinear(rect.x+uv.x*rect.width,rect.y+uv.y*rect.height).a>.5f){hit=true;break;}}if(hit)break;}
    if(hit)covered++;
   }return covered/576f;
  }
  public bool UniformGrain(){if(!ready)return false;foreach(var g in groups)foreach(var p in g.parts)if(Mathf.Abs(p.t.lossyScale.x/p.shape.grid.x-1/PixelsPerMetre)>.0001f||Mathf.Abs(p.t.lossyScale.y/p.shape.grid.y-1/PixelsPerMetre)>.0001f)return false;return true;}
  IEnumerable<Group> RenderGroups(){foreach(var g in groups){yield return g;if(g.next!=null)yield return g.next;}foreach(var g in retiring)yield return g;}
  void BeginCloudShadows(ScriptableRenderContext context,Camera camera){if(!ready||camera!=game.sceneCamera)return;shadowOverlay.Prepare(catalog.atlas,camera,game.environment.sun);camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(shadowOverlay);}
  void Retire(Group source){var root=Instantiate(source.root.gameObject,transform).transform;var renderers=root.GetComponentsInChildren<Renderer>();var g=new Group{id=source.id,layer=source.layer,index=source.index,size=source.size,speed=source.speed,screen=source.screen,root=root,present=true,fading=true,fade=source.fade,parts=new Part[source.parts.Length]};for(int n=0;n<g.parts.Length;n++){var mat=new Material(source.parts[n].material);renderers[n].sharedMaterial=mat;g.parts[n]=new Part{t=renderers[n].transform,r=renderers[n],material=mat,shape=source.parts[n].shape};}retiring.Add(g);}
  void Release(Group g){foreach(var p in g.parts)if(p.material)Destroy(p.material);if(g.root)Destroy(g.root.gameObject);}
  void OnDestroy(){RenderPipelineManager.beginCameraRendering-=BeginCloudShadows;shadowOverlay?.Dispose();foreach(var g in retiring)Release(g);if(groups!=null)foreach(var g in groups){if(g.next!=null)Release(g.next);Release(g);}if(quad)Destroy(quad);}
 }
}
