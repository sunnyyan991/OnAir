using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    // Only independent small meshes qualify; never disable a combined facade or whole building.
    public sealed class DistantDetailShadows : MonoBehaviour
    {
        public Camera sceneCamera;
        readonly List<Renderer> details=new List<Renderer>();float nextUpdate;
        public int CandidateCount=>details.Count;
        public int DisabledCount{get{int count=0;foreach(var r in details)if(r&&r.shadowCastingMode==ShadowCastingMode.Off)count++;return count;}}
        void Start(){foreach(var r in GetComponentsInChildren<Renderer>()){var s=r.bounds.size;if(r.shadowCastingMode==ShadowCastingMode.On&&Mathf.Max(s.x,s.y,s.z)<2.5f)details.Add(r);}}
        void Update()
        {
            if(!sceneCamera||Time.time<nextUpdate)return;nextUpdate=Time.time+.75f;
            foreach(var r in details)if(r){float depth=Vector3.Dot(r.bounds.center-sceneCamera.transform.position,sceneCamera.transform.forward);if(depth>130)r.shadowCastingMode=ShadowCastingMode.Off;else if(depth<115)r.shadowCastingMode=ShadowCastingMode.On;}
        }
    }
}
