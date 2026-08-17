using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Security.Principal;

namespace Numb
{
  public static class TouchDeviceManager
  {
    private static readonly List<string> _disabledTouchDeviceIds = new();

    public static void SetTouchScreenState(bool enable)
    {
      try
      {
        if (!enable)
        {
          // Query active touchscreen devices to disable
          ManagementObjectSearcher searcher = new(
              "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%touch screen%' OR Name LIKE '%touchscreen%'"
          );

          foreach (ManagementObject device in searcher.Get().Cast<ManagementObject>())
          {
            string pnpDeviceId = device["PNPDeviceID"]?.ToString() ?? "";
            if (!string.IsNullOrEmpty(pnpDeviceId))
            {
              if (!_disabledTouchDeviceIds.Contains(pnpDeviceId))
              {
                _disabledTouchDeviceIds.Add(pnpDeviceId);
              }
              ExecutePnpUtil("/disable-device", pnpDeviceId);
            }
          }
        }
        else
        {
          // Re-enable cached devices as well as any WMI touch entities
          HashSet<string> targetIds = new(_disabledTouchDeviceIds);

          try
          {
            // Backup search for any other touch screens that might be disabled
            ManagementObjectSearcher searcher = new(
                "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%touch screen%' OR Name LIKE '%touchscreen%'"
            );

            foreach (ManagementObject device in searcher.Get().Cast<ManagementObject>())
            {
              string id = device["PNPDeviceID"]?.ToString() ?? "";
              if (!string.IsNullOrEmpty(id))
              {
                targetIds.Add(id);
              }
            }
          }
          catch (Exception wmiEx)
          {
            Debug.WriteLine($"WMI backup query failed: {wmiEx.Message}");
          }

          foreach (string pnpDeviceId in targetIds)
          {
            ExecutePnpUtil("/enable-device", pnpDeviceId);
          }

          _disabledTouchDeviceIds.Clear();
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"TouchDeviceManager error: {ex.Message}");
      }
    }

    private static bool IsElevated()
    {
      using WindowsIdentity identity = WindowsIdentity.GetCurrent();
      WindowsPrincipal principal = new(identity);
      return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void ExecutePnpUtil(string action, string pnpDeviceId)
    {
      ProcessStartInfo startInfo = new()
      {
        FileName = "pnputil.exe",
        Arguments = $"{action} \"{pnpDeviceId}\"",
        UseShellExecute = !IsElevated()
      };

      if (startInfo.UseShellExecute)
      {
        startInfo.Verb = "runas";
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
      }
      else
      {
        startInfo.CreateNoWindow = true;
      }

      try
      {
        using Process? process = Process.Start(startInfo);
        process?.WaitForExit(5000);
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Failed to execute pnputil: {ex.Message}");
      }
    }
  }
}

