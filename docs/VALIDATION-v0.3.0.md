# Helium v0.3.0 validation

## Release identification

- Version: `v0.3.0`.
- Release name: `Navigator Experience`.
- Parent baseline before the final release commit: `da432652eec2e7e15c4dcad49eb69d1b06ba85c5` on `v0.3.0-milestone`.
- Release source identity: the commit containing this document. Its resulting hash is recorded in the external release evidence because a commit cannot contain its own hash without changing that hash.
- Theme ID: `e5d3a4ba-1cc7-42b1-a090-8d4d3a922b23`.
- Extension ID: `HeliumCharlo_b59e3c9f-9c52-4582-a332-0716b56cf023`.
- The final release commit includes the version metadata, final stabilization changes, changelog, release notes, and this validation record.

## QA evidence

The maintainer reported manual acceptance of the candidate in Playnite Portable and the primary Playnite installation. The observed scope included sorting and grouping, Recent Activity, Advanced Filters combined with search and native filters, unified Home navigation, selection visibility after explicit reordering, Escape dismissal, Navigator width persistence, and the QA-030 stability scenario. These are human observations of the installed candidate; they were not independently repeated during this finalization.

The finalization attempt did not alter the portable installation: the harness stopped before deployment because Windows denied access to `PlayniteInstaceMutex`, so it could not prove that every Playnite instance was closed.

## Automated checks executed during finalization

- `LogicTests`: PASS, 18 checks; the 50,000-game scenario completed in 546 ms.
- `NavigatorProjectionTests`: PASS, 47 checks.
- `NavigatorRefreshDecisionTests`: PASS, 44 checks.
- `NavigatorFilterComposerTests`: PASS, 34 checks.
- `SettingsFileTests`: PASS, including missing, persisted, corrupt-backup, recovery, migration, and restart-reload cases.
- Release build: PASS, 0 warnings and 0 errors.
- Harness package structure checks: PASS before the OPSEC scanner step; the paired manifests, stable IDs, matching version, packaged DLL, and archive extraction were verified.
- Previously reported Python groups of 21 and 8 checks were not re-executed and no retained finalization report was used to substantiate their counts; they are not counted as current evidence.

## Advanced Filters integration

| Check | Result | Evidence and remaining manual procedure |
|---|---|---|
| AF-01 — state across restart | BLOCKED | Portable deployment was stopped by the mutex protection. Manually apply filters, exit Playnite normally, reopen it, and compare the native filter state with Playnite's normal persistence behavior. |
| AF-02 — user preset integrity | BLOCKED | Before testing, record saved preset names and settings; use and clear Advanced Filters, then confirm that no saved preset was added, removed, renamed, or overwritten. |
| AF-03 — external preset synchronization | BLOCKED | Activate a native Playnite preset and confirm the Advanced Filters controls reflect it. Record timing only with a reliable measurement. |
| AF-04 — clear and AND/OR semantics | PARTIALLY VERIFIED | The 34 pure-logic checks verify deep copying, `Guid.Empty` temporary presets, null sort/group options, unrelated-condition preservation, and Clear preserving AND/OR while removing active conditions. A real Playnite GUI check remains required. |
| AF-05 — real-library performance | BLOCKED | Open Advanced Filters with a metadata-rich portable library and observe genre, tag, and category population for substantial freezes or sustained degradation. |

## Artifact inspection

- `Helium-charlo-fork-v0.3.0.pthm`: SHA-256 `33AEEC3456865A7B03B21D2774ADC6F432CCA9747142C0A3337227E439685F2B`.
- `Helium-Navigator-v0.3.0.pext`: SHA-256 `199D98226DFCD9CD7FF314DA911A5E350CA0B6819947FD2773F3E8E0EA57D2A0`.
- `HeliumCharlo.dll` inside the extension package: version `0.3.0.0`, SHA-256 `32A7CCCB46CCA7870B34B0C6907278E32EB42030A359E3E8747D20D22317A506`.
- The repository's Python artifact scanner was not executed because the available `python.exe` was an inaccessible WindowsApps shim. A separate read-only scan inspected archive names and contents for drive-rooted, UNC, and `/home` or `/Users` paths in single-byte and UTF-16 representations, absolute CodeView paths, PDB files, unsafe member names, and oversized members; it passed both binary packages. This equivalent scan does not replace an execution of the repository's Python test suite.
- The Python package validator was not executed for the same environment reason. Equivalent read-only checks verified 65 XAML/XML files, the exact package member sets, paired version `0.3.0`, stable IDs, the two-file extension layout, the packaged DLL identity, and the 194-file theme layout.
- The 249-entry source archive contained no local identity markers and no unapproved email addresses. Its only generic absolute-path matches are intentional synthetic fixtures in `tests/ArtifactOpsecTests.py`, which exercise the scanner without containing the maintainer's real environment data.
- The source ZIP hash is intentionally stored only in the external `SHA256SUMS-v0.3.0.txt` generated after the archive, avoiding a self-referential checksum.

## Known limitations and publication status

- AF-01, AF-02, AF-03, AF-05, and the runtime portion of AF-04 remain unverified in the finalization environment.
- Advanced Filters retains the runtime verification gaps listed above; these are documented limitations rather than automated passes.
- No claim is made that the build is reproducible across independent toolchains or that every optional Playnite integration was tested.
- A local release commit was created with the approved noreply identity. No push, tag, release, or primary Playnite modification was performed.
