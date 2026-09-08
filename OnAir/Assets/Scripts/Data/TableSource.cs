using System;
using System.IO;
using UnityEngine;
namespace OnAir
{
    public static class TableSource
    {
        public static string Read(TextAsset table)
        {
            if(!table)throw new InvalidOperationException("Missing table reference.");
#if UNITY_EDITOR
            string path=UnityEditor.AssetDatabase.GetAssetPath(table);
            if(File.Exists(path))return File.ReadAllText(path);
#endif
            return table.text;
        }
    }
}
