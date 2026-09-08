using UnityEngine;
namespace OnAir
{
    [DefaultExecutionOrder(100)]
    public sealed class CameraFollow : MonoBehaviour
    {
        public FlightController target;
        public Vector3 offset=new Vector3(-42.5f,43.1f,-42.5f);
        [Min(.01f)] public float smoothTime=.65f;
        [Min(0)] public float lookAhead=2;
        Vector3 velocity;
        int originLeg,teleport=-1;
        void LateUpdate()=>Follow(Time.deltaTime);
        public void Follow(float seconds)
        {
            if(!target||!target.journey||target.journey.Current==null)return;
            var route=target.journey;
            Vector3 wanted=target.transform.position+offset+target.Heading*lookAhead;
            if(teleport!=route.TeleportRevision)
            {
                teleport=route.TeleportRevision;originLeg=route.Current.index;velocity=Vector3.zero;transform.position=wanted;return;
            }
            // Apply the same origin shift as the plane and ground, preserving the camera's damping.
            int delta=route.Current.index-originLeg;
            transform.position+=Vector3.back*(delta*TerrainStreamer.SegmentLength);originLeg=route.Current.index;
            if(seconds>0)transform.position=Vector3.SmoothDamp(transform.position,wanted,ref velocity,Mathf.Max(.01f,smoothTime),Mathf.Infinity,seconds);
        }
    }
}
