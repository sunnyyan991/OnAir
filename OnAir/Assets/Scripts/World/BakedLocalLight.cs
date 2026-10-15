using UnityEngine;
namespace OnAir
{
    // Static light authoring for pixel assets. The editor projects this onto actual
    // surfaces; runtime uses the resulting map and the existing night brightness.
    public sealed class BakedLocalLight:MonoBehaviour
    {
        public Color colour=new Color(1,.78f,.48f);
        [Min(.1f)] public float range=3.6f;
        [Min(0)] public float intensity=3;
    }
}
