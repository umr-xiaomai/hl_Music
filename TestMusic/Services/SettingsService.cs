using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TestMusic.Services;

public enum CloseBehavior
{
    Exit,           
    MinimizeToTray  
}

// 设置数据模型
public class AppSettings
{
    public CloseBehavior CloseBehavior { get; set; } = CloseBehavior.Exit;
    public string MusicQuality { get; set; } = "high"; // standard, high, super, lossless
    public List<string> LocalMusicFolders { get; set; } = new();
}

// 设置管理器
public static class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "TestMusic", 
        "settings.json");

    public static AppSettings Settings { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // 忽略保存错误
        }
    }
}