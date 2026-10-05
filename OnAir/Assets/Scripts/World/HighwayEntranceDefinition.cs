using UnityEngine;
namespace OnAir
{
    // A parametric transport asset, deliberately excluded from the building lottery.
    // Both its deck and connectors are instantiated by the shared highway mesh builder.
    [CreateAssetMenu(menuName="OnAir/Highway entrance")]
    public sealed class HighwayEntranceDefinition:ScriptableObject
    {
        public string assetCode="TRN-HWY-001";
        [Min(320)] public float approachLength=384;
        [Min(3.9f)] public float minimumRoadWidth=3.9f;
        [TextArea] public string description="双向两车道接地引桥；地面接口 0.11m，高架接口 16m；同一网络统一实例化。";
    }
}
