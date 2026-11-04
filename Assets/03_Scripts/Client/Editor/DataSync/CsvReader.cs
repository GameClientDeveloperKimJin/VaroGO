using System.Collections.Generic;
using System.Text;

namespace VaroGO.Client.EditorTools.DataSync
{
    // RFC 4180 CSV 파서: 따옴표 안의 쉼표·줄바꿈·"" 이스케이프 처리
    static class CsvReader
    {
        public static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            int i = 0;
            if (text.Length > 0 && text[0] == '﻿') i = 1; // BOM

            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else quoted = false;
                    }
                    else cell.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"': quoted = true; break;
                    case ',': row.Add(cell.ToString()); cell.Clear(); break;
                    case '\r': break;
                    case '\n':
                        row.Add(cell.ToString()); cell.Clear();
                        rows.Add(row); row = new List<string>();
                        break;
                    default: cell.Append(c); break;
                }
            }

            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
