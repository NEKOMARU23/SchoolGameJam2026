#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace TechC.SchoolGameJam2026.Scripts.Scenario.Import
{
    /// <summary>
    /// Excel ファイル（.xlsx）からシートの文字を読み出す（外部ライブラリを使わない簡易版）
    /// .xlsx は XML を ZIP でまとめたもの。セルの値だけを文字として取り出す（書式・数式は扱わない）
    /// </summary>
    public static class XlsxReader
    {
        private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        /// <summary>
        /// すべてのシートを読み込む。戻り値: シート名 → 行の一覧（行 = セルの文字の一覧。1行目が見出し）
        /// </summary>
        public static Dictionary<string, List<List<string>>> ReadAllSheets(string path)
        {
            // Excel で開いたままでも読めるよう、共有読み取りで開く
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                List<string> sharedStrings = ReadSharedStrings(zip);
                var result = new Dictionary<string, List<List<string>>>(StringComparer.Ordinal);

                foreach (var (sheetName, entryPath) in ReadSheetEntries(zip))
                {
                    ZipArchiveEntry entry = zip.GetEntry(entryPath);
                    if (entry == null) continue;
                    result[sheetName] = ReadSheet(entry, sharedStrings);
                }
                return result;
            }
        }

        private static XmlDocument LoadXml(ZipArchiveEntry entry)
        {
            var doc = new XmlDocument();
            using (Stream s = entry.Open())
            {
                doc.Load(s);
            }
            return doc;
        }

        private static XmlNamespaceManager Ns(XmlDocument doc)
        {
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("m", MainNs);
            ns.AddNamespace("r", RelNs);
            ns.AddNamespace("pr", PackageRelNs);
            return ns;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            ZipArchiveEntry entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return list;

            XmlDocument doc = LoadXml(entry);
            XmlNamespaceManager ns = Ns(doc);
            foreach (XmlNode si in doc.SelectNodes("/m:sst/m:si", ns))
            {
                list.Add(ReadTextOf(si, ns));
            }
            return list;
        }

        /// <summary> 文字列（装飾付きの文字列は各部分をつなげる。ふりがなは除く） </summary>
        private static string ReadTextOf(XmlNode node, XmlNamespaceManager ns)
        {
            var sb = new StringBuilder();
            foreach (XmlNode t in node.SelectNodes("m:t | m:r/m:t", ns))
            {
                sb.Append(t.InnerText);
            }
            return sb.ToString();
        }

        private static IEnumerable<(string name, string path)> ReadSheetEntries(ZipArchive zip)
        {
            XmlDocument workbook = LoadXml(zip.GetEntry("xl/workbook.xml"));
            XmlDocument rels = LoadXml(zip.GetEntry("xl/_rels/workbook.xml.rels"));
            XmlNamespaceManager wns = Ns(workbook);
            XmlNamespaceManager rns = Ns(rels);

            var targets = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XmlElement rel in rels.SelectNodes("/pr:Relationships/pr:Relationship", rns))
            {
                string target = rel.GetAttribute("Target");
                // "worksheets/sheet1.xml" または "/xl/worksheets/sheet1.xml"
                target = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
                targets[rel.GetAttribute("Id")] = target;
            }

            foreach (XmlElement sheet in workbook.SelectNodes("/m:workbook/m:sheets/m:sheet", wns))
            {
                string id = sheet.GetAttribute("id", RelNs);
                if (targets.TryGetValue(id, out string path))
                {
                    yield return (sheet.GetAttribute("name"), path);
                }
            }
        }

        private static List<List<string>> ReadSheet(ZipArchiveEntry entry, List<string> sharedStrings)
        {
            XmlDocument doc = LoadXml(entry);
            XmlNamespaceManager ns = Ns(doc);
            var rows = new List<List<string>>();

            foreach (XmlElement row in doc.SelectNodes("/m:worksheet/m:sheetData/m:row", ns))
            {
                // 行番号どおりに並べる（空の行が省略されていても位置がずれないように）
                int rowIndex = int.Parse(row.GetAttribute("r")) - 1;
                while (rows.Count < rowIndex) rows.Add(new List<string>());

                var cells = new List<string>();
                foreach (XmlElement c in row.SelectNodes("m:c", ns))
                {
                    int col = ColumnIndex(c.GetAttribute("r"));
                    while (cells.Count < col) cells.Add("");
                    cells.Add(ReadCell(c, ns, sharedStrings));
                }
                rows.Add(cells);
            }
            return rows;
        }

        private static string ReadCell(XmlElement c, XmlNamespaceManager ns, List<string> sharedStrings)
        {
            string type = c.GetAttribute("t");
            if (type == "inlineStr")
            {
                XmlNode isNode = c.SelectSingleNode("m:is", ns);
                return isNode != null ? ReadTextOf(isNode, ns) : "";
            }

            string value = c.SelectSingleNode("m:v", ns)?.InnerText ?? "";
            if (type == "s" && int.TryParse(value, out int index) && index >= 0 && index < sharedStrings.Count)
            {
                return sharedStrings[index];
            }
            if (type == "b") return value == "1" ? "TRUE" : "FALSE";
            return value;
        }

        /// <summary> "C12" → 2（0始まりの列番号） </summary>
        private static int ColumnIndex(string cellRef)
        {
            int col = 0;
            foreach (char ch in cellRef)
            {
                if (ch < 'A' || ch > 'Z') break;
                col = col * 26 + (ch - 'A' + 1);
            }
            return col - 1;
        }
    }
}
#endif
