using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HeliumHybrid
{
    public sealed class GameRow : INotifyPropertyChanged
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public NavigatorGroupIdentity GroupIdentity { get; set; }
        public string Key { get; set; }
        public string IconPath { get; set; }
        public long ActivityTicks { get; set; }
        public long GroupOrder { get; set; }
        public ulong? SortValue { get; set; }
        public string SortText { get; set; }
        private bool isInstalled, favorite;
        public bool IsInstalled { get => isInstalled; set { if (isInstalled == value) return; isInstalled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstalled))); } }
        public bool Favorite { get => favorite; set { if (favorite == value) return; favorite = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Favorite))); } }
        public event PropertyChangedEventHandler PropertyChanged;
        public ImageSource Icon => IconCache.Load(IconPath);
    }
    internal static class IconCache
    {
        private const int MaxIconPixels = 40; // Compact icon at 200% DPI.
        private static readonly Dictionary<string, ImageSource> cache = new Dictionary<string, ImageSource>();
        internal static ImageSource Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (cache.TryGetValue(path, out var image)) return image;
            try {
                // Local files only, decoded at visible-row size. No online requests.
                if (!Path.IsPathRooted(path) || !File.Exists(path)) return null;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                    var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                    int width = frame.PixelWidth, height = frame.PixelHeight;
                    stream.Position = 0;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    if (width >= height && width > MaxIconPixels) bitmap.DecodePixelWidth = MaxIconPixels;
                    else if (height > MaxIconPixels) bitmap.DecodePixelHeight = MaxIconPixels;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    image = bitmap;
                }
            } catch { image = null; }
            if (cache.Count >= 512) cache.Clear();
            // Failed decodes may be temporary (download/write in progress). Retry on the next request.
            if (image != null) cache[path] = image;
            return image;
        }
    }
}
