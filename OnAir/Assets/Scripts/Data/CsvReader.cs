using System;
using System.Collections.Generic;
using System.Text;
namespace OnAir
{
    public static class CsvReader
    {
        public static List<string[]> Read(string text)
        {
            var rows = new List<string[]>(); var row = new List<string>(); var field = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (!quoted && (c == ',' || c == '\n' || c == '\r'))
                {
                    row.Add(field.ToString().Trim()); field.Clear();
                    if (c != ',') { rows.Add(row.ToArray()); row.Clear(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; }
                }
                else field.Append(c);
            }
            if (quoted) throw new FormatException("Unclosed CSV quote.");
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString().Trim()); rows.Add(row.ToArray()); }
            return rows;
        }
    }
}
