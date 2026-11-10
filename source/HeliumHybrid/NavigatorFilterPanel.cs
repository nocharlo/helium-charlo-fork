using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Playnite.SDK.Models;

namespace HeliumHybrid
{
    public sealed class FilterWorkspaceColumns : Panel
    {
        private int? preferredColumns;
        private int columns;

        internal void SetWorkspaceWidth(double width)
        {
            var count = FilterWorkspaceCatalog.Columns(width);
            if (preferredColumns == count) return;
            preferredColumns = count;
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? 790 : availableSize.Width;
            columns = preferredColumns ?? FilterWorkspaceCatalog.Columns(width);
            var heights = new double[columns];
            var constraint = new Size(width / columns, double.PositiveInfinity);
            for (int index = 0; index < InternalChildren.Count; index++) {
                var child = InternalChildren[index];
                child.Measure(constraint);
                heights[index % columns] += child.DesiredSize.Height;
            }
            return new Size(width, heights.Max());
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var offsets = new double[columns];
            var width = finalSize.Width / columns;
            // Fill across columns in the saved order, then stack each column independently.
            for (int index = 0; index < InternalChildren.Count; index++) {
                var child = InternalChildren[index];
                var column = index % columns;
                var height = child.DesiredSize.Height;
                child.Arrange(new Rect(column * width, offsets[column], width, height));
                offsets[column] += height;
            }
            return finalSize;
        }
    }

    // Keeps the filter controls alive across native state changes and responsive reflow.
    internal sealed class NavigatorFilterPanel
    {
        private const int PageSize = 16;
        private readonly Func<FilterWorkspaceField, int?, IEnumerable<FilterWorkspaceChoice>> loadChoices;
        private readonly Preferences preferences;
        private readonly Border frame;
        private readonly FilterWorkspaceColumns host;
        private readonly WrapPanel active;
        private readonly TextBox search;
        private readonly TextBlock empty;
        private readonly Action<Func<FilterPresetSettings, FilterPresetSettings>> apply;
        private readonly Dictionary<string, GroupView> groups = new Dictionary<string, GroupView>();
        private readonly DispatcherTimer searchTimer, textTimer;
        private readonly Dictionary<FilterWorkspaceField, string> pendingText = new Dictionary<FilterWorkspaceField, string>();
        private FilterPresetSettings shown;
        private int choiceRevision;
        private bool layoutDirty = true, syncingText;
        internal bool HasManagedSelection => active.Children.Count > 0;

        internal NavigatorFilterPanel(Preferences preferences, Border frame,
            FilterWorkspaceColumns host, WrapPanel active, TextBox search, TextBlock empty,
            Func<FilterWorkspaceField, int?, IEnumerable<FilterWorkspaceChoice>> loadChoices,
            Action<Func<FilterPresetSettings, FilterPresetSettings>> apply)
        {
            this.loadChoices = loadChoices;
            this.preferences = preferences;
            this.frame = frame;
            this.host = host;
            this.active = active;
            this.search = search;
            this.empty = empty;
            this.apply = apply;
            searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            searchTimer.Tick += (s, e) => { searchTimer.Stop(); UpdatePresentation(); };
            search.TextChanged += (s, e) => { searchTimer.Stop(); searchTimer.Start(); };
            textTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            textTimer.Tick += (s, e) => FlushText();
        }

        internal void Position(Popup popup, Window window)
        {
            if (window == null) return;
            popup.PlacementTarget = window;
            frame.Width = Math.Max(1, Math.Min(820, window.ActualWidth - 20));
            frame.MaxHeight = Math.Max(1, Math.Min(610, window.ActualHeight - 32));
            // The vertical scrollbar must not change the number of columns when a field opens.
            host.SetWorkspaceWidth(frame.Width - frame.Padding.Left - frame.Padding.Right
                - frame.BorderThickness.Left - frame.BorderThickness.Right);
        }

        internal CustomPopupPlacement[] Place(Size popup, Size target, Point offset) =>
            new[] { new CustomPopupPlacement(new Point(Math.Max(0, (target.Width - popup.Width) / 2),
                Math.Max(0, (target.Height - frame.MaxHeight) / 2)), PopupPrimaryAxis.None) };

