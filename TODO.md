# Claude Code Terminal Wrapper — Implementation TODO

Companion to `SPEC.md`. That document is the general guideline (what/why); this document is the granular how — every item below traces back to a specific line in `SPEC.md`. Work top to bottom; later milestones assume earlier ones are done and verified, not just written.

Naming flagged as unconfirmed, decide/confirm before scaffolding: solution/exe name (placeholder `ClaudeCodeTerminal` used below), root namespace (placeholder `App.*`, matching `SPEC.md`'s module breakdown).

---

## Milestone 0 — Solution Scaffolding (prerequisite, not in `SPEC.md`'s milestone list but required before Milestone 1) — DONE

- [x] Installed .NET 10 SDK (10.0.401, side-by-side with existing 9.0.302) — required because `Porta.Pty` targets `net10.0` specifically and a net10.0 package cannot be referenced from an older TFM.
- [x] Created solution `ClaudeCodeTerminal.slnx` (.NET 10's `dotnet new sln` now emits the newer XML solution format by default, not classic `.sln`) with a single WinForms project `ClaudeCodeTerminal.csproj`.
- [x] Target framework: `net10.0-windows`, `UseWindowsForms = true`, `Nullable = enable`, `ImplicitUsings = enable` (all template defaults, confirmed in the generated csproj).
- [x] Added NuGet packages:
  - [x] `Porta.Pty` 2.2.3 (pulls in `Microsoft.Windows.Console.ConPTY` 1.24.260710001 transitively, as expected).
  - [x] `LibGit2Sharp` 0.32.0 (pulls in `LibGit2Sharp.NativeBinaries` 2.0.324) — resolved cleanly against `net10.0-windows`, no compatibility warnings.
  - [x] No package needed for JSON (`System.Text.Json` ships in the BCL).
- [x] Folder structure matching the module breakdown created as project subfolders: `App.Core/`, `App.Pty/`, `App.Terminal/`, `App.Diff/`, `App.Usage/`, `App.UI/` (each currently holds only a `.gitkeep` placeholder — populated starting Milestone 1/2).
- [x] DPI awareness: used `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` in the csproj instead of hand-authoring an `app.manifest` — this is the modern SDK-style equivalent for .NET WinForms apps and generates the correct manifest automatically at build time. (Dropped the originally-planned `<supportedOS>` manifest entries: ConPTY availability is a runtime OS-version check, not something manifest-gated, so a hand-written manifest adds nothing here.)
- [x] Confirmed `claude` CLI (`...\WinGet\Packages\Anthropic.ClaudeCode_...\claude.exe`) and `git` (`D:\Programs\Git\cmd\git.exe`) are both on `PATH`.
- [x] `dotnet build` succeeds clean (0 warnings, 0 errors) on the bare scaffold.
- [x] `.gitignore` generated via `dotnet new gitignore` (standard dotnet/VS ignores: bin/obj/.vs/user files/etc.) — separate from the per-*project* `.gitignore` handling built in Milestone 2, which is about repos this app *opens*, not its own source repo.
- [x] Stripped the template's auto-generated XML doc comments from `Program.cs`/`Form1.Designer.cs` to match the no-comments convention.
- [ ] Set up a throwaway local git repo elsewhere for manual testing throughout Milestones 1–7 (not needed yet — do this when Milestone 1 testing starts).

---

## Milestone 1 — ConPTY + Terminal Rendering Proof of Concept — CODE WRITTEN, NOT YET INTERACTIVELY VERIFIED

Ref: `SPEC.md` § "ConPTY Layer", § "Terminal Emulation + Rendering". Highest-risk component — do not proceed to polish/hardening until every item in the validation checklist at the bottom passes against the *real* `claude` CLI.

**Status:** `App.Pty/PtySession.cs`, `App.Terminal/TerminalGridSnapshot.cs`, `App.Terminal/TerminalRenderControl.cs` are written and the solution builds clean (0 warnings/errors) and launches without crashing. What this does **not** mean: nobody has visually confirmed the terminal actually renders `claude`'s output correctly, handles resize/alt-screen/IME correctly, etc. — that requires a human running the app and looking at it (see § 1.6 below, unchanged). A few implementation notes worth knowing before testing:
- Vendored `VtNetCore` turned out to already implement far more than originally planned for: `VirtualTerminalController.KeyPressed(key, ctrl, shift)` and `.MousePress/.MouseRelease/.MouseMove(...)` do the keyboard/mouse-to-VT-sequence translation themselves (respecting `BracketedPasteMode`/mouse-tracking-mode internally), and all outbound bytes — both those and the controller's own protocol responses — funnel through one `SendData` event. So `InputEncoder` as originally scoped (an independent byte-producing component) wasn't built as a separate class; `TerminalRenderControl` calls straight into `TerminalGridSnapshot` (a thin synchronized wrapper around the controller), and the `SendData` event is wired directly to `PtySession.Write`.
- Dirty-row tracking is coarser than planned: VtNetCore's `Changed`/`ChangeCount` is a single whole-screen flag, not per-row. The renderer repaints all visible rows when `Changed` is true and skips entirely otherwise — simpler than true per-row diffing, and fine unless profiling later says otherwise.
- The planned manual UTF-8 chunk-boundary buffer in the ConPTY read path was dropped — VtNetCore's own `DataConsumer`/`XTermInputBuffer` already retains unconsumed trailing bytes across `Push` calls (catches the starved-buffer case and resumes correctly), making a second buffer on our side redundant.
- Ctrl+C and friends: handled via `OnKeyDown` mapping `Keys.A`..`Keys.Z` (when Ctrl is held) straight to `VirtualTerminalController.KeyPressed`, which resolves them against VtNetCore's own control-character table — not hand-rolled.
- `ConsoleTerminal.cs` (VtNetCore's own example console-app adapter) was dropped from the vendored copy; we drive `VirtualTerminalController` directly instead, so it was unused dead weight.

### 1.1 `App.Pty.PtySession`
- [ ] Wrap `Porta.Pty`'s connection factory to spawn a given executable + args + working directory as the ConPTY root process, sized to an initial (rows, cols).
- [ ] Expose the connection's reader stream, writer stream, and resize method through a small internal interface so the rest of the app never touches `Porta.Pty` types directly (keeps the dependency swappable if Milestone 1 validation fails — see fallback note below).
- [ ] Implement UTF-8 boundary-safe reading:
  - [ ] Maintain a small carry-over `byte[]` buffer between reads.
  - [ ] After each raw read, scan backward from the end of the combined (carry-over + new) buffer for a valid UTF-8 sequence boundary; if the trailing bytes are an incomplete multi-byte sequence, hold them back for the next read instead of decoding/forwarding them.
  - [ ] Unit-test this boundary logic in isolation with synthetic byte arrays that deliberately split multi-byte UTF-8 characters (e.g. box-drawing characters, emoji) across chunk boundaries — this is cheap to test without a real ConPTY and easy to get subtly wrong.
- [ ] Implement `ProcessExited` event:
  - [ ] Investigate what `IPtyConnection` actually exposes for exit notification (event, awaitable task, or just stream-closed). Document the finding.
  - [ ] **Fallback plan if `IPtyConnection` exit notification is unreliable or high-latency:** obtain the underlying child process's PID/handle (if `Porta.Pty` exposes it) and use `Process.GetProcessById(pid)` + `EnableRaisingEvents` + `Exited` directly as a more reliable signal, treating `Porta.Pty`'s own notification as secondary/best-effort.
  - [ ] Verify the event fires promptly (sub-second) when `claude` exits normally (`/exit` or natural completion) and when it's killed abruptly (Ctrl+C from within the session, or `taskkill` from outside).
- [ ] Implement `Dispose()`: close writer stream, close reader stream, dispose the `IPtyConnection`, ensure no orphaned child process remains (verify via Task Manager during manual testing).

### 1.2 `App.Pty.InputEncoder`
- [ ] Map WinForms `KeyDown`/`KeyPress` events to raw bytes/VT sequences:
  - [ ] Printable characters → UTF-8 bytes directly.
  - [ ] Enter → `\r`.
  - [ ] Backspace → `\x7f` (or `\b`, confirm which `claude`'s CLI expects — test both if behavior looks wrong).
  - [ ] Arrow keys → `ESC[A` / `ESC[B` / `ESC[C` / `ESC[D`.
  - [ ] Home/End/PageUp/PageDown/Delete → their standard CSI sequences.
  - [ ] Tab, Escape, Ctrl+<letter> combinations (Ctrl+C especially — must be forwarded as a real `\x03` byte so it reaches `claude`, not intercepted as a WinForms shortcut).
- [ ] Implement bracketed paste: on a WinForms paste action (Ctrl+V or context-menu paste), wrap the clipboard text in `ESC[200~ ... ESC[201~` before writing to the input stream, provided the terminal has signaled bracketed-paste mode is active (track this as state updated by the VT parser's mode-change callbacks).
- [ ] Route mouse wheel events to the appropriate VT mouse-reporting sequence only if the terminal has enabled mouse tracking (again, state from the VT parser); otherwise let wheel events scroll the local scrollback view instead (see 1.3).
- [ ] Verify IME input (e.g. composing non-Latin text via Windows IME) round-trips correctly through `WM_IME_*`/`WM_CHAR` rather than raw `WM_KEYDOWN` — test with an actual IME enabled, not just ASCII input.

### 1.3 `App.Terminal` — vendoring VtNetCore
- [ ] Fork `VtNetCore` (confirm current MIT license terms still apply) and vendor its source directly into the solution (e.g. `App.Terminal/VtNetCore/`) rather than consuming it as a NuGet reference — spec explicitly calls for treating it as patchable, not a black box.
- [ ] Get it compiling against `net10.0-windows` (it's `netstandard2.0`, should be source-compatible, but confirm no build warnings/errors need fixing).
- [ ] Read through its parser/screen-buffer implementation enough to answer, concretely:
  - [ ] Does it support the alternate screen buffer? Confirm by feeding it a raw byte capture of a real full-screen TUI session (see 1.6) and checking the screen model updates correctly.
  - [ ] Does it expose scrollback separately from the visible screen, or only the current screen state? (Determines whether `TerminalRenderControl` needs its own scrollback buffer on top.)
  - [ ] Does it track SGR attributes (fg/bg color, bold, italic, underline) per cell in a way `TerminalGridSnapshot` can read cheaply?
  - [ ] Does it gracefully ignore unrecognized CSI/OSC/DCS sequences (no throw/crash), particularly any "synchronized output" private mode a modern TUI might emit?
  - [ ] Does it expose a way to know whether bracketed-paste mode and mouse-tracking mode are currently enabled (needed by `InputEncoder`, 1.2)?
- [ ] Write `App.Terminal.TerminalGridSnapshot`: a thin read-model wrapper over VtNetCore's internal screen state that exposes "rows changed since last flush" (dirty-row tracking) to the renderer — implement as a snapshot/diff of row version numbers or a dirty-bit array, whichever VtNetCore's internals make cheaper.

### 1.4 `App.Terminal.TerminalRenderControl`
- [ ] Create a custom WinForms `Control` subclass with `DoubleBuffered = true` (or manual `BufferedGraphics` if more control is needed over the DIB).
- [ ] Pick and measure a fixed-pitch font (e.g. Cascadia Mono, fallback to Consolas if unavailable) once at control creation; compute cell width/height from `Graphics.MeasureString` or `TextRenderer.MeasureText` on a representative character.
- [ ] Build a glyph cache: `Dictionary<(char, Color fg, Color bg, FontStyle style), Bitmap>` (or a texture atlas if profiling shows per-glyph `Bitmap` allocation is too slow) — render a glyph into the cache lazily on first use.
- [ ] Implement the paint loop:
  - [ ] On a timer tick (~30–60Hz), ask `TerminalGridSnapshot` for dirty rows since last flush.
  - [ ] For each dirty row, blit cached glyphs for each cell into the back buffer at the right cell coordinates.
  - [ ] `Invalidate()` only the changed region, not the whole control, where WinForms allows it.
- [ ] Implement cursor rendering (blinking block/bar per the VT parser's reported cursor style and position) as a separate overlay so it doesn't force full-row glyph cache churn every blink.
- [ ] Implement resize handling: on `SizeChanged` (and on font-metric change), recompute rows/cols from the control's pixel size and cell size, call `PtySession`'s resize (→ `IPtyConnection.Resize`), and resize the internal row/col grid.
- [ ] Implement per-monitor DPI handling:
  - [ ] Handle `WM_DPICHANGED` (via WinForms' `DeviceDpi` changed hook or overriding `WndProc`).
  - [ ] Re-measure font/cell metrics at the new DPI, invalidate the glyph cache (old cached bitmaps are the wrong size), force a full repaint.
  - [ ] Manually test by starting the app on one monitor and dragging the window to a second monitor with different scaling (set this up in Windows display settings if not already available).
- [ ] Wire mouse-wheel scroll to move a local scrollback viewport (distinct from `IPtyConnection` mouse-reporting bytes — only forward wheel events as VT bytes when the terminal has mouse-tracking mode enabled; otherwise scroll locally, per 1.2).

### 1.5 Bare validation harness
- [ ] Build the bare-bones WinForms form (no project/tab chrome yet) that: creates one `PtySession` running `claude`, feeds its output into one `TerminalGridSnapshot`/`TerminalRenderControl`, and forwards keyboard input via `InputEncoder`.
- [ ] This is explicitly throwaway/POC scaffolding — fine to hardcode the working directory and skip any UI polish.

### 1.6 Milestone 1 exit checklist — do not proceed until all pass against the real `claude` CLI
- [ ] Colors and text attributes (SGR) render correctly (compare against the same session running in Windows Terminal side by side).
- [ ] Cursor movement and positioning match Windows Terminal's rendering for the same input.
- [ ] Claude Code's interactive input box (multi-line composition, backspace, arrow-key navigation within it) behaves correctly.
- [ ] Alternate screen buffer transitions (if/when Claude Code's CLI uses them) render without corruption, and restore the prior screen correctly on exit from the alt buffer.
- [ ] Resizing the window mid-session reflows correctly (Claude Code's CLI redraws for the new size without leftover artifacts).
- [ ] Process-exit detection fires correctly and promptly for: natural exit, `/exit`-style command, Ctrl+C, and an external `taskkill`.
- [ ] Basic key input round-trips: typing a prompt, arrow-key editing, Enter to submit, Ctrl+C to interrupt a response.
- [ ] Bracketed paste: pasting multi-line text lands as literal text in the input box, not interpreted as separate keystrokes/commands.
- [ ] IME composition (if a non-Latin keyboard/IME is available to test with) produces correct characters.
- [ ] No unhandled exceptions/crashes from unrecognized escape sequences during a long, varied real session (run an actual multi-turn Claude Code conversation, not just a hello-world prompt).
- [ ] **Decision checkpoint:** if `IPtyConnection` process-exit notification proved unreliable in testing, implement and switch to the PID/handle fallback (1.1) before moving on — don't carry a known-flaky exit signal into Milestone 2's tab-close logic.

---

## Milestone 2 — Project / Preset / Tab Shell — IMPLEMENTED

Ref: `SPEC.md` § "Core Concepts", § "Repository Maintenance" (gitignore part only — gc scheduling itself is Milestone 6).

**Status:** `App.Core/ProjectContext.cs`, `App.Core/GitignoreManager.cs`, `App.Core/PresetStore.cs` (+ `Preset.cs`), `App.UI/PresetPickerDialog.cs`, `App.UI/SessionTabControl.cs`, `App.UI/MainForm.cs` are all written and wired together (File > Open Project / New Session menu, tab auto-close on process exit, per-tab `Repository` via `ProjectContext.OpenRepository()`). The 2.1 "how does a second project get opened" ambiguity was resolved as: "Open Project" while one is already open just closes the current one (disposing its tabs/watcher) and opens the new one in the *same* window — running two projects truly simultaneously means launching the `.exe` a second time yourself (nothing in-app spawns a second process, since nothing in the spec asked for that). Not yet done: preset-management UI polish (add/edit/delete beyond the built-in preset is implemented in `PresetStore` but has no dialog yet — deferred, consistent with "basic level for now, polish in Milestone 7").

### 2.1 `App.Core.ProjectContext`
- [ ] "Open project" flow: folder picker → validate the folder is a git repository via `LibGit2Sharp.Repository.IsValid(path)`.
  - [ ] If invalid: show a blocking message instructing the user to run `git init` themselves; do not create `.sain/` or any other state for a non-repo folder.
  - [ ] If valid: proceed to 2.2.
- [ ] Hold the validated project root path and expose it to the rest of the app (presets, notes, watcher, tab spawning) as the single source of truth for "current project."
- [ ] Enforce single-project-per-process: no UI path should allow opening a second project in the same running instance (per spec — a second project means a second instance of the app). If a "new project" action is invoked while one is already open, either disable it or explicitly document that it launches a new process (`Process.Start` on the app's own executable) rather than reusing the window — pick one and note the choice in code, since the spec says multi-project means multiple app instances but doesn't say *how* a second instance gets launched from inside the UI.

### 2.2 `App.Core.GitignoreManager`
- [ ] On project open, read the root `.gitignore` (create an empty one if missing).
- [ ] Check whether an entry matching `.sain/` (or `/.sain/`) already exists (be tolerant of trailing slash / leading slash variants already present).
- [ ] If absent, append it with a leading newline guard (don't corrupt the last line of an existing file that lacks a trailing newline).
- [ ] Write the file back preserving the original line-ending style (don't silently convert CRLF↔LF for the user's existing file).

### 2.3 `App.Core.PresetStore`
- [ ] On project open, ensure `.sain/` exists (`Directory.CreateDirectory`, idempotent).
- [ ] Load `.sain/presets.json` if present; if absent, create it pre-seeded with the built-in `"Claude Code"` preset exactly as specified in `SPEC.md`.
- [ ] Define the preset model as a C# record/class matching the documented JSON shape (`id`, `name`, `executable`, `args: string[]`, `isBuiltIn`).
- [ ] Implement add/edit/delete for user-defined presets; prevent editing/deleting the built-in preset's `executable`/`args` (it should remain a stable, always-available default) — deleting user-defined presets is fine.
- [ ] Persist on every mutation (simple full-file rewrite is fine at this scale — no need for partial updates).
- [ ] Handle a corrupt/unparseable `presets.json` gracefully: back up the bad file (e.g. rename to `presets.json.bak`) and recreate a fresh default rather than crashing project-open.

### 2.4 Tab lifecycle (`App.UI.MainForm` + `App.UI.SessionTabControl`)
- [ ] `MainForm` hosts a tab strip; one of the fixed tabs is "Usage" (Milestone 4), one area is reserved for the Notes panel (Milestone 5 — decide now whether Notes is its own tab or a persistent side panel, and keep that consistent with the Usage tab's placement).
- [ ] "New session tab" action opens `PresetPickerDialog` (2.5), then:
  - [ ] Creates a new `PtySession` for the chosen preset, cwd = project root.
  - [ ] Creates a new `TerminalRenderControl` bound to that session (reusing the Milestone 1 plumbing, now parameterized instead of hardcoded).
  - [ ] Adds a new `SessionTabControl` hosting that render control, added to the tab strip, and made active.
- [ ] Wire `PtySession.ProcessExited` → close that specific tab automatically (remove from tab strip, dispose the `SessionTabControl` and everything it owns).
- [ ] Switching the active tab (`SelectedIndexChanged` or equivalent) updates the right-hand diff panel to that tab's `TabDiffEngine` (stub this out now with a placeholder panel; real wiring is Milestone 3).
- [ ] App-exit cleanup: on `MainForm` closing, iterate all open `SessionTabControl`s and dispose their `PtySession`s (and, once Milestone 3 lands, their `Repository` instances and the shared `ProjectFileWatcherService`) — verify no orphaned `claude` child processes survive after the app closes (check Task Manager).

### 2.5 `App.UI.PresetPickerDialog`
- [ ] Simple modal: list presets from `PresetStore` (built-in first), "New session" confirms the selection, a secondary action opens preset add/edit (backed by 2.3).
- [ ] Defer any fancier preset-management UI polish to Milestone 7 — this dialog only needs to support pick/create/edit/delete at a basic level for now.

---

## Milestone 3 — Diff Engine — IMPLEMENTED

Ref: `SPEC.md` § "Diff-Baseline Mechanism", § "FileSystemWatcher Design".

**Status:** `App.Diff/BaselineBuilder.cs`, `App.Diff/TabDiffEngine.cs`, `App.Diff/ProjectFileWatcherService.cs`, `App.Diff/FileChange.cs`, `App.UI/DiffPanelControl.cs` are written and wired into `MainForm` (active-tab eager recompute, inactive tabs marked stale and lazily recomputed on switch-to, FSW overflow treated as a force-recompute-everyone signal). **Significant correction found by reading LibGit2Sharp's actual source** (not just its docs) while implementing this: see `SPEC.md`'s "Diff-Baseline Mechanism" section — the originally-planned two-step diff (tree-compare plus a separate untracked-path-set comparison to catch brand-new files) turned out to be unnecessary. `Diff.Compare<T>(Tree, DiffTargets.WorkingDirectory)` unconditionally includes and recurses into untracked content already, so `TabDiffEngine.Recompute()` is just one `Compare<Patch>` call. `BaselineBuilder` also reads each file *eagerly* via `ObjectDatabase.CreateBlob(path)` inside a per-file try/catch rather than the lazy `TreeDefinition.Add(path, filePath, mode)` overload the spec originally cited — that overload defers the actual file read until the single final `CreateTree` call, which would make a single bad file (deleted/locked mid-write) fail the *entire* baseline instead of just that one path. Manual verification (editing/adding/deleting files live with multiple tabs open, confirming independent per-tab baselines) per `SPEC.md`'s Verification section still needs a human to actually do it.

### 3.1 `App.Diff.BaselineBuilder`
- [ ] Given a `Repository` instance (one per tab, per spec) and the project root, implement the baseline algorithm exactly as documented:
  1. [ ] `repo.RetrieveStatus()` → collect Modified / Added(staged) / Removed / Untracked-non-ignored paths.
  2. [ ] `TreeDefinition.From(repo.Head.Tip)` — handle the **no-commits-yet** case explicitly (brand-new repo with zero commits): start from an empty `TreeDefinition` instead of dereferencing a null `Head.Tip`.
  3. [ ] For each Modified/Added/Untracked path: `treeDef.Add(path, absoluteFilePath, Mode.NonExecutableFile)`.
     - [ ] Wrap each individual `Add` in a try/catch for file-read failures (locked/deleted/mid-write): on failure, skip that path for this baseline, log a warning, continue the loop (per spec's read-failure handling) — do not abort the whole baseline build over one bad path.
  4. [ ] For each Removed/deleted path: `treeDef.Remove(path)`.
  5. [ ] `repo.ObjectDatabase.CreateTree(treeDef)`; optionally also `CreateCommit(...)` with a clear author/message (e.g. "baseline snapshot, tab opened <timestamp>") purely for a human-readable SHA to show in diagnostic/debug UI — confirm no `updateRef` parameter is accidentally passed that would attach this to a ref.
  6. [ ] Record the set of known paths (tracked + untracked-non-ignored) from step 1, keyed to this baseline, for use by `TabDiffEngine` step 8.
- [ ] Return a small `TabBaseline` value object: `{ Tree baselineTree, ISet<string> knownPaths, DateTime createdAt }`.
- [ ] Unit-testable in isolation: construct a throwaway temp git repo in a test, exercise modified/added/removed/untracked combinations, assert the resulting tree matches expectations — this is dense enough logic to be worth testing directly even though the spec otherwise defers automated tests.

### 3.2 `App.Diff.TabDiffEngine`
- [ ] Owns one tab's `TabBaseline` (from 3.1) plus the `Repository` instance for that tab.
- [ ] `Recompute()`:
  1. [ ] `repo.Diff.Compare<Patch>(baseline.Tree, DiffTargets.WorkingDirectory)` → per-file diff entries (added/modified/deleted relative to baseline) for everything that existed at baseline time.
  2. [ ] `repo.RetrieveStatus()` again; compute `currentUntracked - baseline.knownPaths` → brand-new files since baseline; render each as an "all lines added" entry (diff against an empty blob) rather than trying to route them through the tree-vs-workdir patch.
  3. [ ] Combine both result sets into one ordered file-change list (decide and document a stable sort — e.g. alphabetical by path, or grouped by change type then path) for the UI to bind to.
- [ ] Expose a "stale" flag: set `true` when the shared file watcher signals a change but this tab isn't the active one (see 3.3); `Recompute()` clears it. `DiffPanelControl` (3.4) triggers `Recompute()` on switch-to if stale.
- [ ] Dispose: dispose the owned `Repository` instance when the tab closes.

### 3.3 `App.Diff.ProjectFileWatcherService`
- [ ] Single instance owned by `ProjectContext`, created on project open, disposed on project close.
- [ ] `FileSystemWatcher` config: `Path` = project root, `IncludeSubdirectories = true`, `NotifyFilter = FileName | DirectoryName | LastWrite | Size`, `InternalBufferSize` raised (e.g. 64 KB).
- [ ] Event handler filtering (since FSW has no built-in exclude list):
  - [ ] Skip any path under `.git/` or `.sain/`.
  - [ ] Skip any path where a throwaway/shared `Repository.Ignore.IsPathIgnored(path)` check returns true.
- [ ] Debounce: coalesce bursts of events into one "something changed" signal via a single timer (~150–250ms), reset on each new event, firing once when it settles.
- [ ] On the coalesced signal: fan out to all open tabs' `TabDiffEngine`s — call `Recompute()` directly on the active tab's engine, and set the `IsStale` flag (3.2) on all others.
- [ ] Handle the FSW `Error` event (buffer overflow): on overflow, instead of trusting incremental events, force every open tab's `TabDiffEngine` to fully recompute (treat it like a coalesced signal to everyone, bypassing the active/stale distinction for this one case, since we've lost track of what actually changed).
- [ ] Manually test the overflow path by generating a very large burst of file changes (e.g. a script that touches thousands of files quickly) and confirming the fallback rescan kicks in rather than the app silently missing changes.

### 3.4 `App.UI.DiffPanelControl`
- [ ] Bound to the currently active tab's `TabDiffEngine`.
- [ ] File list: grouped/marked by change type (added / modified / deleted), clickable to show that file's diff.
- [ ] Diff viewer: render the `Patch`/per-file diff content with added/removed line highlighting (can be a simple colored text view for v1 — no need for a full Monaco-style component).
- [ ] On active-tab switch: if the newly active tab's engine `IsStale`, call `Recompute()` before rendering (per 3.2/3.3's lazy-recompute design).
- [ ] Manual verification (per `SPEC.md`'s Verification section): with two tabs open in the same project, edit/add/delete files while tab A is active, confirm its panel updates live; switch to tab B, confirm it shows its *own* independent baseline diff, not tab A's.

---

## Milestone 4 — Usage Tab — DEFERRED (explicitly deprioritized by user, low value for now)

Ref: `SPEC.md` § "Usage Tab". Nothing below has been built; left in place as the design if/when this gets picked back up.

### 4.1 `App.Usage.TranscriptLogReader`
- [ ] Compute `sanitized-cwd` from the project root path (replace path separators with `-`; confirm the exact replacement rule against more than one real example path on this machine, including a path with a space in it, before assuming the simple separator-swap is the complete rule).
- [ ] Locate `~/.claude/projects/<sanitized-cwd>/*.jsonl`; handle the case where the directory doesn't exist yet (brand-new project with no Claude Code sessions logged yet) without error.
- [ ] For each `.jsonl` file: open with `FileShare.ReadWrite`, track a byte offset.
- [ ] On file growth (poll ~1s, or react to a `FileSystemWatcher` on the log directory if simpler than polling — decide one approach and use it consistently): read only the newly appended bytes, split on `\n`, hold back a trailing partial line (no newline yet) until more data arrives.
- [ ] Parse each complete line as JSON; only act on `"type":"assistant"` entries; pull `sessionId`, `cwd`, `timestamp`, `gitBranch`, `message.model`, `message.usage.*` exactly per the documented schema.
- [ ] Defensive parsing: wrap each line's parse in try/catch; skip and log unparseable lines rather than crashing the reader (log format drift, or a genuinely mid-write line that still had a `\n` but incomplete JSON — rare but possible).
- [ ] Detect and handle new `.jsonl` files appearing while the app is running (a new Claude Code session started in a tab creates a new log file) — the reader should pick these up without an app restart.

### 4.2 `App.Usage.UsageAggregator`
- [ ] Per-session running totals: sum `input_tokens`, `cache_creation_input_tokens`, `cache_read_input_tokens`, `output_tokens`, `thinking_tokens`.
- [ ] Per-model totals (group by `message.model`).
- [ ] A timeline structure (e.g. bucketed by hour or by individual message timestamp) sufficient to compute the rolling 5-hour window (4.4).
- [ ] Cache-read-vs-fresh-input breakdown as a derived view (`cache_read_input_tokens` vs `input_tokens + cache_creation_input_tokens`).

### 4.3 `App.Usage.ModelContextSizeTable`
- [ ] Static lookup: `"claude-sonnet-5"`, `"claude-opus-5"`, `"claude-fable-5-1"` (and other 5-class models) → 1,000,000; `"claude-sonnet-4-5"`, `"claude-haiku-4-5-*"` → 200,000.
- [ ] Fallback for unrecognized model strings → 200,000, paired with a visible "model not recognized, showing conservative estimate" UI note (don't silently guess without flagging it).
- [ ] Put this in one clearly-named file/constant with a comment-free but discoverable location (e.g. top of the file, obvious from the class name) — spec flags this table as needing periodic manual updates as new models ship, so it should be trivial to find and edit later.

### 4.4 `App.UI.UsageTabControl`
- [ ] Context-window gauge for the active session tab: `(input_tokens + cache_creation_input_tokens + cache_read_input_tokens)` from the most recent assistant message in that session, against `ModelContextSizeTable` lookup for that message's model. Display as "X / Y tokens (Z%) context used this session."
- [ ] Rolling 5-hour total across all sessions for the current project, clearly labeled as an estimate (not an official quota) per the exact framing in `SPEC.md`.
- [ ] Per-model and per-session breakdown view (table or simple list) sourced from `UsageAggregator`.
- [ ] Live-updating as `TranscriptLogReader` picks up new lines while sessions are active (don't require a manual refresh).
- [ ] Manual verification (per `SPEC.md`'s Verification section): compare displayed aggregates for one session against a manual line-count/grep check on that session's real `.jsonl` file.

---

## Milestone 5 — Repository Maintenance — DEFERRED (explicitly deprioritized by user, low priority for now)

Ref: `SPEC.md` § "Repository Maintenance (git gc)". Nothing below has been built; left in place as the design if/when this gets picked back up. (The dangling baseline objects this exists to clean up are still being created by `BaselineBuilder` either way — see `SPEC.md`'s "accept it as negligible for v1" framing, which is effectively now the operative choice until this milestone is revisited.)

### 5.1 `App.Core.SettingsStore`
- [ ] Load/save `.sain/settings.json` with the documented shape (`version`, `gcIntervalMinutes`, default `60`).
- [ ] Same corrupt-file handling pattern as `PresetStore` (2.3): back up and recreate defaults rather than crash on a bad file.
- [ ] Designed to hold future settings beyond `gcIntervalMinutes` without a schema rewrite (e.g. a flat dictionary-like model, or just add fields as needed later — don't over-engineer an extensibility mechanism for settings that don't exist yet).

### 5.2 `App.Diff.GcScheduler`
- [ ] Timer driven by `SettingsStore`'s current `gcIntervalMinutes` (support changing the interval at runtime from `SettingsDialog` without restarting the app — re-arm the timer on a settings change).
- [ ] In-flight gate: a simple shared flag/semaphore that `BaselineBuilder`/`TabDiffEngine` operations set while running; `GcScheduler` checks it immediately before firing `git gc --auto` and skips the tick (waiting for the next one) if any tab's git operation is in progress.
- [ ] Shell out to `git gc --auto` as a `Process` run with the project root as working directory; capture stderr/exit code for diagnostics (don't let a failed/non-zero gc crash the app — log and move on).
- [ ] Verify this is the **only** place in the codebase that spawns `git.exe` — grep the finished implementation for any other shell-outs before considering this milestone done, since the whole point of this exception was that it stays narrowly scoped.

### 5.3 `App.UI.SettingsDialog`
- [ ] A simple modal exposing `gcIntervalMinutes` as an editable numeric field, persisted via `SettingsStore` on save.
- [ ] Structured so a future setting can be added as another field in the same dialog without redesigning it.

---

## Milestone 6 — Hardening Pass — Milestones 4 and 5 deferred, so this now follows Milestone 3

Ref: `SPEC.md`'s Build Order item 7, plus loose ends flagged elsewhere in `SPEC.md`.

- [ ] FSW overflow fallback (3.3) — if not already fully exercised in Milestone 3, stress-test it now with a heavier synthetic workload.
- [ ] Multi-tab stale-diff lazy recompute (3.2/3.3) — verify with 3+ tabs open simultaneously, confirm only the active tab eagerly recomputes and inactive ones correctly recompute on switch-to without noticeable lag.
- [ ] VT edge cases discovered from real usage beyond the Milestone 1 checklist — keep a running log of anything that renders incorrectly during normal day-to-day use of the finished app, and patch the vendored VtNetCore copy as needed.
- [ ] Per-monitor DPI-change re-measurement (1.4) — retest after all other milestones, since later UI additions (Usage tab, dialogs) should also be checked for DPI correctness, not just the terminal control.
- [ ] Preset management UI polish (2.5) — richer add/edit/delete flow if the Milestone 2 version was deliberately minimal.
- [ ] Full end-to-end pass: fresh project open → multiple concurrent tabs → live diffing → usage tracking → settings change → gc firing → clean app shutdown, with no leaked processes, no orphaned file handles, no unhandled exceptions, run once start-to-finish as a final sanity check.

---

## Explicitly Out of Scope (per `SPEC.md` — not TODO items, listed here only so they aren't mistaken for gaps)

- No automated test suite beyond the isolated unit tests called out above (3.1, 1.1's UTF-8 boundary logic) — `SPEC.md` defers broader automated testing until core engines stabilize.
- No installer/packaging/distribution story — not addressed anywhere in `SPEC.md`; raise it separately with the user if/when needed.
- No support for presets beyond "executable + args" (no env vars/config file overrides) — considered and explicitly decided against earlier in scoping.
- No multi-project-in-one-window support — a second project is a second app instance, by design.
