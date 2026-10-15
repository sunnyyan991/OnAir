using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OnAir
{
    // Fade only the separated roof panels; the hangar walls and frame retain
    // their normal materials, depth and shadows throughout the visit.
    public sealed class AirportHangarRoof : MonoBehaviour
    {
        public const float InteriorOpacity = .1f, FadeSeconds = 2.5f;
        public const float AircraftHalfLength = 3.9f, AircraftHalfSpan = 5f;
        public const float HangarHalfDepth = 13f, HangarHalfWidth = 14f, DoorClearance = .6f;
        public float Opacity { get; private set; } = 1;

        sealed class RoofRenderer
        {
            public Renderer renderer;
            public Material[] originalMaterials;
            public ShadowCastingMode originalShadows;
        }

        readonly List<RoofRenderer> roofs = new List<RoofRenderer>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly Dictionary<Material, Material> originalByCopy = new Dictionary<Material, Material>();
        AirportVisitPlan.Site site;

        public void Build(Transform airportTransform, AirportVisitPlan.Site value, AirportPhase phase, Vector3 globalPosition)
        {
            ReleaseMaterials();
            site = value;
            // A restored plane already beneath the panels must be visible on
            // the first frame. A fresh approach begins with an opaque roof.
            Opacity = WantsInterior(site, phase, globalPosition) && UnderRoof(site, globalPosition) ? InteriorOpacity : 1;
            if (!airportTransform) return;

            var replacements = new Dictionary<Material, Material>();
            var collected = new HashSet<Renderer>();
            foreach (var child in airportTransform.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Hangar-front-roof" && child.name != "Hangar-back-roof") continue;
                foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    if (!collected.Add(renderer)) continue;
                    var originals = renderer.sharedMaterials;
                    var materials = new Material[originals.Length];
                    for (int i = 0; i < originals.Length; i++)
                    {
                        var original = originals[i];
                        if (!original) continue;
                        if (!replacements.TryGetValue(original, out var replacement))
                        {
                            replacement = TransparentCopy(original);
                            replacements.Add(original, replacement);
                            ownedMaterials.Add(replacement);
                            originalByCopy.Add(replacement, original);
                        }
                        materials[i] = replacement;
                    }
                    roofs.Add(new RoofRenderer { renderer = renderer, originalMaterials = originals, originalShadows = renderer.shadowCastingMode });
                    renderer.sharedMaterials = materials;
                    // Keep this choice stable even while the roof is opaque:
                    // ordinary shadow maps cannot represent fractional opacity.
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            ApplyOpacity();
        }

        static Material TransparentCopy(Material original)
        {
            // A Resources material retains the shader's transparent variant in
            // player builds. Copy each panel's surface colours into that template.
            var template = Resources.Load<Material>("Airport/HangarRoofFade");
            var material = new Material(template ? template : original) { name = original.name + " / hangar roof fade" };
            material.CopyPropertiesFromMaterial(original);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_AlphaClip", 0);
            material.SetFloat("_AlphaToMask", 0);
            material.SetFloat("_BlendModePreserveSpecular", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("DepthNormals", false);
            material.SetShaderPassEnabled("DepthNormalsOnly", false);
            material.SetShaderPassEnabled("GBuffer", false);
            return material;
        }

        public void Refresh(AirportPhase phase, Vector3 globalPosition, float seconds, bool paused)
        {
            // The controller owns night-light updates on the source materials.
            // Carry those updates into our independent roof copies as well.
            foreach (var pair in originalByCopy)
                if (pair.Key && pair.Value && pair.Key.HasProperty("_EmissionColor") && pair.Value.HasProperty("_EmissionColor"))
                    pair.Key.SetColor("_EmissionColor", pair.Value.GetColor("_EmissionColor"));
            if (paused || seconds <= 0) return;
            float target = WantsInterior(site, phase, globalPosition) ? InteriorOpacity : 1;
            float next = NextOpacity(Opacity, target, seconds);
            if (next == Opacity) return;
            Opacity = next;
            ApplyOpacity();
        }

        // The full 0.9 alpha range takes 2.5 seconds; partial reversals remain
        // continuous and retain that same fade rate. This has no frame state.
        public static float NextOpacity(float current, float target, float seconds)
        {
            current = Mathf.Clamp(current, InteriorOpacity, 1);
            target = Mathf.Clamp(target, InteriorOpacity, 1);
            return Mathf.MoveTowards(current, target, (1 - InteriorOpacity) * Mathf.Max(0, seconds) / FadeSeconds);
        }

        public static bool WantsInterior(AirportVisitPlan.Site value, AirportPhase phase, Vector3 globalPosition)
        {
            if (value == null) return false;
            if (phase == AirportPhase.BackIntoHangar || phase == AirportPhase.Maintenance) return true;
            // TaxiOut begins inside the building. Keep the plane revealed until
            // its tail and a small clearance have passed the front wall.
            return phase == AirportPhase.TaxiOut && UnderRoof(value, globalPosition);
        }

        static bool UnderRoof(AirportVisitPlan.Site value, Vector3 globalPosition)
        {
            if (value == null) return false;
            var source = SmallAirportNetwork.Source(value, globalPosition);
            var centre = SmallAirportNetwork.Bays[value.bay].centre;
            float clearFront = centre.x - HangarHalfDepth - AircraftHalfLength - DoorClearance;
            return source.x > clearFront && source.x <= centre.x + HangarHalfDepth + AircraftHalfLength + DoorClearance
                && Mathf.Abs(source.z - centre.z) <= HangarHalfWidth + AircraftHalfSpan + DoorClearance;
        }

        void ApplyOpacity()
        {
            foreach (var material in ownedMaterials)
            {
                if (!material) continue;
                if (material.HasProperty("_BaseColor"))
                {
                    var color = material.GetColor("_BaseColor");
                    color.a = Opacity;
                    material.SetColor("_BaseColor", color);
                }
                if (material.HasProperty("_Color"))
                {
                    var color = material.GetColor("_Color");
                    color.a = Opacity;
                    material.SetColor("_Color", color);
                }
            }
        }

        void ReleaseMaterials()
        {
            foreach (var roof in roofs)
            {
                if (!roof.renderer) continue;
                roof.renderer.sharedMaterials = roof.originalMaterials;
                roof.renderer.shadowCastingMode = roof.originalShadows;
            }
            roofs.Clear();
            foreach (var material in ownedMaterials)
            {
                if (!material) continue;
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            }
            ownedMaterials.Clear();
            originalByCopy.Clear();
        }

        void OnDestroy() => ReleaseMaterials();
    }
}
