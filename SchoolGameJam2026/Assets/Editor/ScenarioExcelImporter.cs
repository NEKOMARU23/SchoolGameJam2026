using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.Scenario;
using TechC.SchoolGameJam2026.Scripts.Scenario.Import;

namespace TechC.SchoolGameJam2026.Scripts.Editor
{
    /// <summary>
    /// シナリオの Excel（Assets/Editor/ScenarioData.xlsx）を取り込んで ScenarioDatabase を作る
    /// メニュー: Tools ＞ シナリオExcelを取り込む
    /// </summary>
    public static class ScenarioExcelImporter
    {
        public const string ExcelPath = "Assets/Editor/ScenarioData.xlsx";
        public const string DataFolder = "Assets/TechC/SchoolGameJam2026/Data";
        public const string DatabasePath = DataFolder + "/ScenarioDatabase.asset";
        public const int MaxDays = 7;

        public static readonly string[] CharacterIds = { "A", "B", "C", "D" };

        // 素材の種類ごとのフォルダ（Data の中）
        private static readonly Dictionary<AssetKind, string> AssetFolders = new Dictionary<AssetKind, string>
        {
            { AssetKind.Character, "Character" },
            { AssetKind.Background, "Background" },
            { AssetKind.EventCG, "EventCG" },
            { AssetKind.Effect, "Effect" },
            { AssetKind.SE, "SE" },
            { AssetKind.BGM, "BGM" },
            { AssetKind.Item, "Item" },
        };

        [MenuItem("Tools/シナリオExcelを取り込む")]
        public static void ImportFromMenu()
        {
            ImportReport report = Import(out string summary);
            ScenarioImportResultWindow.Show(report, summary);
        }

        /// <summary>
        /// バッチモード用（-executeMethod）。エラーがあれば終了コード 1
        /// </summary>
        public static void ImportFromCommandLine()
        {
            ImportReport report = Import(out string summary);
            Debug.Log("[ScenarioExcelImporter] " + summary);
            foreach (ImportMessage m in report.Errors) Debug.LogError(m.ToString());
            foreach (ImportMessage m in report.Warnings) Debug.LogWarning(m.ToString());
            EditorApplication.Exit(report.HasErrors ? 1 : 0);
        }

        /// <summary>
        /// 取り込む。エラーがあれば保存しない
        /// </summary>
        public static ImportReport Import(out string summary)
        {
            EnsureAssetFolders();

            if (!File.Exists(ExcelPath))
            {
                var missing = new ImportReport();
                missing.Error(ExcelPath, 0, "Excel ファイルが見つかりません");
                summary = "Excel ファイルが見つかりません";
                return missing;
            }

            Dictionary<string, List<List<string>>> sheets;
            try
            {
                sheets = XlsxReader.ReadAllSheets(ExcelPath);
            }
            catch (Exception e)
            {
                var failed = new ImportReport();
                failed.Error(ExcelPath, 0, "Excel ファイルを読み込めませんでした: " + e.Message);
                summary = "Excel ファイルを読み込めませんでした";
                return failed;
            }

            var assets = CollectAssets();
            var temp = ScriptableObject.CreateInstance<ScenarioDatabase>();
            try
            {
                ImportReport report = ScenarioWorkbookParser.Parse(
                    sheets, temp,
                    (kind, name) => assets[kind].ContainsKey(name),
                    CharacterIds, MaxDays);

                if (report.HasErrors)
                {
                    summary = $"エラーが {report.Errors.Count} 件あるため、取り込みを止めました（警告 {report.Warnings.Count} 件）";
                    return report;
                }

                ApplyAssets(temp, assets);
                Save(temp);
                summary = $"取り込みました: 会話 {temp.dialogues.Count} 件 / 条件 {temp.conditions.Count} 行 / 選択肢 {temp.choices.Count} 行 / 日別設定 {temp.dailySettings.Count} 行 / " +
                          $"フラグ {temp.flags.Count} 個 / AI判定 {temp.aiJudgements.Count} 行 / エンディング {temp.endings.Count} 行（警告 {report.Warnings.Count} 件）";
                return report;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private static void Save(ScenarioDatabase source)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScenarioDatabase>(DatabasePath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ScenarioDatabase>();
                AssetDatabase.CreateAsset(asset, DatabasePath);
            }

            EditorUtility.CopySerialized(source, asset);
            asset.name = Path.GetFileNameWithoutExtension(DatabasePath);
            asset.ClearCache();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        #region 素材

        private static void EnsureAssetFolders()
        {
            foreach (string folder in AssetFolders.Values)
            {
                string path = DataFolder + "/" + folder;
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(DataFolder, folder);
            }
        }

        /// <summary> 種類ごとに「ファイル名 → 素材」を集める </summary>
        private static Dictionary<AssetKind, Dictionary<string, UnityEngine.Object>> CollectAssets()
        {
            var result = new Dictionary<AssetKind, Dictionary<string, UnityEngine.Object>>();
            foreach (var pair in AssetFolders)
            {
                string folder = DataFolder + "/" + pair.Value;
                var map = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
                Type type = pair.Key == AssetKind.SE || pair.Key == AssetKind.BGM ? typeof(AudioClip)
                          : pair.Key == AssetKind.Effect ? typeof(GameObject)
                          : typeof(Sprite);

                if (AssetDatabase.IsValidFolder(folder))
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name, new[] { folder }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(path, type);
                        string name = Path.GetFileNameWithoutExtension(path);
                        if (asset != null && !map.ContainsKey(name)) map[name] = asset;
                    }
                }
                result[pair.Key] = map;
            }
            return result;
        }

