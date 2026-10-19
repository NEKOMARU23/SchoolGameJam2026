using System;
using System.IO;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.Core.State
{
    /// <summary>
    /// セーブ枠（20枠）のファイルの読み書き
    /// 保存先: persistentDataPath/saves/slot_01.json 〜 slot_20.json
    /// オプションの設定（settings.json）と見たエンディングの記録（ending_record.json）とは別に保存する
    /// </summary>
    public static class SaveStore
    {
        public const int SlotCount = 20;

        private static string folderOverride;
        private static SaveData pendingLoad;

        /// <summary> 保存先のフォルダ（テストでは別の場所に変える） </summary>
        public static string Folder => folderOverride ?? Path.Combine(Application.persistentDataPath, "saves");

        /// <summary>
        /// 今遊んでいるゲームを保存・ロードした枠（1〜20。まだなら 0）
        /// クリア・ゲームオーバーのときに「このセーブを消しますか？」と聞くのに使う
        /// </summary>
        public static int CurrentSlot { get; set; }

        /// <summary> 【テスト用】保存先を変える（null で元に戻す） </summary>
        public static void SetFolderForTest(string folder)
        {
            folderOverride = folder;
            pendingLoad = null;
            CurrentSlot = 0;
        }

        public static bool IsValidSlot(int slot) => slot >= 1 && slot <= SlotCount;

        public static string GetPath(int slot) => Path.Combine(Folder, $"slot_{slot:00}.json");

        public static bool Exists(int slot) => IsValidSlot(slot) && File.Exists(GetPath(slot));

        /// <summary> 保存する（同じ枠のデータは上書きする） </summary>
        /// <returns>保存できた場合 true</returns>
        public static bool Save(int slot, SaveData data)
        {
            if (!IsValidSlot(slot) || data == null) return false;
            try
            {
                Directory.CreateDirectory(Folder);
                data.savedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
                File.WriteAllText(GetPath(slot), JsonUtility.ToJson(data));
                CurrentSlot = slot;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveStore] {slot}番にセーブできませんでした: {e.Message}");
                return false;
            }
        }

        /// <summary> 読み込む（空き枠・読めないファイルなら null） </summary>
        public static SaveData Load(int slot)
        {
            if (!Exists(slot)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(GetPath(slot)));
                return data != null && data.current != null ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveStore] {slot}番のセーブを読み込めませんでした: {e.Message}");
                return null;
            }
        }

        public static void Delete(int slot)
        {
            if (!Exists(slot)) return;
            File.Delete(GetPath(slot));
            if (CurrentSlot == slot) CurrentSlot = 0;
        }

        /// <summary> 【デバッグ用】すべての枠のセーブを消す </summary>
        public static void DeleteAll()
        {
            for (int slot = 1; slot <= SlotCount; slot++) Delete(slot);
        }

        /// <summary>
        /// タイトル画面からロードするとき: シーンを切り替えた後の InGame に渡すセーブを預ける
        /// </summary>
        public static void SetPendingLoad(int slot, SaveData data)
        {
            pendingLoad = data;
            CurrentSlot = slot;
        }

        /// <summary> 預けたセーブを受け取る（受け取ったら空になる） </summary>
        public static bool TakePendingLoad(out SaveData data)
        {
            data = pendingLoad;
            pendingLoad = null;
            return data != null;
        }
    }
}
