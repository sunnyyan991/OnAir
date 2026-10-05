using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // Authored entrances and billboards must face real streets after placement rotation.
    public sealed class BuildingStreetFrontage : MonoBehaviour
    {
        public bool frontStreet=true,leftStreet,rightStreet;
        public float clearApron=2.5f;
        public bool allowRearPacking=false;
        public IEnumerable<Rect> Aprons(Rect lot,int rotation)
        {
            var directions=new List<Vector3>();
            if(frontStreet)directions.Add(Vector3.back);
            if(leftStreet)directions.Add(Vector3.left);
            if(rightStreet)directions.Add(Vector3.right);
            foreach(var local in directions)
            {
                var n=Quaternion.Euler(0,rotation,0)*local;Rect apron;
                if(Mathf.Abs(n.x)>.5f)apron=new Rect(n.x<0?lot.xMin-clearApron:lot.xMax,lot.yMin,clearApron,lot.height);
                else apron=new Rect(lot.xMin,n.z<0?lot.yMin-clearApron:lot.yMax,lot.width,clearApron);
                yield return apron;
            }
        }
        public bool CanPlace(Rect lot,int rotation,IEnumerable<Rect> roads,IEnumerable<Rect> neighbours)
        {
            foreach(var apron in Aprons(lot,rotation))
            {
                foreach(var b in neighbours)if(b.Overlaps(apron))return false;
                bool street=false;
                foreach(var r in roads)
                {
                    float overlapX=Mathf.Max(0,Mathf.Min(r.xMax,apron.xMax)-Mathf.Max(r.xMin,apron.xMin));
                    float overlapZ=Mathf.Max(0,Mathf.Min(r.yMax,apron.yMax)-Mathf.Max(r.yMin,apron.yMin));
                    bool side=apron.xMax<=lot.xMin+.01f||apron.xMin>=lot.xMax-.01f;
                    if(overlapX>0&&overlapZ>0&&(side?overlapZ>=lot.height*.65f:overlapX>=lot.width*.65f)){street=true;break;}
                }
                if(!street)return false;
            }
            return true;
        }
    }
}
