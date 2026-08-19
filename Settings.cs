using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace Numb
{
  public class HotkeyConfig
  {
    public bool Ctrl { get; set; } = true;
    public bool Alt { get; set; } = true;
    public bool Shift { get; set; } = false;
    public Keys Key { get; set; } = Keys.F;

    public override string ToString()
    {
      List<string> parts = new();

      if (Ctrl)
      {
        parts.Add("Ctrl");
      }

      if (Alt)
      {
        parts.Add("Alt");
      }


      if (Shift)
      {
        parts.Add("Shift");
      }

      parts.Add(Key.ToString());
      return string.Join(" + ", parts);
    }
  }

  public class Settings
  {
    public HotkeyConfig Hotkey { get; set; } = new();
    public bool LockMouse { get; set; } = true;
    public bool LockKeyboard { get; set; } = true;
    public bool LockTouch { get; set; } = true;
    public bool UseScreenBlocker { get; set; } = false;
    public bool TouchFilterByName { get; set; } = true;
    public string TouchDeviceNames { get; set; } = "touch screen, touchscreen";
    public string TouchDeviceIds { get; set; } = "";
    public bool UserLockOnUnlock { get; set; } = false;
    public bool UnlockWithCtrlAltDel { get; set; } = false;
    public int LockCountdownSeconds { get; set; } = 0;
    public int IdleLockSeconds { get; set; } = 0;
    public bool LockOnStartup { get; set; } = false;
    public bool ShowPopups { get; set; } = true;
    public bool PlaySounds { get; set; } = true;
    public bool HideTrayIconWhenLocked { get; set; } = false;
  }

  public static class SettingsManager
  {
    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "settings.json"
    );

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
      WriteIndented = true
    };

    public static Settings Load()
    {
      try
      {
        if (File.Exists(SettingsPath))
        {
          string json = File.ReadAllText(SettingsPath);
          Settings? loaded = JsonSerializer.Deserialize<Settings>(json, SerializerOptions);
          if (loaded != null)
          {
            return loaded;
          }
        }
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
      }

      // Return defaults and save them
      Settings defaults = new();
      Save(defaults);
      return defaults;
    }

    public static void Save(Settings settings)
    {
      try
      {
        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(SettingsPath, json);
      }
      catch (Exception ex)
      {
        System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
      }
    }
  }
}
