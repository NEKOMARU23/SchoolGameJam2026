using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;
using TechC.SchoolGameJam2026.Scripts.Scenario;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// エンディングの会話を画面全体で見せる（本文シートの文章・背景・イベント絵・SE・BGM）
    /// 最終日の後と、タイトル画面のエンディング一覧からの再生で使う
    /// 画面のどこを押しても次のページに進む（文字を表示している途中なら、全文を出す）
    /// </summary>
    public class EndingPlayerUI : MonoBehaviour
    {
        private const string HiddenImageKey = "非表示";

        [SerializeField] private Button advanceButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image eventImage;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private TextMeshProUGUI bodyText;

        private readonly List<ScenarioPage> pages = new List<ScenarioPage>();
        private ScenarioDatabase database;
        private Action onFinished;
        private int pageIndex;
        private Coroutine typing;
        private float characterInterval = 0.04f;

        public bool IsPlaying { get; private set; }

        /// <summary> 表示中のページの番号（0 始まり。テスト・確認用） </summary>
        public int PageIndex => pageIndex;

        private void Awake()
        {
            if (advanceButton != null) advanceButton.onClick.AddListener(Advance);
        }

        /// <summary>
        /// エンディングの会話を最初から見せる
        /// </summary>
        /// <param name="dialogue">本文シートのエンディングの会話</param>
        /// <param name="db">背景・イベント絵・SE・BGM の素材を探すシナリオのデータ</param>
        /// <param name="finished">最後のページを閉じたときに呼ばれる</param>
        public void Play(ScenarioDialogue dialogue, ScenarioDatabase db, Action finished)
        {
            pages.Clear();
            if (dialogue != null) pages.AddRange(dialogue.pages);
            database = db;
            onFinished = finished;
            pageIndex = 0;

            if (SettingsManager.Instance != null) characterInterval = SettingsManager.Instance.Current.TextIntervalSeconds;

            SetImage(backgroundImage, null);
            SetImage(eventImage, null);
            gameObject.SetActive(true);
            IsPlaying = true;

            if (pages.Count == 0) Finish();
            else ShowPage();
        }

        /// <summary> 次のページへ（文字の表示中なら全文を出す） </summary>
        public void Advance()
        {
            if (!IsPlaying) return;

            if (typing != null)
            {
                StopCoroutine(typing);
                typing = null;
                bodyText.maxVisibleCharacters = int.MaxValue;
                return;
            }

            pageIndex++;
            if (pageIndex >= pages.Count) Finish();
            else ShowPage();
        }

        private void ShowPage()
        {
            ScenarioPage page = pages[pageIndex];

            // 空欄は「変更なし」、非表示 で消す
            if (!string.IsNullOrEmpty(page.background)) SetImage(backgroundImage, FindSprite(page.background, d => d.FindBackground(page.background)));
            if (!string.IsNullOrEmpty(page.eventCG)) SetImage(eventImage, FindSprite(page.eventCG, d => d.FindEventCG(page.eventCG)));

            if (database != null && AudioManager.Instance != null)
            {
                if (!string.IsNullOrEmpty(page.bgm)) AudioManager.Instance.PlayBGMClip(database.FindBGM(page.bgm));
                if (!string.IsNullOrEmpty(page.se)) AudioManager.Instance.PlaySE(database.FindSE(page.se));
            }

            bool isNarration = page.kind == PageKind.Narration;
            if (speakerText != null)
            {
                speakerText.text = isNarration ? "" : page.speaker;
                speakerText.gameObject.SetActive(!isNarration && !string.IsNullOrEmpty(page.speaker));
            }

            bodyText.text = page.text;
            bodyText.fontStyle = isNarration ? FontStyles.Italic : FontStyles.Normal;
            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(TypeText());
        }

        private IEnumerator TypeText()
        {
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;

            for (int i = 1; i <= total; i++)
            {
                bodyText.maxVisibleCharacters = i;
                if (characterInterval > 0f) yield return new WaitForSecondsRealtime(characterInterval);
            }
            bodyText.maxVisibleCharacters = int.MaxValue;
            typing = null;
        }

        private void Finish()
        {
            if (typing != null) StopCoroutine(typing);
            typing = null;
            IsPlaying = false;
            gameObject.SetActive(false);

            Action callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }

        private Sprite FindSprite(string name, Func<ScenarioDatabase, Sprite> find)
        {
            if (name == HiddenImageKey || database == null) return null;
            return find(database);
        }

        private static void SetImage(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.gameObject.SetActive(sprite != null);
        }
    }
}
