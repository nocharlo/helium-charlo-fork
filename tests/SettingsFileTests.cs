using System;
using System.IO;
using System.Linq;
using HeliumHybrid;

public sealed class TestPreferences
{
    public int SidebarClickAction { get; set; } = 1;
    public int GridCoverClickAction { get; set; } = 0;
}

internal static class SettingsFileTests
{
    private static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
    }

    private static void Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "helium-settings-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try {
            var path = Path.Combine(directory, "navigator.xml");
            Exception error = null;
            var missing = SettingsFile.Load<TestPreferences>(path, ex => error = ex, out var canSave);
            Check(canSave && error == null && missing.SidebarClickAction == 1, "Missing settings defaults");
            missing.SidebarClickAction = 0;
            SettingsFile.Save(path, missing);
            var read = SettingsFile.Load<TestPreferences>(path, ex => error = ex, out canSave);
            Check(canSave && error == null && read.SidebarClickAction == 0, "Saved value reloads");
            read.GridCoverClickAction = 1;
            SettingsFile.Save(path, read);
            Check(File.Exists(path + ".bak"), "Previous valid settings backup");
            var corrupt = new byte[] { 0, 0xFF, 0x3C, 0x3E, 0x12 };
            File.WriteAllBytes(path, corrupt);
            error = null;
            var fallback = SettingsFile.Load<TestPreferences>(path, ex => error = ex, out canSave);
            Check(canSave && error != null && fallback.SidebarClickAction == 1, "Corrupt settings fallback is logged");
            var backup = Directory.GetFiles(directory, "navigator.xml.corrupt-*").Single();
            Check(File.ReadAllBytes(backup).SequenceEqual(corrupt), "Corrupt bytes preserved");
            SettingsFile.Save(path, fallback);
            Check(SettingsFile.Load<TestPreferences>(path, ex => throw ex, out canSave).SidebarClickAction == 1,
                "New file loads after recovery");
            Check(File.ReadAllBytes(backup).SequenceEqual(corrupt), "Recovery does not overwrite corrupt backup");
            Console.WriteLine("PASS: settings missing, persisted, corrupt backup, recovery and restart reload.");
        } finally { Directory.Delete(directory, true); }
    }
}
