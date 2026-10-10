using System;
using System.IO;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary>
    /// オプション設定の読み込み・保存・反映を管理するマネージャー（Manager シーンに置き、全シーンで共有）
    /// 設定はセーブ枠とは別のファイル（settings.json）に保存する
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        private const string FileName = "settings.json";

        // ウィンドウ表示にしたときの大きさ（16:9）
        private const int WindowedWidth = 1280;
        private const int WindowedHeight = 720;

        public static SettingsManager Instance { get; private set; }

        private GameSettings settings = new GameSettings();

        /// <summary> 現在の設定（読み取り専用のコピー） </summary>
        public GameSettings Current => settings.Clone();

        /// <summary> 設定が変わったときに通知 </summary>
        public event Action<GameSettings> OnSettingsChanged;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            Load();
        }

        private void Start()
        {
            // AudioManager の Awake 完了後に反映する
            ApplyAll();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #region 設定の変更

        public void SetBgmVolume(float volume)
        {
            settings.bgmVolume = Mathf.Clamp01(volume);
            if (AudioManager.Instance != null) AudioManager.Instance.SetBGMVolume(settings.bgmVolume);
            NotifyChanged();
        }

        public void SetSeVolume(float volume)
        {
            settings.seVolume = Mathf.Clamp01(volume);
            if (AudioManager.Instance != null) AudioManager.Instance.SetSEVolume(settings.seVolume);
            NotifyChanged();
        }

        public void SetFullScreen(bool isFullScreen)
        {
            settings.isFullScreen = isFullScreen;
            ApplyScreenMode();
            NotifyChanged();
        }

        public void SetTextSpeed(TextSpeed speed)
        {
            settings.textSpeed = speed;
            NotifyChanged();
        }

        public void SetAutoWait(AutoWait wait)
        {
            settings.autoWait = wait;
            NotifyChanged();
        }

        public void SetSkipMode(SkipMode mode)
        {
            settings.skipMode = mode;
            NotifyChanged();
        }

        #endregion

        #region 保存・読み込み

        /// <summary>
        /// 設定をファイルに保存する（オプション画面を閉じたとき・ゲーム終了時）
        /// </summary>
        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(settings, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SettingsManager] 設定を保存できませんでした: {e.Message}");
            }
        }

        private void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    settings = JsonUtility.FromJson<GameSettings>(File.ReadAllText(FilePath)) ?? new GameSettings();
                }
            }
            catch (Exception e)
            {
                // 壊れたファイルは初期設定で上書きされる
                Debug.LogWarning($"[SettingsManager] 設定を読み込めなかったため初期設定を使います: {e.Message}");
                settings = new GameSettings();
            }
        }

        #endregion

        private void ApplyAll()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBGMVolume(settings.bgmVolume);
                AudioManager.Instance.SetSEVolume(settings.seVolume);
            }
            ApplyScreenMode();
            NotifyChanged();
        }

        private void ApplyScreenMode()
        {
#if !UNITY_EDITOR
            if (settings.isFullScreen)
            {
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(WindowedWidth, WindowedHeight, FullScreenMode.Windowed);
            }
#endif
        }

        private void NotifyChanged()
        {
            OnSettingsChanged?.Invoke(settings.Clone());
        }
    }
}
