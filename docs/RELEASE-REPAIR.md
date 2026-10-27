# Historical release OPSEC repair

## Scope

These release assets were found to contain developer-machine paths in their
compiled Navigator DLLs. Only the `.pext` extensions require replacement.
The `.pthm` theme assets remain untouched.

| Release tag | Original .pext SHA-256 |
| --- | --- |
| `v0.2.5` | `1d15227ebf74372963a6e83b007799b7b055791d97603b38572b9513ad00f8df` |
| `v.0.2.6` | `e6d91b376e9138e8a839fd9f8b901af565f061023fa1a8a1263b76e04f0f9357` |
| `v0.2.7` | `3768d56c3d26df183f082152a96677996cf61fa1662db4e596b2287754ca4c96` |

## Procedure

1. After the verified repair PR merges, the **Historical release repair candidates**
   workflow runs automatically on `main` and builds all three packages. A
   manual run is also supported. PR-run artifacts are for preliminary review
   **only**. The matrix checks out the exact original release commits; it does
   not modify release tags or package the latest Navigator under an old version.
2. Download `helium-fixed-0.2.5`, `helium-fixed-0.2.6`, and
   `helium-fixed-0.2.7` from the successful **main-branch build run**.
   Each contains a `.pext` and `release-evidence.json` with the pinned source
   commit and candidate SHA-256. Review the CI log and evidence.
3. Test the **exact SHA-256** of each candidate in the isolated portable
   Playnite environment, not the main installation. Confirm the Navigator
   loads, selection and filtering work, and the extension version agrees
   with its theme. Do not claim these tests happened until actually observed.
4. For each successfully tested version, open **Publish inspected historical
   release asset** on the default branch, enter the corresponding *successful
   main-branch rebuild run ID*, exact release tag, and the SHA-256 of
   the file tested. Confirm runtime testing. This workflow downloads the
   archived CI package, verifies the hash against its provenance evidence and
   the exact tag commit, and replaces **only the corresponding `.pext`**
   after verification. The published digest is checked after upload.
5. Check all three public release pages; record the new extension SHA-256
   values and retain the original hashes above for the incident audit.

Do not touch `.pthm` packages, change tag refs, force-push or rewrite release
history. GitHub's `--clobber` replaces existing asset contents but cannot
revoke older copies that were already downloaded.

The scanner is a focused absolute-path check, not a general credentials/secret
scanner. No CI build here is evidence of full Playnite UI compatibility.

**Limitation:** Direct uploads via GitHub's web UI can bypass this workflow;
release permissions and required organization protections were not verified.
