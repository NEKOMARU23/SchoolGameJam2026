using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TechC.SchoolGameJam2026.Scripts.Core.State;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// セーブ・ロードの対象（ゲーム中の画面）。InGame の GameUIController が自分を登録する
    /// </summary>
    public interface ISaveTarget
    {
        /// <summary> 今セーブしてよいか（会話の途中などはだめ） </summary>
        bool CanSave(out string reason);

        SaveData CreateSaveData();

        /// <summary> ゲーム中にセーブを読み込む </summary>
        void ApplySave(SaveData save);
    }

    /// <summary>
    /// セーブ・ロードの画面の流れを決める（Manager シーンのオプションと同じ場所に置き、タイトル・ゲーム中で共有）
    /// ・セーブはゲーム中だけ。使用中の枠に上書きするときは確認する
    /// ・ロードは、ゲーム中なら確認してその場で戻し、タイトルなら InGame に移ってから戻す
    /// ・削除の前には確認する
    /// </summary>
    public class SaveLoadController : MonoBehaviour
    {
        public static SaveLoadController Instance { get; private set; }

        /// <summary> ゲーム中の画面（タイトルなどでは null） </summary>
        public static ISaveTarget Target { get; set; }

        [SerializeField] private SaveSlotUI slotUI;
        [SerializeField] private string inGameSceneName = "InGame";

        private bool isSaveMode;

        public bool IsOpen => slotUI != null && slotUI.IsOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary> セーブの枠を開く（ゲーム中だけ） </summary>
        public void OpenSave()
        {
            if (Target == null || slotUI == null) return;
            isSaveMode = true;
            slotUI.Show("セーブ", BuildViews(), OnSelect, OnDelete);
        }

        /// <summary> ロードの枠を開く（タイトルの「つづきから」・ゲーム中のオプション） </summary>
        public void OpenLoad()
        {
            if (slotUI == null) return;
            isSaveMode = false;
            slotUI.Show("ロード", BuildViews(), OnSelect, OnDelete);
        }

        public void Close()
        {
            if (slotUI != null) slotUI.Hide();
        }

        private void OnSelect(int slot)
        {
            if (isSaveMode) TrySave(slot);
            else TryLoad(slot);
        }

        private void TrySave(int slot)
        {
            if (Target == null) return;
            if (!Target.CanSave(out string reason))
            {
                slotUI.ShowNotice(reason);
                return;
            }

            if (SaveStore.Exists(slot)) slotUI.Confirm($"{slot}番に上書きしますか？", () => DoSave(slot));
            else DoSave(slot);
        }

        private void DoSave(int slot)
        {
            bool saved = SaveStore.Save(slot, Target.CreateSaveData());
            slotUI.SetSlots(BuildViews());
            slotUI.ShowNotice(saved ? $"{slot}番にセーブしました" : $"{slot}番にセーブできませんでした");
        }

        private void TryLoad(int slot)
        {
            SaveData save = SaveStore.Load(slot);
            if (save == null)
            {
                slotUI.ShowNotice($"{slot}番のセーブを読み込めませんでした");
                return;
            }

            if (Target != null)
            {
                // ゲーム中: 今の進み具合が失われるので確認してから戻す
                slotUI.Confirm($"{slot}番をロードしますか？\n今の進み具合は失われます", () =>
                {
                    SaveStore.CurrentSlot = slot;
                    Target.ApplySave(save);
                    Close();
                    if (OptionUI.Instance != null) OptionUI.Instance.Close();
                });
                return;
            }

            // タイトル: InGame に移ってから GameManager が受け取る
            SaveStore.SetPendingLoad(slot, save);
            Close();
            SceneManager.LoadScene(inGameSceneName);
        }

        private void OnDelete(int slot)
        {
            slotUI.Confirm($"{slot}番のセーブを消しますか？", () =>
            {
                SaveStore.Delete(slot);
                slotUI.SetSlots(BuildViews());
                slotUI.ShowNotice($"{slot}番のセーブを消しました");
            });
        }

        private List<SaveSlotView> BuildViews()
        {
            var views = new List<SaveSlotView>();
            for (int slot = 1; slot <= SaveStore.SlotCount; slot++)
            {
                SaveData save = SaveStore.Load(slot);
                views.Add(new SaveSlotView
                {
                    Slot = slot,
                    Label = save != null
                        ? $"{slot:00}　{save.day}日目　残り行動 {save.remainingActions}回　{save.savedAt}"
                        : $"{slot:00}　（空き）",
                    CanSelect = isSaveMode || save != null,
                    CanDelete = save != null,
                });
            }
            return views;
        }
    }
}
