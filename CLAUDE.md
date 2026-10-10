# CLAUDE.md — SchoolGameJam2026

Context for Claude Code sessions. The human-facing summary (rules, progress, known issues) is in `Docs/開発状況まとめ.md`; read it too when game rules matter.
**Planned features and all decisions made with the user are in `Docs/仕様書.md` (16 items, agreed implementation order: options → flag/state foundation → debug menu → new Excel format → AI/final-day judgement → choices → endings → save/load → auto/skip). Read it before implementing any of them.**
The new Excel template is `Assets/Editor/ScenarioData.xlsx` (sheets: 説明/本文/条件/日別設定/フラグ一覧/AI判定/エンディング/リスト). Columns are looked up by header name; rows whose first cell starts with `#` are comments. The game does not read it yet — the importer still reads the old single-sheet `ScenarioData.csv` format.

## User / communication
- The user (NEKOMARU23) cannot read English. **All replies, reports, commit messages and code comments must be in Japanese.**
- The user is a student developer on a team; explain Unity/git steps concretely.
- Before running PlayMode tests or editing `.prefab`/`.unity` files, **ask the user to close Unity** and verify with `tasklist //FI "IMAGENAME eq Unity.exe"`. Editing YAML while the editor is open caused stale/mixed state once (a phantom "最終結果" bug that disappeared after reopening).

## Repository layout
- Git root: `c:/Unity/SchoolGameJam2026` (paths in git output are prefixed `SchoolGameJam2026/`).
- Unity project: `c:/Unity/SchoolGameJam2026/SchoolGameJam2026`, Unity **6000.4.3f1**, URP, Input System, TextMeshPro, **UniTask** (no DOTween), Test Framework 1.6.
- Game code: `Assets/TechC/SchoolGameJam2026/Script/` — no asmdef; everything compiles into Assembly-CSharp. `ProjectSettings.asset` has `playModeTestRunnerEnabled: 1` so tests can live in Assembly-CSharp (test files are wrapped in `#if UNITY_EDITOR`).
- Editor tool: `Assets/Editor/ScenarioCSVImporter.cs` (CSV → DayScenarioSO, writes to `Assets/Resources/Scenarios` — NOT the assets the game uses).
- `Scenes/WorkSpace/Work_*` are other members' sandboxes. Don't refactor them beyond what's required to compile.

## Scenes (build order)
`Wolk_ABE`(disabled), `Title`, `InGame`, `Result`(disabled), `Manager`, `ClearScene`, `GameOverScene`.
`AppBootstrapper` additively loads `Manager` (AudioManager) at startup. `InGame.unity` is just an instance of `Prefab/InGame.prefab` with a few overrides (notably `successPointReward: 200`, `Header` m_IsActive 0).

## Architecture (namespace root `TechC.SchoolGameJam2026.Scripts`)
Rule the user cares about: **logic in Managers, display in Views, `GameUIController` mediates.** Views receive results only (no game rules inside views).

| Layer | Class | Responsibility |
|---|---|---|
| Manager | `Core.Manager.GameManager` | days (maxDays=7), scenarios, morning backup/reset, AI confirm (`CanConfirmAI`/`ConfirmAI`), `IsGameCleared`, `AdvanceDayOrFinishGame` (loads ClearScene/GameOverScene) |
| Manager | `Core.Manager.InterrogationManager` | actions, points, affection, daily limit (4), `CanExecuteAction` (incl. released check), release flag, `TryUnlockProfile`, `GetHearingTargetIds`, `GetConfirmAIDialogue` |
| Manager | `Core.Manager.SmartphoneTalkManager` | per-day smartphone talk count; resets on `OnDayStarted`/`OnDayReset` |
| Manager | `Core.Manager.AudioManager` (Manager scene) | BGM by scene name (`Title`/`InGame`/`Result`), SE via `EventManager` + `PlaySEEvent` |
| Mediator | `UI.GameUIController` | receives view input, calls managers, builds view data (`ProfileViewData`, result text), orchestrates day transition (UniTask) |
| View | `UI.ScreenNavigationUI` | panel switching with history stack |
| View | `UI.InterrogationUI`, `ProfileUI`, `MonitorUI`, `ResultUI`, `LogUI`, `StatusHeaderUI`, `DialogueUI`, `DayTransitionUI`, dialogs | display only |
| Format | `UI.DialogueTextFormat` | single parser for dialogue text: `|` pages, `[SYS]`, `[IMG:xxx]` |
| View(+own data) | `UI.Dialogue.DialogueClickable` | other member's class (restored). Holds per-day/per-talk text for 補助ちゃん; opened via `OpenDialogueForTalk(day, talkNumber)`; raises `OnClosed` |
| Data | `SO.DayScenarioSO`, `SO.ProfileDataSO` (`BasicInfoKey = "BASIC"`) | |

Game rules summary: 4 suspects A–D (A=ココア is AI), 7 days, 4 actions/day, once per person per day; correct = `elementIndex == 0 && action == correctAction` (known fragility); clear = confirmed AI on day 7 and AI never released.

