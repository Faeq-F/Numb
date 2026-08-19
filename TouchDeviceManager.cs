using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace Numb
{
  public static class TouchDeviceManager
  {
    private static readonly List<string> _disabledTouchDeviceIds = new();
    private static readonly string[] NewLineSeparators = ["\r\n", "\n"];

    public static void SetTouchScreenState(bool enable, Settings settings)
    {
      try
      {
        if (!enable)
        {
          List<string> activeIds = GetTouchDeviceInstanceIds(settings);
          foreach (string id in activeIds)
          {
            if (!_disabledTouchDeviceIds.Contains(id))
            {
              _disabledTouchDeviceIds.Add(id);
            }
            ExecutePnpUtil("/disable-device", id);
          }
        }
        else
        {
          // Re-enable cached devices as well as any other touch screen devices we discover
          HashSet<string> targetIds = new(_disabledTouchDeviceIds);
          List<string> allIds = GetTouchDeviceInstanceIds(settings);
          foreach (string id in allIds)
          {
            targetIds.Add(id);
          }

          foreach (string id in targetIds)
          {
            ExecutePnpUtil("/enable-device", id);
          }

          _disabledTouchDeviceIds.Clear();
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"TouchDeviceManager error: {ex.Message}\n{ex.StackTrace}");
      }
    }

    private static List<string> GetTouchDeviceInstanceIds(Settings settings)
    {
      List<string> deviceIds = new();
      if (!settings.TouchFilterByName)
      {
        // Use user-provided exact IDs
        string[] ids = settings.TouchDeviceIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (string id in ids)
        {
          string trimmedId = id.Trim();
          if (!string.IsNullOrEmpty(trimmedId))
          {
            deviceIds.Add(trimmedId);
          }
        }
        return deviceIds;
      }

      // Filter by Name
      try
      {
        ProcessStartInfo startInfo = new()
        {
          FileName = GetPnpUtilPath(),
          Arguments = "/enum-devices",
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = true,
          RedirectStandardError = true
        };

        using Process? process = Process.Start(startInfo);
        if (process != null)
        {
          string output = process.StandardOutput.ReadToEnd();
          process.WaitForExit(5000);

          string[] lines = output.Split(NewLineSeparators, StringSplitOptions.RemoveEmptyEntries);
          string currentInstanceId = "";
          bool isTouchDevice = false;

          string[] names = settings.TouchDeviceNames.Split(',', StringSplitOptions.RemoveEmptyEntries);

          foreach (string line in lines)
          {
            if (line.StartsWith("Instance ID:"))
            {
              if (isTouchDevice && !string.IsNullOrEmpty(currentInstanceId))
              {
                deviceIds.Add(currentInstanceId);
              }

              currentInstanceId = line.Substring("Instance ID:".Length).Trim();
              isTouchDevice = false;
            }
            else if (line.StartsWith("Device Description:"))
            {
              string desc = line.Substring("Device Description:".Length).Trim();
              foreach (string name in names)
              {
                if (desc.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                  isTouchDevice = true;
                  break;
                }
              }
            }
          }

          if (isTouchDevice && !string.IsNullOrEmpty(currentInstanceId))
          {
            deviceIds.Add(currentInstanceId);
          }
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Failed to enumerate devices via pnputil: {ex.Message}");
      }

      return deviceIds;
    }

    private static bool IsElevated()
    {
      using WindowsIdentity identity = WindowsIdentity.GetCurrent();
      WindowsPrincipal principal = new(identity);
      return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string GetPnpUtilPath()
    {
      string windir = Environment.GetEnvironmentVariable("windir") ?? @"C:\Windows";
      string subfolder = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
      return Path.Combine(windir, subfolder, "pnputil.exe");
    }

    private static void ExecutePnpUtil(string action, string pnpDeviceId)
    {
      ProcessStartInfo startInfo = new()
      {
        FileName = GetPnpUtilPath(),
        Arguments = $"{action} \"{pnpDeviceId}\"",
        UseShellExecute = !IsElevated()
      };

      if (startInfo.UseShellExecute)
      {
        startInfo.Verb = "runas";
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;

        try
        {
          using Process? process = Process.Start(startInfo);
          process?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
          Debug.WriteLine($"Elevation Exception: {ex.Message}");
        }
      }
      else
      {
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        try
        {
          using Process? process = Process.Start(startInfo);
          if (process != null)
          {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);
          }
        }
        catch (Exception ex)
        {
          Debug.WriteLine($"Exception: {ex.Message}");
        }
      }
    }
  }
}
