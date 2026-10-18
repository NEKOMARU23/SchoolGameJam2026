#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TechC.SchoolGameJam2026.Scripts.Core.State;
using TechC.SchoolGameJam2026.Scripts.Scenario;

namespace TechC.SchoolGameJam2026.Scripts.Test
{
    /// <summary>
    /// 全ルートの確認（仕様書16番）で使う、シナリオのデータの調べ方
    /// ゲームを動かさずに、Excel から取り込んだデータだけで調べる
    /// ・場面（何日目・だれに・どの行動）ごとに、条件に出てくる値の組み合わせをすべて試し、会話が選ばれない状態がないか
    /// ・すべてのエンディングに、手に入るフラグと判定結果の組み合わせでたどり着けるか
    /// </summary>
    public class ScenarioRouteChecker
    {
        /// <summary> 好感度がとても低い状態（条件に出てくるどの値より低い） </summary>
        private const int VeryLowAffection = -1000;

        /// <summary> 1つの場面で試すフラグの数の上限（2のこの数乗の組み合わせを試す） </summary>
        private const int MaxFlagsPerScene = 12;

        /// <summary> 取り調べの行動（1〜6日目にできるもの） </summary>
        private static readonly ScenarioAction[] InterrogationActions = { ScenarioAction.Torture, ScenarioAction.Talk, ScenarioAction.Negotiate };

        /// <summary> 判定の行動（最終日にできるもの） </summary>
        private static readonly ScenarioAction[] JudgementActions = { ScenarioAction.Release, ScenarioAction.ConfirmAI };

        /// <summary> 最終日の判定結果（指定なし以外） </summary>
        private static readonly EndingResult[] Results = { EndingResult.AllCorrect, EndingResult.ConfirmedHuman, EndingResult.ReleasedAI };

        private readonly ScenarioDatabase db;
        private readonly int maxDays;
        private readonly IReadOnlyList<string> characterIds;

        /// <summary> ゲームの中で手に入る（ON にできる）フラグ </summary>
        public HashSet<string> ObtainableFlags { get; }

        /// <summary> だれかが AI になる道があるか（会話・選択肢の「AI化」か、AI判定シート） </summary>
        public bool HasAIRoute { get; }

        public ScenarioRouteChecker(ScenarioDatabase db, int maxDays, IReadOnlyList<string> characterIds)
        {
            this.db = db;
            this.maxDays = maxDays;
            this.characterIds = characterIds;

            ObtainableFlags = new HashSet<string>();
            foreach (ScenarioConditionRow row in db.conditions) ObtainableFlags.UnionWith(row.gainFlags);
            foreach (ScenarioChoice choice in db.choices) ObtainableFlags.UnionWith(choice.gainFlags);
            foreach (ScenarioFlagDefinition flag in db.flags)
            {
                if (flag.IsPointUnlock) ObtainableFlags.Add(flag.name);
            }

            HasAIRoute = db.aiJudgements.Count > 0
                || db.conditions.Exists(r => r.aiTargets.Count > 0)
                || db.choices.Exists(c => c.aiTargets.Count > 0);
        }

        #region 会話が選ばれない場面

        /// <summary> 1つの場面（何日目・だれに・どの行動） </summary>
        public class Scene
        {
            public int Day;
            public string CharacterId = "";     // スマホは空
            public ScenarioAction Action;
            public string HearingTargetId = ""; // 聞き取りのときだけ
            public int TalkNumber;              // スマホのときだけ

            public override string ToString()
            {
                switch (Action)
                {
                    case ScenarioAction.Smartphone: return $"{Day}日目 スマホ {TalkNumber}回目";
                    case ScenarioAction.HearAbout: return $"{Day}日目 {CharacterId} に聞き取り（{HearingTargetId} について）";
                    default: return $"{Day}日目 {CharacterId} に {ActionName(Action)}";
                }
            }
        }

