using UnityEngine;
using UnityEngine.UI;

namespace TechC.SchoolGameJam2026.Scripts.UI
{
    /// <summary>
    /// 会話枠の「オート」「スキップ」ボタン（表示するだけ。オン・オフの管理は GameUIController が行う）
    /// 取調室とスマホの会話枠にそれぞれ置き、どちらで押しても同じ状態になる
    /// </summary>
    public class AutoSkipButtonsUI : MonoBehaviour
    {
        [SerializeField] private GameUIController gameUIController;
        [SerializeField] private Button autoButton;
        [SerializeField] private Button skipButton;

        [SerializeField] private Color onColor = new Color(0.95f, 0.65f, 0.2f, 0.95f);
        [SerializeField] private Color offColor = new Color(0.15f, 0.15f, 0.2f, 0.85f);

        private void Awake()
        {
            if (autoButton != null) autoButton.onClick.AddListener(() => { if (gameUIController != null) gameUIController.ToggleAuto(); });
            if (skipButton != null) skipButton.onClick.AddListener(() => { if (gameUIController != null) gameUIController.ToggleSkip(); });
        }

        /// <summary> オン・オフの状態をボタンの色に反映する </summary>
        public void SetState(bool autoOn, bool skipOn)
        {
            SetColor(autoButton, autoOn);
            SetColor(skipButton, skipOn);
        }

        private void SetColor(Button button, bool on)
        {
            if (button != null && button.targetGraphic != null) button.targetGraphic.color = on ? onColor : offColor;
        }
    }
}
