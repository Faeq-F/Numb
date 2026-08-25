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

    public HotkeyConfig Hotkey { get; set; } = new();
    public bool LockMouse { get; set; } = true;
    public bool LockKeyboard { get; set; } = true;
    public bool UnlockWithCtrlAltDel { get; set; } = false;

    public bool IsLocked { get; private set; }

    public event EventHandler? HotkeyTriggered;
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

    public void StartHook()
    {
      if (_keyboardHookId != IntPtr.Zero)
      {
        return;
      }

      _keyboardProc = KeyboardHookCallback;
      using Process curProcess = Process.GetCurrentProcess();
      using ProcessModule curModule = curProcess.MainModule!;
      IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
      _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
    }

    public void StopHook()
    {
      SetLockState(false, false, false);

      if (_keyboardHookId != IntPtr.Zero)
      {
        UnhookWindowsHookEx(_keyboardHookId);
        _keyboardHookId = IntPtr.Zero;
      }
    }

    public void SetLockState(bool locked, bool lockMouse, bool lockKeyboard)
    {
      IsLocked = locked;
      LockMouse = lockMouse;
      LockKeyboard = lockKeyboard;

      if (locked)
      {
        if (LockMouse && _mouseHookId == IntPtr.Zero)
        {
          _mouseProc = MouseHookCallback;
          using Process curProcess = Process.GetCurrentProcess();
          using ProcessModule curModule = curProcess.MainModule!;
          IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
          _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
        }
      }
      else
      {
        if (_mouseHookId != IntPtr.Zero)
        {
          UnhookWindowsHookEx(_mouseHookId);
          _mouseHookId = IntPtr.Zero;
        }
      }

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

        // Check hotkey combination on KeyDown
        if (isKeyDown && key == Hotkey.Key)
        {
          // Fallback: also check GetAsyncKeyState in case modifier was pressed prior to hook activation
          bool ctrlActive = _isCtrlDown || (GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0;
          bool altActive = _isAltDown || (GetAsyncKeyState((int)Keys.Menu) & 0x8000) != 0;
          bool shiftActive = _isShiftDown || (GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0;

          if (ctrlActive == Hotkey.Ctrl && altActive == Hotkey.Alt && shiftActive == Hotkey.Shift)
          {
            HotkeyTriggered?.Invoke(this, EventArgs.Empty);
            // Swallowed so hotkey doesn't type/interfere
            return 1;
          }
        }

        // Check Ctrl+Alt+Del for unblocking if enabled and locked
        if (UnlockWithCtrlAltDel && isKeyDown && key == Keys.Delete)
        {
          bool ctrlActive = _isCtrlDown || (GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0;
          bool altActive = _isAltDown || (GetAsyncKeyState((int)Keys.Menu) & 0x8000) != 0;
          if (ctrlActive && altActive)
          {
            if (IsLocked)
            {
              HotkeyTriggered?.Invoke(this, EventArgs.Empty);
            }
          }
        }
      }

      if (IsLocked && LockKeyboard)
      {
        // Suppress input
        return 1;
      }

      return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
      if (IsLocked && LockMouse && nCode >= 0)
      {
        // Suppress mouse input
        return 1;
      }

      return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
      StopHook();
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