        /// <summary>
        /// プレイヤーが出会う場面をすべて並べる
        /// 1〜(最終日-1)日目: 全員に拷問・対話・交渉、ほかの3人についての聞き取り。最終日: 全員に解放・AI確定。毎日: スマホの回数分
        /// （朝の会話は、ない日があってよいので調べない）
        /// </summary>
        public List<Scene> AllScenes()
        {
            var scenes = new List<Scene>();
            for (int day = 1; day <= maxDays; day++)
            {
                bool isFinalDay = day == maxDays;
                foreach (string id in characterIds)
                {
                    foreach (ScenarioAction action in isFinalDay ? JudgementActions : InterrogationActions)
                    {
                        scenes.Add(new Scene { Day = day, CharacterId = id, Action = action });
                    }
                    if (isFinalDay) continue;
                    foreach (string target in characterIds)
                    {
                        if (target != id) scenes.Add(new Scene { Day = day, CharacterId = id, Action = ScenarioAction.HearAbout, HearingTargetId = target });
                    }
                }
                for (int talk = 1; talk <= db.GetSmartphoneTalkCount(day); talk++)
                {
                    scenes.Add(new Scene { Day = day, Action = ScenarioAction.Smartphone, TalkNumber = talk });
                }
            }
            return scenes;
        }

        /// <summary>
        /// すべての場面で、条件の組み合わせをすべて試し、会話が選ばれない状態や、会話の中身がない行を探す
        /// </summary>
        /// <returns>見つかった問題（日本語の説明）。なければ空</returns>
        public List<string> FindScenesWithoutDialogue()
        {
            var problems = new List<string>();
            foreach (Scene scene in AllScenes())
            {
                List<ScenarioConditionRow> rows = db.GetConditionRows(scene.Day, scene.CharacterId, scene.Action, scene.HearingTargetId, scene.TalkNumber);
                if (rows.Count == 0)
                {
                    problems.Add($"{scene}: 条件シートに行がありません");
                    continue;
                }

                foreach (ScenarioSelector.Context context in EnumerateContexts(scene, rows))
                {
                    ScenarioConditionRow row = ScenarioSelector.Select(rows, context);
                    if (row == null)
                    {
                        problems.Add($"{scene}: {Describe(context, rows, scene)} のとき、どの行の条件も満たさず、会話が出ません");
                        break;  // 同じ場面の問題は1つだけ出す
                    }
                    ScenarioDialogue dialogue = db.FindDialogue(row.dialogueId);
                    if (dialogue == null || dialogue.pages.Count == 0)
                    {
                        problems.Add($"{scene}: {row.sourceLocation} のセリフID「{row.dialogueId}」が本文シートにないか、中身がありません");
                        break;
                    }
                }
            }
            return problems;
        }

