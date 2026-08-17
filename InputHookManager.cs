using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Numb
{
  public class InputHookManager : IDisposable
  {
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0200;
    private const int WM_SYSKEYUP = 0x0201;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private HookProc? _keyboardProc;
    private HookProc? _mouseProc;

    private IntPtr _keyboardHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;

    public bool IsLocked { get; private set; }

    public event EventHandler? UnlockRequested;
    public event EventHandler? LockStateChanged;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
      public uint vkCode;
      public uint scanCode;
      public uint flags;
      public uint time;
      public IntPtr dwExtraInfo;
    }

    public void StartLock()
    {
      if (IsLocked)
      {
        return;
      }


      _keyboardProc = KeyboardHookCallback;
      _mouseProc = MouseHookCallback;

      using (Process curProcess = Process.GetCurrentProcess())
      using (ProcessModule curModule = curProcess.MainModule!)
      {
        IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
        _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
        _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
      }

      IsLocked = true;
      LockStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StopLock()
    {
      if (!IsLocked)
      {
        return;
      }


      if (_keyboardHookId != IntPtr.Zero)
      {
        UnhookWindowsHookEx(_keyboardHookId);
        _keyboardHookId = IntPtr.Zero;
      }

      if (_mouseHookId != IntPtr.Zero)
      {
        UnhookWindowsHookEx(_mouseHookId);
        _mouseHookId = IntPtr.Zero;
      }

      IsLocked = false;
      LockStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool _isCtrlDown;
    private bool _isAltDown;
    private bool _isShiftDown;

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
      if (nCode >= 0)
      {
        KBDLLHOOKSTRUCT kbStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        Keys key = (Keys)kbStruct.vkCode;
        bool isKeyDown = wParam is WM_KEYDOWN or WM_SYSKEYDOWN;
        bool isKeyUp = wParam is WM_KEYUP or WM_SYSKEYUP;

        // Track modifier key states as events occur
        if (key is Keys.LControlKey or Keys.RControlKey or Keys.ControlKey)
        {
          if (isKeyDown)
          {
            _isCtrlDown = true;
          }

          if (isKeyUp)
          {
            _isCtrlDown = false;
          }
        }
        else if (key is Keys.LMenu or Keys.RMenu or Keys.Menu)
        {
          if (isKeyDown)
          {
            _isAltDown = true;
          }

          if (isKeyUp)
          {
            _isAltDown = false;
          }
        }
        else if (key is Keys.LShiftKey or Keys.RShiftKey or Keys.ShiftKey)
        {
          if (isKeyDown)
          {
            _isShiftDown = true;
          }

          if (isKeyUp)
          {
            _isShiftDown = false;
          }

        }

        // Check unlock shortcut on KeyDown
        if (isKeyDown && key == Keys.U)
        {
          // Fallback: also check GetAsyncKeyState in case modifier was pressed prior to hook activation
          bool ctrlActive = _isCtrlDown || (GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0;
          bool altActive = _isAltDown || (GetAsyncKeyState((int)Keys.Menu) & 0x8000) != 0;
          bool shiftActive = _isShiftDown || (GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0;

          if (ctrlActive && altActive && shiftActive)
          {
            UnlockRequested?.Invoke(this, EventArgs.Empty);
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
          }
        }
      }

      if (IsLocked)
      {
        // Suppress input
        return 1;
      }

      return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
      if (IsLocked && nCode >= 0)
      {
        // Suppress mouse input
        return 1;
      }

      return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
      StopLock();
      GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern short GetAsyncKeyState(int vKey);
  }
}
