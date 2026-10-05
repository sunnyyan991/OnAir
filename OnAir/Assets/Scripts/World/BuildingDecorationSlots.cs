using System;
using UnityEngine;
namespace OnAir {
 // Authoring metadata only: no Update, allocation or decoration spawning in play.
 public sealed class BuildingDecorationSlots : MonoBehaviour {
  public enum Surface {Facade,Roof}
  [Flags] public enum Kind {Sign=1,Screen=2,Antenna=4,Tower=8}
  [Serializable] public struct Slot {
   public string id;
   public Surface surface;
   public Kind allowed;
   public Vector3 localPosition;
   public Vector3 outwardNormal;
   // Local-space envelope width, height, depth; not a scaling instruction.
   public Vector3 maximumSize;
   public bool animatedAllowed;
  }
  public Slot[] slots=Array.Empty<Slot>();
 }
}