        /// <summary>
        /// その場面の行の条件に出てくる値だけを使って、ありうる状態をすべて作る
        /// （好感度は「とても低い・条件の値の1つ下・条件の値」、行動回数は 0 と「条件の値の1つ下・条件の値」、フラグは ON と OFF）
        /// </summary>
        private IEnumerable<ScenarioSelector.Context> EnumerateContexts(Scene scene, List<ScenarioConditionRow> rows)
        {
            bool hasPartner = !string.IsNullOrEmpty(scene.CharacterId);

            // 好感度: キャラごとに試す値（会話の相手の好感度は「本人の好感度」と同じ値として扱う）
            var affectionValues = new Dictionary<string, List<int>>();
            void AddAffection(string id, int threshold)
            {
                if (!affectionValues.TryGetValue(id, out List<int> values)) affectionValues[id] = values = new List<int> { VeryLowAffection };
                values.Add(threshold - 1);
                values.Add(threshold);
            }
            // （スマホは相手がいないので、「本人の好感度」はいつも 0 として判定される）
            foreach (ScenarioConditionRow row in rows)
            {
                if (row.hasSelfAffection && hasPartner) AddAffection(scene.CharacterId, row.selfAffection);
                foreach (AffectionCondition other in row.otherAffections) AddAffection(other.characterId, other.minValue);
            }

            // 行動回数（今回の分は含まない）: その日までにできる回数は 0〜(日-1)。スマホは 0
            var actionCounts = new List<int> { 0 };
            if (hasPartner)
            {
                foreach (ScenarioConditionRow row in rows)
                {
                    if (!row.hasActionCount) continue;
                    actionCounts.Add(row.actionCount - 1);
                    actionCounts.Add(row.actionCount);
                }
                actionCounts = actionCounts.Where(c => c >= 0 && c <= scene.Day - 1).Distinct().ToList();
            }

            // フラグ: 条件に出てくるもののうち、手に入るものは ON と OFF。手に入らないものはいつも OFF
            List<string> flags = rows.SelectMany(r => r.requiredFlags.Concat(r.ngFlags))
                .Where(ObtainableFlags.Contains).Distinct().Take(MaxFlagsPerScene).ToList();

            List<string> affectionIds = affectionValues.Keys.ToList();
            List<List<int>> affectionLists = affectionIds.Select(id => affectionValues[id].Distinct().ToList()).ToList();

            foreach (int[] affectionPick in Combinations(affectionLists))
            {
                var affections = new Dictionary<string, int>();
                for (int i = 0; i < affectionIds.Count; i++) affections[affectionIds[i]] = affectionPick[i];

                foreach (int actionCount in actionCounts)
                {
                    for (int mask = 0; mask < (1 << flags.Count); mask++)
                    {
                        var store = new FlagStore();
                        for (int i = 0; i < flags.Count; i++)
                        {
                            if ((mask & (1 << i)) != 0) store.TurnOn(flags[i]);
                        }

                        int self = hasPartner && affections.TryGetValue(scene.CharacterId, out int s) ? s : 0;
                        yield return new ScenarioSelector.Context
                        {
                            SelfAffection = self,
                            AffectionOf = id => affections.TryGetValue(id, out int v) ? v : 0,
                            ActionCount = actionCount,
                            Flags = store,
                        };
                    }
                }
            }
        }

        /// <summary> 値の候補のリストから、組み合わせをすべて作る </summary>
        private static IEnumerable<int[]> Combinations(List<List<int>> lists)
        {
            var pick = new int[lists.Count];
            IEnumerable<int[]> Walk(int index)
            {
                if (index == lists.Count)
                {
                    yield return (int[])pick.Clone();
                    yield break;
                }
                foreach (int value in lists[index])
                {
                    pick[index] = value;
                    foreach (int[] result in Walk(index + 1)) yield return result;
                }
            }
            return Walk(0);
        }

        /// <summary> 状態の説明（例: 好感度 A=-1・行動回数 0回・フラグ info_x が OFF） </summary>
        private static string Describe(ScenarioSelector.Context context, List<ScenarioConditionRow> rows, Scene scene)
        {
            var parts = new List<string>();
            var ids = rows.SelectMany(r => r.otherAffections.Select(o => o.characterId)).ToList();
            if (rows.Exists(r => r.hasSelfAffection) && !string.IsNullOrEmpty(scene.CharacterId)) ids.Add(scene.CharacterId);
            foreach (string id in ids.Distinct())
            {
                int value = context.AffectionOf(id);
                parts.Add($"{id} の好感度が {(value == VeryLowAffection ? "とても低い" : value.ToString())}");
            }
            if (rows.Exists(r => r.hasActionCount)) parts.Add($"この行動をした回数が {context.ActionCount}回");
            foreach (string flag in rows.SelectMany(r => r.requiredFlags.Concat(r.ngFlags)).Distinct())
            {
                parts.Add($"{flag} が {(context.Flags.IsOn(flag) ? "ON" : "OFF")}");
            }
            return parts.Count > 0 ? string.Join("・", parts) : "条件なし";
        }

        private static string ActionName(ScenarioAction action)
        {
            switch (action)
            {
                case ScenarioAction.Torture: return "拷問";
                case ScenarioAction.Talk: return "対話";
                case ScenarioAction.Negotiate: return "交渉";
                case ScenarioAction.HearAbout: return "聞き取り";
                case ScenarioAction.Release: return "解放";
                case ScenarioAction.ConfirmAI: return "AI確定";
                case ScenarioAction.Smartphone: return "スマホ";
                default: return "朝";
            }
        }

        #endregion

