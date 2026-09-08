using UnityEngine;
namespace OnAir
{
    [CreateAssetMenu(menuName="OnAir/World Kit")]
    public sealed class WorldKit : ScriptableObject
    {
        public GameObject roadStraight,roadCorner,roadT,roadCross;
        public GameObject pavement,park,tree,treeTall,hedge,lamp,bench,planter;
    }
}
