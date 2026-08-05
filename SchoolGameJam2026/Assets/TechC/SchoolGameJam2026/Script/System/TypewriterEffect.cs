using System.Collections;
using UnityEngine;
using TMPro;

public class TypewriterEffect : MonoBehaviour
{
    private TextMeshProUGUI textMeshPro;
    [SerializeField] private float speed = 0.05f;

    private void Start()
    {
        textMeshPro = GetComponent<TextMeshProUGUI>();
        if (textMeshPro == null) textMeshPro = GetComponentInChildren<TextMeshProUGUI>();

        if (textMeshPro == null)
        {
            Debug.LogError($"【設定ミス】{gameObject.name} に TextMeshProUGUI が見つかりません。");
            return;
        }

        string sampleText = "左から一文字ずつ、じゅんばんに文字が表示されます。";
        StartCoroutine(TypeText(sampleText));
    }

    private IEnumerator TypeText(string textToDisplay)
    {
        yield return null;

        if (textMeshPro == null) yield break;

        // 文字をセットして、一度すべて非表示にする
        textMeshPro.text = textToDisplay;
        textMeshPro.maxVisibleCharacters = 0;

        // テキストのメッシュ（形状）情報が更新されるのをもう1フレーム待つ
        yield return null;

        // 全体の文字数を安全に取得
        int totalCharacters = textMeshPro.textInfo.characterCount;

        // 1文字ずつ表示していく
        for (int i = 0; i <= totalCharacters; i++)
        {
            if (textMeshPro == null) yield break; // 再度安全確認

            textMeshPro.maxVisibleCharacters = i;
            yield return new WaitForSeconds(speed);
        }
    }
}
