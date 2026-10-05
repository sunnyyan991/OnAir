using System;
namespace OnAir
{
    [Serializable] public sealed class DisplaySettings
    {
        public int schemaVersion = 1;
        public int windowMode, frameLimit;
        public float uiScale = 1;
    }
}