        internal void InvalidateLayout() => layoutDirty = true;

        internal void Refresh(FilterPresetSettings current, bool reloadChoices = false)
        {
            bool initialize = groups.Count == 0;
            if (initialize) CreateControls();
            bool stateChanged = !NavigatorFilterComposer.Same(shown, current);
            if (stateChanged) shown = NavigatorFilterComposer.Copy(current);
            if (reloadChoices) choiceRevision++;
            if (initialize || reloadChoices || stateChanged) UpdateChips();
            if (initialize || reloadChoices || stateChanged || layoutDirty) UpdatePresentation();
        }

        internal void FlushText()
        {
            textTimer.Stop();
            if (pendingText.Count == 0) return;
            var edits = pendingText.ToArray();
            pendingText.Clear();
            Change(state => {
                foreach (var edit in edits) edit.Key.SetText(state, edit.Value);
            });
        }

        internal void ClearAll()
        {
            textTimer.Stop();
            pendingText.Clear();
            apply(NavigatorFilterComposer.ClearManaged);
            UpdatePresentation();
        }

        private void Change(Action<FilterPresetSettings> edit) => apply(state => {
            var next = NavigatorFilterComposer.Copy(state);
            edit(next);
            return next;
        });

        private void CreateControls()
        {
            foreach (var group in FilterWorkspaceCatalog.Groups) {
                var groupView = new GroupView(group);
                groups.Add(group.Id, groupView);
                foreach (var model in group.Fields) {
                    var field = new FieldView(model, (key, selected) => {
                        FlushText();
                        Change(state => model.SetChoice(state, key, selected));
                    }, () => {
                        pendingText.Remove(model);
                        Change(model.Clear);
                        UpdatePresentation();
                    });
                    groupView.Fields.Add(model.Id, field);
                    if (field.Input != null) {
                        field.Input.TextChanged += (s, e) => {
                            if (syncingText) return;
                            pendingText[model] = field.Input.Text;
                            textTimer.Stop();
                            textTimer.Start();
                        };
                    }
                    field.Opened = () => UpdateField(field, search.Text.Trim());
                }
            }
        }

