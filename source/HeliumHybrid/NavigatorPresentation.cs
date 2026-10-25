using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HeliumHybrid
{
    public sealed class GameRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public string Key { get; set; }
        public string IconPath { get; set; }
        public ImageSource Icon => IconCache.Load(IconPath);
    }
    internal static class IconCache
    {
        private static readonly Dictionary<string, ImageSource> cache = new Dictionary<string, ImageSource>();
        internal static ImageSource Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (cache.TryGetValue(path, out var image)) return image;
            try {
                // Local files only, decoded at visible-row size. No online requests.
                if (!Path.IsPathRooted(path) || !File.Exists(path)) return null;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 32; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); image = bitmap;
                }
            } catch { image = null; }
            if (cache.Count >= 512) cache.Clear();
            cache[path] = image; return image;
        }
    }
}
