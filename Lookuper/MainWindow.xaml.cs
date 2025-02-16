using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
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
    private static readonly HttpClient HttpClient = new() { BaseAddress = new Uri("http://127.0.0.1:6969") };


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

    private static async void CaptureScreen()
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

            /*string projectPath = AppDomain.CurrentDomain.BaseDirectory;
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

            File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(screenData, Formatting.Indented));*/
            /*
            SendDataToPython(screenshotFilePath, point.X, point.Y);
            */
            
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            string base64Image = Convert.ToBase64String(ms.ToArray());

            MoveWindowToMouse(point);

            await SendDataToServer(base64Image, point.X, point.Y);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save screenshot: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static async Task SendDataToServer(string base64Image, int mouseX, int mouseY)
    {
        try
        {
            var payload = new
            {
                image = base64Image,
                x = mouseX,
                y = mouseY
            };

            string json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await HttpClient.PostAsync("/process_image", content);
            string responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject<dynamic>(responseBody);
            string foundWord = result?.word;

            MessageBox.Show($"Найденное слово: {foundWord}", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при отправке запроса в FastAPI: {ex.Message}");
        }
    }
    
    private static void SendDataToPython(string screenshotPath, int mouseX, int mouseY)
    {
        try
        {
            string pythonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR", "screen_ocr", ".venv", "Scripts", "python.exe");
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OCR", "screen_ocr", "main.py");

            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = scriptPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process())
            {
                process.StartInfo = psi;
                process.Start();

                Thread.Sleep(200);

                using (StreamWriter sw = process.StandardInput)
                {
                    if (!sw.BaseStream.CanWrite)
                    {
                        throw new IOException("Поток StandardInput закрыт.");
                    }

                    var jsonData = JsonConvert.SerializeObject(new
                    {
                        image = Convert.ToBase64String(File.ReadAllBytes(screenshotPath)),
                        x = mouseX,
                        y = mouseY
                    });

                    sw.WriteLine(jsonData);
                    sw.Flush();  // Принудительно отправляем данные в Python
                }

                using (StreamReader sr = process.StandardOutput)
                {
                    string result = sr.ReadToEnd();
                    MessageBox.Show($"Python ответил: {result}");
                }

                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка передачи данных в Python: {ex.Message}");
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