        private void UpdateChips()
        {
            active.Children.Clear();
            foreach (var field in groups.Values.SelectMany(group => group.Fields.Values)) {
                var model = field.Model;
                if (model.Text != null) {
                    var value = model.Text(shown);
                    if (!string.IsNullOrWhiteSpace(value)) AddChip(model.Label, value, () => {
                        pendingText.Remove(model);
                        Change(model.Clear);
                    });
                    continue;
                }
                var selected = model.Selected(shown).Distinct().ToArray();
                if (selected.Length > 0) {
                    field.EnsureChoices(choiceRevision, loadChoices, field.NeedsAllChoices);
                    if (selected.Any(key => !field.HasChoice(key))) field.EnsureChoices(choiceRevision, loadChoices, true);
                }
                foreach (var key in selected) {
                    AddChip(model.Label, field.Label(key), () => Change(state => model.SetChoice(state, key, false)));
                }
            }
            active.Visibility = HasManagedSelection ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddChip(string field, string value, Action remove)
        {
            var label = field + ": " + value;
            var button = new Button {
                Content = new TextBlock { Text = label + "  ×", TextTrimming = TextTrimming.CharacterEllipsis },
                MaxWidth = 220, ToolTip = "Remove " + label, Margin = new Thickness(0, 0, 4, 4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            AutomationProperties.SetName(button, "Remove " + label);
            button.Click += (s, e) => remove();
            active.Children.Add(button);
        }

        private void UpdatePresentation()
        {
            if (shown == null) return;
            var query = search.Text.Trim();
            var layout = preferences.FilterWorkspace;
            if (layoutDirty) {
                foreach (var item in layout) {
                    if (item.Hidden || !groups.TryGetValue(item.Id, out var group)) continue;
                    group.Panel.Children.Clear();
                    foreach (var id in item.FieldOrder) {
                        if (!item.HiddenFields.Contains(id) && group.Fields.TryGetValue(id, out var field))
                            group.Panel.Children.Add(field.Root);
                    }
                }
                layoutDirty = false;
            }
            bool found = false;
            var visibleGroups = new List<UIElement>();
            foreach (var item in layout) {
                if (item.Hidden || !groups.TryGetValue(item.Id, out var group)) continue;
                bool hasVisible = false;
                foreach (var id in item.FieldOrder) {
                    if (item.HiddenFields.Contains(id) || !group.Fields.TryGetValue(id, out var field)) continue;
                    found |= UpdateField(field, query);
                    hasVisible |= field.Root.Visibility == Visibility.Visible;
                }
                if (hasVisible) visibleGroups.Add(group.Root);
            }
            // Only sections with visible fields take up a column slot.
            for (int index = host.Children.Count - 1; index >= 0; index--)
                if (!visibleGroups.Contains(host.Children[index])) host.Children.RemoveAt(index);
            for (int index = 0; index < visibleGroups.Count; index++) {
                var section = visibleGroups[index];
                if (host.Children.IndexOf(section) == index) continue;
                host.Children.Remove(section);
                host.Children.Insert(index, section);
            }
            empty.Visibility = query.Length > 0 && !found ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool UpdateField(FieldView field, string query)
        {
            var model = field.Model;
            var text = model.Text?.Invoke(shown);
            var selected = model.Selected == null ? new HashSet<object>() : new HashSet<object>(model.Selected(shown));
            if (model.Choices != null && (field.IsOpen || query.Length > 0)) {
                field.EnsureChoices(choiceRevision, loadChoices, query.Length > 0 || field.NeedsAllChoices);
            }
            var fieldMatch = NavigatorFilterComposer.OptionMatches(model.Label, query);
            var matches = fieldMatch || (text != null && NavigatorFilterComposer.OptionMatches(text, query)) ||
                field.Options.Any(option => NavigatorFilterComposer.OptionMatches(option.Label, query));
            var count = model.Text != null ? (string.IsNullOrWhiteSpace(text) ? 0 : 1) : selected.Count;
            field.SetHeader(count);
            field.Root.Visibility = matches || count > 0 ? Visibility.Visible : Visibility.Collapsed;
            field.Clear.Visibility = count > 0 && model.InlineChoiceLimit != 1 ? Visibility.Visible : Visibility.Collapsed;
            if (field.Input != null && !pendingText.ContainsKey(model) && field.Input.Text != (text ?? "")) {
                syncingText = true;
                try { field.Input.Text = text ?? ""; }
                finally { syncingText = false; }
            }
            field.ApplySearch(query, matches);
            if (field.IsOpen && field.Input == null)
                field.RenderChoices(query, selected, model.ExtraCondition?.Invoke(shown));
            return matches;
        }

        private sealed class SectionExpander : Expander
        {
            private bool? beforeSearch;
            internal bool ApplyingSearch { get; private set; }

            internal void ApplySearch(string query, bool matches)
            {
                bool expanded;
                if (query.Length > 0) {
                    if (!matches || beforeSearch.HasValue) return;
                    beforeSearch = IsExpanded;
                    expanded = true;
                } else {
                    if (!beforeSearch.HasValue) return;
                    expanded = beforeSearch.Value;
                    beforeSearch = null;
                }
                ApplyingSearch = true;
                try { IsExpanded = expanded; }
                finally { ApplyingSearch = false; }
            }
        }

        private sealed class GroupView
        {
            internal readonly StackPanel Root = new StackPanel { Margin = new Thickness(2, 0, 11, 10) };
            internal readonly StackPanel Panel = new StackPanel();
            internal readonly Dictionary<string, FieldView> Fields = new Dictionary<string, FieldView>();

            internal GroupView(FilterWorkspaceGroup group)
            {
                var heading = new TextBlock { Text = group.Label, TextWrapping = TextWrapping.Wrap };
                heading.SetResourceReference(FrameworkElement.StyleProperty, "FilterSectionHeadingStyle");
                Root.Children.Add(heading);
                Root.Children.Add(Panel);
            }
        }

        private sealed class FieldView
        {
            internal readonly FilterWorkspaceField Model;
            internal readonly FrameworkElement Root;
            internal readonly TextBox Input;
            internal readonly Button Clear;
            internal Action Opened;
            private readonly Panel choices;
            private readonly SectionExpander disclosure;
            private readonly TextBlock label;
            private readonly TextBlock message = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap };
            private readonly Button more = new Button();
            private readonly Button fewer = new Button { Content = "Show fewer" };
            private readonly Action<object, bool> edit;
            private Dictionary<object, FilterWorkspaceChoice> byKey = new Dictionary<object, FilterWorkspaceChoice>();
            private FilterWorkspaceChoice[] options = new FilterWorkspaceChoice[0];
            private readonly Dictionary<object, CheckBox> controls = new Dictionary<object, CheckBox>();
            private string lastQuery;
            private int limit;
            private int loadedRevision = -1;
            private bool allChoicesLoaded;
            private int DefaultLimit => Model.InlineChoiceLimit > 0 ? Model.InlineChoiceLimit : PageSize;
            internal bool IsOpen => disclosure == null || disclosure.IsExpanded;
            internal bool NeedsAllChoices => Model.InlineChoiceLimit == 0 || limit > DefaultLimit;
            internal IEnumerable<FilterWorkspaceChoice> Options => options;

            internal FieldView(FilterWorkspaceField model, Action<object, bool> edit, Action clear)
            {
                Model = model;
                this.edit = edit;
                limit = DefaultLimit;
                var panel = new StackPanel { Margin = model.IsInline ? new Thickness(0, 0, 0, 5) : new Thickness(12, 1, 0, 4) };
                Clear = new Button { Content = "Clear", HorizontalAlignment = HorizontalAlignment.Right };
                Clear.SetResourceReference(FrameworkElement.StyleProperty, "FilterSmallButtonStyle");
                AutomationProperties.SetName(Clear, "Clear " + model.Label);
                Clear.Click += (s, e) => clear();
                if (model.IsInline) {
                    Root = panel;
                    if (model.InlineChoiceLimit != 1) {
                        label = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                        label.SetResourceReference(FrameworkElement.StyleProperty, "FilterFieldLabelStyle");
                        var header = new DockPanel();
                        DockPanel.SetDock(Clear, Dock.Right);
                        header.Children.Add(Clear);
                        header.Children.Add(label);
                        panel.Children.Add(header);
                    }
                } else {
                    disclosure = new SectionExpander { Header = model.Label, Content = panel };
                    disclosure.SetResourceReference(FrameworkElement.StyleProperty, "FilterFieldDisclosureStyle");
                    disclosure.Expanded += (s, e) => {
                        if (!disclosure.ApplyingSearch) Opened?.Invoke();
                    };
                    Root = disclosure;
                    panel.Children.Add(Clear);
                }
                if (model.Text != null) {
                    Input = new TextBox { MinHeight = 24, ToolTip = model.Label };
                    Input.SetResourceReference(FrameworkElement.StyleProperty, "FilterInputStyle");
                    AutomationProperties.SetName(Input, model.Label);
                    panel.Children.Add(Input);
                } else {
                    choices = model.IsInline ? (Panel)new WrapPanel() : new StackPanel();
                    message.SetResourceReference(TextBlock.ForegroundProperty, "SteamMutedBrush");
                    panel.Children.Add(message);
                    panel.Children.Add(choices);
                    more.Click += (s, e) => {
                        limit += PageSize;
                        Opened?.Invoke();
                        if (!more.IsVisible) {
                            if (fewer.IsVisible) fewer.Focus();
                            else choices.Children.OfType<CheckBox>().LastOrDefault()?.Focus();
                        }
                    };
                    fewer.Click += (s, e) => {
                        limit = DefaultLimit;
                        Opened?.Invoke();
                        if (more.IsVisible) more.Focus();
                    };
                    more.SetResourceReference(FrameworkElement.StyleProperty, "FilterSmallButtonStyle");
                    fewer.SetResourceReference(FrameworkElement.StyleProperty, "FilterSmallButtonStyle");
                    AutomationProperties.SetName(fewer, "Show fewer " + model.Label + " options");
                    var actions = new WrapPanel();
                    actions.Children.Add(more);
                    if (model.IsInline) actions.Children.Add(fewer);
                    panel.Children.Add(actions);
                }
            }

            internal void ApplySearch(string query, bool matches) => disclosure?.ApplySearch(query, matches);

            internal void SetHeader(int count)
            {
                var header = count == 0 ? Model.Label : Model.Label + " (" + count + ")";
                if (label != null) label.Text = header;
                if (disclosure != null) disclosure.Header = header;
            }

            internal bool HasChoice(object key) => byKey.ContainsKey(key);

            internal void EnsureChoices(int revision,
                Func<FilterWorkspaceField, int?, IEnumerable<FilterWorkspaceChoice>> load, bool all)
            {
                if (loadedRevision == revision && (allChoicesLoaded || !all)) return;
                int? bound = all ? (int?)null : DefaultLimit + 1;
                var loaded = load(Model, bound).Where(choice => choice.Key != null)
                    .GroupBy(choice => choice.Key).Select(group => group.First()).ToArray();
                // Keep the visible preview in place when the first full page is loaded.
                if (all && loadedRevision == revision) {
                    var prefix = options.Take(DefaultLimit).ToArray();
                    var keys = new HashSet<object>(prefix.Select(choice => choice.Key));
                    loaded = prefix.Concat(loaded.Where(choice => !keys.Contains(choice.Key))).ToArray();
                }
                options = loaded;
                byKey = options.ToDictionary(choice => choice.Key);
                loadedRevision = revision;
                allChoicesLoaded = !bound.HasValue || loaded.Length < bound.Value;
            }

            internal string Label(object key)
            {
                if (byKey.TryGetValue(key, out var item)) return item.Label;
                if (key is Guid id) return id == Guid.Empty ? "None" : "Unavailable value (" + id.ToString("N").Substring(0, 8) + ")";
                return key?.ToString() ?? "Unavailable value";
            }

            internal void RenderChoices(string query, HashSet<object> selected, string extraCondition)
            {
                if (lastQuery != query) { lastQuery = query; limit = DefaultLimit; }
                var matching = options.Where(option => query.Length == 0 || selected.Contains(option.Key) ||
                    FilterWorkspaceCatalog.Matches(Model.Label, option.Label, query)).Select(option => option.Key)
                    .Concat(selected.Where(key => !byKey.ContainsKey(key))).ToArray();
                var visible = matching.Take(limit).ToArray();
                var visibleKeys = new HashSet<object>(visible);
                foreach (var key in controls.Keys.Where(key => !visibleKeys.Contains(key)).ToArray()) {
                    choices.Children.Remove(controls[key]);
                    controls.Remove(key);
                }
                for (int i = 0; i < visible.Length; i++) {
                    var key = visible[i];
                    if (!controls.TryGetValue(key, out var box)) {
                        box = new CheckBox { Margin = new Thickness(0, 1, 9, 1) };
                        box.SetResourceReference(FrameworkElement.StyleProperty, "FilterChoiceStyle");
                        box.Click += (s, e) => edit(key, box.IsChecked == true);
                        controls.Add(key, box);
                    }
                    if (choices.Children.IndexOf(box) != i) {
                        choices.Children.Remove(box);
                        choices.Children.Insert(i, box);
                    }
                    var label = Label(key);
                    if (!(box.Content is TextBlock caption)) {
                        caption = new TextBlock { TextWrapping = TextWrapping.Wrap };
                        caption.SetResourceReference(TextBlock.ForegroundProperty, "SteamTextBrush");
                        box.Content = caption;
                    }
                    caption.Text = label;
                    box.ToolTip = label;
                    box.IsChecked = selected.Contains(key);
                }
                message.Text = !string.IsNullOrWhiteSpace(extraCondition) ? "Additional text condition active in Playnite" :
                    matching.Length == 0 && query.Length == 0 ? "No values available" : "";
                message.Visibility = message.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
                more.Visibility = matching.Length > limit ? Visibility.Visible : Visibility.Collapsed;
                more.Content = allChoicesLoaded ? "Show more (" + Math.Max(0, matching.Length - limit) + ")" : "Show more";
                AutomationProperties.SetName(more, "Show more " + Model.Label + " options");
                fewer.Visibility = limit > DefaultLimit ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
