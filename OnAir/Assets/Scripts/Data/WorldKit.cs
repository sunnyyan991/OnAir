using UnityEngine;
namespace OnAir
{
    [CreateAssetMenu(menuName="OnAir/World Kit")]
    public sealed class WorldKit : ScriptableObject
    {
        public GameObject roadStraight,roadCorner,roadT,roadCross;
        public GameObject pavement,park,tree,treeTall,hedge,lamp,bench,planter;
        [Tooltip("Tiny shrine, pocket park, basketball, small football, temple garden. Separate from street-building weights.")]
        public GameObject[] greenSpaceAssets=System.Array.Empty<GameObject>();
        [Header("Dense city streets")]
        public Material cityAsphalt, cityPaving, cityPaint;
        [Min(0)] public int buildingGapModules=1,streetSetbackModules=1;
        [Min(1)] public int emptyPlotModules=12;
        [Range(0,.2f)] public float emptyPlotChance=.04f;
        public Color[] facadePalette=System.Array.Empty<Color>();
    }
}
