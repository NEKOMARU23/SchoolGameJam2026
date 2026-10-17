using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary> 最終日のモニターに出す、判定の状態 </summary>
    public enum MonitorJudgementLabel
    {
        None,       // 出さない（最終日以外）
        Unjudged,   // 未判定
        ConfirmedAI,// AI と判定した
        Released    // 解放した
    }

    [Serializable]
    public struct MonitorRoomBinding
    {
        public string characterId;
        public Button roomButton;
        public GameObject releasedOverlay;
        // モニターに映すキャラの顔（立ち絵の上のほうだけを見せる。見せる位置と大きさはプレハブで調整する）
        public Image faceImage;
        // 最終日の「未判定」の札（モニターの左上）
        public GameObject unjudgedLabel;
        // 最終日の判定結果の札（モニターの真ん中。「AI判定」「解放」）
        public GameObject judgedLabel;
        public TextMeshProUGUI judgedText;
    }

    public class MonitorUI : MonoBehaviour
    {
        [Header("--- 司令塔コントローラー ---")]
        [SerializeField] private GameUIController gameUIController;

        [Header("--- モニター画面の部屋ボタン登録 ---")]
        [SerializeField] private List<MonitorRoomBinding> monitorRoomBindings = new List<MonitorRoomBinding>();

        [Header("--- スマホ（補助ちゃんとの会話）ボタン ---")]
        [SerializeField] private Button smartphoneButton;

        [Header("--- スマホのお知らせ表示（今日はもう連絡できない等） ---")]
        [SerializeField] private GameObject smartphoneNoticeRoot;
        [SerializeField] private TextMeshProUGUI smartphoneNoticeText;
        [SerializeField, Min(0f)] private float noticeDuration = 2f;

        [Header("--- 最終日の判定結果の文字と色 ---")]
        [SerializeField] private string confirmedAIText = "AI判定";
        [SerializeField] private Color confirmedAIColor = new Color(1f, 0.35f, 0.35f);
        [SerializeField] private string releasedText = "解放";
        [SerializeField] private Color releasedColor = Color.white;

        private CancellationTokenSource noticeCts;

        private void Awake()
        {
            // 各部屋ボタンの OnClick イベントを自動バインド
            foreach (var binding in monitorRoomBindings)
            {
                if (binding.roomButton != null)
                {
                    string charId = binding.characterId;
                    binding.roomButton.onClick.AddListener(() => OnClickRoomButton(charId));
                }
            }

            if (smartphoneButton != null)
            {
                smartphoneButton.onClick.AddListener(OnClickSmartphoneButton);
            }

            if (smartphoneNoticeRoot != null)
            {
                smartphoneNoticeRoot.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // 画面を離れたらお知らせは消す
            HideSmartphoneNotice();
        }

        /// <summary>
        /// スマホの近くにお知らせを一定時間表示する（連続で呼ばれた場合は表示時間を延長）
        /// 「今日はもう連絡ができない」「まだ判定していない人がいます」などに使う
        /// </summary>
        public void ShowNotice(string message)
        {
            if (smartphoneNoticeRoot == null) return;

            if (smartphoneNoticeText != null) smartphoneNoticeText.text = message;
            smartphoneNoticeRoot.SetActive(true);

            noticeCts?.Cancel();
            noticeCts?.Dispose();
            noticeCts = new CancellationTokenSource();
            HideNoticeAfterDelayAsync(noticeCts.Token).Forget();
        }

        private async UniTaskVoid HideNoticeAfterDelayAsync(CancellationToken token)
        {
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(noticeDuration), ignoreTimeScale: true, cancellationToken: token)
                .SuppressCancellationThrow();
            if (!canceled && smartphoneNoticeRoot != null)
            {
                smartphoneNoticeRoot.SetActive(false);
            }
        }

        private void HideSmartphoneNotice()
        {
            noticeCts?.Cancel();
            noticeCts?.Dispose();
            noticeCts = null;
            if (smartphoneNoticeRoot != null) smartphoneNoticeRoot.SetActive(false);
        }

        private void OnClickSmartphoneButton()
        {
            if (gameUIController != null)
            {
                gameUIController.OpenSmartphoneTalk();
            }
        }

        /// <summary>
        /// IDごとの「部屋に入れないか」（解放済み・判定済み）を受け取り、ボタンの活性/非活性およびオーバーレイを表示（描画専用）
        /// </summary>
        public void UpdateRoomStatus(Dictionary<string, bool> closedRoomMap)
        {
            foreach (var binding in monitorRoomBindings)
            {
                // 辞書から対象キャラの状態を取得（見つからなければ入れる）
                bool isReleased = closedRoomMap != null
                    && closedRoomMap.TryGetValue(binding.characterId, out bool closed)
                    && closed;

                if (binding.roomButton != null)
                {
                    binding.roomButton.interactable = !isReleased;
                }

                if (binding.releasedOverlay != null)
                {
                    binding.releasedOverlay.SetActive(isReleased);
                }
            }
        }

        /// <summary>
        /// IDごとの判定の状態を受け取り、最終日の「未判定」と判定結果の札を出す（描画専用）
        /// </summary>
        public void UpdateJudgementLabels(Dictionary<string, MonitorJudgementLabel> labelMap)
        {
            foreach (var binding in monitorRoomBindings)
            {
                MonitorJudgementLabel label = MonitorJudgementLabel.None;
                if (labelMap != null) labelMap.TryGetValue(binding.characterId, out label);

                if (binding.unjudgedLabel != null) binding.unjudgedLabel.SetActive(label == MonitorJudgementLabel.Unjudged);

                bool judged = label == MonitorJudgementLabel.ConfirmedAI || label == MonitorJudgementLabel.Released;
                if (binding.judgedLabel != null) binding.judgedLabel.SetActive(judged);
                if (judged && binding.judgedText != null)
                {
                    bool isAI = label == MonitorJudgementLabel.ConfirmedAI;
                    binding.judgedText.text = isAI ? confirmedAIText : releasedText;
                    binding.judgedText.color = isAI ? confirmedAIColor : releasedColor;
                }
            }
        }

        /// <summary>
        /// IDごとの顔の画像を受け取り、各モニターに映す（描画専用）
        /// </summary>
        public void UpdateRoomFaces(Dictionary<string, Sprite> faceMap)
        {
            foreach (var binding in monitorRoomBindings)
            {
                if (binding.faceImage == null) continue;

                Sprite face = null;
                bool hasFace = faceMap != null && faceMap.TryGetValue(binding.characterId, out face) && face != null;
                binding.faceImage.enabled = hasFace;
                if (hasFace) binding.faceImage.sprite = face;
            }
        }

        /// <summary>
        /// 部屋ボタン押下時のイベント通知
        /// </summary>
        private void OnClickRoomButton(string characterId)
        {
            if (gameUIController != null)
            {
                gameUIController.SelectCharacter(characterId);
            }
        }
    }
}