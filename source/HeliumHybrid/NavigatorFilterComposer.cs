using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Playnite.SDK.Models;

namespace HeliumHybrid
{
    internal enum NavigatorFilterField { Library, Source, Platform, Genre, Category, Tag, CompletionStatus }

    // Copies the entire SDK filter state before changing one field. Saved presets are never edited.
    internal static class NavigatorFilterComposer
    {
        internal static FilterPresetSettings Copy(FilterPresetSettings source)
        {
            if (source == null) source = new FilterPresetSettings();
            var result = new FilterPresetSettings();
            foreach (var property in typeof(FilterPresetSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                var value = property.GetValue(source);
                if (value is IdItemFilterItemProperties ids)
                    value = new IdItemFilterItemProperties { Ids = ids.Ids == null ? null : new List<Guid>(ids.Ids), Text = ids.Text };
                else if (value is StringFilterItemProperties strings)
                    value = new StringFilterItemProperties { Values = strings.Values == null ? null : new List<string>(strings.Values) };
                else if (value is EnumFilterItemProperties enums)
                    value = new EnumFilterItemProperties { Values = enums.Values == null ? null : new List<int>(enums.Values) };
                else if (value != null && !property.PropertyType.IsValueType && property.PropertyType != typeof(string))
                    throw new NotSupportedException("Unsupported Playnite filter property: " + property.Name);
                property.SetValue(result, value);
            }
            return result;
        }

        internal static FilterPresetSettings Installation(FilterPresetSettings source, bool installed, bool uninstalled)
        {
            var next = Copy(source);
            next.IsInstalled = installed;
            next.IsUnInstalled = uninstalled;
            return next;
        }

        internal static FilterPresetSettings Favorite(FilterPresetSettings source, bool favorite)
        {
            var next = Copy(source);
            next.Favorite = favorite;
            return next;
        }

        internal static FilterPresetSettings ToggleId(FilterPresetSettings source, NavigatorFilterField field, Guid id)
        {
            var next = Copy(source);
            var current = IdField(next, field) ?? new IdItemFilterItemProperties();
            current.Ids = current.Ids ?? new List<Guid>();
            if (current.Ids.Contains(id)) current.Ids.Remove(id);
            else current.Ids.Add(id);
            SetIdField(next, field, current.Ids.Count == 0 && string.IsNullOrWhiteSpace(current.Text) ? null : current);
            return next;
        }

        internal static FilterPresetSettings Clear(FilterPresetSettings source) =>
            new FilterPresetSettings { UseAndFilteringStyle = source?.UseAndFilteringStyle ?? false };

        internal static FilterPreset TemporaryPreset(FilterPresetSettings settings) => new FilterPreset {
            Id = Guid.Empty,
            Settings = Copy(settings)
            // Null view options leave Playnite's sorting direction and grouping untouched.
        };

        internal static IdItemFilterItemProperties IdField(FilterPresetSettings settings, NavigatorFilterField field)
        {
            switch (field) {
                case NavigatorFilterField.Library: return settings.Library;
                case NavigatorFilterField.Source: return settings.Source;
                case NavigatorFilterField.Platform: return settings.Platform;
                case NavigatorFilterField.Genre: return settings.Genre;
                case NavigatorFilterField.Category: return settings.Category;
                case NavigatorFilterField.Tag: return settings.Tag;
                case NavigatorFilterField.CompletionStatus: return settings.CompletionStatuses;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        private static void SetIdField(FilterPresetSettings settings, NavigatorFilterField field, IdItemFilterItemProperties value)
        {
            switch (field) {
                case NavigatorFilterField.Library: settings.Library = value; break;
                case NavigatorFilterField.Source: settings.Source = value; break;
                case NavigatorFilterField.Platform: settings.Platform = value; break;
                case NavigatorFilterField.Genre: settings.Genre = value; break;
                case NavigatorFilterField.Category: settings.Category = value; break;
                case NavigatorFilterField.Tag: settings.Tag = value; break;
                case NavigatorFilterField.CompletionStatus: settings.CompletionStatuses = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        internal static bool HasActiveFilters(FilterPresetSettings settings)
        {
            if (settings == null) return false;
            if (settings.IsInstalled || settings.IsUnInstalled || settings.Favorite || settings.Hidden ||
                !string.IsNullOrWhiteSpace(settings.Name) || !string.IsNullOrWhiteSpace(settings.Version)) return true;
            foreach (var property in typeof(FilterPresetSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                var value = property.GetValue(settings);
                if (value is IdItemFilterItemProperties ids &&
                    (ids.Ids?.Count > 0 || !string.IsNullOrWhiteSpace(ids.Text))) return true;
                if (value is StringFilterItemProperties strings && strings.Values?.Count > 0) return true;
                if (value is EnumFilterItemProperties enums && enums.Values?.Count > 0) return true;
            }
            return false;
        }

        internal static bool Same(FilterPresetSettings left, FilterPresetSettings right)
        {
            if (left == null || right == null) return left == right;
            foreach (var property in typeof(FilterPresetSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                var a = property.GetValue(left);
                var b = property.GetValue(right);
                if (a is IdItemFilterItemProperties ai && b is IdItemFilterItemProperties bi) {
                    if (ai.Text != bi.Text || !(ai.Ids ?? new List<Guid>()).SequenceEqual(bi.Ids ?? new List<Guid>())) return false;
                } else if (a is StringFilterItemProperties astr && b is StringFilterItemProperties bstr) {
                    if (!(astr.Values ?? new List<string>()).SequenceEqual(bstr.Values ?? new List<string>())) return false;
                } else if (a is EnumFilterItemProperties ae && b is EnumFilterItemProperties be) {
                    if (!(ae.Values ?? new List<int>()).SequenceEqual(be.Values ?? new List<int>())) return false;
                } else if (!Equals(a, b)) return false;
            }
            return true;
        }
    }

    internal sealed class ReadyToPlayState
    {
        private bool ownsRestriction;
        private bool priorInstalled, priorUninstalled;

        internal bool IsActive(FilterPresetSettings current) => current?.IsInstalled == true && !current.IsUnInstalled;
        internal bool OwnsRestriction => ownsRestriction;

        internal void Observe(FilterPresetSettings current)
        {
            if (!IsActive(current)) ownsRestriction = false;
        }

        internal FilterPresetSettings Toggle(FilterPresetSettings current)
        {
            current = current ?? new FilterPresetSettings();
            if (IsActive(current)) {
                var next = ownsRestriction
                    ? NavigatorFilterComposer.Installation(current, priorInstalled, priorUninstalled)
                    : NavigatorFilterComposer.Copy(current);
                ownsRestriction = false;
                return next;
            }
            priorInstalled = current.IsInstalled;
            priorUninstalled = current.IsUnInstalled;
            ownsRestriction = true;
            return NavigatorFilterComposer.Installation(current, true, false);
        }
    }
}
