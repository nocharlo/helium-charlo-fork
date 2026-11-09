using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace HeliumHybrid
{
    internal sealed class FilterWorkspaceGroup
    {
        internal readonly string Id, Label;
        internal readonly FilterWorkspaceField[] Fields;
        internal FilterWorkspaceGroup(string id, string label, params FilterWorkspaceField[] fields)
        { Id = id; Label = label; Fields = fields; }
    }

    internal sealed class FilterWorkspaceChoice
    {
        internal readonly object Key;
        internal readonly string Label;
        internal FilterWorkspaceChoice(object key, string label) { Key = key; Label = label; }
    }

    internal sealed class FilterWorkspaceField
    {
        internal readonly string Id, Label;
        internal Func<IPlayniteAPI, IEnumerable<FilterWorkspaceChoice>> Choices;
        internal Func<FilterPresetSettings, IEnumerable<object>> Selected;
        internal Action<FilterPresetSettings, object, bool> SetChoice;
        internal Func<FilterPresetSettings, string> Text, ExtraCondition;
        internal Action<FilterPresetSettings, string> SetText;
        internal Action<FilterPresetSettings> Clear;
        internal FilterWorkspaceField(string id, string label) { Id = id; Label = label; }
    }

    // IDs are saved in navigator.xml. Labels and native option captions can change independently.
    internal static class FilterWorkspaceCatalog
    {
        internal static readonly FilterWorkspaceGroup[] Groups = {
            new FilterWorkspaceGroup("ownership", "Library & Status",
                Ids("Platform", NavigatorFilterField.Platform, api => api.Database.Platforms),
                Ids("Library", NavigatorFilterField.Library, api => api.Addons.Plugins.OfType<LibraryPlugin>()
                    .Select(plugin => new DatabaseObject { Id = plugin.Id, Name = plugin.Name })),
                Ids("Source", NavigatorFilterField.Source, api => api.Database.Sources),
                Flags("installation", "Installation Status", new[] { "Installed", "Not installed" },
                    (state, index) => index == 0 ? state.IsInstalled : state.IsUnInstalled,
                    (state, index, value) => { if (index == 0) state.IsInstalled = value; else state.IsUnInstalled = value; }),
                Ids("Completion Status", NavigatorFilterField.CompletionStatus, api => api.Database.CompletionStatuses),
                EnumValues<InstallSizeGroup>("install-size", "Install Size", s => s.InstallSize, (s, v) => s.InstallSize = v),
                Flags("favorites", "Favorites", new[] { "Favorites only" }, (s, i) => s.Favorite, (s, i, v) => s.Favorite = v)),
            new FilterWorkspaceGroup("classification", "Classification",
                Ids("Genre", NavigatorFilterField.Genre, api => api.Database.Genres),
                Ids("Category", NavigatorFilterField.Category, api => api.Database.Categories),
                Ids("Tag", NavigatorFilterField.Tag, api => api.Database.Tags),
                Ids("Feature", NavigatorFilterField.Feature, api => api.Database.Features),
                Ids("Series", NavigatorFilterField.Series, api => api.Database.Series),
                Ids("Region", NavigatorFilterField.Region, api => api.Database.Regions),
                Ids("Age Rating", NavigatorFilterField.AgeRating, api => api.Database.AgeRatings)),
            new FilterWorkspaceGroup("identity", "Game Information",
                Text("name", "Name", s => s.Name, (s, v) => s.Name = v),
                List("release-year", "Release Year", s => s.ReleaseYear?.Values,
                    (s, v) => s.ReleaseYear = v == null ? null : new StringFilterItemProperties { Values = v },
                    api => api.Database.Games.Select(game => game.ReleaseYear).Where(year => year.HasValue)
                        .Select(year => year.Value).Distinct().OrderByDescending(year => year)
                        .Select(year => { var text = year.ToString(CultureInfo.CurrentCulture); return new FilterWorkspaceChoice(text, text); })),
                Ids("Developer", NavigatorFilterField.Developer, api => api.Database.Companies),
                Ids("Publisher", NavigatorFilterField.Publisher, api => api.Database.Companies),
                Text("version", "Version", s => s.Version, (s, v) => s.Version = v)),
            new FilterWorkspaceGroup("activity", "Activity & History",
                EnumValues<PlaytimeCategory>("playtime", "Time Played", s => s.PlayTime, (s, v) => s.PlayTime = v),
                EnumValues<PastTimeSegment>("last-played", "Last Played", s => s.LastActivity, (s, v) => s.LastActivity = v),
                EnumValues<PastTimeSegment>("recent-activity", "Recent Activity", s => s.RecentActivity, (s, v) => s.RecentActivity = v),
                EnumValues<PastTimeSegment>("date-added", "Date Added", s => s.Added, (s, v) => s.Added = v),
                EnumValues<PastTimeSegment>("date-modified", "Date Modified", s => s.Modified, (s, v) => s.Modified = v)),
            new FilterWorkspaceGroup("ratings", "Ratings",
                EnumValues<ScoreGroup>("user-score", "User Score", s => s.UserScore, (s, v) => s.UserScore = v),
                EnumValues<ScoreGroup>("community-score", "Community Score", s => s.CommunityScore, (s, v) => s.CommunityScore = v),
                EnumValues<ScoreGroup>("critic-score", "Critic Score", s => s.CriticScore, (s, v) => s.CriticScore = v))
        };

        private static FilterWorkspaceField Ids(string label, NavigatorFilterField id, Func<IPlayniteAPI, IEnumerable<DatabaseObject>> source) =>
            new FilterWorkspaceField(id.ToString(), label) {
                Choices = api => source(api).Where(item => item.Id != Guid.Empty)
                    .Select(item => new FilterWorkspaceChoice(item.Id, string.IsNullOrWhiteSpace(item.Name) ? "Unnamed value" : item.Name))
                    .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase),
                Selected = s => NavigatorFilterComposer.IdField(s, id)?.Ids?.Cast<object>() ?? Enumerable.Empty<object>(),
                ExtraCondition = s => NavigatorFilterComposer.IdField(s, id)?.Text,
                SetChoice = (s, key, selected) => NavigatorFilterComposer.SetId(s, id, (Guid)key, selected),
                Clear = s => NavigatorFilterComposer.ClearIdField(s, id)
            };

        private static FilterWorkspaceField Flags(string id, string label, string[] options,
            Func<FilterPresetSettings, int, bool> get, Action<FilterPresetSettings, int, bool> set) =>
            new FilterWorkspaceField(id, label) {
                Choices = api => options.Select((text, index) => new FilterWorkspaceChoice(index, text)),
                Selected = s => Enumerable.Range(0, options.Length).Where(index => get(s, index)).Cast<object>(),
                SetChoice = (s, key, selected) => set(s, (int)key, selected),
                Clear = s => { for (int i = 0; i < options.Length; i++) set(s, i, false); }
            };

        private static FilterWorkspaceField Text(string id, string label, Func<FilterPresetSettings, string> get,
            Action<FilterPresetSettings, string> set) => new FilterWorkspaceField(id, label) {
                Text = get, SetText = (s, value) => set(s, string.IsNullOrWhiteSpace(value) ? null : value),
                Clear = s => set(s, null)
            };

        private static FilterWorkspaceField EnumValues<T>(string id, string label,
            Func<FilterPresetSettings, EnumFilterItemProperties> get, Action<FilterPresetSettings, EnumFilterItemProperties> set)
            where T : struct, Enum =>
            List(id, label, s => get(s)?.Values,
                (s, v) => set(s, v == null ? null : new EnumFilterItemProperties { Values = v }),
                api => Enum.GetValues(typeof(T)).Cast<T>().Select(value => {
                    var name = value.ToString();
                    var description = (DescriptionAttribute)Attribute.GetCustomAttribute(typeof(T).GetField(name), typeof(DescriptionAttribute));
                    var caption = description?.Description ?? name;
                    if (caption.StartsWith("LOC", StringComparison.Ordinal))
                        caption = api.Resources.GetString(caption) ?? name;
                    return new FilterWorkspaceChoice(Convert.ToInt32(value), caption);
                }));

        private static FilterWorkspaceField List<T>(string id, string label, Func<FilterPresetSettings, List<T>> get,
            Action<FilterPresetSettings, List<T>> set, Func<IPlayniteAPI, IEnumerable<FilterWorkspaceChoice>> choices) =>
            new FilterWorkspaceField(id, label) {
                Choices = choices,
                Selected = s => get(s)?.Cast<object>().Where(value => value != null) ?? Enumerable.Empty<object>(),
                SetChoice = (s, key, selected) => {
                    var values = get(s) ?? new List<T>();
                    var value = (T)key;
                    if (selected && !values.Contains(value)) values.Add(value);
                    else if (!selected) values.RemoveAll(item => EqualityComparer<T>.Default.Equals(item, value));
                    set(s, values.Count == 0 ? null : values);
                },
                Clear = s => set(s, null)
            };

        internal static bool Matches(string field, string value, string query) =>
            NavigatorFilterComposer.OptionMatches(field, query) || NavigatorFilterComposer.OptionMatches(value, query);

        internal static int Columns(double contentWidth) => contentWidth >= 720 ? 3 : contentWidth >= 600 ? 2 : 1;
    }
}
