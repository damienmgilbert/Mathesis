---
name: mathesis-planning
description: Plan, hand off and review Mathesis work done by Claude Code — draft milestone plans (PLAN-M2.md and later) and phase prompts, decide what a phase must read and prove, review a phase summary or diff against its "Done when" checks, and keep the design docs (00–10, d1–d9) consistent when decisions change. Use whenever the user mentions a Mathesis phase or milestone, PLAN.md, CLAUDE.md, exit checks, "what's next", a Claude Code summary to check, or wants to change, reconcile or record a design decision (ADR) in the docs.
---

# Mathesis planning and review

Mathesis is built by Claude Code, one phase per session, from `Plan 1 Math library.md` (saved in the repo as `PLAN.md`) and the design docs in `docs/design/`. This Project holds the same docs. The user's job in chat is usually one of four things: prepare the next prompt, check what Claude Code reported, change a design decision cleanly, or plan the next milestone. Each has a workflow below.

Always `project_read` the documents involved instead of relying on memory; phases cite exact sections, and small differences (a tolerance, a corpus size) are what the exit checks test.

## 1. Preparing a phase prompt

1. Read the phase in the plan: its **Read first** docs, bullet scope and **Done when** checks.
2. Check prerequisites: earlier phases done, any "Decide first" or "Still open" item that this phase depends on (e.g. display rounding must be confirmed before Phase 4). If something is open, ask the user to decide before writing the prompt.
3. Produce the prompt in the plan's own wording, ready to paste:

   ```text
   Do Phase N of PLAN.md. Read the docs its "Read first" line names before writing code.
   Stop when its exit checks pass and summarize what changed.
   ```

   Add only what the user decided in chat that isn't in the repo yet (a new decision, a correction to a doc), and say whether the doc itself should be updated first — the repo's docs are what Claude Code reads, so a decision that lives only in a prompt is lost next session.
4. List what to look for in the summary (the Done-when checks as a checklist), so the user can verify quickly.

## 2. Reviewing a phase result

Given Claude Code's summary, a diff, test output or a pasted file:

1. Build a table: `Done-when check | evidence in the result | status (met / not shown / failed)`. "Not shown" is not "met" — ask for the command output that proves it (test counts, policy-check output, coverage report, AOT publish warnings).
2. Check the standing rules from `CLAUDE.md` (in the plan): .NET 10 only; package policy (`dotnet run eng/policy-check.cs` after package changes); no rule without a `.mlaw` entry; conditions and tolerances not weakened; `Outcome` instead of exceptions; `Budget` honored; MSTest; inlined small helpers; existing formatting preserved; never copied NR/GPL code.
3. Look for silent design drift: renamed types or namespaces vs `03-namespaces-and-packages.md`, new packages, different defaults from "Decide first", tolerances different from the phase text, corpus sizes below the stated minimum.
4. Give a verdict — done, done with follow-ups, or not done — and the exact next prompt (a fix-up prompt for Claude Code, or the next phase prompt).

## 3. Changing a design decision

The docs cross-reference each other heavily; a change in one place usually needs edits in several.

1. Find every place the decision appears with `project_search` (and by reading the likely docs: decisions often live in `02` ADRs, `05` conventions, `06` `conv.*` entries, the domain doc's Conventions table, and the plan's "Decide first"/CLAUDE.md block).
2. Present the change as a list of `doc → section → current text → new text`. Keep each doc's formatting exactly; change only the text the decision touches.
3. New architecture decisions get the next ADR number in `02-architecture.md`'s table with the same columns (ID, Decision, Chosen, Main alternative, Why).
4. Conventions already decided (name `Mathesis`, MIT, real mode with `I` as the imaginary unit, exact decimals, bare `log` base 10, 0⁰ = 1, real odd roots) are marked "do not change" in the plan. If asked to change one, point out what depends on it before editing.
5. Write updated docs back to the Project only when the user asks; then `project_write` the full updated content to the same path.

## 4. Planning the next milestone

The roadmap (`10-roadmap.md`) ends with the prompt Claude Code uses to draft `PLAN-M<N+1>.md`. When the user wants to plan in chat instead:

- Follow `PLAN.md`'s format: How to run it, Decide first, Scope (delivers / leaves out), Layout changes, phases each with **Read first** and **Done when**, and any spec sections.
- Size phases so each ends with a green build and is completable in one Claude Code session; every phase's Done-when must be checkable by a command or a test, with numbers (corpus sizes, tolerances, iteration caps, coverage %).
- Pull the catalog IDs to seed from the milestone's domain docs and state the coverage target (≥ 95% for completed domains).
- Respect milestone dependencies (3, 6 and 7 can run in parallel with 2, 4 and 5 after Milestone 1).

## Style for outputs

- Lead with the verdict or the paste-ready prompt; reasoning after.
- Tables for checklists and doc-change lists. Quote doc text only as much as needed to locate an edit.
- Be blunt about gaps: a phase without evidence for an exit check is not done.
