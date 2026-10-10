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
            if (row.hasSelfAffection && context.SelfAffection < row.selfAffection) return false;
            if (row.hasActionCount && context.ActionCount < row.actionCount) return false;

            foreach (AffectionCondition other in row.otherAffections)
            {
                if (context.AffectionOf(other.characterId) < other.minValue) return false;
            }

            if (context.Flags != null)
            {
                if (!context.Flags.AreAllOn(row.requiredFlags)) return false;
                if (!context.Flags.AreAllOff(row.ngFlags)) return false;
            }
            else if (row.requiredFlags.Count > 0)
            {
                return false;
            }

            return true;
        }
    }
}
