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
    // Keeps the filter controls alive across native state changes and responsive reflow.
    internal sealed class NavigatorFilterPanel
    {
        private const int PageSize = 16;
        private readonly Func<FilterWorkspaceField, IEnumerable<FilterWorkspaceChoice>> loadChoices;
        private readonly Preferences preferences;
        private readonly Border frame;
        private readonly WrapPanel host, active;
        private readonly TextBox search;
        private readonly TextBlock empty;
        private readonly Action<Func<FilterPresetSettings, FilterPresetSettings>> apply;
        private readonly Dictionary<string, GroupView> groups = new Dictionary<string, GroupView>();
        private readonly DispatcherTimer searchTimer, textTimer;
        private readonly Dictionary<FilterWorkspaceField, string> pendingText = new Dictionary<FilterWorkspaceField, string>();
        private FilterPresetSettings shown;
        private bool layoutDirty = true, syncingText;
        internal bool HasManagedSelection => active.Children.Count > 0;

        internal NavigatorFilterPanel(Preferences preferences, Border frame,
            WrapPanel host, WrapPanel active, TextBox search, TextBlock empty,
            Func<FilterWorkspaceField, IEnumerable<FilterWorkspaceChoice>> loadChoices,
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
            host.SizeChanged += (s, e) => {
                var width = e.NewSize.Width;
                if (width > 0) host.ItemWidth = Math.Floor(width / FilterWorkspaceCatalog.Columns(width));
            };
        }

        internal void Position(Popup popup, Window window)
        {
            if (window == null) return;
            popup.PlacementTarget = window;
            frame.Width = Math.Max(1, Math.Min(820, window.ActualWidth - 20));
            frame.Height = Math.Max(1, Math.Min(610, window.ActualHeight - 32));
        }

        internal static CustomPopupPlacement[] Center(Size popup, Size target, Point offset) =>
            new[] { new CustomPopupPlacement(new Point(Math.Max(0, (target.Width - popup.Width) / 2),
                Math.Max(0, (target.Height - popup.Height) / 2)), PopupPrimaryAxis.None) };

        internal void InvalidateLayout() => layoutDirty = true;

        internal void Refresh(FilterPresetSettings current, bool reloadChoices = false)
        {
            bool initialize = groups.Count == 0;
            if (initialize) CreateControls();
            bool stateChanged = !NavigatorFilterComposer.Same(shown, current);
            if (stateChanged) shown = NavigatorFilterComposer.Copy(current);
            if (initialize || reloadChoices) {
                foreach (var field in groups.Values.SelectMany(group => group.Fields.Values)) {
                    if (field.Model.Choices != null) field.LoadChoices(loadChoices(field.Model));
                }
            }
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
                foreach (var key in model.Selected(shown).Distinct()) {
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
                            group.Panel.Children.Add(field.Section);
                    }
                }
                layoutDirty = false;
            }
            bool found = false;
            var visibleGroups = new List<UIElement>();
            foreach (var item in layout) {
                if (item.Hidden || !groups.TryGetValue(item.Id, out var group)) continue;
                bool groupMatches = false, hasVisible = false;
                foreach (var id in item.FieldOrder) {
                    if (item.HiddenFields.Contains(id) || !group.Fields.TryGetValue(id, out var field)) continue;
                    var matches = UpdateField(field, query);
                    groupMatches |= matches;
                    hasVisible |= field.Section.Visibility == Visibility.Visible;
                }
                if (hasVisible) visibleGroups.Add(group.Section);
                group.Section.ApplySearch(query, groupMatches);
                found |= groupMatches;
            }
            // WrapPanel reserves a fixed-width slot even for a collapsed child.
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
            var fieldMatch = NavigatorFilterComposer.OptionMatches(model.Label, query);
            var matches = fieldMatch || (text != null && NavigatorFilterComposer.OptionMatches(text, query)) ||
                field.Options.Any(option => NavigatorFilterComposer.OptionMatches(option.Label, query));
            var count = model.Text != null ? (string.IsNullOrWhiteSpace(text) ? 0 : 1) : selected.Count;
            field.Section.Header = count == 0 ? model.Label : model.Label + " (" + count + ")";
            field.Section.Visibility = matches || count > 0 ? Visibility.Visible : Visibility.Collapsed;
            field.Clear.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (field.Input != null && !pendingText.ContainsKey(model) && field.Input.Text != (text ?? "")) {
                syncingText = true;
                try { field.Input.Text = text ?? ""; }
                finally { syncingText = false; }
            }
            field.Section.ApplySearch(query, matches);
            if (field.Section.IsExpanded && field.Input == null)
                field.RenderChoices(query, selected, model.ExtraCondition?.Invoke(shown));
            return matches;
        }

        private sealed class SectionExpander : Expander
        {
            private bool? beforeSearch;
            internal void ApplySearch(string query, bool matches)
            {
                if (query.Length > 0) {
                    if (!matches || beforeSearch.HasValue) return;
                    beforeSearch = IsExpanded;
                    IsExpanded = true;
                } else if (beforeSearch.HasValue) {
                    var prior = beforeSearch.Value;
                    beforeSearch = null;
                    IsExpanded = prior;
                }
            }
        }

        private sealed class GroupView
        {
            internal readonly SectionExpander Section;
            internal readonly StackPanel Panel = new StackPanel { Margin = new Thickness(4, 4, 0, 0) };
            internal readonly Dictionary<string, FieldView> Fields = new Dictionary<string, FieldView>();

            internal GroupView(FilterWorkspaceGroup group)
            {
                Section = new SectionExpander { Content = Panel, IsExpanded = true, Margin = new Thickness(0, 0, 14, 10) };
                var heading = new TextBlock { Text = group.Label, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
                heading.SetResourceReference(TextBlock.ForegroundProperty, "SteamActiveBrush");
                Section.Header = heading;
                AutomationProperties.SetName(Section, group.Label);
            }
        }

        private sealed class FieldView
        {
            internal readonly FilterWorkspaceField Model;
            internal readonly SectionExpander Section;
            internal readonly TextBox Input;
            internal readonly Button Clear;
            internal Action Opened;
            private readonly StackPanel choices = new StackPanel();
            private readonly TextBlock message = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap };
            private readonly Button more = new Button { HorizontalAlignment = HorizontalAlignment.Left };
            private readonly Action<object, bool> edit;
            private Dictionary<object, FilterWorkspaceChoice> byKey = new Dictionary<object, FilterWorkspaceChoice>();
            private FilterWorkspaceChoice[] options = new FilterWorkspaceChoice[0];
            private readonly Dictionary<object, CheckBox> controls = new Dictionary<object, CheckBox>();
            private string lastQuery;
            private int limit = PageSize;
            internal IEnumerable<FilterWorkspaceChoice> Options => options;

            internal FieldView(FilterWorkspaceField model, Action<object, bool> edit, Action clear)
            {
                Model = model;
                this.edit = edit;
                var panel = new StackPanel { Margin = new Thickness(13, 2, 0, 4) };
                Section = new SectionExpander { Header = model.Label, Content = panel, Margin = new Thickness(0, 0, 0, 4) };
                Section.SetResourceReference(Control.ForegroundProperty, "SteamTextBrush");
                Clear = new Button { Content = "Clear", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(4, 1, 4, 1) };
                AutomationProperties.SetName(Clear, "Clear " + model.Label);
                Clear.Click += (s, e) => clear();
                panel.Children.Add(Clear);
                if (model.Text != null) {
                    Input = new TextBox { MinHeight = 24, ToolTip = model.Label };
                    Input.SetResourceReference(FrameworkElement.StyleProperty, "FilterInputStyle");
                    AutomationProperties.SetName(Input, model.Label);
                    panel.Children.Add(Input);
                } else {
                    message.SetResourceReference(TextBlock.ForegroundProperty, "SteamMutedBrush");
                    panel.Children.Add(message);
                    panel.Children.Add(choices);
                    more.Click += (s, e) => { limit += PageSize; Opened?.Invoke(); };
                    panel.Children.Add(more);
                }
                Section.Expanded += (s, e) => { if (ReferenceEquals(e.OriginalSource, Section)) Opened?.Invoke(); };
            }

            internal void LoadChoices(IEnumerable<FilterWorkspaceChoice> values)
            {
                options = values.Where(choice => choice.Key != null).GroupBy(choice => choice.Key).Select(group => group.First()).ToArray();
                byKey = options.ToDictionary(choice => choice.Key);
            }

            internal string Label(object key)
            {
                if (byKey.TryGetValue(key, out var item)) return item.Label;
                if (key is Guid id) return id == Guid.Empty ? "None" : "Unavailable value (" + id.ToString("N").Substring(0, 8) + ")";
                return key?.ToString() ?? "Unavailable value";
            }

            internal void RenderChoices(string query, HashSet<object> selected, string extraCondition)
            {
                if (lastQuery != query) { lastQuery = query; limit = PageSize; }
                var matching = options.Where(option => selected.Contains(option.Key) ||
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
                        box = new CheckBox { Margin = new Thickness(1, 2, 0, 2) };
                        box.SetResourceReference(Control.ForegroundProperty, "SteamTextBrush");
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
                more.Content = "Show more (" + Math.Max(0, matching.Length - limit) + ")";
            }
        }
    }
}
