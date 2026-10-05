using UnityEngine;
namespace OnAir {
 // Authoring identity; placement is enabled only after visual approval.
 public sealed class CommercialSignature:MonoBehaviour {
  public string assetCode;
  public int maximumPerCity=2;
  [Range(0,1)] public int variant;
  public string[] advertisementCodes;
 }
}
