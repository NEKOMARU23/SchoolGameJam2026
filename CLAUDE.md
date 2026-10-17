# CLAUDE.md — SchoolGameJam2026

Context for Claude Code sessions. Last updated 2026-10-17 (main = PR #38 merged; PR #39 docs-only, awaiting review).
Human-facing docs (Japanese): `Docs/開発状況まとめ.md` (rules, progress, known issues), `Docs/仕様書.md` (planned features + every decision made with the user, items 1–17), `Docs/操作の分かりやすさ_改善案.md` (UX issues found from screenshots, U1–U15; section 4 = decided designs U2–U9 with mockups), `Docs/資料画面のデザイン相談.md` (profile-screen design request for the designer — design only, no programmer behaviour), `Docs/資料画面の動き_プログラム側.md` (profile-screen behaviour/data to implement, section 9 = build list). Read the relevant one before changing game rules or UI.

## User / communication
- The user (NEKOMARU23) **cannot read English. Every reply, report, PR description, commit message and code comment must be in Japanese.** This has slipped many times right after reading English tool output — check the language before sending.
- Student developer on a team; explain Unity/git steps concretely and plainly.
- Workflow the user wants: one branch per feature named `feature/kono/<english-word>` → commits (`feat:` `fix:` `refactoring:` `chore:` `test:` + Japanese, split by feature, Japanese body, `Co-Authored-By` trailer) → push → **PR with a Japanese title and a short explanation** → the user reviews and merges. Don't merge locally into main.
- Push/PR (origin is SSH, which Claude can't use): `git push https://github.com/NEKOMARU23/SchoolGameJam2026.git <branch>:<branch>`; `gh` is at `C:\Program Files\GitHub CLI` (add to `$env:Path`), `gh pr create --repo NEKOMARU23/SchoolGameJam2026 --base main --head <branch> --title … --body-file …`. Write commit messages to a file with bash `printf` (PowerShell `Set-Content -Encoding utf8` adds a BOM to the message).

## Repository layout
- Git root `c:/Unity/SchoolGameJam2026` (git paths are prefixed `SchoolGameJam2026/`). Unity project `c:/Unity/SchoolGameJam2026/SchoolGameJam2026`, Unity **6000.4.3f1**, URP, Input System, TextMeshPro, UniTask (no DOTween), Test Framework 1.6, **com.unity.pipeline** (Unity CLI bridge).
- Game code: `Assets/TechC/SchoolGameJam2026/Script/`, no asmdef (Assembly-CSharp). Tests live there too, wrapped in `#if UNITY_EDITOR` (`playModeTestRunnerEnabled: 1`).
- Editor tools: `Assets/Editor/` (Assembly-CSharp-Editor). Temporary one-off editor scripts go in `Assets/Editor/ClaudeTemp/` and are deleted afterwards.
- `Scenes/WorkSpace/Work_*` are members' sandboxes. `Work_KONO.unity`, `ProjectSettings.asset`, `SchoolGameJam2026.slnx`, `VFXManager.asset`, deleted `Readme`/`TutorialInfo` show as modified locally — never commit them.
- Local-only branches (not pushed, not for main): `feature/kono/vfx-test` (VFX Graph experiments: hit effect, fire tornado + synthesized SE), `feature/kono/choice-sample` (test choice data).

## Scenes
`Title` (start, つづきから, エンディング一覧, option), `InGame` (instance of `Prefab/InGame.prefab`, overrides e.g. `successPointReward: 200`, `Header` inactive), `Manager` (additively loaded by `AppBootstrapper`; AudioManager + `Prefab/Option.prefab` = SettingsManager, OptionUI, SaveSlotUI, SaveLoadController), `ClearScene` / `GameOverScene` (SaveDeletePromptUI). `Result` is disabled.

## Architecture (namespace root `TechC.SchoolGameJam2026.Scripts`)
Rule the user cares about: **logic in Managers, display in Views, `GameUIController` mediates.** Views only display what they're given.

| Area | Classes |
|---|---|
| Managers (`Core.Manager`) | `GameManager` (days, `ScenarioDatabase`, morning snapshot / `ResetCurrentDay`, `CaptureState`/`RestoreState`, AI judgement sheet at day end, final-day `EvaluateFinalResult`, `DecideEnding`/`FinishGame`, `CreateSaveData`/`ApplySave`, choices `GetAvailableChoices`/`SelectChoice`), `InterrogationManager` (actions, points, affection, 4/day, final day = judgement only, `CanUnlockProfile`), `SmartphoneTalkManager`, `SettingsManager` (settings.json), `AudioManager` |
| State (`Core.State`) | `FlagStore`, `GameStateSnapshot`, `SaveData`/`SaveStore` (20 slots, `saves/slot_NN.json`, pending load for Title→InGame), `EndingRecordStore` (ending_record.json), `ReadHistoryStore` (read_record.json, global) |
| Scenario (`Scenario`) | `ScenarioDatabase` (asset `Data/ScenarioDatabase.asset`, generated), `ScenarioRows`, `ScenarioSelector`; `Scenario.Import`: `XlsxReader`, `ScenarioWorkbookParser` (validation); importer `Assets/Editor/ScenarioExcelImporter.cs` (menu Tools ＞ シナリオExcelを取り込む) |
| Mediator (`UI`) | `GameUIController` (also `ISaveTarget`; log history; auto/skip state; skippable = read or SkipMode.All) |
| Views (`UI`) | `ScreenNavigationUI`, `MonitorUI`, `InterrogationUI`, `DialogueUI` (typing, auto/skip), `Dialogue.DialogueClickable` (teammate's smartphone view; auto/skip + `ignoreClickAreas`), `ChoiceUI`, `ConfirmDialogUI`, `EndingPlayerUI`, `EndingListUI`, `SaveSlotUI`, `SaveLoadController`, `OptionUI`, `AutoSkipButtonsUI`, `ProfileUI`, `LogUI`, `ResultUI`, `StatusHeaderUI`, `DayTransitionUI`, `SaveDeletePromptUI`, `OpenLoadButton` |
| Dev | `DebugTools.DebugMenu` (F1, editor/dev builds only) |

Scenario data comes only from `Assets/Editor/ScenarioData.xlsx` (sheets 説明/本文/条件/選択肢/日別設定/フラグ一覧/AI判定/エンディング/リスト; columns found by header; `#` rows are comments). Old `Data/Day1–7_Scenario.asset` + `DayScenarioSO` are unused (deletion not yet confirmed; `InterrogationActionType` lives in `DayScenarioSO.cs`).

## How to work here
### Prefer the Unity CLI while the Editor is open (memory: unity-cli-pipeline)
- `unity status` / `unity command editor_status` → "ready". `unity recompile --json` (`compilationFailed` is `null` when already up to date).
- Tests: `unity command run_tests --mode PlayMode --async_tests true`, then poll `unity command test_status --result-only` until `"status": "completed"` (89 tests ≈ 2–9 min).
- Prefab/scene edits: write a temp `[MenuItem("Tools/ClaudeTemp/…")]` script, `unity recompile`, then `unity command menu --path …`. Edit other scenes with `OpenScene(Additive)` → `SaveScene` → `CloseScene` so the user's open scene isn't changed. Right after a recompile, commands can fail with "Connection reset" or "Main thread operation timed out" — wait and retry.
- C# in the Editor: `unity command eval_file --file x.cs` (put code in a file; PowerShell mangles quoted `--code`).
- Screenshots: open the Game view (`menu --path "Window/General/Game"`), set `Application.runInBackground = true` via eval (otherwise play mode pauses when Unity is in the background), `capture_game_view --save_path Temp/x.png` (saves under **Assets/**Temp — delete it afterwards). `--source camera` misses VFX and post-processing.
- If the Editor is closed: batch mode `Unity.exe -batchmode -projectPath … -executeMethod …` / `-runTests -testPlatform PlayMode`.

### Tests
`Script/Test/`: `InGameTestUtility` (`LoadInGameScene` also redirects ending/read records to temp files via `UseTestEndingRecord`; `JudgeOnFinalDay`, `EndDayByButton`, `ClickConfirmYes`, `GetConfirmDialog(path)` — two `ConfirmDialog` objects exist, InGame's and Option's, so find by path). Don't `using …Core.Manager;` in tests — `GameManager` collides with the utility property; qualify as `Core.Manager.X`. Test names can't start with a digit.

### Shell gotchas (Windows)
- Bash heredocs: don't use them for files with `\\` or quotes — write Python/C# files with the Write tool. Never `cat > file` without input (it hangs waiting for stdin). `sed` replacement `\U` uppercases the rest.
- Console is cp932: `PYTHONIOENCODING=utf-8`. Keep CRLF when patching files from Python.
- The scratchpad and Unity's `Temp/` get cleared between days — don't rely on files kept there.
- Docs-only commits without touching the Unity project's checked-out branch: `git -c core.longpaths=true worktree add <scratchpad>/wt <branch>` (long Unity paths fail without core.longpaths), copy the docs in, commit, push, `git worktree remove`.
- Mockup images for UI discussions: compose with Python/PIL over the screenshots in `Docs/images/ux/` and the real sprites (stand art `Image/修正版/`, name plates `Image/CharacterUI/`); fonts `C:/Windows/Fonts/YuGothB.ttc`. The user decides UI from images, not text.

## Open items
See `Docs/開発状況まとめ.md` (仮のまま・既知の問題) and `Docs/操作の分かりやすさ_改善案.md` (section 3 = status table).
- Done: U1 header (PR #37), U10 overlap / U12 / U13 / U14 (PR #38).
- Decided but not built: U2–U9 (改善案 section 4, with mockups). Includes the "朝の会話" system (new 行動「朝」 in the 条件 sheet → auto smartphone talk at day start, ring SE, same transition as day change; not replayed on reset/load) used by U4 (final day) and U9 (day 1).
- Profile screen rebuild (U7): design request `Docs/資料画面のデザイン相談.md` (designer-only content — keep programmer behaviour out of it), implementation plan `Docs/資料画面の動き_プログラム側.md` (section 9 = build list). Waiting for the designer.
- Still open: U10 English headings, U11 (マキナ = 補助ちゃん's name; Meri's 友人について button uses the wrong plate `Game_Charactername_MakinaAI.png`), U15, U5 undecided parts, hidden-stat (好感度) discussion (改善案 U5).
