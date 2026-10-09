using System;
using System.Collections.Generic;
using System.Text;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 会話テキストの1ページ分の解析結果
    /// </summary>
    public readonly struct DialoguePage
    {
        /// <summary> [SYS] 付きのページ（話者名なしの地の文）か </summary>
        public bool IsSystem { get; }

        /// <summary> [IMG:xxx] で指定された立ち絵の指定（指定なしは null） </summary>
        public string ImageKey { get; }

        /// <summary> 演出タグを除いた本文 </summary>
        public string Body { get; }

        /// <summary> 立ち絵を隠す指定（[IMG:OFF] / [IMG:HIDE]）か </summary>
        public bool HidesImage => ImageKey != null
            && (ImageKey.Equals("OFF", StringComparison.OrdinalIgnoreCase) || ImageKey.Equals("HIDE", StringComparison.OrdinalIgnoreCase));

        public DialoguePage(bool isSystem, string imageKey, string body)
        {
            IsSystem = isSystem;
            ImageKey = imageKey;
            Body = body;
        }
    }

    /// <summary>
    /// 会話テキストの書式の解析をまとめたクラス
    /// 書式: ページ区切りは '|'、ページ先頭の [SYS] で話者名なし、[IMG:OFF] / [IMG:ON] で立ち絵の表示切替
    /// </summary>
    public static class DialogueTextFormat
    {
        public const char PageSeparator = '|';
        public const string SystemTag = "[SYS]";
        private const string ImageTagPrefix = "[IMG:";

        /// <summary>
        /// 会話テキストをページごとに分解し、ページ先頭の演出タグを解析する
        /// </summary>
        public static List<DialoguePage> Parse(string rawText)
        {
            var pages = new List<DialoguePage>();
            if (string.IsNullOrEmpty(rawText)) return pages;

            foreach (string rawPage in rawText.Split(PageSeparator))
            {
                string page = rawPage.Trim();
                if (string.IsNullOrEmpty(page)) continue;

                pages.Add(ParsePage(page));
            }

            return pages;
        }

        /// <summary>
        /// 演出タグを除いた本文だけを改行区切りで返す（ログやリザルト表示用）
        /// </summary>
        public static string ToPlainText(string rawText)
        {
            var sb = new StringBuilder();
            foreach (DialoguePage page in Parse(rawText))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(page.Body);
            }
            return sb.ToString();
        }

        private static DialoguePage ParsePage(string page)
        {
            bool isSystem = false;
            string imageKey = null;

            // [SYS] と [IMG:xxx] は順不同・複数指定可
            bool parsed = true;
            while (parsed)
            {
                parsed = false;

                if (page.StartsWith(SystemTag, StringComparison.Ordinal))
                {
                    isSystem = true;
                    page = page.Substring(SystemTag.Length);
                    parsed = true;
                }
                else if (page.StartsWith(ImageTagPrefix, StringComparison.Ordinal))
                {
                    int closeIndex = page.IndexOf(']');
                    if (closeIndex < 0) break;

                    imageKey = page.Substring(ImageTagPrefix.Length, closeIndex - ImageTagPrefix.Length);
                    page = page.Substring(closeIndex + 1);
                    parsed = true;
                }
            }

            return new DialoguePage(isSystem, imageKey, page.Trim());
        }
    }
}
