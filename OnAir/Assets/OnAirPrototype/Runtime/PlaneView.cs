using UnityEngine;
namespace OnAir.Prototype
{
    public sealed class PlaneView : MonoBehaviour
    {
        [SerializeField] Transform visual;
        [SerializeField] float bobAmplitude = .16f;
        [SerializeField] float bobFrequency = 1.6f;
        Vector3 origin; Quaternion rotation;
        public void Configure(Transform sprite) { visual = sprite; }
        void Awake() { if (!visual) visual = transform; origin = visual.localPosition; rotation = visual.localRotation; }
        void Update()
        {
            visual.localPosition = origin + Vector3.up * (Mathf.Sin(Time.time * bobFrequency) * bobAmplitude);
            visual.localRotation = rotation * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * .8f) * 3f);
        }
    }
}
