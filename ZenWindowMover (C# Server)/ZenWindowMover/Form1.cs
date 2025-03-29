using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace ZenWindowMover
{

    // General class
    public partial class ZenWindowMover : Form
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // turn on WS_EX_TOOLWINDOW style bit
                cp.ExStyle |= 0x80;
                return cp;
            }
        }
        private readonly WebSocketServer _webSocketServer;
        private readonly WindowManager _windowManager;
        private readonly HotKeyManager _hotKeyManager;

        public ZenWindowMover()
        {
            InitializeComponent();
            Size = new System.Drawing.Size(1, 1);
            Opacity = 0;
            Visible = false;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None; 
                
            _windowManager = new WindowManager("MozillaWindowClass");
            _hotKeyManager = new HotKeyManager();

            _webSocketServer = new WebSocketServer("ws://127.0.0.1:8080");
            _webSocketServer.AddWebSocketService("/mover", () =>
                new WindowMoverWebSocket(_windowManager, _hotKeyManager, UpdateStatus));

            _webSocketServer.Start();
            UpdateStatus("WebSocket server started on ws://127.0.0.1:8080/mover");
        }

        private void UpdateStatus(string message)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new Action<string>(UpdateStatus), message);
            }
            else
            {
                lblStatus.Text = message;
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WinApiConstants.WM_NCHITTEST && m.Result.ToInt32() == WinApiConstants.HTCAPTION)
            {
                m.Result = (IntPtr)WinApiConstants.HTCAPTION;
            }
        }

        private async void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            await Task.Run(() => _windowManager.MoveWindowToCenter(_windowManager.FindTargetWindow()));
            _hotKeyManager.Dispose();
            await Task.Run(() => _webSocketServer.Stop());
            base.OnFormClosed(e);
        }
    }
    
    public static class WinApiConstants
    {
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const int SW_MAXIMIZE = 3;
        public const int SW_RESTORE = 9;
        public const int WM_NCHITTEST = 0x0084;
        public const int HTCAPTION = 0x02;

        public const byte VK_LWIN = 0x5B;
        public const byte VK_LEFT = 0x25;
        public const byte VK_UP = 0x26;
        public const byte VK_RIGHT = 0x27;
        public const uint KEYEVENTF_KEYDOWN = 0x0000;
        public const uint KEYEVENTF_KEYUP = 0x0002;
    }

    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    public struct POINT
    {
        public int X;
        public int Y;
    }

    // Class for WinApi calls
    public static class WindowApiHelper
    {
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }

    // Class of Keyboard shorcut just in case if needed
    public class HotKeyManager : IDisposable
    {
        private bool _areKeysPressed = false;
        private bool _isWinKeyPressed = false;

        public void PressWinUp()
        {
            WindowApiHelper.keybd_event(WinApiConstants.VK_LWIN, 0, WinApiConstants.KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            WindowApiHelper.keybd_event(WinApiConstants.VK_UP, 0, WinApiConstants.KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            _areKeysPressed = true;
            _isWinKeyPressed = true;
        }

        public void ReleaseAll()
        {
            WindowApiHelper.keybd_event(WinApiConstants.VK_LWIN, 0, WinApiConstants.KEYEVENTF_KEYUP, UIntPtr.Zero);
            WindowApiHelper.keybd_event(WinApiConstants.VK_LEFT, 0, WinApiConstants.KEYEVENTF_KEYUP, UIntPtr.Zero);
            WindowApiHelper.keybd_event(WinApiConstants.VK_UP, 0, WinApiConstants.KEYEVENTF_KEYUP, UIntPtr.Zero);
            WindowApiHelper.keybd_event(WinApiConstants.VK_RIGHT, 0, WinApiConstants.KEYEVENTF_KEYUP, UIntPtr.Zero);
            _areKeysPressed = false;
            _isWinKeyPressed = false;
        }

        public void Dispose()
        {
            ReleaseAll();
        }
    }
    // Class for Window Manipulation
    public class WindowManager
    {
        private readonly string _targetWindowClass;

        public WindowManager(string targetWindowClass)
        {
            _targetWindowClass = targetWindowClass;
        }

        public IntPtr FindTargetWindow()
        {
            return WindowApiHelper.FindWindow(_targetWindowClass, null);
        }

        public bool MoveWindow(IntPtr hWnd, int x, int y)
        {
            return WindowApiHelper.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0,
                WinApiConstants.SWP_NOSIZE | WinApiConstants.SWP_NOZORDER);
        }

        public bool GetWindowRect(IntPtr hWnd, out RECT rect)
        {
            return WindowApiHelper.GetWindowRect(hWnd, out rect);
        }
        public bool _moverBlocked = false;
        public bool ShowWindow(IntPtr hWnd, int command)
        {
            if (!_moverBlocked)
                return WindowApiHelper.ShowWindow(hWnd, command);
            else return false;
        }

        public bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement)
        {
            return WindowApiHelper.GetWindowPlacement(hWnd, ref placement);
        }
        public void MoveWindowToCenter(IntPtr hWnd)
        {
            // Get screen size
            var screen = Screen.PrimaryScreen.Bounds;
            int screenWidth = screen.Width;
            int screenHeight = screen.Height;

            // Get Window current size
            if (WindowApiHelper.GetWindowRect(hWnd, out RECT rect))
            {
                int windowWidth = rect.Right - rect.Left;
                int windowHeight = rect.Bottom - rect.Top;

                // Get center points of display
                int centerX = (screenWidth - windowWidth) / 2;
                int centerY = (screenHeight - windowHeight) / 2;

                // Move Window to Ceenter of current display
                MoveWindow(hWnd, centerX, centerY);
            }
        }
    }

    // Class for WebSocket job
    public class WindowMoverWebSocket : WebSocketBehavior
    {
        private readonly WindowManager _windowManager;
        private readonly HotKeyManager _hotKeyManager;
        private readonly Action<string> _updateStatusAction;

        private int _windowWidth = 0;
        private int _restoredWindowWidth = 0;
        private int _cursorPercentage = 0;
        private int _headerWidth = 0;
        private bool _isMaximized = false;

        public WindowMoverWebSocket(WindowManager windowManager, HotKeyManager hotKeyManager, Action<string> updateStatusAction)
        {
            _windowManager = windowManager;
            _hotKeyManager = hotKeyManager;
            _updateStatusAction = updateStatusAction;
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var data = e.Data;
            Console.WriteLine($"Received message: {data}");

            try
            {
                if (data.StartsWith("getMovableElement:"))
                {
                    HandleGetMovableElement(data);
                }
                else if (data == "headerFound")
                {
                    _updateStatusAction("Header found!");
                }
                else if (data == "headerNotFound")
                {
                    _updateStatusAction("Header not found!");
                }
                else if (data.StartsWith("headerWidth:"))
                {
                    HandleHeaderWidth(data);
                }
                else if (data.StartsWith("cursorPercentage:"))
                {
                    HandleCursorPercentage(data);
                }
                else if (data.StartsWith("windowWidth:"))
                {
                    HandleWindowWidth(data);
                }
                else if (data.StartsWith("restoredWindowWidth:"))
                {
                    HandleRestoredWindowWidth(data);
                }
                else if (data == "dragEnd")
                {
                    HandleDragEnd();
                }
                else if (data == "doubleClick")
                {
                    HandleDoubleClick();
                }
                else
                {
                    HandleWindowMove(data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
                _updateStatusAction($"Error: {ex.Message}");
            }
        }

        void HandleDoubleClick()
        {
            var hWnd = _windowManager.FindTargetWindow();
            if (hWnd != IntPtr.Zero)
            {
                WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                placement.length = Marshal.SizeOf(placement);
                _windowManager.GetWindowPlacement(hWnd, ref placement);

                if (placement.showCmd == WinApiConstants.SW_MAXIMIZE)
                {
                    _windowManager.ShowWindow(hWnd, WinApiConstants.SW_RESTORE);
                    _isMaximized = false;

                    _windowManager.GetWindowRect(hWnd, out RECT rect);
                    var screen = Screen.PrimaryScreen.Bounds;

                    if (rect.Top < 0 && !_isMaximized)
                    {
                        _isMaximized = false;
                        _windowManager.MoveWindow(hWnd, rect.Left, 0);
                        _updateStatusAction("Window adjusted to top edge");
                    }
                    // _updateStatusAction("Window Restored");
                }
                else
                {
                    _windowManager.ShowWindow(hWnd, WinApiConstants.SW_MAXIMIZE);
                    _isMaximized = true;
                    // _updateStatusAction("Window Maximized");
                }
            }
        }


        private void HandleGetMovableElement(string data)
        {
            var domain = data.Substring("getMovableElement:".Length);
            var fileName = Path.Combine("movable", $"{domain}.txt");

            _updateStatusAction($"Looking for: {fileName}");

            if (File.Exists(fileName))
            {
                var className = File.ReadAllText(fileName).Trim();
                Send($"movableElement:{className}");
            }
            else
            {
                Send("movableElement:");
            }
        }

        private void HandleHeaderWidth(string data)
        {
            var width = data.Substring("headerWidth:".Length);
            _headerWidth = int.Parse(width);
            _updateStatusAction($"Header width: {_headerWidth}px");
        }

        private void HandleCursorPercentage(string data)
        {
            var percentage = data.Substring("cursorPercentage:".Length);
            _cursorPercentage = int.Parse(percentage);
        }

        private void HandleWindowWidth(string data)
        {
            var width = data.Substring("windowWidth:".Length);
            _windowWidth = int.Parse(width);
        }

        private void HandleRestoredWindowWidth(string data)
        {
            var width = data.Substring("restoredWindowWidth:".Length);
            _restoredWindowWidth = int.Parse(width);
        }

        void HandleDragEnd()
        {
            _hotKeyManager.ReleaseAll();
            var hWnd = _windowManager.FindTargetWindow();

            if (hWnd != IntPtr.Zero)
            {
                _windowManager.GetWindowRect(hWnd, out RECT rect);
                var screen = Screen.PrimaryScreen.Bounds;

                if (rect.Top < 0 && !_isMaximized)
                {
                    _isMaximized = false;
                    _windowManager.MoveWindow(hWnd, rect.Left, 0);
                    _updateStatusAction("Window adjusted to top edge");
                }
                //CheckCursorPosition();
            }
        }

        private void HandleWindowMove(string data)
        {
            var coords = data.Split(',');
            if (coords.Length == 2 && int.TryParse(coords[0], out int deltaX) && int.TryParse(coords[1], out int deltaY))
            {
                if (Math.Abs(deltaX) > 1000 || Math.Abs(deltaY) > 1000)
                {
                    _updateStatusAction("Invalid delta values!");
                    return;
                }
                else
                {
                    _updateStatusAction("Good delta values!");

                }

                var hWnd = _windowManager.FindTargetWindow();
                if (hWnd != IntPtr.Zero)
                {
                    WindowApiHelper.GetCursorPos(out POINT cursorPos);
                    _windowManager.GetWindowRect(hWnd, out RECT rect);
                    WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                    placement.length = Marshal.SizeOf(placement);
                    _windowManager.GetWindowPlacement(hWnd, ref placement);

                    if (placement.showCmd == WinApiConstants.SW_MAXIMIZE)
                    {
                        HandleMaximizedWindow(hWnd, cursorPos, rect);
                    }
                    else
                    {
                        _windowManager.MoveWindow(hWnd, rect.Left + deltaX, rect.Top + deltaY);
                    }

                    CheckCursorPosition();
                }
                else
                {
                    _updateStatusAction("Target window not found!");
                }
            }
        }

        private void HandleMaximizedWindow(IntPtr hWnd, POINT cursorPos, RECT rect)
        {
            _windowManager.ShowWindow(hWnd, WinApiConstants.SW_RESTORE);
            _isMaximized = false;
            _windowManager.GetWindowRect(hWnd, out RECT newRect);

            int windowWidth = newRect.Right - newRect.Left;
            int cursorPointer = windowWidth / 100 * _cursorPercentage;
            int newX = cursorPos.X - cursorPointer;

            _windowManager.MoveWindow(hWnd, newX, cursorPos.Y - (cursorPos.Y - rect.Top));
        }
        private async void CheckCursorPosition()
        {
            WindowApiHelper.GetCursorPos(out POINT cursorPos);
            var currentScreen = Screen.AllScreens.FirstOrDefault(s =>
                cursorPos.X >= s.Bounds.Left && cursorPos.X <= s.Bounds.Right &&
                cursorPos.Y >= s.Bounds.Top && cursorPos.Y <= s.Bounds.Bottom
            ) ?? Screen.PrimaryScreen;

            int screenTop = currentScreen.Bounds.Top;
            int topThreshold = 0;

            if (cursorPos.Y <= screenTop + topThreshold)
            {
                var hWnd = _windowManager.FindTargetWindow();
                if (hWnd != IntPtr.Zero)
                {
                    WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                    placement.length = Marshal.SizeOf(placement);
                    _windowManager.GetWindowPlacement(hWnd, ref placement);

                    // Check if window already maximized
                    if (placement.showCmd != WinApiConstants.SW_MAXIMIZE && !_isMaximized)
                    {
                        _isMaximized = true;
                        await Task.Delay(150);
                        _windowManager.ShowWindow(hWnd, WinApiConstants.SW_MAXIMIZE);
                    }
                }
            }
            else
            {
                // If cursor goes away from top of screen
                _isMaximized = false;
                await Task.Delay(150);
            }
        }
    }
}