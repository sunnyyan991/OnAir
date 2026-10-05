using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    // A Scene view should remain an editable 3D city. The fixed Game camera uses
    // depth sprites; unsupported projections automatically reveal the source models.
    public static class PixelObjectCameraPolicy
    {
        static readonly HashSet<PixelObjectView> objects=new HashSet<PixelObjectView>();
        static readonly Dictionary<PixelObjectArt,bool> supported=new Dictionary<PixelObjectArt,bool>();
        static int revision,lastRevision=-1;static Quaternion lastRotation;static bool lastProjection;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){objects.Clear();supported.Clear();revision=0;lastRevision=-1;RenderPipelineManager.beginCameraRendering-=Begin;RenderPipelineManager.beginCameraRendering+=Begin;}
        public static void Register(PixelObjectView obj){if(objects.Count==0){RenderPipelineManager.beginCameraRendering-=Begin;RenderPipelineManager.beginCameraRendering+=Begin;}if(objects.Add(obj))revision++;}
        public static void Unregister(PixelObjectView obj){if(objects.Remove(obj))revision++;}
        public static bool Supports(Camera camera,PixelObjectArt art)=>camera&&art&&camera.cameraType!=CameraType.SceneView&&camera.orthographic&&Quaternion.Angle(camera.transform.rotation,art.viewRotation)<.05f;
        static void Begin(ScriptableRenderContext context,Camera camera)
        {
            bool projection=camera.cameraType!=CameraType.SceneView&&camera.orthographic;var rotation=camera.transform.rotation;
            // Translation and screen size do not change this decision. In normal
            // flight do not query thousands of native Camera properties each frame.
            if(lastRevision==revision&&lastProjection==projection&&(!projection||lastRotation.Equals(rotation)))return;
            lastRevision=revision;lastProjection=projection;lastRotation=rotation;supported.Clear();
            foreach(var obj in objects)if(obj)
            {
                if(!supported.TryGetValue(obj.art,out bool usePixels)){usePixels=projection&&Quaternion.Angle(rotation,obj.art.viewRotation)<.05f;supported.Add(obj.art,usePixels);}
                obj.ShowPixels(usePixels);
            }
        }
    }
}
