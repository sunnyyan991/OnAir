using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // One material per source and palette colour, shared across all streamed tiles.
    // Avoids per-renderer overrides that remove these facades from SRP batching.
    public sealed class FacadePalette : System.IDisposable
    {
        readonly Dictionary<(Material,Color),Material> materials=new Dictionary<(Material,Color),Material>();
        public int Count=>materials.Count;
        public void Apply(BuildingView building,Color color)
        {
            if(building.preserveFacadeColor)return;
            foreach(var renderer in building.facades)
            {
                if(!renderer)continue;
                var sources=renderer.sharedMaterials;
                for(int i=0;i<sources.Length;i++)
                {
                    var source=sources[i];if(!source||!source.HasProperty("_BaseColor"))continue;
                    var key=(source,color);
                    if(!materials.TryGetValue(key,out var material))
                    {
                        material=new Material(source){name=source.name+" / city palette"};
                        material.SetColor("_BaseColor",color);materials.Add(key,material);
                    }
                    sources[i]=material;
                }
                renderer.sharedMaterials=sources;
            }
        }
        public void Dispose(){foreach(var material in materials.Values)Object.Destroy(material);materials.Clear();}
    }
}
