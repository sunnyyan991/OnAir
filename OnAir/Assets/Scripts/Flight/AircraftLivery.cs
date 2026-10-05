using UnityEngine;
namespace OnAir
{
    [CreateAssetMenu(menuName="OnAir/Aircraft livery")]
    public sealed class AircraftLivery:ScriptableObject
    {
        public string id="wine",displayName="Wine";
        public Color accent=new Color(.38f,.17f,.20f);
        public Texture2D cabinWing;
        public Vector2 cabinLightUV;
    }
}
