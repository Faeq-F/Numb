using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;

namespace Numb
{
  public partial class MainWindow : Window
  {
    private readonly InputHookManager _hookManager = new();
    private readonly List<TouchShieldOverlay> _overlays = new();

    public MainWindow()
    {
      InitializeComponent();
      _hookManager.UnlockRequested += HookManager_UnlockRequested;
      _hookManager.LockStateChanged += HookManager_LockStateChanged;
      Unloaded += MainWindow_Unloaded;
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
      if (_hookManager.IsLocked)
      {
        Unlock();
      }
      else
      {
        Lock();
      }
    }

    private void Lock()
    {
      // Launch touch shield overlay on every active monitor
      _overlays.Clear();
      foreach (Screen screen in Screen.AllScreens)
      {
        TouchShieldOverlay overlay = new(screen);
        overlay.Show();
        _overlays.Add(overlay);
      }

      // Enable low level hardware hooks
      _hookManager.StartLock();

      // Disable touch screen hardware device
      TouchDeviceManager.SetTouchScreenState(false);
    }

    private void Unlock()
    {
      _hookManager.StopLock();

      // Re-enable touch screen hardware device
      TouchDeviceManager.SetTouchScreenState(true);

      // Close touch overlays
      foreach (TouchShieldOverlay overlay in _overlays)
      {
        overlay.Close();
      }
      _overlays.Clear();
    }

    private void HookManager_UnlockRequested(object? sender, EventArgs e)
    {
      Dispatcher.Invoke(Unlock);
    }

    private void HookManager_LockStateChanged(object? sender, EventArgs e)
    {
      Dispatcher.Invoke(() =>
      {
        if (_hookManager.IsLocked)
        {
          StatusText.Text = "Status: LOCKED (Touch & Input Blocked)";
          LockButton.Content = "Unlock";
        }
        else
        {
          StatusText.Text = "Status: Unlocked";
          LockButton.Content = "Lock Input & Touch";
        }
      });
    }

    private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
    {
      Unlock();
      _hookManager.Dispose();
    }
  }
}