# Helium development instructions

- Work from the current approved branch. Investigate, implement, test and commit approved changes without pausing between routine steps.
- Preserve Playnite Desktop SDK contracts, theme and extension IDs, XAML host names, native commands, settings compatibility and existing click behavior.
- Never change the main Playnite installation. Use the isolated portable sandbox with the harness process, mutex, backup and rollback checks intact.
- Run focused tests during development and a full build, package validation and relevant Playnite checks before calling a candidate ready. Distinguish code checks from visual observations; report untested behavior.
- Keep changes small and documentation concise. Do not add features or refactor outside the approved scope.
- Before every commit, review the complete diff, new files and Git metadata for personal names, absolute local paths, private email, credentials, logs, screenshots, library data and other private evidence. Use environment variables, relative paths or generic placeholders in public files.
- Commit with nocharlo@users.noreply.github.com as both author and committer. Never use a personal email.
- Never force push, rewrite history, publish or modify releases, or alter the main Playnite installation without explicit user authorization.
