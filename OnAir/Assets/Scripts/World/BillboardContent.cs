using UnityEngine;
namespace OnAir {
 // Native pixel content remains separate from building identity and mounting geometry.
 public sealed class BillboardContent:ScriptableObject {
  public string assetCode,theme;
  public int width,height;
  public Color[] palette;
  public byte[] pixels;
 }
}