        #region エンディング

        /// <summary> 最終日の判定結果の名前（Excel のエンディングシートの書き方） </summary>
        public static string ResultName(EndingResult result)
        {
            switch (result)
            {
                case EndingResult.AllCorrect: return "全員正解";
                case EndingResult.ConfirmedHuman: return "人間をAI確定した";
                case EndingResult.ReleasedAI: return "AIを解放した";
                default: return "指定なし";
            }
        }

        /// <summary>
        /// 判定結果と、エンディングの条件に出てくるフラグ（手に入るものだけ）の ON / OFF の組み合わせをすべて試し、
        /// それぞれでどのエンディングが選ばれるかを集める
        /// </summary>
        /// <param name="results">試す判定結果</param>
        /// <param name="noEnding">エンディングが1つも当てはまらなかった組み合わせの説明（エンディングの会話なしで終わる）</param>
        /// <returns>選ばれたエンディングの ID</returns>
        public HashSet<string> CollectReachableEndings(IEnumerable<EndingResult> results, List<string> noEnding = null)
        {
            List<string> flags = db.endings.SelectMany(e => e.requiredFlags.Concat(e.ngFlags))
                .Where(ObtainableFlags.Contains).Distinct().Take(MaxFlagsPerScene).ToList();

            var reached = new HashSet<string>();
            foreach (EndingResult result in results)
            {
                for (int mask = 0; mask < (1 << flags.Count); mask++)
                {
                    var store = new FlagStore();
                    for (int i = 0; i < flags.Count; i++)
                    {
                        if ((mask & (1 << i)) != 0) store.TurnOn(flags[i]);
                    }

                    ScenarioEnding ending = db.SelectEnding(result, store);
                    if (ending != null)
                    {
                        reached.Add(ending.endingId);
                    }
                    else if (noEnding != null)
                    {
                        string on = string.Join("・", flags.Where(store.IsOn));
                        noEnding.Add($"判定結果「{ResultName(result)}」{(on.Length > 0 ? $"・ON のフラグ {on}" : "")}");
                    }
                }
            }
            return reached;
        }

        /// <summary>
        /// どんな判定結果・フラグでもたどり着けないエンディングと、エンディングの会話がないエンディングを探す
        /// （AI になる道がなくても、判定結果はすべてありうるものとして調べる。AI になる道は HasAIRoute で別に調べる）
        /// </summary>
        public List<string> FindUnreachableEndings()
        {
            var problems = new List<string>();
            HashSet<string> reached = CollectReachableEndings(Results);
            foreach (ScenarioEnding ending in db.endings)
            {
                if (!reached.Contains(ending.endingId))
                {
                    List<string> missing = ending.requiredFlags.Where(f => !ObtainableFlags.Contains(f)).ToList();
                    problems.Add(missing.Count > 0
                        ? $"エンディング「{ending.listName}」（{ending.endingId}）: 必要フラグ {string.Join("・", missing)} を手に入れる会話・選択肢がありません"
                        : $"エンディング「{ending.listName}」（{ending.endingId}）: 優先度の小さいほかのエンディングに先に当てはまり、選ばれることがありません");
                }

                ScenarioDialogue dialogue = db.FindDialogue(ending.endingId);
                if (dialogue == null || dialogue.pages.Count == 0)
                {
                    problems.Add($"エンディング「{ending.listName}」: 会話「{ending.endingId}」が本文シートにないか、中身がありません");
                }
            }
            return problems;
        }

        /// <summary> AI を解放したときにしか選ばれないエンディング（AI になる道がないとたどり着けない） </summary>
        public List<ScenarioEnding> EndingsOnlyWhenAIReleased()
        {
            HashSet<string> withoutAI = CollectReachableEndings(new[] { EndingResult.AllCorrect, EndingResult.ConfirmedHuman });
            HashSet<string> withAI = CollectReachableEndings(new[] { EndingResult.ReleasedAI });
            return db.endings.Where(e => withAI.Contains(e.endingId) && !withoutAI.Contains(e.endingId)).ToList();
        }

        #endregion
    }
}
#endif
