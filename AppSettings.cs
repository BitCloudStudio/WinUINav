using System;
using System.IO;
using System.Text.Json;

namespace WinUINav;

public sealed class AppSettingsModel
{
    public string Theme { get; set; } = "Default";      // Default / Light / Dark
    public string BackdropMode { get; set; } = "MicaAlt"; // Mica / MicaAlt / Acrylic
    public string NavMode { get; set; } = "Auto";       // Left / Top / Auto
}

public static class AppSettings
{
    private static readonly string SettingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinUINav");

    private static readonly string SettingsFile =
        Path.Combine(SettingsFolder, "settings.json");

    public static AppSettingsModel Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new AppSettingsModel();

            string json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<AppSettingsModel>(json) ?? new AppSettingsModel();
        }
        catch
        {
            return new AppSettingsModel();
        }
    }

    public static void Save(AppSettingsModel settings)
    {
        Directory.CreateDirectory(SettingsFolder);

        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(SettingsFile, json);
    }
}