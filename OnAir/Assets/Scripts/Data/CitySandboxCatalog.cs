using System;
using UnityEngine;
namespace OnAir
{
    // Presentation metadata only; numeric rule IDs remain in buildings.csv.
    public sealed class CitySandboxCatalog:ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string id,code,name;
            public Texture2D thumbnail;
        }
        public Entry[] entries=Array.Empty<Entry>();
        public Texture2D airportThumbnail;
    }
}
