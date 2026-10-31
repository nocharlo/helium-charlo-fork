# Helium — Development Instructions

## 1. Project Scope & Architecture

Helium (charlo fork) is a Playnite 10 Desktop theme and companion Navigator extension. It builds on the original Helium theme and the KKCD fork.

The goal is to provide Steam-inspired library navigation while preserving Playnite's native functionality, SDK contracts, and compatibility with existing extensions.

Main architectural responsibilities:

- `LibraryStateAdapter`: Access and interpret Playnite library data and state.
- `NavigatorProjection`: Transform game collections through search, sorting, and grouping.
- `NavigatorRefreshDecision`: Determine when and how Navigator updates are required.
- `NavigatorGroupState`: Preserve grouping and expansion-related state.
- `NavigatorFilterComposer`: Compose filters using Playnite's native filtering model.
- `NavigatorFilterPanel`: Present filtering controls.
- `NavigatorPresentation`: Handle Navigator presentation-related logic.
- `Navigator.cs`: Coordinate UI events, navigation, selection, and refresh behavior.
- `theme/`: XAML/WPF presentation and native Playnite view integration.
- `tests/`: Automated checks for supported logic and behaviors.

Inspect the actual implementation before assuming these responsibilities are perfectly isolated. Do not introduce redundant state or parallel implementations of existing functionality.

## 2. Behavioral Contracts

The following behaviors are project invariants. Preserve them unless the user explicitly approves changing their design.

- Navigator selection must remain synchronized with the supported Grid and Details views.
- Switching between supported views must preserve valid selection and navigation state.
- Navigator search must coexist correctly with Playnite's native/global filters.
- Independent Navigator sorting and grouping must not unexpectedly modify the Grid's native organization.
- Following Playnite must correctly reflect the supported native organization.
- Refresh operations must preserve selection, scroll position, and expansion state whenever applicable.
- Filtering, grouping, sorting, and navigation must behave consistently when combined.
- Installed and favorite indicators must reflect the relevant Playnite data.
- Persisted settings must survive restarts, supported upgrades, and normal configuration reloads.
- Existing serialized settings values must not be repurposed or renumbered without an explicit compatibility and migration strategy.
- Existing click behavior, XAML host names, native commands, theme IDs, and extension IDs must remain compatible.
- The current Home action returns to the Grid; do not reinterpret it as the future Helium Home feature without approval.

Do not solve synchronization problems by introducing independent sources of truth when Playnite already owns the relevant state.

## 3. Implementation Guidelines

- Work from the current approved branch and within the explicitly approved scope.
- Investigate, implement, test, and commit approved changes without requesting confirmation for routine intermediate steps.
- Prefer small, focused changes over broad rewrites.
- Keep business and transformation logic separate from WPF event handlers whenever practical.
- Prefer pure, testable logic outside `Navigator.cs`.
- Avoid increasing the responsibilities of `Navigator.cs` unnecessarily.
- Reuse existing adapters, projection logic, and state-management components before creating new abstractions.
- Preserve backwards compatibility unless a breaking change is explicitly approved.
- Avoid duplicated event subscriptions, recursive selection updates, unnecessary refreshes, and avoidable UI-thread work.
- Do not introduce unrelated features, speculative abstractions, or opportunistic refactors.
- Do not silently change the project's established UX to simplify implementation.
- Avoid replacing working native Playnite behavior with custom alternatives without a demonstrated need.

When multiple solutions are viable, prefer the simplest maintainable implementation that preserves existing contracts.

## 4. Bug Investigation

For bug fixes, follow this process:

1. Reproduce the problem or establish a reliable, evidence-backed failure scenario.
2. Identify the relevant state transitions, event handlers, and component boundaries.
3. Investigate the root cause before applying a workaround.
4. Implement the smallest appropriate correction.
5. Add or update regression tests where the behavior is testable.
6. Verify affected neighboring behaviors.
7. Report any remaining limitations or unverified assumptions.

Do not conceal problems with additional state flags, arbitrary delays, repeated refreshes, or broad exception handling without technical justification.

Do not turn a localized bug fix into an architectural rewrite without approval.

## 5. Testing & Validation

Use the repository's established test projects, build procedures, packaging checks, and isolated Playnite testing harness.

- Run focused tests for modified logic.
- Run the complete automated test suite before declaring a release candidate ready.
- Validate the release build and relevant theme/extension package structures.
- Verify matching component versions, stable IDs, and expected package contents when packaging changes.
- Preserve the isolated portable Playnite sandbox, including its process, mutex, backup, and rollback safeguards.
- Never alter or use the user's main Playnite installation for automated testing.
- Do not remove, disable, or weaken tests merely to obtain a passing result.
- Distinguish automated test results from code inspection and manual UI validation.
- Never claim a WPF interaction was tested in Playnite unless it was actually exercised in a running Playnite instance.
- Treat popup focus, keyboard navigation, resizing, native filter integration, and selection synchronization as UI integration concerns.
- Report relevant checks as `PASS`, `FAIL`, or `NOT RUN`, with concise explanations where necessary.

A successful build or passing unit test suite does not establish that interactive behavior is correct.

Do not declare changes fully validated when required checks remain incomplete.

## 6. Repository Safety 

Before every commit, inspect the complete diff, staged files, new files, and Git metadata.

Do not expose:

- Personal names, private email addresses, or identifying account information.
- Absolute local filesystem paths or private directory structures.
- Credentials, tokens, secrets, or authentication material.
- Personal Playnite library exports, screenshots, logs, or testing evidence unless explicitly approved for publication.
- Machine-specific settings or private development artifacts.

Use relative paths, environment variables, and generic placeholders in public documentation.

Preserve existing ignore rules and privacy safeguards. Never commit sensitive files merely because they are needed locally for testing.

Use `nocharlo@users.noreply.github.com` for both Git author and committer email.

## 7. Documentation & Release Policy

- Keep documentation accurate, concise, and consistent with the implemented behavior.
- Update relevant documentation when functionality, compatibility, configuration, or testing expectations change.
- Record material limitations and unverified behaviors rather than presenting them as completed work.
- Keep version-specific plans and detailed validation reports outside `AGENTS.md`.
- Preserve attribution and inherited licensing requirements.
- Do not claim compatibility with configurations that have not been tested.
- Do not publish or modify releases without explicit user authorization.
- Do not force-push, rewrite Git history, or perform destructive repository operations without explicit authorization.
- Do not modify the user's main Playnite installation without explicit authorization.
- Approved implementation work may be committed after reviewing the diff and completing applicable checks.
- Approval to implement a change does not authorize unrelated functionality, breaking changes, or release publication.

## 8. Completion Report

After an implementation task, provide a concise report containing:

- What changed and why.
- Which components and files were affected.
- Tests and validation performed, including failures and checks not run.
- Known limitations, compatibility concerns, and remaining risks.
- Commit reference, when applicable.

Separate verified behavior from assumptions.

The objective is not merely to make the requested feature work. It is to make the change understandable, testable, compatible, and safe to maintain.
