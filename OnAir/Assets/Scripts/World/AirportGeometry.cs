using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    // Airport is a reserved biome composed of clipped, material-batched geometry.
    // Global site coordinates make neighbouring streamed tiles meet exactly.
    public sealed class AirportGeometry:MonoBehaviour
    {
        public struct Site { public Rect bounds; public float CentreX=>bounds.center.x; }
        public static readonly string[] PaletteNames={"Concrete","Runway","Ivory","Glass","Roof","Steel","Paint","Yellow","Navy","Red"};
        public static readonly string[] PaletteHex={"A0A396","464F50","D2D1C1","547D87","7D8A87","64716F","E4E2CC","CAB667","3C5974","AA5B50"};
        readonly List<Mesh> owned=new List<Mesh>();
        List<Vector3>[] vertices;List<int>[] triangles;Rect clip;float origin;
        public void Build(Site site,Rect tile,float originZ)
        {
            clip=tile;origin=originZ;vertices=new List<Vector3>[10];triangles=new List<int>[10];for(int i=0;i<10;i++){vertices[i]=new List<Vector3>();triangles[i]=new List<int>();}
            float cx=site.CentreX,mid=site.bounds.center.y,length=site.bounds.height-120,run=cx+88;
            Box(0,cx-50,.045f,mid,210,.09f,site.bounds.height-32);
            Box(1,run,.11f,mid,36,.12f,length);Box(1,cx+30,.10f,mid,18,.10f,length-80);
            foreach(float side in new[]{-1f,1f}){
                Box(6,run+side*17,.19f,mid,.25f,.04f,length);
                for(float dz=-length/2+40;dz<length/2;dz+=60)Box(7,run+side*20,.4f,mid+dz,.65f,.6f,.65f);
                for(int k=0;k<6;k++)Box(6,run-12+k*4.8f,.2f,mid+side*(length/2-30),1.6f,.04f,22);
                Box(1,cx+58,.105f,mid+side*(length/2-90),58,.10f,20);
                for(int k=0;k<5;k++)Box(7,cx+34+k*11,.18f,mid+side*(length/2-90),7,.04f,.25f);
            }
            for(float dz=-length/2+85;dz<length/2-70;dz+=55)Box(6,run,.20f,mid+dz,.5f,.04f,26);
            // Terminal and concourse on the landside of the taxiway.
            Box(2,cx-95,5,mid,60,10,126);Box(3,cx-64.7f,5.2f,mid,.7f,7.6f,120);
            Box(4,cx-95,10.8f,mid,64,1.6f,130);Box(2,cx-119,14,mid+34,12,7,34);
            for(int i=-5;i<=5;i++){Box(5,cx-64.2f,5.3f,mid+i*10,.4f,8, .45f);Box(3,cx-95,11.7f,mid+i*10,32,.3f,3);}
            Box(1,cx-139,.12f,mid,12,.12f,190);for(int i=-8;i<=8;i++)Box(6,cx-139,.2f,mid+i*10,.2f,.05f,5);
            for(int gate=0;gate<5;gate++){
                float z=mid-60+gate*30;
                Box(5,cx-52,2.5f,z,1.2f,5,1.2f);Box(2,cx-51,5.3f,z,26,3.2f,4.4f);Box(3,cx-51,5.6f,z-2.25f,24,1.5f,.15f);Box(4,cx-51,7,z,27,.35f,4.9f);Box(5,cx-37,4.5f,z,3,5,5);
                Box(7,cx-20,.17f,z,32,.04f,.25f);Plane(cx-20,z,gate%2==0?8:9);
            }
            // Tower is a tapered-looking stepped shaft with an overhanging glazed cab.
            Box(2,cx-110,16,mid+112,7,32,7);Box(5,cx-110,29,mid+112,10,2,10);Box(3,cx-110,32,mid+112,12,4,12);Box(4,cx-110,34.4f,mid+112,14,.8f,14);Box(5,cx-110,38,mid+112,.35f,7,.35f);
            for(int h=0;h<3;h++){float z=mid-160-h*56;Box(2,cx-87,7,z,55,14,42);Box(4,cx-87,14.7f,z,58,1.4f,45);Box(5,cx-59.3f,5.8f,z,.5f,11,34);for(int j=-2;j<=2;j++)Box(3,cx-59,10,z+j*6,.3f,1.7f,4);}
            // Sparse perimeter fence, never a car lane or a traffic route.
            foreach(float side in new[]{-1f,1f}){float x=cx+side*(site.bounds.width/2-4);Box(5,x,1.5f,mid,.12f,.12f,site.bounds.height-12);for(float z=site.bounds.yMin+8;z<site.bounds.yMax-8;z+=12)Box(5,x,.9f,z,.18f,1.8f,.18f);}
            for(int i=0;i<10;i++)if(vertices[i].Count>0){var mesh=new Mesh{name="Airport "+PaletteNames[i],indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices[i]);mesh.SetTriangles(triangles[i],0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);var go=new GameObject(mesh.name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Airport/"+PaletteNames[i]);}
        }
        void Plane(float x,float z,int accent){
            // 28m regional airliner; eight-sided tapered fuselage, swept wings and tail.
            float[] xx={-15,-12,8,12,14};float[] rr={.15f,1.6f,1.7f,1,.1f};
            for(int k=0;k<4;k++)for(int j=0;j<8;j++){float a=j*Mathf.PI/4,b=(j+1)*Mathf.PI/4;Face(2,new[]{new Vector3(x+xx[k+1],3+Mathf.Sin(a)*rr[k+1],z+Mathf.Cos(a)*rr[k+1]),new Vector3(x+xx[k+1],3+Mathf.Sin(b)*rr[k+1],z+Mathf.Cos(b)*rr[k+1]),new Vector3(x+xx[k],3+Mathf.Sin(b)*rr[k],z+Mathf.Cos(b)*rr[k]),new Vector3(x+xx[k],3+Mathf.Sin(a)*rr[k],z+Mathf.Cos(a)*rr[k])});}
            foreach(float s in new[]{-1f,1f}){Face(2,new[]{new Vector3(x-4,3,z+s),new Vector3(x+5,3,z+s*14),new Vector3(x+8,3,z+s*13),new Vector3(x+5,3,z+s)});Face(2,new[]{new Vector3(x+7,3.1f,z+s),new Vector3(x+11,3.1f,z+s*5.7f),new Vector3(x+13,3.1f,z+s*5),new Vector3(x+12,3.1f,z+s)});Box(accent,x+1,2.4f,z+s*6,4,1.4f,1.4f);for(int w=-10;w<8;w+=2)Box(3,x+w,3.5f,z+s*1.58f,.6f,.5f,.16f);}
            Face(accent,new[]{new Vector3(x+7,3.5f,z),new Vector3(x+11,9,z),new Vector3(x+13,9,z),new Vector3(x+13,3.5f,z)});Box(3,x-12,3.6f,z,1,.65f,1.8f);foreach(float q in new[]{-9f,4f})Box(1,x+q,1.2f,z,1,1,.7f);
        }
        void Box(int m,float x,float y,float z,float w,float h,float d){if(x+w/2<clip.xMin||x-w/2>clip.xMax||z+d/2<clip.yMin||z-d/2>clip.yMax)return;float l=x-w/2,r=x+w/2,b=z-d/2,f=z+d/2,u=y+h/2,v=y-h/2;Face(m,new[]{new Vector3(l,u,b),new Vector3(l,u,f),new Vector3(r,u,f),new Vector3(r,u,b)});Face(m,new[]{new Vector3(l,v,b),new Vector3(l,u,b),new Vector3(r,u,b),new Vector3(r,v,b)});Face(m,new[]{new Vector3(r,v,f),new Vector3(r,u,f),new Vector3(l,u,f),new Vector3(l,v,f)});Face(m,new[]{new Vector3(l,v,f),new Vector3(l,u,f),new Vector3(l,u,b),new Vector3(l,v,b)});Face(m,new[]{new Vector3(r,v,b),new Vector3(r,u,b),new Vector3(r,u,f),new Vector3(r,v,f)});}
        void Face(int mat,Vector3[] input){if(input.Length>2&&Mathf.Abs(input[0].y-input[1].y)<.001f&&Mathf.Abs(input[1].y-input[2].y)<.001f&&Vector3.Cross(input[1]-input[0],input[2]-input[0]).y<0)System.Array.Reverse(input);var p=new List<Vector3>(input);for(int side=0;side<4&&p.Count>0;side++){var output=new List<Vector3>();for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];float da=Distance(a,side),db=Distance(b,side);if(da>=0)output.Add(a);if((da>=0)!=(db>=0))output.Add(Vector3.Lerp(a,b,da/(da-db)));}p=output;}if(p.Count<3)return;int n=vertices[mat].Count;foreach(var v in p)vertices[mat].Add(v-new Vector3(0,0,origin));for(int i=1;i<p.Count-1;i++){triangles[mat].Add(n);triangles[mat].Add(n+i);triangles[mat].Add(n+i+1);}}
        float Distance(Vector3 p,int side)=>side==0?p.x-clip.xMin:side==1?clip.xMax-p.x:side==2?p.z-clip.yMin:clip.yMax-p.z;
        void OnDestroy(){if(Application.isPlaying)foreach(var m in owned)if(m)Destroy(m);}
    }
}
