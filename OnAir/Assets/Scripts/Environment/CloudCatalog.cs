using UnityEngine;
namespace OnAir {
 [CreateAssetMenu(menuName="OnAir/Cloud Catalog")] public sealed class CloudCatalog:ScriptableObject {
  [System.Serializable] public sealed class Shape {public string id;public Rect uv;public Vector2Int grid;public Vector2 Size(float ppu)=>new Vector2(grid.x/ppu,grid.y/ppu);}
  public Texture2D atlas;
  public float pixelsPerMetre=1.4f;
  public Shape[] shapes;
 }
}
