using System;
using System.IO;
using System.Xml.Serialization;

namespace HeliumHybrid
{
    // Keep the settings format and path stable. Never replace an unreadable file
    // unless a separate copy of its original bytes has been preserved.
    internal static class SettingsFile
    {
        internal static T Load<T>(string path, Action<Exception> log, out bool canSave) where T : new()
        {
            canSave = true;
            if (!File.Exists(path)) return new T();
            try {
                using (var reader = File.OpenRead(path))
                    return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
            } catch (Exception error) {
                log?.Invoke(error);
                try {
                    var backup = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    var suffix = 0;
                    while (File.Exists(backup)) backup = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + ++suffix;
                    File.Copy(path, backup, false);
                } catch (Exception backupError) {
                    log?.Invoke(backupError);
                    canSave = false;
                }
                return new T();
            }
        }

        internal static void Save<T>(string path, T settings)
        {
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            try {
                using (var writer = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    new XmlSerializer(typeof(T)).Serialize(writer, settings);
                    writer.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            } finally {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
