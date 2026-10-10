using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TechC.SchoolGameJam2026.Scripts.Core.Manager;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// オプション画面のビュー（Manager シーンに1つだけ置き、タイトル・ゲーム中のどちらからも開く）
    /// 設定の保存・反映は SettingsManager が行う
    /// </summary>
    public class OptionUI : MonoBehaviour
    {
        public static OptionUI Instance { get; private set; }

        /// <summary> オプション画面が開いているか（開いている間は下の画面の操作を止めるために使う） </summary>
        public static bool IsOpen => Instance != null && Instance.panelRoot != null && Instance.panelRoot.activeSelf;

        [Header("--- 設定の管理 ---")]
        [SerializeField] private SettingsManager settingsManager;

        [Header("--- 画面 ---")]
        [SerializeField] private GameObject panelRoot;

        [Header("--- 音量 ---")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider seSlider;

        [Header("--- 画面表示（0: フルスクリーン, 1: ウィンドウ） ---")]
        [SerializeField] private Button[] screenModeButtons = new Button[2];

        [Header("--- 文字の速さ（0: 遅い, 1: 普通, 2: 速い） ---")]
        [SerializeField] private Button[] textSpeedButtons = new Button[3];

        [Header("--- オートの待ち時間（0: 短い, 1: 普通, 2: 長い） ---")]
        [SerializeField] private Button[] autoWaitButtons = new Button[3];

        [Header("--- スキップの種類（0: 既読のみ, 1: すべて） ---")]
        [SerializeField] private Button[] skipModeButtons = new Button[2];

        [Header("--- 下部のボタン ---")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button quitButton;

        [Header("--- 選択中の見た目 ---")]
        [SerializeField] private Color selectedColor = new Color(0.95f, 0.75f, 0.3f, 1f);
        [SerializeField] private Color unselectedColor = new Color(0.35f, 0.35f, 0.4f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (settingsManager == null) settingsManager = GetComponent<SettingsManager>();

            BindControls();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Esc でオプションを開閉
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsOpen) Close();
                else Open();
            }
        }

        /// <summary>
        /// オプション画面を開く
        /// </summary>
        public void Open()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(true);
            Refresh();
        }

        /// <summary>
        /// オプション画面を閉じて、設定を保存する
        /// </summary>
        public void Close()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(false);
            if (settingsManager != null) settingsManager.Save();
        }

        private void BindControls()
        {
            if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(v => settingsManager.SetBgmVolume(v));
            if (seSlider != null) seSlider.onValueChanged.AddListener(v => settingsManager.SetSeVolume(v));

            BindChoice(screenModeButtons, i => settingsManager.SetFullScreen(i == 0));
            BindChoice(textSpeedButtons, i => settingsManager.SetTextSpeed((TextSpeed)i));
            BindChoice(autoWaitButtons, i => settingsManager.SetAutoWait((AutoWait)i));
            BindChoice(skipModeButtons, i => settingsManager.SetSkipMode((SkipMode)i));

            if (backButton != null) backButton.onClick.AddListener(Close);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        }

        private void BindChoice(Button[] buttons, System.Action<int> onSelect)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                int index = i;
                buttons[i].onClick.AddListener(() =>
                {
                    onSelect(index);
                    Refresh();
                });
            }
        }

        /// <summary>
        /// 今の設定を画面に反映する
        /// </summary>
        private void Refresh()
        {
            if (settingsManager == null) return;
            GameSettings current = settingsManager.Current;

            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(current.bgmVolume);
            if (seSlider != null) seSlider.SetValueWithoutNotify(current.seVolume);

            ShowSelected(screenModeButtons, current.isFullScreen ? 0 : 1);
            ShowSelected(textSpeedButtons, (int)current.textSpeed);
            ShowSelected(autoWaitButtons, (int)current.autoWait);
            ShowSelected(skipModeButtons, (int)current.skipMode);
        }

        private void ShowSelected(Button[] buttons, int selectedIndex)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                if (buttons[i].targetGraphic != null)
                {
                    buttons[i].targetGraphic.color = i == selectedIndex ? selectedColor : unselectedColor;
                }
            }
        }

        private void QuitGame()
        {
            if (settingsManager != null) settingsManager.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
