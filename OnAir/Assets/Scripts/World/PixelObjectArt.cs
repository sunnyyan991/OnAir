using UnityEngine;
namespace OnAir
{
    public sealed class PixelObjectArt:ScriptableObject
    {
        public Mesh[] meshes=new Mesh[4];
        public Material[] materials=new Material[4];
        public float pixelsPerUnit;
        public Quaternion viewRotation;
        public string sourceGeometryHash;
        public string artRevision;
    }
}
