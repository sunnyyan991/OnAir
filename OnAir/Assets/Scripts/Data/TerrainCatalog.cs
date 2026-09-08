using System;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    [CreateAssetMenu(menuName="OnAir/Terrain Catalog")]
    public sealed class TerrainCatalog : ScriptableObject
    {
        public TerrainProfile[] profiles;
        public TerrainDefinition[] Resolve(IReadOnlyList<TerrainRule> rules)
        {
            var byId=new Dictionary<string,TerrainProfile>();
            if(profiles==null)throw new InvalidOperationException("Missing terrain profiles.");
            foreach(var profile in profiles)
            {
                if(!profile||string.IsNullOrEmpty(profile.id)||byId.ContainsKey(profile.id)||!profile.kit||!profile.ground)throw new InvalidOperationException("Invalid/duplicate terrain profile.");
                byId.Add(profile.id,profile);
            }
            var result=new TerrainDefinition[rules.Count];
            for(int i=0;i<rules.Count;i++)
            {
                if(!byId.TryGetValue(rules[i].id,out var profile))throw new InvalidOperationException("Missing terrain profile for id "+rules[i].id);
                result[i]=new TerrainDefinition(rules[i],profile);
            }
            return result;
        }
    }
}