        private static void ApplyAssets(ScenarioDatabase db, Dictionary<AssetKind, Dictionary<string, UnityEngine.Object>> assets)
        {
            db.characterSprites = ToSprites(assets[AssetKind.Character]);
            db.backgrounds = ToSprites(assets[AssetKind.Background]);
            db.eventCGs = ToSprites(assets[AssetKind.EventCG]);
            db.effects = new List<NamedObject>();
            foreach (var p in assets[AssetKind.Effect]) db.effects.Add(new NamedObject { name = p.Key, prefab = (GameObject)p.Value });
            db.seClips = ToClips(assets[AssetKind.SE]);
            db.bgmClips = ToClips(assets[AssetKind.BGM]);
            db.itemImages = ToSprites(assets[AssetKind.Item]);
        }

        private static List<NamedSprite> ToSprites(Dictionary<string, UnityEngine.Object> map)
        {
            var list = new List<NamedSprite>();
            foreach (var p in map) list.Add(new NamedSprite { name = p.Key, sprite = (Sprite)p.Value });
            return list;
        }

        private static List<NamedAudioClip> ToClips(Dictionary<string, UnityEngine.Object> map)
        {
            var list = new List<NamedAudioClip>();
            foreach (var p in map) list.Add(new NamedAudioClip { name = p.Key, clip = (AudioClip)p.Value });
            return list;
        }

        #endregion
    }

    /// <summary>
    /// 取り込み結果（エラー・警告の一覧）を表示する画面
    /// </summary>
    public class ScenarioImportResultWindow : EditorWindow
    {
        private ImportReport report;
        private string summary;
        private Vector2 scroll;

        public static void Show(ImportReport report, string summary)
        {
            var window = GetWindow<ScenarioImportResultWindow>("シナリオ取り込み結果");
            window.report = report;
            window.summary = summary;
            window.Show();
        }

        private void OnGUI()
        {
            if (report == null) return;

            EditorGUILayout.HelpBox(summary, report.HasErrors ? MessageType.Error : (report.Warnings.Count > 0 ? MessageType.Warning : MessageType.Info));
            if (GUILayout.Button("もう一度取り込む"))
            {
                report = ScenarioExcelImporter.Import(out summary);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (ImportMessage m in report.Errors) EditorGUILayout.HelpBox(m.ToString(), MessageType.Error);
            foreach (ImportMessage m in report.Warnings) EditorGUILayout.HelpBox(m.ToString(), MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }
    }
}
