using System;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.Scenario;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary>
    /// スマホ（マキナ）との会話回数を管理するマネージャー
    /// 1日に聞ける回数はその日に用意されたセリフの数まで。日付が変わる・その日をリセットすると0回に戻る
    /// </summary>
    public class SmartphoneTalkManager : MonoBehaviour
    {
        public static SmartphoneTalkManager Instance { get; private set; }

        private int talkCountToday = 0;

        /// <summary> 今日すでに会話した回数 </summary>
        public int TalkCountToday => talkCountToday;

        /// <summary> 会話回数が変わったときに通知（引数は今日の会話回数） </summary>
        public event Action<int> OnTalkCountChanged;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Excel の条件シート（行動「スマホ」）から次の会話を選び、会話を1回分消費する
        /// その日に聞ける回数は、書かれた「回目」の最大の数
        /// </summary>
        /// <returns>会話を始められた場合 true。回数を使い切った・次の会話の条件を満たさない場合は false（回数は使わない）</returns>
        public bool TryStartTalk(out ScenarioDialogue dialogue)
        {
            dialogue = null;
            GameManager game = GameManager.Instance;
            ScenarioDatabase db = game != null ? game.Scenario : null;
            if (db == null) return false;

            int day = game.CurrentDay;
            int talkNumber = talkCountToday + 1;
            if (talkNumber > db.GetSmartphoneTalkCount(day)) return false;

            ScenarioConditionRow row = game.SelectConditionRow(
                $"スマホ {talkNumber}回目",
                db.GetConditionRows(day, "", ScenarioAction.Smartphone, "", talkNumber),
                game.CreateSelectorContext(null, 0));
            if (row == null) return false;

            dialogue = db.FindDialogue(row.dialogueId);
            game.ApplyScenarioEffects(row, "スマホ");
            SetTalkCount(talkNumber);
            return true;
        }

        /// <summary>
        /// 新しい日の朝に会話回数を0に戻す（GameManager が朝の状態を記録する前に呼ぶ）
        /// </summary>
        public void ResetForNewDay()
        {
            SetTalkCount(0);
        }

        /// <summary> 会話回数をスナップショットに書き出す </summary>
        public void CaptureState(GameStateSnapshot snapshot)
        {
            snapshot.smartphoneTalkCount = talkCountToday;
        }

        /// <summary> スナップショットの会話回数に戻す（今日をやり直す・ロード用） </summary>
        public void RestoreState(GameStateSnapshot snapshot)
        {
            SetTalkCount(snapshot.smartphoneTalkCount);
        }

        private void SetTalkCount(int count)
        {
            talkCountToday = Mathf.Max(0, count);
            OnTalkCountChanged?.Invoke(talkCountToday);
        }
    }
}
