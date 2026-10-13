using System;
using System.Collections.Generic;
using TechC.SchoolGameJam2026.Scripts.Core.State;

namespace TechC.SchoolGameJam2026.Scripts.Scenario
{
    /// <summary>
    /// 条件シートの候補の行から、今出す会話を選ぶ
    /// 優先度の小さい行から順に調べ、書いてある条件をすべて満たした最初の行を返す
    /// </summary>
    public static class ScenarioSelector
    {
        /// <summary>
        /// 条件の判定に使う、今のゲームの状態
        /// </summary>
        public class Context
        {
            /// <summary> 会話の相手の好感度（スマホのときは使わない） </summary>
            public int SelfAffection;

            /// <summary> キャラID → 好感度 </summary>
            public Func<string, int> AffectionOf = _ => 0;

            /// <summary> この行動をこのキャラにした通算回数（今回の分は含まない） </summary>
            public int ActionCount;

            public FlagStore Flags;
        }

        /// <param name="rows">優先度順に並んだ候補の行（ScenarioDatabase.GetConditionRows の結果）</param>
        /// <returns>条件を満たした最初の行。どれも満たさなければ null</returns>
        public static ScenarioConditionRow Select(IReadOnlyList<ScenarioConditionRow> rows, Context context)
        {
            if (rows == null) return null;
            foreach (ScenarioConditionRow row in rows)
            {
                if (IsSatisfied(row, context)) return row;
            }
            return null;
        }

        public static bool IsSatisfied(ScenarioConditionRow row, Context context)
        {
            if (row.hasActionCount && context.ActionCount < row.actionCount) return false;
            return AreCommonConditionsMet(row.hasSelfAffection, row.selfAffection, row.otherAffections, row.requiredFlags, row.ngFlags, context);
        }

        /// <summary>
        /// 選択肢を出してよいか（条件をすべて満たしているか）
        /// </summary>
        public static bool IsSatisfied(ScenarioChoice choice, Context context)
        {
            return AreCommonConditionsMet(choice.hasSelfAffection, choice.selfAffection, choice.otherAffections, choice.requiredFlags, choice.ngFlags, context);
        }

        /// <summary>
        /// 条件シートと選択肢シートで共通の条件（好感度・フラグ）
        /// </summary>
        private static bool AreCommonConditionsMet(bool hasSelfAffection, int selfAffection, List<AffectionCondition> otherAffections,
            List<string> requiredFlags, List<string> ngFlags, Context context)
        {
            if (hasSelfAffection && context.SelfAffection < selfAffection) return false;

            foreach (AffectionCondition other in otherAffections)
            {
                if (context.AffectionOf(other.characterId) < other.minValue) return false;
            }

            if (context.Flags != null)
            {
                if (!context.Flags.AreAllOn(requiredFlags)) return false;
                if (!context.Flags.AreAllOff(ngFlags)) return false;
            }
            else if (requiredFlags.Count > 0)
            {
                return false;
            }

            return true;
        }
    }
}
