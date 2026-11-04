# Navigator UX conventions

**Before refining UI copy, challenge the necessity of the element itself. If an element adds no actionable information, remove it instead of explaining it better.**

- Question whether a control, label, warning, or help block needs to exist before rewriting it. Remove redundant elements instead of adding helper text or polishing an explanation for unnecessary UI.
- Show controls only when they apply. Keep implementation states out of the interface unless they help the user act. Avoid repeating information another control already communicates.
- Keep explanations that help users understand a choice, consequence, or error. Preserve accessible names, state, and keyboard behavior for the controls that remain.

For example, **Following Playnite** already tells users who controls the sort. Hide the entire **Order** section in that mode, including its heading and separator. Show the direction choices again for an independent sort and retain the user's previous direction.

## Controls and layout

- Use short labels for actions and fields. Put help beside a choice when it explains a meaningful difference, such as how Hybrid and Detailed group older activity. Use tooltips or validation messages for ranges and constraints.
- Use dropdowns for choosing an item and text fields for entering text. Show the current value and support keyboard navigation, type-ahead where applicable, Escape, and predictable focus return.
- Dim unavailable actions when they remain relevant to the task. Preserve the user's preferences when switching modes.
- Put related settings and actions together. Align controls with their labels, use consistent padding and typography, and let narrow layouts wrap or scroll without clipping.
- Keep group headers concise and use counts that describe the displayed rows. Use neutral empty and missing-data labels. For example, “No Recorded Activity” describes missing data without guessing whether a game was played.
- Give controls accessible names, including search fields with placeholders. Ensure selection, focus, and disabled states are visible without relying on color alone.
- Verify changes in isolated Playnite Desktop. Check mouse and keyboard input, focus, narrow windows, and settings after reopening. Keep screenshots with personal library data out of the repository.

- Short option descriptions and helper fragments omit terminal periods. Keep ellipses for search placeholders and punctuation for complete error messages.
- Put paired customization lists in equal-width panels when space permits and stack them on narrow windows. Preserve each list's search, selection, and scroll state while resizing. Keep checkbox visibility, row selection, and keyboard focus visually distinct.
