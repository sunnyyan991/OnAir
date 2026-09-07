using UnityEngine;
namespace OnAir.Prototype
{
    [CreateAssetMenu(menuName="OnAir/City Block Kit")]
    public sealed class CityBlockKit : ScriptableObject
    {
        public BuildingCatalog buildings;
        public TextAsset rules;
        public GameObject roadStraight,roadCorner,roadT,roadCross;
        public GameObject pavement,park,tree,treeTall,hedge,lamp,bench,planter;
    }
}
