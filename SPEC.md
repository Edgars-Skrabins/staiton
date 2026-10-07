# Claude Code Terminal Wrapper — Spec Sheet

## Context

A lightweight Windows desktop app that wraps Claude Code CLI sessions: each tab is an isolated Claude Code session running directly in a project's git repo, with a live-updating diff panel (added/removed/modified files + full diffs) scoped to what changed since that specific tab was opened, plus a local-only token usage tab and a per-project notes/spec panel. This is a greenfield build. All ambiguous points below were resolved directly with the user across two rounds of clarification plus a research pass validating the library/ecosystem choices. Nothing here is guessed; where the ecosystem had no good off-the-shelf answer (terminal emulation), that's flagged explicitly as the main execution risk, not hidden.

## Tech Stack

| Concern | Choice |
|---|---|
| Language/runtime | C# / .NET (WinForms app) |
| UI framework | WinForms — chosen over WPF/WinUI 3/Electron for lower resource overhead and snappier feel |
| Pseudo-console | `Porta.Pty` (MIT) — spawns the preset executable as the ConPTY's root process via `IPtyConnection` (Reader/Writer streams, `Resize()`); internally wraps Microsoft's official out-of-band `Microsoft.Windows.Console.ConPTY` package, so ConPTY behavior is already pinned consistently across Windows builds |
| VT/ANSI parsing + screen model | Fork & vendor `VtNetCore` (MIT) source directly into the project; patch as needed. Used only for the parser + screen-buffer model, not rendering |
| Rendering | Custom WinForms `Control`, GDI+, double-buffered, glyph-cache, dirty-row repaint, per-monitor DPI aware |
| Git plumbing | LibGit2Sharp for all diffing — no `git.exe` shell-out, no `git stash` API (that API mutates the working tree and is unsafe here). One narrow, deliberate exception: periodic `git gc --auto` for repo maintenance (see "Repository Maintenance" below) — not part of the diffing pipeline, so it doesn't reintroduce the risk the no-shell-out rule protects against |
| File watching | Single project-wide `FileSystemWatcher`, debounced, `.gitignore`-aware filtering |
| Data storage | JSON (`System.Text.Json`) for presets and settings; plain `.md` files for notes |
| Usage log parsing | Tailing read over `~/.claude/projects/<sanitized-cwd>/*.jsonl` (Claude Code's own local session transcripts) |

**Rejected alternatives (with reasons, so they aren't re-litigated):**
- Electron + xterm.js + node-pty: more mature terminal stack, but heavier runtime than native WinForms; user explicitly prioritized "light and snappy."
- WPF/WinUI 3: more modern/composited UI, but heavier startup/compositor overhead than WinForms for this use case.
- Hand-rolled ConPTY P/Invoke: the first research pass rejected `Porta.Pty` as "only days old," which was wrong — directly re-verified against NuGet, it was first published Dec 23, 2025 (9+ months before this spec was written), has 15 published versions, ~204K total downloads, and is under active maintenance. With the maturity concern corrected, hand-rolling the P/Invoke surface ourselves for no benefit was dropped in favor of the maintained package. (Still need to confirm during Milestone 1 that `IPtyConnection` exposes reliable process-exit detection — see ConPTY Layer below.)
- XtermSharp (VT parser alternative): stagnant since Nov 2022 (Xamarin fork archived Dec 2024), Mac-oriented front-end integrations, no Windows precedent.
- Shelling out to `git.exe` for diffing: unnecessary — LibGit2Sharp's plumbing (`TreeDefinition`, `ObjectDatabase.CreateTree`) covers everything needed without a process dependency. (`git.exe` is still used, narrowly, for maintenance-only `git gc --auto` — see "Repository Maintenance.")

**Minimum supported platform:** Windows 10 build 17763 (1809) or later — the floor for the ConPTY API itself, which everything else in this stack depends on.

## Core Concepts

### Projects
- One project = one folder on disk, **must already be a git repository**. If it isn't, the app blocks opening it with a message telling the user to `git init` themselves — no auto-init.
- The app runs **one project at a time**. Working on two projects simultaneously means running two instances of the app.
- All session tabs opened while a project is active run in that same project folder (shared cwd).

### Per-project data folder: `.sain/`
```
<project-root>/.sain/
  presets.json
  settings.json
  notes/
    *.md
```
- Created on first project open if missing.
- The app ensures `.sain/` is listed in the project's root `.gitignore` (appending an entry if absent) — this is the one git-adjacent mutation in the whole app, and it only ever touches `.gitignore` text, never actual git state (refs/index/stash).

### Presets
- A preset = executable name/path + CLI args. v1 ships one built-in preset ("Claude Code" → `claude`, no args); the data model supports user-defined additional presets per project.
- `presets.json`:
```json
{
  "version": 1,
  "presets": [
    { "id": "builtin-claude", "name": "Claude Code", "executable": "claude", "args": [], "isBuiltIn": true }
  ]
}
```
- `executable` is resolved via normal `PATH` search semantics (pass the bare name to `CreateProcess`, same resolution as cmd.exe — no custom PATH-walking code).

### Session tabs
- Opening a tab directly execs the chosen preset's executable as the **root process of a ConPTY** — no intermediate shell, no simulated typing into a shell prompt.
- This means: no general shell commands are possible in a tab (there's no shell to run them in), and "AI process ended" detection is just the root child process exiting.
- When the root process exits, the tab closes automatically.
- Switching the app's active tab switches the visible diff panel to that tab's own baseline/session.

**v1 is a real embedded terminal, deliberately** — `VtNetCore`/`TerminalRenderControl` render whatever bytes `claude`'s CLI emits with zero understanding of what they mean, the same way any terminal emulator treats any program. The alternative (driving Claude Code's headless/`stream-json` mode and building a native chat-style UI instead of a terminal grid) was considered and explicitly deferred, not ruled out — a terminal gives permanent 1:1 fidelity with the real CLI experience (permission prompts, plan mode, slash commands, streaming output) for a one-time VT-parsing cost, versus a native UI that would need to hand-reimplement all of that and track Claude Code's event schema as it evolves.

**Keeping that door open:** the diff engine (git-based) and usage tab (local log-based) already never look at terminal content — they're independent of whichever session-execution mechanism is used (see the clarification on this in project history). So a future "app-managed, no embedded terminal" mode mainly only touches `App.Pty`/`App.Terminal` and the preset model (e.g. a per-preset execution mode flag) and `SessionTabControl`'s content area — not the diff/usage/notes/project machinery. No speculative code for this is being added in v1; this is just a constraint to keep in mind so those modules' boundaries don't quietly end up assuming ConPTY/VT is the only possible session backend.

## ConPTY Layer

Lifecycle:
1. Tab opens → `Porta.Pty` creates the pseudo-console sized to the terminal control's current rows/cols → spawns the preset's executable as the ConPTY's root process with `cwd` = project root, returning an `IPtyConnection`.
2. Wire up process-exit detection on that connection → on signal, close the tab, dispose the connection.
3. Resize: WinForms control size/font-metric change → `IPtyConnection.Resize(rows, cols)` (ConPTY synthesizes the resize signal to the child itself).
4. Input: WinForms key/paste events are encoded into VT input byte sequences (arrow keys, bracketed paste markers, etc.) and written directly to the connection's writer stream.
5. Output: read raw bytes off the connection's reader stream and hand them straight to the VT parser (`XTermParser.DataConsumer.Push`) with no buffering of our own. **Correction from the original plan:** a chunk-boundary UTF-8/escape-sequence buffer was planned here, but reading VtNetCore's actual `XTermInputBuffer`/`DataConsumer` source shows it already retains any unconsumed trailing bytes internally across `Push` calls (catches the `IndexOutOfRangeException` a split multi-byte character or incomplete escape sequence produces, rolls back, and resumes correctly once more bytes arrive) — general handling, not limited to UTF-8. Implementing our own buffering on top would have been redundant.

Build as `App.Pty`:
- `PtySession` — thin wrapper around `Porta.Pty`'s `IPtyConnection`: spawns the preset exe as ConPTY root process, exposes raw byte In/Out streams (with UTF-8 boundary buffering per step 5 above), `ProcessExited` event.
- `InputEncoder` — WinForms input events → VT input byte sequences.

**Known risks to validate early (Milestone 1):** whether `IPtyConnection` surfaces reliable, low-latency process-exit notification (if not, fall back to watching the underlying process handle/PID directly); bracketed paste mode correctness; IME/Unicode input via `WM_CHAR`; mouse-wheel passthrough; alternate-screen-buffer handling for Claude Code's full-screen TUI redraws; whether modern synchronized-output modes (if Claude Code's CLI uses them) are safely ignored rather than causing a crash.

## Terminal Emulation + Rendering (highest-risk component)

- Vendor `VtNetCore`'s source into the repo as our own forked copy; treat it as something we will read and patch, not a black-box dependency (it's been dormant 2.5+ years and has no known WinForms consumer).
  - **Implementation refinement:** vendored as its own class-library project (`VtNetCore/VtNetCore.csproj`, `net10.0`, `Nullable` disabled) referenced by the main app, rather than as a subfolder inside the WinForms project. The library predates nullable reference types (90 warnings if compiled under our `Nullable=enable` setting); isolating it in its own project keeps those pre-existing warnings from drowning out real ones in our own code, without needing to touch the vendored source just to silence them. `ConsoleTerminal.cs` (VtNetCore's own example console-app adapter, which we don't use) was dropped from the vendored copy — we drive `VirtualTerminalController` directly instead.
  - **API surface confirmed by reading the vendored source** (so the rest of this section reflects what's actually there, not assumptions): `VirtualTerminalController` is the concrete screen/state model (implements `IVirtualTerminalController`); `new XTermParser.DataConsumer(controller).Push(byte[])` feeds it raw bytes; `controller.GetPageSpans(startRow, rowCount, width)` returns renderable `LayoutRow`/`LayoutSpan` data with colors already resolved to `"#RRGGBB"` strings (parse directly with `ColorTranslator.FromHtml` — no ANSI color table needed on our side); `controller.ChangeCount`/`Changed`/`ClearChanges()` is the dirty-tracking signal (coarse — whole-screen, not per-row; see rendering note below); `controller.CursorState` carries cursor position/shape/visibility.
  - **Significant reuse found:** the controller already implements keyboard-to-VT-sequence and mouse-to-VT-sequence translation itself — `controller.KeyPressed(key, ctrl, shift)` (where `key` matches WinForms `Keys` enum names almost exactly: `"Up"`, `"Back"`, `"Enter"`, `"F1"`, single-letter names for Ctrl+letter, etc.) and `controller.MousePress(...)`/related methods, respecting `BracketedPasteMode`/mouse-tracking-mode state internally. Both these calls and the controller's own protocol responses (cursor position reports, device attribute queries) funnel through one `controller.SendData` event — so `InputEncoder`'s real job shrinks to "translate a WinForms key/mouse event into the matching `VirtualTerminalController` call," not independently producing raw VT bytes; the actual byte-writing to the PTY happens in one place (the `SendData` handler), not two.
- Feed raw ConPTY output bytes into VtNetCore's parser; read back a grid of rows/spans with character + SGR-attribute data.
- Build `App.Terminal.TerminalRenderControl`: a custom double-buffered WinForms `Control`:
  - Fixed-pitch font only (e.g. Cascadia Mono/Consolas), glyph cell size measured once.
  - Glyph bitmap cache keyed by `(char, fgColor, bgColor, bold/italic)` to avoid per-cell `DrawString` cost.
  - Dirty tracking: VtNetCore's `ChangeCount`/`Changed` is a single whole-screen counter, not per-row — on each timer tick, repaint all visible rows if `Changed` is true (then call `ClearChanges()`), otherwise skip the repaint entirely. This is coarser than true per-row diffing but still avoids redundant redraws between ConPTY output bursts, and the glyph cache keeps a full-grid repaint cheap; revisit only if profiling shows it's not enough.
  - Repaint throttled to a timer (~30–60Hz), decoupled from the raw ConPTY read loop.
  - Per-monitor DPI aware (PerMonitorV2, via application manifest): glyph cell size and font metrics are re-measured on the control's DPI-changed event, so dragging the window to a monitor with different scaling re-renders crisply instead of blurring/mis-scaling.

## Diff-Baseline Mechanism

Per-tab, at tab-open time, snapshot the repo's full current state (tracked changes + untracked files) into an in-memory, fully dangling git tree — **without touching the real working tree, index, HEAD, or stash** (required since multiple tabs run concurrent sessions in the same shared directory).

**Baseline build (`BaselineBuilder`), at tab open:**
1. `repo.RetrieveStatus()` → Added/Staged/Modified/Untracked/Removed/Missing paths (already `.gitignore`-aware; `RetrieveStatus()`'s defaults already include untracked files and recurse into untracked directories).
2. `TreeDefinition.From(repo.Head.Tip)` (empty tree if no commits yet) as the starting point.
3. For every Added/Staged/Modified/Untracked path: eagerly read the file via `repo.ObjectDatabase.CreateBlob(absolutePath)` (reads and hashes the content immediately, inside a try/catch — see read-failure handling below), then `treeDef.Add(path, blob, Mode.NonExecutableFile)`.
4. For every Removed/Missing path: `treeDef.Remove(path)`.
5. `repo.ObjectDatabase.CreateTree(treeDef)` — a plain `Tree`, fully dangling (nothing references it; no ref/commit is created at all for v1, since nothing in the UI needs a human-readable SHA yet).

**Live re-diff (`TabDiffEngine`), on file-watcher signal, scoped to one tab:**
6. A single call: `repo.Diff.Compare<Patch>(baselineTree, DiffTargets.WorkingDirectory)`.

**Correction from the original plan (verified by reading LibGit2Sharp's own source, not just its docs):** the original design assumed a tree-vs-workdir compare behaves like plain `git diff <tree>` and excludes untracked files, requiring a second, separate comparison step (re-running `RetrieveStatus()` and diffing the untracked path sets) to catch brand-new files. That assumption was wrong for this API: `Diff.Compare<T>(Tree, DiffTargets.WorkingDirectory)` unconditionally sets `DiffModifiers.IncludeUntracked`, which in turn sets `GIT_DIFF_INCLUDE_UNTRACKED`, `GIT_DIFF_RECURSE_UNTRACKED_DIRS`, and `GIT_DIFF_SHOW_UNTRACKED_CONTENT` on the underlying libgit2 diff options. So the single `Compare<Patch>` call already fully recurses into and shows the content of any file that didn't exist in the baseline tree — the second comparison step was unnecessary and has been dropped. (Brand-new files surface with `ChangeKind.Untracked` rather than `ChangeKind.Added` in the resulting `Patch`; both map to `FileChangeKind.Added` in the UI layer.) `TabBaseline` accordingly only needs to hold the `Tree`, not a separate known-paths set.

**Explicit deviation from earlier framing:** LibGit2Sharp's own `Repository.Stashes` API must be avoided entirely — its underlying `git_stash_save` call mutates the working tree before returning control, unlike the git CLI's `stash create` plumbing command. The `TreeDefinition`/`ObjectDatabase` approach above replaces that idea completely and needs no stash of any kind, and no `git.exe` shell-out anywhere in the diff pipeline.

**Repository instance per tab:** each tab opens its own `LibGit2Sharp.Repository` handle onto the same repo path, rather than sharing one instance across tabs. `Repository`'s thread-safety under concurrent use isn't documented/guaranteed, and since multiple tabs build baselines and re-diff concurrently, giving each tab its own instance sidesteps the question entirely instead of adding locking around a shared one.

**Read-failure handling during baseline build:** between `RetrieveStatus()` (step 1) and the `Add`/`Remove` loop (steps 3–4), a file can be deleted, locked, or mid-write by a concurrently-running Claude session. This is exactly why step 3 reads each file *eagerly* via `ObjectDatabase.CreateBlob(path)` inside its own try/catch rather than relying on `TreeDefinition.Add(path, filePath, mode)`'s lazy, deferred file read (that overload only registers a builder delegate that reads the file later, inside the single `CreateTree` call — by which point a failure can no longer be isolated to one path without failing the entire baseline). On any read failure for a given path: skip that path for this baseline (log a warning, don't crash), and let it resolve naturally — it'll show up as changed/added/removed on the next live re-diff once the write settles.

## Repository Maintenance (git gc)

Every tab-open writes a dangling tree (and optionally a debug commit) that nothing ever references — by design, so it's safe and inert, but it also means nothing ever cleans these objects up either. Git's own automatic gc only triggers when `git.exe` itself runs a command (commit, checkout, fetch, etc.); since this app talks to the repo purely through the LibGit2Sharp library, our own object-writing never trips that trigger.

- `GcScheduler` runs `git gc --auto` as a one-off shelled-out process on a timer — the one deliberate, narrow exception to "no `git.exe`," isolated purely to maintenance and never part of the diff pipeline.
- Safety: `git gc --auto` only repacks/prunes already-unreachable or stale objects — it doesn't touch refs or the working tree — but it still takes a repository-wide lock, so it must not run concurrently with an in-flight `BaselineBuilder`/`TabDiffEngine` operation. `GcScheduler` checks a simple in-process "git operation in flight" gate before firing each tick; if busy, it skips that tick and waits for the next one rather than blocking.
- Configurable via `.sain/settings.json`:
```json
{
  "version": 1,
  "gcIntervalMinutes": 60
}
```
- Exposed in the UI as a simple settings control (part of `SettingsDialog`) so the interval can be changed without hand-editing the file.

## FileSystemWatcher Design

- One `ProjectFileWatcherService` singleton per running app instance, rooted at the project folder, `IncludeSubdirectories = true`, `NotifyFilter = FileName | DirectoryName | LastWrite | Size`.
- Increased `InternalBufferSize` (e.g. 64 KB); on an `Error` (buffer overflow) event, fall back to a full `RetrieveStatus()` rescan for all open tabs.
- Filter in the event handler (FSW has no built-in exclude list): skip `.git/` and `.sain/`, and check `repo.Ignore.IsPathIgnored(path)` to skip gitignored paths for free (covers `node_modules`, build output, etc.).
- Debounce with a single coalescing timer (~150–250ms); one fan-out event to all open tabs (they all watch the same directory).
- Only eagerly re-diff/re-render the **active** tab on each coalesced change; mark other open tabs' baselines "stale" and lazily recompute on switch-to.

## Usage Tab

- Source: `~/.claude/projects/<sanitized-cwd>/*.jsonl`, where `sanitized-cwd` = project root path with separators replaced by `-` (e.g. `D:\GameDevelopment` → `D--GameDevelopment`). Confirmed directly against a real log file on this machine.
- Confirmed per-line schema for `"type":"assistant"` entries: top-level `sessionId`, `cwd`, `timestamp` (ISO 8601), `gitBranch`; nested `message.model` (e.g. `"claude-sonnet-5"`) and `message.usage = { input_tokens, cache_creation_input_tokens, cache_read_input_tokens, output_tokens, output_tokens_details.thinking_tokens }`.
- `TranscriptLogReader`: tailing `FileStream` (opened `FileShare.ReadWrite`) + byte offset per file; on growth, read only new bytes, split on `\n`, discard a trailing partial line until more data arrives (log is actively being appended by the running Claude Code process). Parse only `"type":"assistant"` lines; ignore unrecognized fields defensively (log format may drift over time).
- `UsageAggregator`: per-session and per-model running totals, timeline, cache-read-vs-fresh-input breakdown.
- **Limits framing (confirmed design):**
  1. Context-window gauge for the *active* session: most recent assistant message's `input_tokens + cache_creation_input_tokens + cache_read_input_tokens` shown against a small static per-model context-size lookup table (Sonnet 5 / Opus 5 / Fable 5.1-class → 1,000,000 tokens; Sonnet 4.5 / Haiku 4.5 → 200,000 tokens; unrecognized model → fall back to 200,000 with a visible "model not recognized, showing conservative estimate" note). Displayed as e.g. "412K / 1M tokens (41%) context used this session."
  2. Rolling 5-hour token total across sessions, clearly labeled as an estimate/heuristic, not an official quota: "~X tokens used in the last 5h (estimate only — no account-wide plan data available locally)."
  3. The app never claims to know the actual account plan quota — both numbers above are locally-derived estimates only.
  4. The per-model context-size table lives in one small, easily-updatable static map (`ModelContextSizeTable`), since it will need updates as new models ship.

## Notes / Spec Panel

- Backed by `.sain/notes/*.md` — flat list, enumerated on demand (`Directory.EnumerateFiles`), no separate index file.
- Simple file list + markdown editor UI; create/rename/delete/edit.

## Module / Class Breakdown

```
App.Core
  ProjectContext          - open project root, validates git repo (LibGit2Sharp Repository.IsValid), owns one Repository instance per tab
  PresetStore             - load/save presets.json, seeds built-in "Claude Code" preset
  SettingsStore           - load/save .sain/settings.json (gcIntervalMinutes, future settings)
  NotesStore              - enumerate/read/write notes/*.md
  GitignoreManager        - ensures .sain/ entry exists in root .gitignore

App.Pty
  PtySession              - wraps Porta.Pty's IPtyConnection: spawns preset exe as ConPTY root process, exposes UTF-8-boundary-safe byte In/Out streams, ProcessExited event
  InputEncoder            - WinForms key/paste events -> VT input byte sequences

App.Terminal
  VtNetCore (vendored/patched) - VT parser + screen buffer model
  TerminalGridSnapshot    - dirty-row-tracked read model consumed by the renderer
  TerminalRenderControl   - WinForms Control, GDI+ glyph-cache renderer, double buffered, per-monitor DPI aware

App.Diff
  BaselineBuilder         - builds the synthetic TreeDefinition/Tree per the algorithm above; skips-and-logs unreadable paths
  TabDiffEngine           - owns a tab's baseline Tree; re-diffs on watcher signal via a single Compare<Patch> call (untracked files included automatically)
  ProjectFileWatcherService - singleton FSW wrapper, debounced, gitignore-filtered, fan-out event
  GcScheduler             - timer-driven git gc --auto, gated against in-flight git operations

App.Usage
  TranscriptLogReader     - tailing jsonl reader per session file
  UsageAggregator         - per-session/per-model rollups
  ModelContextSizeTable   - static lookup, flagged for maintenance

App.UI (WinForms)
  MainForm                - tab strip (session tabs + Usage tab + Notes panel), right-hand diff panel host
  SessionTabControl       - one per open Claude session: hosts TerminalRenderControl + binds to its TabDiffEngine
  DiffPanelControl        - file list + per-file diff viewer for the active tab
  UsageTabControl
  NotesPanelControl
  PresetPickerDialog      - choose/create preset when opening a new tab
  SettingsDialog          - edit gcIntervalMinutes and future app settings
```

## Build Order / Milestones

1. **ConPTY + terminal rendering proof of concept** (highest risk first): wire up `Porta.Pty`, spawn `claude`, pipe bytes through the vendored VtNetCore parser, render a minimal fixed grid with GDI+ in a bare WinForms form. Validate colors/SGR, cursor movement, alt-screen buffer, resize, reliable process-exit detection via `IPtyConnection`, basic key input round-trip against the *real* Claude Code CLI before proceeding.
2. **Project / preset / tab shell**: `ProjectContext` git-repo validation + blocking message, `.sain/` creation + `.gitignore` entry, `presets.json` CRUD, tab strip opening real `PtySession`s via the proof-of-concept terminal control, tab auto-close on process exit.
3. **Diff engine**: `BaselineBuilder` + `TabDiffEngine` (one `Repository` per tab), wired to `ProjectFileWatcherService`, right-hand diff panel UI, switching active tab switches visible baseline.
4. **Usage tab**: transcript tailing, aggregation, context-gauge + rolling-window display.
5. **Notes panel**: file list + simple markdown editor over `.sain/notes/`.
6. **Repository maintenance**: `SettingsStore` (`.sain/settings.json`), `GcScheduler` timer + in-flight gate, `SettingsDialog` UI for the gc interval.
7. **Hardening pass**: FSW overflow fallback, multi-tab stale-diff lazy recompute, VT edge cases discovered from real usage (bracketed paste, IME, mouse wheel), per-monitor DPI-change re-measurement, preset management UI polish.

## Verification

Since this is a new desktop app with no existing test harness:
- Milestone 1 is itself the verification gate for the highest-risk component — must be run interactively against the real `claude` CLI (not a mock), checking colors, cursor movement, resize, and alt-screen behavior by eye.
- Diff engine (Milestone 3): verified by manually editing/adding/deleting files in the project folder while a tab is open and confirming the right-hand panel reflects exactly those changes, scoped only to that tab's baseline, while a second tab in the same folder shows its own independent baseline.
- Usage tab (Milestone 4): verified by comparing displayed aggregates against manual `grep`/line-count checks on a real `.jsonl` log file.
- No automated test suite is planned for v1 given the UI-heavy, interactively-verified nature of the work; revisit if/when core engines (diff, usage parsing) stabilize enough to warrant unit tests.
