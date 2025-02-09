using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;

namespace Lookuper;

public partial class MainWindow
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private static readonly LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;

    public MainWindow()
    {
        InitializeComponent();
        _hookID = SetHook(_proc);
        Closing += (_, _) => UnhookWindowsHookEx(_hookID);
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
            GetModuleHandle(curModule!.ModuleName), 0);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && vkCode == KeyInterop.VirtualKeyFromKey(Key.D))
            {
                CaptureScreen();
            }
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private static void CaptureScreen()
    {
        try
        {
            if (!GetCursorPos(out POINT point))
                MessageBox.Show($"Failed to get cursor position: {Marshal.GetLastWin32Error()}");

            int width = (int)SystemParameters.PrimaryScreenWidth;
            int height = (int)SystemParameters.PrimaryScreenHeight;

            using var bitmap = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
            }

            string projectPath = AppDomain.CurrentDomain.BaseDirectory;
            string screenshotsPath = Path.Combine(projectPath, "Screenshots");

            if (!Directory.Exists(screenshotsPath))
                Directory.CreateDirectory(screenshotsPath);

            string screenshotFilePath =
                Path.Combine(screenshotsPath, $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            bitmap.Save(screenshotFilePath, ImageFormat.Png);

            var screenData = new
            {
                MouseX = point.X,
                MouseY = point.Y,
                ScreenshotPath = screenshotFilePath
            };

            string jsonFilePath =
                Path.Combine(screenshotsPath, $"ScreenshotData_{DateTime.Now:yyyyMMdd_HHmmss}.json");

            File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(screenData, Formatting.Indented));

            MoveWindowToMouse(point);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save screenshot: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void MoveWindowToMouse(POINT cursorPos)
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Left = cursorPos.X;
            mainWindow.Top = cursorPos.Y;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }
}