## Key IDs in `Prefab/InGame.prefab` (for YAML edits; re-verify before use)
- Canvas GO `7364358246981998045`, Canvas RectTransform `8649986850158955345`
- GameManager GO `8762172388528793037` (GameManager, InterrogationManager, GameUIController `8892950995207965899`, SmartphoneTalkManager `4711512873405592785`)
- ScreenNavigationUI `6042100579899794112`, StatusHeaderUI `5173942068317205519`, MonitorUI `1762519590160357425`, InterrogationUI `735181164149097286`, ProfileUI `1421899173729478357`, LogUI `1035069311232201608`, DialogueUI `4815162342108123457`, DayTransitionUI `3340496237113850003`
- MonitorPanel GO `6056070719320347505`; smartphone button = `MonitorPanel/Button_Help` Button `2364422990428228726`
- SmartphoneTalkPanel GO `4196940290206858242`, its DialogueClickable `5622193443835218396`
- TMP template for new texts: `Header/DayText` TMP `1152734100104709572`
- Script GUIDs: DialogueClickable `47c34825aeab0c44aac0b5ebc75ca2cb`, StatusHeaderUI `05afc6b96bd71c644abccd2cc59dea15`, DayTransitionUI `7142340ce51d4f9f8cea3b05d9d7a496`, SmartphoneTalkManager `cc0049871c024802b8c21fdf272dd1d4`, DialogueUI `09e0d483ed6341f4f9ce917a1e7f717e`; built-ins Image `fe87c0e1cc204ed48ad3b37840f39efc`, Button `4e29b1a8efbd4b44bb3f3716e73f07ff`, TMP `f4688fdb7df04437aeb418b961361dc5`
- Designer mockups (SatumaYoukan): `InGame_Home`, `Ingame_Chat` (補助ちゃん art), `Ingame_Chat_Select`, `Ingame_Data` — references for layout, not used at runtime.

## How to work here
### Prefab / scene YAML edits
- Do them with a Python script (Unity closed), keep CRLF line endings, verify afterwards: no dangling `{fileID: X}`, no duplicate IDs, correct parent/children.
- New MonoBehaviour that a prefab references: **write the `.meta` with a GUID first**, then the `.cs`. Reuse an old GUID from git history when restoring a deleted script so existing references reconnect.
- Write Japanese in YAML as JSON-style `"\uXXXX"` strings; in `re.sub` use a lambda replacement (backslashes break templates).
- `UnityEvent` persistent calls with `m_Target: {fileID: 0}` are dead bindings — look for them after refactors.

### Compile check without Unity
Unity-generated `Assembly-CSharp.csproj` can be stale. Build a temp copy: drop `<Compile>` entries for deleted files, add new `.cs` files, add `Library/ScriptAssemblies/UnityEngine.TestRunner.dll` reference, then `dotnet build _tmpcheck.csproj -o <scratch>` and delete the temp csproj.

### Running PlayMode tests (Unity must be closed)
```
"C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe" -batchmode -projectPath "c:\Unity\SchoolGameJam2026\SchoolGameJam2026" -runTests -testPlatform PlayMode -testResults <scratch>\results.xml -logFile <scratch>\unity.log
```
Run via PowerShell `Start-Process -Wait`; parse the NUnit XML. Tests: `Script/Test/` (`InGameTestUtility`, `InGameDay1SmokeTest`, `InGameFullWeekTest`; 14 tests, all passing as of 2026-10-10). `LoadInGameScene()` sets `DayTransitionUI.SetDurations(0,0)`; tests that check the transition re-enable real durations. Test method names cannot start with a digit. Use `FindAnyObjectByType`/`FindObjectsByType(FindObjectsInactive)` (sort-mode overloads are deprecated in 6.4).

### Shell gotchas (Windows)
- Bash heredocs mangle `\\` and some quotes — write Python helpers to files (scratchpad) and run them.
- Console is cp932: set `PYTHONIOENCODING=utf-8` and write output to a file before reading Japanese.
- `ProjectSettings.asset`, `Assets/Settings/*.asset`, `Script/UI/Dialogue.meta` often show as modified with line-ending-only diffs — don't commit those.

### Commits
- Prefix + Japanese description: `fix:` `feat:` `refactoring:` `chore:` `test:`; **one commit per feature**, with a Japanese body explaining what/why. End with the Co-Authored-By trailer.
- When one file mixes features, build intermediate file states and commit step by step, compile-checking each step.
- Don't push unless asked. Current work branch: `refactoring/kono/code` (7 commits ahead of `main`, last 2 not pushed as of 2026-10-10).

## Open items (see Docs for full list)
- CSV importer target vs used assets (`Resources/Scenarios` vs `Data/`); Priority gaps → null → NRE in `ActionResponseGroup`; no in-cell newline/quote support. User said to ignore Excel until lines are written.
- Correct-answer judgement depends on dialogue list order.
- `successPointReward` prefab 2000000 vs scene override 200.
- `Header` inactive in InGame scene (status texts may be invisible).
- `Setting_Button` unwired; options/volume and `ButtonSoundTrigger` not in production scenes; `OnNightPhaseStarted` has no subscriber.
- Placeholder content/layout: 補助ちゃん lines, SmartphoneTalkPanel, DayTransitionPanel, SmartphoneNotice.
- Intentional: only `ProfileItem_3` (PAST_3) is shown on the profile panel.
