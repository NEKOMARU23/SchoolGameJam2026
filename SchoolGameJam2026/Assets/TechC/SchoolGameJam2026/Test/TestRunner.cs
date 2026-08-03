using System.Collections.Generic;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core;

public class TestRunner : MonoBehaviour
{
    [Header("作成したSOのリスト（Inspectorでセット）")]
    [SerializeField] private List<AITraitSO> aiTraitPool;

    private readonly string[] sampleNames = { "サトウ", "スズキ", "タナカ", "タカハシ", "ワタナベ" };
    private readonly string[] sampleEyeColors = { "黒", "青", "赤", "緑", "茶" };

    private VisitorInstance currentVisitor;
    private List<VisitorInstance> containedList = new List<VisitorInstance>();

    void Start()
    {
        Debug.Log("=== テスト開始: 関所に訪問者がやってきました ===");
        GenerateRandomVisitor();
    }

    [ContextMenu("1. 関所に新しい人を呼ぶ")]
    public void GenerateRandomVisitor()
    {
        bool isAI = Random.value < 0.6f;

        string dbName = sampleNames[Random.Range(0, sampleNames.Length)];
        string dbEye = sampleEyeColors[Random.Range(0, sampleEyeColors.Length)];
        string dbNumber = Random.Range(10000000, 99999999).ToString();

        string presentedName = dbName;
        string presentedEye = dbEye;
        string presentedNumber = dbNumber;

        if (isAI)
        {
            int errorType = Random.Range(0, 3);
            switch (errorType)
            {
                case 0:
                    do { presentedName = sampleNames[Random.Range(0, sampleNames.Length)]; } while (presentedName == dbName);
                    break;
                case 1:
                    do { presentedEye = sampleEyeColors[Random.Range(0, sampleEyeColors.Length)]; } while (presentedEye == dbEye);
                    break;
                case 2:
                    do { presentedNumber = Random.Range(10000000, 99999999).ToString(); } while (presentedNumber == dbNumber);
                    break;
            }
        }

        currentVisitor = new VisitorInstance(
            isAI, dbName, dbEye, dbNumber, presentedName, presentedEye, presentedNumber
        );

        bool hasMismatch = currentVisitor.HasDataMismatch();
        Debug.Log($"【関所】対象: {currentVisitor.presentedName} | 照合: {(hasMismatch ? "<color=red>[偽物]</color>" : "<color=green>[本物]</color>")} (※正体: {(currentVisitor.isAI ? "AI" : "人間")})");
        
        // ★ DBデータと提示データの詳細ログ
        Debug.Log($"   └ DB情報: [名前:{dbName}, 目:{dbEye}, ID:{dbNumber}] / 提示: [名前:{presentedName}, 目:{presentedEye}, ID:{presentedNumber}]");
    }

    [ContextMenu("2. 収容する (SOガチャ付与)")]
    public void ContainCurrentVisitor()
    {
        if (currentVisitor == null) return;

        if (currentVisitor.isAI)
        {
            if (aiTraitPool != null && aiTraitPool.Count > 0)
            {
                AITraitSO randomTrait = aiTraitPool[Random.Range(0, aiTraitPool.Count)];
                currentVisitor.AssignAITrait(randomTrait);
                Debug.Log($"<color=yellow>【収容完了】{currentVisitor.presentedName} に SO [{randomTrait.traitName}] (タイプ:{randomTrait.aiType}) を付与しました！</color>");
            }
            else
            {
                Debug.LogWarning("aiTraitPoolが空です！InspectorでSOをアタッチしてください。");
            }
        }
        else
        {
            Debug.Log($"<color=red>【収容完了】{currentVisitor.presentedName} は人間ですが誤って収容しました。</color>");
        }

        containedList.Add(currentVisitor);
    }

    // --- 尋問（3種類のアクション） ---

    [ContextMenu("3-A. アクション: 尋問 (Torture) を実行")]
    public void ExecuteTorture() => ExecuteAction(WorkType.Torture);

    [ContextMenu("3-B. アクション: 対話 (Talk) を実行")]
    public void ExecuteTalk() => ExecuteAction(WorkType.Talk);

    [ContextMenu("3-C. アクション: 交渉 (Negotiate) を実行")]
    public void ExecuteNegotiate() => ExecuteAction(WorkType.Negotiate);

    private void ExecuteAction(WorkType actionType)
    {
        if (containedList.Count == 0)
        {
            Debug.LogWarning("収容室に誰もいません。まずは収容してください。");
            return;
        }

        VisitorInstance target = containedList[containedList.Count - 1];
        string response = target.Interrogate(actionType);

        Debug.Log($"【行動: {actionType}】{target.presentedName} (段階:{target.currentStageIndex} | 進捗:{target.currentProgress})\n応答: <color=cyan>\"{response}\"</color>");
    }
}