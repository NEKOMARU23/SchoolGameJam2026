using System.Collections.Generic;
using UnityEngine;
using TechC.SchoolGameJam2026.Scripts.InGame.MainSystem;
using TechC.SchoolGameJam2026.Scripts.Core;

namespace TechC.SchoolGameJam2026.Scripts.InGame.Core.Manager
{
    /// <summary>
    /// ゲーム中の管理系の処理をまとめるマネージャクラス
    /// </summary>
    public class InGameManager : MonoBehaviour
    {
        [Header("エディタで設定するAI素性SOのプール（リスト）")]
        [SerializeField] private List<AITraitSO> aiTraitPool;

        // 収容所にいる人たちのリスト
        private List<VisitorInstance> containedList = new List<VisitorInstance>();

        // 関所で「収容（Contain）」ボタンを押した時の処理
        public void OnClickContain(VisitorInstance visitor)
        {
            if (visitor.isAI)
            {
                // AIだった場合：あらかじめ作ったSOの中からランダムに1つ付与する
                int randomIndex = Random.Range(0, aiTraitPool.Count);
                AITraitSO randomTrait = aiTraitPool[randomIndex];

                visitor.AssignAITrait(randomTrait);
                Debug.Log($"{visitor.presentedName} に SO [{randomTrait.traitName}] を付与して収容しました。");
            }
            else
            {
                // 人間だった場合（冤罪収容）はSOなしで収容
                // ビルド前にはこのログは消す予定
                Debug.Log($"{visitor.presentedName} は人間ですが誤って収容しました。");
            }

            containedList.Add(visitor);
        }
    }
}