using UnityEngine;

namespace OnAir
{
    // Approved fixed flight altitude. The historical type name is retained.
    // The controller supplies a separate fixed camera/streaming anchor.
    [DefaultExecutionOrder(300)]
    public sealed class AircraftHeightPreview : MonoBehaviour
    {
        public static readonly bool Enabled = true;
        public const float AbsoluteHeight = 130f;
        public const float CameraAnchorHeight = 65f;
        public const float MaximumWorldHeight = AbsoluteHeight - 10f;
        FlightView view;
        Transform originalVisual;
        Vector3 originalLocalPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Enabled) return;
            var flightView = Object.FindObjectOfType<FlightView>();
            if (flightView && !flightView.GetComponent<AircraftHeightPreview>())
                flightView.gameObject.AddComponent<AircraftHeightPreview>();
        }

        void LateUpdate()
        {
            if (!view) view = GetComponent<FlightView>();
            if (!view || !view.visual || !view.sceneCamera) return;
            if (originalVisual != view.visual)
            {
                originalVisual = view.visual;
                originalLocalPosition = originalVisual.localPosition;
            }
            Vector3 baseline = transform.TransformPoint(originalLocalPosition);
            Vector3 direction = view.sceneCamera.transform.forward;
            if (Mathf.Abs(direction.y) < .001f) return;
            // Moving along the view ray preserves both screen coordinates.
            // Orthographic projection preserves apparent size as well.
            float rise = AbsoluteHeight - baseline.y;
            originalVisual.position = baseline + direction * (rise / direction.y);
        }

        void OnDisable()
        {
            if (originalVisual) originalVisual.localPosition = originalLocalPosition;
        }
    }
}
