using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsInput;
using WindowsInput.Native;
using System.Windows.Forms;
using static Interop;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using Rectangle = System.Windows.Shapes.Rectangle;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using WindowsVirtualDesktopHelper.VirtualDesktopAPI;
using Microsoft.Toolkit.Uwp.Notifications;
namespace DragWin
{
    public partial class MainWindow
    {
        [DllImport("user32.dll")]
        static extern void NotifyWinEvent(uint eventType, IntPtr hwnd, int idObject, int idChild);
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        static SettingsWindow SWindow;
        static bool shiftDown = false;

        // update url
        // -----------------
        string url = "https://api.github.com/repos/PinchToDebug/DragWin/releases/latest";

        // mutex
        // -----------------
        private const string mutexId = "{47bb6j6d-l38e-4bb5-92jb-a239cr17bj9e}";
        private Mutex mutex; // it is used 

        // Consts for helping to hide the window
        // -----------------
        private const int GWL_EX_STYLE = -20;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        // Virtual desktop
        // -----------------
        public static string DetectedVDImplementation;
        public static IVirtualDesktopManager VDAPI;

        // mouse
        // -----------------
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WS_OVERLAPPEDWINDOW = 0x00CF0000; // fullscreen style like
        private const int WM_SYSCOMMAND = 0x112;
        private const int SC_MOVE = 0xF010;

        // window sides & corners
        // -----------------
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private static int hitTestCode = 0;

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002; // Handle to the monitor that is nearest to the point.
        private const int MDT_EFFECTIVE_DPI = 0;

        private static IntPtr hookIdMouse = IntPtr.Zero;
        private static IntPtr hookIdKeyboard = IntPtr.Zero;
        private static IntPtr hWnd;
        private static IntPtr resizeHwnd;
        private static IntPtr prevhWnd;
        private static IntPtr prevMovedhWnd;
        private static IntPtr prevMovedInnerhWnd;
        private static IntPtr prevMovedlParam;
        private static IntPtr prevInnerHwnd;
        private static List<IntPtr> windowOrder = new List<IntPtr>();
        private static List<IntPtr> windowHandles = new List<IntPtr>();

        private static Screen? screen;
        private static MSLLHOOKSTRUCT hookStruct;

        private static RECT rectBefore;
        private static RECT rect;
        private static POINT initialMouseClickPosition;
        private static POINT initialMiddleClickPosition;
        private static POINT previousMousePosition;
        private static Interop.LowLevelMouseProc mouseProc = MouseHookCallback;
        private static Interop.LowLevelKeyboardProc keyboardProc = KeyboardHookCallback;
        private static PointerTouchInfo[]? touchPointers;


        //ui buttons
        // -----------------
        private static bool bringToFront = true;
        private static bool canOverflow = true;
        private static bool startOnLogin = false;
        private static bool canResizeCorners = false;
        private static bool canScrollWindows = false;
        private static bool AutoFancyZones = false;
        private static bool WheelGesture = false;
        private static bool OpacityScrolling = false;
        private static bool DesktopScrolling = false;


        //logic 
        // -----------------
        private static bool hwndCheck = false;
        private static bool rightMouseDown = false;
        private static bool middleMouseDownHasCount = false;
        private static bool middleMouseDown = false;
        private static bool dontTouchMove = false;
        private static bool touchMoving = false;
        private static bool usedFancyZones = false;
        private static bool useSnapAssist = true;
        private static bool maximizeOnTop = true;
        private static bool fancyZonesWasChromium = false;
        private static bool fancyZonesRunning = false;
        // private static bool leftdown = false;
        private static bool doOnce = true;
        private static bool doOnce2 = true;
        // private static bool doneMove = false;
        private static bool movingWindow = false;
        private static bool movingExplorerFix = true;
        private static bool enabled = true;
        // private static bool outsideFix = false;
        private static bool didScrollWindows = false;
        private static bool didChangeVirtualDesktopWinScroll = false;
        private static bool isToastNotificationShown = false;

        private uint _taskbarRestartMessage;

        static bool legacyFixOnceOnly = false;
        static uint processId1, processId2 = 0;
        static bool reSizing;
        // private static bool movedWindowBefore = false;
        private static bool clickFix = false;
        private static bool clickFixMiddleClick = false;
        private static bool clickFixNoTop = false;
        private static bool leftWindow = false;
        private static bool fixExplorerInside = false; // technically not used
        private static bool fixChromiumWindow = false;
        private static bool ignoreRB = false;
        private static bool ignoreMB = false;

        private static string innerMovedHwndTitle = "";
        private static string[] innerHwndSpecialResize = { "Speed Dial - Opera", " - Brave", " - Google Chrome", " - Edge" /*probably edge too*/ }; // hwnds that require a different method

        private static int deltaX;
        private static int deltaY;
        private static int newX;
        private static int newY;
        private static int width;
        private static int height;
        private static string exePath = "";
        private static string[] dontMove = {/*"",*/ "Windows Shell Experience Host", "New notification", "System tray overflow window.", "Quick settings", "EarTrumpet", "Notification Overflow", "Task Switching", "Program Manager", "Start", "Notification Center", "Ear Trumpet", "Task Manager", "Windows Input Experience" };
        private static string[] blockedParts;





        public MainWindow()
        {

            mutex = new Mutex(true, mutexId, out bool createdNew);
            if (!createdNew)
            {
                Application.Current.Shutdown();
            }
            hookIdMouse = SetHook(WH_MOUSE_LL, mouseProc);
            hookIdKeyboard = SetKeyboardHook(WH_KEYBOARD_LL, keyboardProc);
            try
            {
                exePath = Process.GetCurrentProcess().MainModule!.FileName;
            }
            catch
            {
                ShowErrorNotif(Properties.Lang.Message_error_executable);
            }


            InitializeComponent();
            LoadVDAPI();
            versionHeader.Header += Process.GetCurrentProcess().MainModule.FileVersionInfo.FileVersion.ToString();
            TouchInjector.InitializeTouchInjection(feedbackMode: TouchFeedback.NONE);

            if (KeyExists("canOverflow")) canOverflow = (bool)ReadKeyValue("canOverflow");
            else canOverflow = true;
            if (KeyExists("OpacityScrolling")) OpacityScrolling = (bool)ReadKeyValue("OpacityScrolling");
            if (KeyExists("DesktopScrolling")) DesktopScrolling = (bool)ReadKeyValue("DesktopScrolling");
            if (KeyExists("WheelGesture")) WheelGesture = (bool)ReadKeyValue("WheelGesture");
            if (KeyExists("bringToFront")) bringToFront = (bool)ReadKeyValue("bringToFront");
            if (KeyExists("enabled")) enabled = (bool)ReadKeyValue("enabled");
            if (KeyExists("canResizeCorners")) canResizeCorners = (bool)ReadKeyValue("canResizeCorners");
            if (KeyExists("canScrollWindows")) canScrollWindows = (bool)ReadKeyValue("canScrollWindows");
            if (KeyExists("startOnLogin")) startOnLogin = (bool)ReadKeyValue("startOnLogin");
            if (KeyExists("useSnapAssist")) useSnapAssist = (bool)ReadKeyValue("useSnapAssist");
            if (KeyExists("maximizeOnTop")) maximizeOnTop = (bool)ReadKeyValue("maximizeOnTop");

            AutoFancyZones_Button.Visibility = Process.GetProcessesByName("PowerToys.FancyZones").Any(p => !p.HasExited) ? Visibility.Visible : Visibility.Collapsed;
            if (KeyExists("AutoFancyZones") && AutoFancyZones_Button.Visibility == Visibility.Visible)
            {
                AutoFancyZones = (bool)ReadKeyValue("AutoFancyZones");
                AutoFancyZones_Button.IsChecked = AutoFancyZones;
            }



            bringToFront = true; // Other option isn't available
            DesktopScrolling_Button.IsChecked = DesktopScrolling;
            Resize_Button.IsChecked = canResizeCorners;
            Corner_Button.IsChecked = canOverflow;
            UseSnapAssist_Button.IsChecked = useSnapAssist;
            MaximizeOnTop_Button.IsEnabled = useSnapAssist;
            MaximizeOnTop_Button.IsChecked = maximizeOnTop;
            Opacity_Button.IsChecked = OpacityScrolling;
            WheelGesture_Button.IsChecked = WheelGesture;
            ScrollWindows_Button.IsChecked = canScrollWindows;
            SetTop_Button.IsChecked = bringToFront;
            Autorun_Button.IsChecked = startOnLogin;
            Enable_Button.IsChecked = enabled;
            Update();

        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _taskbarRestartMessage = RegisterWindowMessage("TaskbarCreated");
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource.AddHook(WndProc);
            SetWindowLong(new WindowInteropHelper(this).Handle, GWL_EX_STYLE, (GetWindowLong(new WindowInteropHelper(this).Handle, GWL_EX_STYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
        }
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == _taskbarRestartMessage) TrayIcon.Register();
            return IntPtr.Zero;
        }
        public void LoadVDAPI()
        {
            DetectedVDImplementation = Loader.GetImplementationForOS();
            VDAPI = Loader.LoadImplementationWithFallback(DetectedVDImplementation);
        }

        private async void Update()
        {
            await Updater.CheckUpdateAsync(url);
            // mutex.ReleaseMutex();
            // Application.Current.Shutdown();
        }


        private static void MouseEvent(MouseEventFlags flags)
        {
            ignoreRB = true;
            ignoreMB = true;
            mouse_event((uint)flags, 0, 0, 0, 0);
            ignoreRB = false;
            ignoreMB = false;
        }


        private static string GetProcessExecutableFilePath(uint processId)
        {
            const int MAX_PATH = 256; // Max file path lenght
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
            if (hProcess != IntPtr.Zero)
            {
                StringBuilder sb = new StringBuilder(MAX_PATH);
                int length = (int)GetModuleFileNameEx(hProcess, IntPtr.Zero, sb, MAX_PATH);
                CloseHandle(hProcess);
                if (length > 0)
                {
                    return sb.ToString();
                }
            }
            return ""; // IMPORTANT: It was null before
        }

        public static async Task ShowErrorNotifInput()
        {
            if (isToastNotificationShown) return;
            isToastNotificationShown = true;
            await Task.Run(() => new ToastContentBuilder().AddText(Properties.Lang.Message_error_input).Show());
            await Task.Delay(7000);
            isToastNotificationShown = false;
        }
        public static async Task ShowErrorNotif(string msg)
        {
            await Task.Run(() => new ToastContentBuilder().AddText(msg).Show());
        }
        public static void SafeInput(Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch
            {
                ShowErrorNotifInput();
            }
        }

        public static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (DesktopScrolling && nCode >= 0 && wParam == (IntPtr)0x0101) // WM_KEYUP
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // left and right Win key
                if ((vkCode == 0x5B || vkCode == 0x5C) && didChangeVirtualDesktopWinScroll)
                {
                    SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.ESCAPE));
                    didChangeVirtualDesktopWinScroll = false;
                }
            }
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                int vkCode = Marshal.ReadInt32(lParam);
                if ((msg == 0x0100 || msg == 0x0104) && (vkCode == 160 || vkCode == 161)) // shift down
                {
                    shiftDown = true;
                }
                else if (vkCode == 160 || vkCode == 161)
                {
                    shiftDown = false;
                }
            }
            return CallNextHookEx(hookIdKeyboard, nCode, wParam, lParam);
        }


        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (!enabled) return 0;
            if (SWindow != null && shiftDown)
            {
                GetWindowThreadProcessId(GetAncestor(innerHwnd(lParam), 2), out uint pid);
                string[] parts = new[]
                {
                    Process.GetProcessById((int)pid).ProcessName,
                    parentHwndTitle(innerHwnd(lParam)),
                    GetWindowTitle(innerHwnd(lParam))
                };
                blockedParts = parts;

                SWindow.tempblacklisted_TB1.Text = string.IsNullOrEmpty(parts[0]) ? "*" : parts[0];
                SWindow.tempblacklisted_TB2.Text = string.IsNullOrEmpty(parts[1]) ? "*" : parts[1];
                SWindow.tempblacklisted_TB3.Text = string.IsNullOrEmpty(parts[2]) ? "*" : parts[2];
            }

            if (reSizing)
            {
                clickFix = false;
            }
            try
            {
                hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
            }
            catch
            {
                ShowErrorNotif(Properties.Lang.Message_error_hookstruct);
            }
            GetWindowRect(GetAncestor(innerHwnd(lParam), 2), out rect);
            leftWindow = mouseOutsideHwndWindow(rect);



            if (clickFix && prevMovedInnerhWnd != innerHwnd(lParam) && !reSizing) // prevInnerHwnd is updated every time after this if statement, runs if clickFix and if the inner hwnd changes under the cursor
            {
                if ((int)(GetAsyncKeyState(0x02) & 0x8000) == 0)
                {
                    Debug.WriteLine("Clickfix canceled, right click state is up");
                    clickFix = false;
                    fixChromiumWindow = false;
                    legacyFixOnceOnly = false;
                    fixExplorerInside = false;
                    return 0;
                }

                if (prevInnerHwnd != innerHwnd(lParam)) GetWindowThreadProcessId(WindowFromPoint(hookStruct.pt), out processId2);

                if (fixChromiumWindow)
                {
                    // Debug.WriteLine($"{innerMovedHwndTitle}\t{innerHwndTitle(lParam)}");
                    // Debug.WriteLine(prevMovedhWnd+"\t" + innerHwnd(lParam));
                    if (innerMovedHwndTitle.Contains("Chrome Legacy Window") && !innerHwndTitle(lParam).Contains("Chrome Legacy Window") && !leftWindow)
                    {
                        fixChromiumWindow = false;
                        clickFix = false;
                        Debug.WriteLine("Chrome Legacy Window -> different HWND");
                        Task.Run(() =>
                        {
                            Thread.Sleep(10);
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));
                            MouseEvent(MouseEventFlags.RightUp);
                            Thread.Sleep(10); // otherwise context menu can pop up
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));

                        });

                        return 0;
                    }

                    if (legacyFixOnceOnly && prevInnerHwnd != innerHwnd(lParam) && !mouseOutsideHwndWindow(rect) && innerMovedHwndTitle.Contains("Chrome Legacy Window") && parentHwnd(innerHwnd(lParam)) == prevMovedhWnd)
                    {
                        fixChromiumWindow = false;
                        legacyFixOnceOnly = false;
                        clickFix = false;
                        Debug.WriteLine("Chrome Legacy Window -> Chrome Legacy Window");
                        if (!mouseOutsideHwndWindow(rect))
                        {
                            Task.Run(() =>
                            {
                                //  MouseEvent(MouseEventFlags.LeftDown);
                                MouseEvent(MouseEventFlags.LeftUp);
                                MouseEvent(MouseEventFlags.RightUp); // 0.9.23+ update, cursor doesn't lag

                            });
                        }
                        return 0;
                    }

                    if (innerMovedHwndTitle.Contains("Chrome Legacy Window") && prevMovedInnerhWnd != innerHwnd(lParam) &&
                        GetAncestor(prevMovedhWnd, 2) == GetAncestor(innerHwnd(lParam), 2) &&
                        parentHwndTitle(prevMovedhWnd) == parentHwndTitle(innerHwnd(lParam)))
                    {
                        Debug.WriteLine("smt else happened");
                    }
                }

                // Click fix part
                if (fixExplorerInside && GetFileDescription(hWnd) == "Windows Explorer")
                {
                    //  Debug.WriteLine("clickfix explorer | ", innerHwndTitle(lParam));
                    fixExplorerInside = false;
                    clickFix = false;
                    Task.Run(() =>
                    {
                        Debug.WriteLine("Windows Explorer ClickFix");
                        //  MouseEvent(MouseEventFlags.LeftDown);
                        //  MouseEvent(MouseEventFlags.LeftUp);
                        SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));
                        Thread.Sleep(10);
                        MouseEvent(MouseEventFlags.RightUp);
                        SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));
                        SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));
                        SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));
                    });
                    //hWnd = GetAncestor(innerHwnd(lParam), 2); // idk its not needed here at all
                    return 0;
                }

                if (!leftWindow && bringToFront  /*  '!' ??, but works */ )
                {
                    // TODO: Checks whether right click event happened after left the window (bool)
                    // GetWindowThreadProcessId(prevMovedhWnd, out processId1);
                    Thread.Sleep(10);
                    GetWindowThreadProcessId(WindowFromPoint(hookStruct.pt), out processId2);
                    if (processId1 != processId2 /*&& !isChromium(hWnd, lParam)*/)
                    {
                        Debug.WriteLine("Out of window BTF Click fix");
                        clickFix = false;
                        Task.Run(() =>
                        {
                            MouseEvent(MouseEventFlags.RightDown | MouseEventFlags.RightUp | MouseEventFlags.LeftDown | MouseEventFlags.LeftUp); // MouseEvent(MouseEventFlags.LeftDown | MouseEventFlags.LeftUp);
                        });
                        return 0;
                    }
                    else
                    {
                        //prevInnerHwnd = innerHwnd(lParam);
                        // fixChromiumWindow = true;
                    }
                }

                if (!movingWindow && (dontMove.Contains(GetWindowTitle(hWnd)) || CheckBlacklisted()))  // example: moving a window under the taskbar releasing the mouse
                {
                    Debug.WriteLine("behind? Click Fixed");
                    Task.Run(() =>
                    {
                        MouseEvent(MouseEventFlags.LeftDown | MouseEventFlags.LeftUp | MouseEventFlags.RightDown | MouseEventFlags.RightUp);
                    });
                    clickFix = false;
                    return 0;
                }

                //   leftWindow = true;
            }
            if (clickFix && prevMovedInnerhWnd == innerHwnd(lParam) && !reSizing && fixChromiumWindow) // for browsers
            {
                if ((int)(GetAsyncKeyState(0x02) & 0x8000) == 0)
                {
                    Debug.WriteLine("Clickfix canceled, right click state is up (2)");
                    clickFix = false;
                    fixChromiumWindow = false;
                    legacyFixOnceOnly = false;
                    fixExplorerInside = false;
                    return 0;
                }
                if (fixChromiumWindow && !innerMovedHwndTitle.Contains("Chrome Legacy Window") && !innerHwndTitle(lParam).Contains("Chrome Legacy Window")) // title to title (usually)
                {

                    //  clickFix = false; // No need for ClickFix as it releases the right mouse button
                    Debug.WriteLine($"fix inner hwnd: {fixChromiumWindow}");
                    Task.Run(() =>
                    {
                        Thread.Sleep(10);
                        SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));
                        SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));
                        MouseEvent(MouseEventFlags.RightUp);
                        Thread.Sleep(10); // Otherwise context menu can pop up
                        SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));
                        SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));
                    });
                    return 0;
                }
            }


            prevInnerHwnd = innerHwnd(lParam);

            if (clickFixMiddleClick)
            {

                if (mouseOutsideHwndWindow(rect))
                {
                    clickFixMiddleClick = false;
                    Task.Run(() =>
                    {
                        Debug.WriteLine("SAME WINDOW RETURN MID");
                        mouse_event(0x0040, 0, 0, 0, 0);

                    });
                }
                if (innerMovedHwndTitle.Contains("Chrome Legacy Window") && !innerHwndTitle(lParam).Contains("Chrome Legacy Window") && !leftWindow)
                {
                    clickFixMiddleClick = false;
                    Debug.WriteLine("[MIDDLE CLICK FIX] Chrome Legacy Window -> different HWND");
                    Task.Run(() =>
                    {
                        mouse_event(0x0040, 0, 0, 0, 0);
                    });
                    return 0;
                }
                if (parentHwnd(hWnd) != parentHwnd(innerHwnd(lParam)))
                {
                    clickFixMiddleClick = false;
                    Debug.WriteLine("HWND (parent) -> different parent HWND");
                    Task.Run(() =>
                    {
                        mouse_event(0x0040, 0, 0, 0, 0);
                    });
                    return 0;
                }

            }


            if (wParam == (IntPtr)WM_MBUTTONUP && canScrollWindows)
            {
                windowOrder.Clear();
            }
            if (wParam == (IntPtr)WM_MBUTTONUP)
            {
                touchMoving = false;
                dontTouchMove = false;
                middleMouseDown = false;
                if (touchPointers != null)
                {
                    for (int i = 0; i < touchPointers.Length; i++)
                    {
                        touchPointers[i].PointerInfo.PointerFlags = PointerFlags.UP;
                    }
                    TouchInjector.InjectTouchInput(touchPointers.Length, touchPointers);
                    touchPointers = null;
                }
            }

            if (wParam == (IntPtr)WM_MBUTTONUP && canScrollWindows && !ignoreMB /*&& didScrollWindows*/) // TODO: Logic might be faulty check later! Looks like it WORKS perfectly tho.
            {

                middleMouseDownHasCount = false;
                GetWindowRect(GetAncestor(innerHwnd(lParam), 2), out rect);
                Debug.WriteLine("GOT RECT");
                if (windowOrder.Count != 0 && didScrollWindows)
                {
                    didScrollWindows = false;

                    if (windowOrder.IndexOf(GetAncestor(WindowFromPoint(hookStruct.pt), 2)) != 0)
                    {
                        Debug.WriteLine("MIDDLE mouse UP");
                        windowOrder.Clear(); didScrollWindows = false;
                    }
                    else
                    {
                        innerMovedHwndTitle = GetWindowTitle(innerHwnd(lParam));
                        windowOrder.Clear();
                        clickFixMiddleClick = true;
                        Debug.WriteLine("MIDDLE mouse UP + MIDDLE CLICK FIX true");
                        return -1;
                    }
                }
            }

            if (wParam == (IntPtr)WM_MBUTTONDOWN)
            {
                middleMouseDown = true;
                initialMiddleClickPosition = GetMousePosition();
            }

            if (wParam == (IntPtr)WM_MOUSEMOVE && middleMouseDown && !touchMoving)
            {
                if (Math.Abs(initialMiddleClickPosition.Y - hookStruct.pt.Y) > 100)
                {
                    dontTouchMove = true;
                    Debug.WriteLine("touch return");
                    return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);
                }
            }


            if (wParam == (IntPtr)WM_MOUSEMOVE && WheelGesture && middleMouseDown && !dontTouchMove &&
                Math.Abs(initialMiddleClickPosition.X - hookStruct.pt.X) > 20)
            {
                touchMoving = true;
                if (touchPointers == null)
                {
                    touchPointers = new PointerTouchInfo[4];
                    for (int i = 0; i < touchPointers.Length; i++)
                    {
                        touchPointers[i].PointerInfo.PointerId = (uint)i;
                        touchPointers[i].PointerInfo.pointerType = PointerInputType.TOUCH;
                        touchPointers[i].PointerInfo.PtPixelLocation.X = hookStruct.pt.X;
                        touchPointers[i].PointerInfo.PtPixelLocation.Y = hookStruct.pt.Y;
                        touchPointers[i].PointerInfo.PointerFlags = PointerFlags.DOWN | PointerFlags.INRANGE | PointerFlags.INCONTACT;
                    }
                }
                else
                {
                    for (int i = 0; i < touchPointers.Length; i++)
                    {
                        touchPointers[i].PointerInfo.PtPixelLocation.X = hookStruct.pt.X;
                        touchPointers[i].PointerInfo.PtPixelLocation.Y = hookStruct.pt.Y;
                        touchPointers[i].PointerInfo.PointerFlags = PointerFlags.UPDATE | PointerFlags.INRANGE | PointerFlags.INCONTACT;
                    }
                }
                TouchInjector.InjectTouchInput(touchPointers.Length, touchPointers);
                return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);
            }

            if (wParam == (IntPtr)WM_MBUTTONDOWN && canScrollWindows)
            {
                string windowTitle;
                RECT _rect;
                RECT startRect;



                IntPtr _hWnd = GetAncestor(WindowFromPoint(hookStruct.pt), 2);
                //  hWnd = GetAncestor(WindowFromPoint(hookStruct.pt), 2);

                SetForegroundWindow(_hWnd);
                GetWindowRect(_hWnd, out _rect);
                GetWindowRect(_hWnd, out startRect);


                Debug.WriteLine($"hWnd: {hWnd}\t_hWnd: {_hWnd}\tstartRtop: {startRect.top}");

                #region fullscreen check
                int style = GetWindowLong(_hWnd, -16);

                screen = Screen.FromPoint(new System.Drawing.Point(hookStruct.pt.X, hookStruct.pt.Y));


                if ((startRect.right - startRect.left) >= screen.Bounds.Width &&
                    (startRect.bottom - startRect.top) >= screen.Bounds.Height &&
                    startRect.top <= 0 && startRect.right >= screen.Bounds.Width)
                {
                    Debug.WriteLine((startRect.right - startRect.left) + "x" + (startRect.bottom - startRect.top) + " [MB 3rd check] Its fullscreen, not gonna attempt to scroll between windows!");
                    return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);
                }
                #endregion


                do
                {
                    if (IsWindowVisible(_hWnd) && !IsIconic(_hWnd))
                    {
                        GetWindowRect(_hWnd, out _rect);
                        StringBuilder sb = new StringBuilder(256);
                        GetWindowText(_hWnd, sb, sb.Capacity);
                        windowTitle = sb.ToString();
                        if (!string.IsNullOrWhiteSpace(windowTitle) && (!dontMove.Contains(GetWindowTitle(_hWnd)) || !CheckBlacklisted()))
                        {
                            if (startRect.left < _rect.right &&
                                startRect.right > _rect.left &&
                                startRect.top < _rect.bottom &&
                                startRect.bottom > _rect.top
                                )
                            {
                                var hwnd = GetAncestor(_hWnd, 2);
                                if (!dontMove.Contains(parentHwndTitle(hwnd)))
                                {
                                    windowOrder.Add(hwnd);
                                }
                            }
                        }
                    }

                    _hWnd = GetWindow(_hWnd, 2);
                }
                while (_hWnd != IntPtr.Zero);





                if (windowOrder.Count() > 0)
                {
                    middleMouseDownHasCount = true;
                }
                Debug.WriteLine("done gathering window hwnds\n");
                foreach (var item in windowOrder)
                {
                    Debug.WriteLine($"{item}:\t{parentHwndTitle(item)}");
                }
                Debug.WriteLine("\n");
                //  MessageBox.Show($"md: {middleMouseDown}\n{windowOrder.Count()}");

                return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);

            }


            if (wParam == (IntPtr)WM_MOUSEWHEEL) // Virtual desktop change in the top corners of the screen
            {

                if ((hookStruct.pt.X <= 0 || hookStruct.pt.X >= Screen.FromPoint(new System.Drawing.Point(hookStruct.pt.X, hookStruct.pt.Y)).Bounds.Width - 1) && hookStruct.pt.Y <= 0)
                {
                    if ((short)(hookStruct.mouseData >> 16) < 0)
                    {
                        Task.Run(() => { VDAPI.SwitchBackward(); });
                    }
                    else
                    {
                        Task.Run(() => { VDAPI.SwitchForward(); });
                    }
                }
                if (OpacityScrolling && (GetAsyncKeyState(0x12) & 0x8000) != 0) // if Alt is down
                {
                    IntPtr _hwnd = GetAncestor(innerHwnd(lParam), 2);
                    if ((dontMove.Contains(GetWindowTitle(_hwnd)) || CheckBlacklisted()) | string.IsNullOrEmpty(GetWindowTitle(_hwnd)))
                    {
                        return 0;
                    }
                    int GWL_EXSTYLE = -20;
                    int WS_EX_LAYERED = 0x80000;
                    int LWA_ALPHA = 0x2;
                    int style = GetWindowLong(_hwnd, GWL_EXSTYLE);
                    GetLayeredWindowAttributes(_hwnd, out uint crKey, out byte alpha, out uint dwFlags);

                    if ((short)(hookStruct.mouseData >> 16) < 0)
                    {
                        if (alpha - 20 >= 30)
                        {
                            alpha -= 20;
                        }
                        else if (alpha != 0)
                        {
                            alpha = 20;
                        }
                        else
                        {
                            alpha = 255 - 20;
                        }
                    }
                    else
                    {
                        if (alpha + 20 <= 255 && alpha != 0)
                        {
                            alpha += 20;
                        }
                        else
                        {
                            alpha = 255;
                        }
                    }
                    SetWindowLong(_hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED);
                    SetLayeredWindowAttributes(_hwnd, 0, alpha, (uint)LWA_ALPHA);
                    return -1;
                }
                if (DesktopScrolling && ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0))
                {

                    didChangeVirtualDesktopWinScroll = true;
                    try
                    {
                        SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.ESCAPE));
                    }
                    catch { }

                    if ((short)(hookStruct.mouseData >> 16) < 0)
                    {
                        Task.Run(() => { VDAPI.SwitchBackward(); });

                        Debug.WriteLine("VDesktop changed -1");
                    }
                    else
                    {
                        Task.Run(() => { VDAPI.SwitchForward(); });
                        Debug.WriteLine("VDesktop changed +1");
                    }
                    return -1; // to prevent scrolling if it doesn't changes desktop

                }
            }


            if (wParam == (IntPtr)WM_MOUSEWHEEL && middleMouseDownHasCount && windowOrder.Count > 0 && canScrollWindows)
            {
                didScrollWindows = true;
                int currentIndex = windowOrder.IndexOf(GetForegroundWindow());
                // Debug.WriteLine($"index: {currentIndex}");
                //  Debug.WriteLine(currentIndex);
                if (currentIndex == -1)
                {
                    return -1;
                }
                if ((short)((hookStruct.mouseData >> 16) & 0xffff) < 0 && (currentIndex + 1) <= windowOrder.Count() - 1) // Scroll down
                {
                    // Debug.WriteLine($"down\t{GetWindowTitle(windowOrder[currentIndex + 1])}");
                    SetForegroundWindow(windowOrder[currentIndex + 1]);
                    Debug.WriteLine($"set +1: {windowOrder[currentIndex + 1]}");

                }
                else if ((short)((hookStruct.mouseData >> 16) & 0xffff) > 0 && currentIndex - 1 != -1) // Scroll up
                {

                    // Debug.WriteLine($"up\t\t{GetWindowTitle(windowOrder[currentIndex - 1])}");
                    SetForegroundWindow(windowOrder[currentIndex - 1]);
                    Debug.WriteLine($"set -1: {windowOrder[currentIndex - 1]}");

                }
                return -1;
            }



            if (wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                if (usedFancyZones)
                {
                    //  return -1;
                }
                if (!bringToFront && !clickFixNoTop)
                {

                    if (leftWindow)
                    {
                        Debug.WriteLine("DOING");
                        Task.Run(() =>
                        {
                            MouseEvent(MouseEventFlags.LeftDown | MouseEventFlags.LeftUp | MouseEventFlags.RightDown | MouseEventFlags.RightUp);
                        });
                    }
                    if (prevhWnd != hWnd)
                    {
                        Debug.WriteLine("DIF hwnd");
                        Task.Run(() =>
                        {
                            MouseEvent(MouseEventFlags.LeftDown | MouseEventFlags.LeftUp | MouseEventFlags.RightDown | MouseEventFlags.RightUp);
                        });
                    }
                    leftWindow = false;
                    clickFixNoTop = true;
                    clickFix = true;
                }
                // prevhWnd = hWnd;
                // leftdown = true;// Debug.WriteLine(leftdown);
            }


            if (wParam == (IntPtr)WM_LBUTTONUP)
            {
                if (usedFancyZones)
                {
                    // return -1;
                }
                // leftdown = false;
            }

            if (wParam == (IntPtr)WM_RBUTTONDOWN)
            {


                if (ignoreRB)
                {
                    // fixExplorerInside = false;
                    return 0;
                }
                GetWindowThreadProcessId(GetAncestor(innerHwnd(lParam), 2), out uint pid);
                blockedParts = new[]
                {
                    Process.GetProcessById((int)pid).ProcessName,
                    parentHwndTitle(innerHwnd(lParam)),
                    GetWindowTitle(innerHwnd(lParam))
                };

                hitTestCode = HitTest(rect, hookStruct.pt);
                GetWindowRect(GetAncestor(innerHwnd(lParam), 2), out RECT tempRect);

                fancyZonesRunning = Process.GetProcessesByName("PowerToys.FancyZones").Any(p => !p.HasExited) ? true : false;

                if (hwndCheck)
                {
                    hWnd = GetAncestor(innerHwnd(lParam), 2);
                }

                screen = Screen.FromPoint(new System.Drawing.Point(hookStruct.pt.X, hookStruct.pt.Y));
                int style = GetWindowLong(GetAncestor(innerHwnd(lParam), 2), -16);

                if ((style & WS_OVERLAPPEDWINDOW) == 0)
                {
                    Debug.WriteLine(" [2nd check] Its fullscreen, not gonna attempt to move it!");
                    return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);
                }
                else if ((tempRect.right - tempRect.left) >= screen.Bounds.Width &&
                    (tempRect.bottom - tempRect.top) >= screen.Bounds.Height &&
                    tempRect.top <= 0 && tempRect.right >= screen.Bounds.Width)
                {
                    if (!string.IsNullOrEmpty(parentHwndTitle(lParam)))
                    {
                        Debug.WriteLine((tempRect.right - tempRect.left) + "x" + (tempRect.bottom - tempRect.top) + " [RB 3rd check] Its fullscreen, not gonna attempt to move it!");
                        return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);
                    }

                }

                hwndCheck = true;
                rightMouseDown = true;
                doOnce = false;
                //doneMove = false;
                previousMousePosition = hookStruct.pt;
                initialMouseClickPosition = GetMousePosition();

                // Debug.WriteLine($"Mouse pos: {initialMouseClickPosition}");
                //  return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);
                Debug.WriteLine("rbutton didnt get interrupted");

            }

            if (wParam == (IntPtr)WM_RBUTTONUP)
            {

                //   doneMove = true;
                //if (fixExplorerInside && clickFix)
                //{
                //    // left click not working
                //    clickFix = false;
                //    fixExplorerInside = false;
                //}
                Debug.WriteLine("Right button up called");
                if (ignoreRB)
                {
                    Debug.WriteLine("RB UP ignored");
                    return 0;
                }
                Debug.WriteLine("RB UP");
                GetWindowRect(hWnd, out rect);
                movingExplorerFix = true;
                if (reSizing)
                {
                    clickFix = false;
                    fixChromiumWindow = false;
                    rightMouseDown = false;

                    Task.Run(() =>
                    {
                        MouseEvent(MouseEventFlags.LeftUp | MouseEventFlags.LeftDown);
                        if (innerHwndSpecialResize.Contains(GetWindowTitle(innerHwnd(lParam))))  // Theres a list for browser inner hwnds that are right clicked after release 
                        {
                            Debug.WriteLine("alt after left click after resizing");
                            Thread.Sleep(5);
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));
                        }
                    });

                    doOnce2 = true;
                    doOnce = false;

                    reSizing = false;
                    return -1;
                }

                doOnce2 = true;
                doOnce = false;
                rightMouseDown = false;

                if (!bringToFront && leftWindow)
                {
                    Debug.WriteLine("ignor");
                    ignoreRB = true;
                    Task.Run(() =>
                    {
                        MouseEvent(MouseEventFlags.RightDown | MouseEventFlags.RightUp);
                        ignoreRB = false;
                        //  MouseEvent(MouseEventFlags.LeftDown | MouseEventFlags.LeftUp );
                    });
                    clickFix = false;
                }



                // rbutton up 
                if (GetFileDescription(hWnd) == "Windows Explorer" && fixExplorerInside && movingWindow)
                {

                    Debug.WriteLine($"FEL EXPLORERRR: \"{innerHwndTitle(lParam)}\"");

                    // clickFix = false;
                    // fixExplorerInside = true;// for testing
                    if (innerHwndTitle(lParam) == "")
                    {
                        clickFix = false;

                        Task.Run(() => // After moving it's doing this
                        {
                            Debug.WriteLine("OKI");


                            MouseEvent(MouseEventFlags.RightUp);
                            Thread.Sleep(5);
                            try
                            {
                                SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.ESCAPE));
                                SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.ESCAPE));
                            }
                            catch (Exception e)
                            {
                                //ShowErrorNotif(e.Message);
                            }


                        });
                    }
                    else if (innerHwndTitle(lParam) == "Navigation Pane")
                    {
                        Task.Run(() =>
                        {
                            Debug.WriteLine("MOKI");

                            MouseEvent(MouseEventFlags.RightUp);
                            Thread.Sleep(5);
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.ESCAPE));
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.ESCAPE)); // NOTE: temp, but seems good
                        });
                    }
                    //  clickFix = false;
                    hwndCheck = true;

                    //  return 0;

                }
                //hwndCheck = true; gethere


                if (movingWindow)
                {
                    movingWindow = false;
                    NotifyWinEvent(0x000B, hWnd, 0, 0);




                    if (isChromium(hWnd, lParam))
                    {
                        Debug.WriteLine("FIX CHROME INSIDE ");
                        fixChromiumWindow = true;
                        legacyFixOnceOnly = true;
                    }
                    else
                    {
                        fixChromiumWindow = false;
                    }
                    clickFix = true;
                    var currScreen = Screen.GetBounds(new System.Drawing.Point(hookStruct.pt.X, hookStruct.pt.Y));
                    if (!maximizeOnTop && useSnapAssist && (hookStruct.pt.Y <= 40 && !usedFancyZones))
                    {
                        Debug.WriteLine("set full 1");
                        Task.Run(() =>
                        {
                            ShowWindow(hWnd, 3);
                            Thread.Sleep(10);
                            GetWindowRect(hWnd, out rect);
                        });
                    }
                    // if (useSnapAssist && (hookStruct.pt.Y <= 40 | rect.top < 0 && !usedFancyZones))
                    else if (maximizeOnTop && useSnapAssist && (hookStruct.pt.Y <= 40 | rect.top < 0 && !usedFancyZones))
                    {
                        Debug.WriteLine("set full 2");
                        Task.Run(() =>
                        {
                            ShowWindow(hWnd, 3);
                            Thread.Sleep(10);
                            GetWindowRect(hWnd, out rect);
                        });
                    }
                    else if (useSnapAssist && (hookStruct.pt.X <= currScreen.X + 40 && !usedFancyZones))
                    {
                        Debug.WriteLine("set full left");
                        Task.Run(() =>
                        {
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.LWIN));
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.LEFT));
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.LEFT));
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.LWIN));
                            Thread.Sleep(10);
                            GetWindowRect(hWnd, out rect);
                        });
                    }
                    else if (useSnapAssist && (hookStruct.pt.X - Screen.GetBounds(new System.Drawing.Point(hookStruct.pt.X, hookStruct.pt.Y)).X + 40 >= currScreen.Width && !usedFancyZones))
                    {
                        Debug.WriteLine("set full right");
                        Task.Run(() =>
                        {
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.LWIN));
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.RIGHT));
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.RIGHT));
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.LWIN));
                            Thread.Sleep(10);
                            GetWindowRect(hWnd, out rect);
                        });
                    }
                    if (usedFancyZones)
                    {
                        GetWindowRect(hWnd, out rect);

                        Debug.WriteLine("FancyZone: off");
                        usedFancyZones = false;
                        ReleaseCapture();

                        Task.Run(() =>
                        {
                            MouseEvent(MouseEventFlags.LeftDown);
                            MouseEvent(MouseEventFlags.LeftUp);

                            SendMessage(hWnd, WM_LBUTTONDOWN, IntPtr.Zero, IntPtr.Zero);
                            SendMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);

                            Task.Delay(10);
                            if (AutoFancyZones)
                            {
                                SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.LSHIFT));
                                Debug.WriteLine("SHIFT UP\n\n");
                            }
                            else
                            {
                                MoveWindow(hWnd, newX, newY, width, height, true);
                                Debug.WriteLine("NOT AUTO");
                            }
                            GetWindowRect(hWnd, out rect);

                        });
                    }
                    return -1; // -1 to prevent right-click

                }

                if (mouseOutsideHwndWindow(rect))
                {
                    Debug.WriteLine("RB UP && outside of rect");
                    return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);
                }
                else
                {
                    Debug.WriteLine("RB UP cancelright");
                    movingWindow = false;
                }
            }


            if (wParam == (IntPtr)WM_MOUSEMOVE && rightMouseDown && (
                    Math.Abs(initialMouseClickPosition.Y - hookStruct.pt.Y) > 3 |
                    Math.Abs(initialMouseClickPosition.X - hookStruct.pt.X) > 3))
            {
                if (hwndCheck)
                {
                    hWnd = GetAncestor(innerHwnd(lParam), 2);
                    hwndCheck = false;
                }
                if (dontMove.Contains(GetWindowTitle(hWnd)) || CheckBlacklisted())
                {
                    // doneMove = true;
                    return 0;  // Won't move if hwnd is in the list
                }
                GetWindowRect(hWnd, out rectBefore);
                ShowWindow(hWnd, 9); // Set to normal from maximized
                GetWindowRect(hWnd, out rect);
                if (doOnce2)
                {
                    NotifyWinEvent(0x000A, hWnd, 0, 0);
                }
                if (doOnce2 && canResizeCorners)
                {
                    doOnce2 = false;

                    if (hitTestCode != 0 && !IsZoomed(GetAncestor(innerHwnd(lParam), 3)))
                    {
                        ignoreRB = true;
                        reSizing = true;
                        Debug.WriteLine(innerHwnd(lParam));

                        resizeHwnd = GetAncestor(innerHwnd(lParam), 2);
                        Task.Run(() =>
                        {
                            IntPtr result;
                            MouseEvent(MouseEventFlags.RightUp);
                            Thread.Sleep(5);
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));
                            SendMessageTimeout(resizeHwnd, 0x00A1, (IntPtr)hitTestCode, IntPtr.Zero, 0, 5, out result);
                            SendMessageTimeout(resizeHwnd, 0x00A1, (IntPtr)hitTestCode, IntPtr.Zero, 0, 5, out result);
                            // MouseEvent(MouseEventFlags.RightUp);
                            Debug.WriteLine($"{GetWindowTitle(GetAncestor(innerHwnd(lParam), 2))}\t{GetWindowTitle(innerHwnd(lParam))}");
                            if (innerHwndSpecialResize.Contains(GetWindowTitle(innerHwnd(lParam)))) //  A list for browser inner HWNDs that are right clicking before it even moves, this fixes that issue
                            {
                                Debug.WriteLine("escape");
                                Thread.Sleep(5);
                                SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));
                                SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));
                            }
                            Debug.WriteLine(innerHwnd(lParam));
                        });
                    }
                    else
                    {
                        Debug.WriteLine("moving\n");
                    }
                }

                if (hitTestCode != 0 && canResizeCorners) // This needs to be done here too
                {
                    Task.Run(() =>
                    {
                        IntPtr result;
                        SendMessageTimeout(resizeHwnd, 0x00A1, (IntPtr)hitTestCode, IntPtr.Zero, 0, 5, out result);
                    });
                }

                if (bringToFront && !doOnce && !reSizing)
                {
                    fixChromiumWindow = false;
                    legacyFixOnceOnly = false;
                    clickFix = false;

                    movingWindow = true;
                    SetForegroundWindow(hWnd); // NOTE: Moved here from out of if
                    doOnce = true;
                    ShowWindow(hWnd, 9); // Set to normal from maximized

                    GetWindowRect(hWnd, out rect);
                    System.Drawing.Point currentMousePosition = new System.Drawing.Point(hookStruct.pt.X, hookStruct.pt.Y);
                    deltaX = currentMousePosition.X - previousMousePosition.X;
                    deltaY = currentMousePosition.Y - previousMousePosition.Y;
                    newX = rect.left + deltaX;
                    newY = rect.top + deltaY;
                    width = rect.right - rect.left;
                    height = rect.bottom - rect.top;
                    if (IsZoomed(hWnd))
                    {
                        Debug.WriteLine("ZOOMED");

                        if (rect.top <= 0)
                        {
                            Debug.WriteLine("normalize: 1");
                            MoveWindow(hWnd, hookStruct.pt.X - width / 2, 0, width, height, true);
                        }
                        else
                        {
                            Debug.WriteLine("normalize: 2");
                            MoveWindow(hWnd, hookStruct.pt.X - width / 2, hookStruct.pt.Y - height / 2, width, height, true);
                        }
                    }

                    if (useSnapAssist && (rect.top <= 0 && rect.left >= 1))
                    {
                        Debug.WriteLine("normalize: 3");
                        MoveWindow(hWnd, hookStruct.pt.X - width / 2, 0, width, height, true);
                    }
                    if (useSnapAssist && (rect.left <= 0 && rectBefore.top <= 1))
                    {
                        Debug.WriteLine("normalize: 4");
                        if (rect.top <= 0)
                        {
                            MoveWindow(hWnd, 0, 0, width, height, true);
                        }
                        else
                        {
                            MoveWindow(hWnd, 0, rect.top, width, height, true);
                        }
                    }
                    screen = Screen.FromPoint(currentMousePosition);
                    if (useSnapAssist && (rect.right >= screen.WorkingArea.Width && rectBefore.top <= 1))
                    {
                        Debug.WriteLine("normalize: 5");
                        if (rect.top <= 0)
                        {
                            MoveWindow(hWnd, screen.WorkingArea.Width - rect.width, 0, width, height, true);
                        }
                        else
                        {
                            MoveWindow(hWnd, screen.WorkingArea.Width - rect.width, rect.top, width, height, true);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("no normalization");
                    }
                    GetWindowRect(hWnd, out rect); // Setting rect after window is normal sized
                    innerMovedHwndTitle = GetWindowTitle(innerHwnd(lParam));
                    prevMovedhWnd = hWnd;
                    prevMovedInnerhWnd = innerHwnd(lParam);
                    prevMovedlParam = lParam;
                    return 0;
                } // INFORMATION: TODO: doesn't work as intented try to fix it later. UPDATE: it does

                if (movingExplorerFix)
                {
                    fixExplorerInside = true;
                    if (GetFileDescription(hWnd) == "Windows Explorer")
                    {
                        Debug.WriteLine("ITS EXP");
                        Task.Run(() =>
                        {
                            Thread.Sleep(5);
                            MouseEvent(MouseEventFlags.LeftDown);
                            MouseEvent(MouseEventFlags.LeftUp);
                        });
                    }
                    else if (GetFileDescription(hWnd) == "Windows Explorer")
                    {
                        // Debug.WriteLine("moving exp WM fix");
                        Task.Run(() =>
                        {
                            Thread.Sleep(5);
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));

                            Thread.Sleep(5);
                            MouseEvent(MouseEventFlags.LeftDown);
                            MouseEvent(MouseEventFlags.LeftUp);
                            Thread.Sleep(5);
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));
                            SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU));
                            SafeInput(() => new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU));
                        });
                    }
                    movingExplorerFix = false;
                }


                if (!reSizing)
                {
                    if (fancyZonesRunning && ((GetAsyncKeyState(0x10) & 0x8000) != 0 | AutoFancyZones)) // If shift is down [FancyZones support]
                    {

                        if (AutoFancyZones && (GetAsyncKeyState(0x10) & 0x8000) == 0)
                        {
                            Debug.WriteLine("SHIFT DOWN ONCE");
                            Task.Run(() =>
                            {
                                SafeInput(() => new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.LSHIFT));
                            });
                        }

                        IntPtr _hwnd = FindWindowEx(0, 0, null, parentHwndTitle(GetAncestor(innerHwnd(lParam), 2)));
                        PostMessage(_hwnd, WM_SYSCOMMAND, SC_MOVE, lParam);
                        if (isChromium(_hwnd, lParam) && !usedFancyZones)
                        {
                            usedFancyZones = true;
                            Debug.WriteLine("FancyZone: is chrome");
                            Window newWindow = new Window();

                            Rectangle redRectangle = new Rectangle
                            {
                                Width = 20,
                                Height = 20,
                                Fill = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255))
                            };

                            Canvas canvas = new Canvas();
                            canvas.Children.Add(redRectangle);
                            newWindow.Content = canvas;

                            newWindow.Left = hookStruct.pt.X - 10;
                            newWindow.Top = hookStruct.pt.Y - 10;
                            newWindow.Width = 20;
                            newWindow.Height = 20;
                            newWindow.WindowStyle = WindowStyle.None;
                            newWindow.ShowActivated = true;
                            newWindow.Topmost = true;
                            newWindow.AllowsTransparency = true;
                            newWindow.Background = Brushes.Transparent;
                            newWindow.Show();



                            IntPtr hwnd = new WindowInteropHelper(newWindow).Handle;
                            Task.Run(() =>
                            {
                                SendMessage(hwnd, WM_LBUTTONDOWN, IntPtr.Zero, IntPtr.Zero);
                                SendMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);
                            });
                            Task.Delay(20);
                            newWindow.Close();
                        }
                        usedFancyZones = true;
                    }

                    System.Drawing.Point currentMousePosition = new System.Drawing.Point(hookStruct.pt.X, hookStruct.pt.Y);
                    deltaX = currentMousePosition.X - previousMousePosition.X;
                    deltaY = currentMousePosition.Y - previousMousePosition.Y;

                    newX = rect.left + deltaX;
                    newY = rect.top + deltaY;
                    width = rect.right - rect.left;
                    height = rect.bottom - rect.top;
                    screen = Screen.FromPoint(currentMousePosition);

                    if (!canOverflow)
                    {
                        if (newX + width > screen.WorkingArea.Width) newX = (int)screen.WorkingArea.Width - width;
                        if (newX < screen.WorkingArea.Left) newX = (int)screen.WorkingArea.Left;
                        if (newY + height > screen.WorkingArea.Height) newY = (int)(screen.WorkingArea.Height - height);
                        if (newY < screen.WorkingArea.Top) newY = (int)screen.WorkingArea.Top;
                    }

                    if (hookStruct.pt.X == 0 || hookStruct.pt.X == screen.Bounds.Width - 1) newX = rect.left;
                    if (hookStruct.pt.Y == 0 || hookStruct.pt.Y == screen.Bounds.Height - 1) newY = rect.top;

                    IntPtr hMonitor = MonitorFromPoint(new POINT(hookStruct.pt.X, hookStruct.pt.Y), MONITOR_DEFAULTTONEAREST);
                    uint dpiX, dpiY;
                    int result = GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                    int scale = (int)(dpiX / 96.0);
                    MoveWindow(hWnd, newX * scale, newY * scale, width * scale, height * scale, true);

                }

                GetWindowThreadProcessId(hWnd, out processId1);
                previousMousePosition = hookStruct.pt;
            }
            prevhWnd = hWnd;
            return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);
        }



        #region logic funcs



        private void WriteKey(MenuItem Button, string boolName, ref bool Bool)
        {
            if (Button.IsChecked) Bool = true;
            else Bool = false;
            WriteToRegistry(boolName, Bool);
        }

        private static string GetFileDescription(IntPtr hwnd)
        {
            GetWindowThreadProcessId(hwnd, out uint processId);
            string executableFilePath = GetProcessExecutableFilePath(processId);
            string fileDescription = "";
            if (!string.IsNullOrEmpty(executableFilePath))
            {
                fileDescription = FileVersionInfo.GetVersionInfo(executableFilePath).FileDescription ?? "";
            }
            return fileDescription;

        }
        private static int HitTest(RECT windowRect, POINT pt)
        {
            const int edgeSize = 100;
            bool left = pt.X >= windowRect.left && pt.X < windowRect.left + edgeSize;
            bool right = pt.X < windowRect.right && pt.X >= windowRect.right - edgeSize;
            bool top = pt.Y >= windowRect.top && pt.Y < windowRect.top + edgeSize;
            bool bottom = pt.Y < windowRect.bottom && pt.Y >= windowRect.bottom - edgeSize;

            if (left && top) return HTTOPLEFT;
            if (left && bottom) return HTBOTTOMLEFT;
            if (right && top) return HTTOPRIGHT;
            if (right && bottom) return HTBOTTOMRIGHT;
            if (left) return HTLEFT;
            if (right) return HTRIGHT;
            if (top) return HTTOP;
            if (bottom) return HTBOTTOM;

            return 0;
        }

        private static bool mouseOutsideHwndWindow(RECT rect)
        {
            if ((int)GetMousePosition().X < rect.left || (int)GetMousePosition().X > rect.right ||
                    (int)GetMousePosition().Y < rect.top || (int)GetMousePosition().Y > rect.bottom)
            {
                return true;
            }
            return false;
        }

        private static string parentHwndTitle(IntPtr hWnd)
        {
            return GetWindowTitle(GetAncestor(hWnd, 2));
        }
        private static nint parentHwnd(IntPtr hWnd)
        {
            return GetAncestor(hWnd, 2);
        }
        private static string innerHwndTitle(IntPtr lParam)
        {
            MSLLHOOKSTRUCT hs = new MSLLHOOKSTRUCT();
            try
            {
                hs = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
            }
            catch (Exception)
            {
                ShowErrorNotif(Properties.Lang.Message_error_window_title);
            }
            return GetWindowTitle(WindowFromPoint(hs.pt));
        }

        private static bool isChromium(IntPtr hWnd, IntPtr lParam)
        {
            return !EnumChildWindows(hWnd, (hWnd, lParam) =>
            {
                const int nChars = 256;
                StringBuilder windowText = new StringBuilder(nChars);
                GetWindowText(hWnd, windowText, nChars);
                if (windowText.ToString().Contains("Chrome Legacy Window", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }

        private static IntPtr innerHwnd(IntPtr lParam)
        {
            MSLLHOOKSTRUCT hs = new MSLLHOOKSTRUCT();
            try
            {
                hs = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
            }
            catch (Exception)
            {
                ShowErrorNotif(Properties.Lang.Message_error_window_handle);
            }
            return WindowFromPoint(hs.pt);
        }
        #endregion


        static string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 1024;
            StringBuilder sb = new StringBuilder(nChars);
            GetWindowText(hWnd, sb, nChars);
            return sb.ToString();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            e.Cancel = true;
            this.Hide();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.ShowInTaskbar = false;
            this.Width = 0;
            this.Height = 0;
            this.ResizeMode = ResizeMode.NoResize;
            this.WindowStyle = WindowStyle.None;
            this.Visibility = Visibility.Collapsed;
            this.Left = -500;
            this.Top = -500;

            CloseHide(); // Makes sure that the window is properly "hidden"
        }
        private void CloseHide()
        {
            Task.Run(() =>
            {
                Thread.Sleep(100);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    this.Close();
                });
            });
        }

        private void Enable_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(Enable_Button, nameof(enabled), ref enabled);
        }

        private void ScrollWindows_Button_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(ScrollWindows_Button, nameof(canScrollWindows), ref canScrollWindows);
        }

        private void SetTop_Button_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(SetTop_Button, nameof(bringToFront), ref bringToFront);
        }

        private void Resize_Button_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(Resize_Button, nameof(canResizeCorners), ref canResizeCorners);
        }

        private void Corner_Button_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(Corner_Button, nameof(canOverflow), ref canOverflow);
        }
        private void UseSnapAssist_Button_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(UseSnapAssist_Button, nameof(useSnapAssist), ref useSnapAssist);
            MaximizeOnTop_Button.IsEnabled = useSnapAssist;
        }
        private void MaximizeOnTop_Button_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(MaximizeOnTop_Button, nameof(maximizeOnTop), ref maximizeOnTop);
        }
        private void WheelGesture_Button_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(WheelGesture_Button, nameof(WheelGesture), ref WheelGesture);
        }
        private void Opacity_Button_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(Opacity_Button, nameof(OpacityScrolling), ref OpacityScrolling);
        }
        private void DesktopScrolling_Button_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(DesktopScrolling_Button, nameof(DesktopScrolling), ref DesktopScrolling);
        }
        private void AutoFancyZones_Button_Checked(object sender, RoutedEventArgs e)
        {
            WriteKey(AutoFancyZones_Button, nameof(AutoFancyZones), ref AutoFancyZones);

        }
        private void ExitApp(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }



        private void WriteToRegistry(string keyName, object value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\DragWin"))
                {
                    key.SetValue(keyName, value);
                }
            }
            catch { }

        }
        private object ReadKeyValue(string keyName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\DragWin")!)
                {
                    if (key != null)
                    {
                        object value = key.GetValue(keyName)!;
                        if (value != null)
                        {
                            if (value is bool)
                            {
                                return (bool)value;
                            }
                            else if (bool.TryParse(value.ToString(), out bool boolValue))
                            {
                                return boolValue;
                            }

                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Lang.Message_error_read_key + ": " + ex.Message);
                return false;
            }
        }
        private bool KeyExists(string keyName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\DragWin")!)
                {
                    if (key != null)
                    {
                        if (key.GetValue(keyName) != null)
                        {
                            bool value = Convert.ToBoolean(key.GetValue(keyName));
                            Debug.WriteLine($"exists: {keyName}");
                            return true;
                        }
                    }
                }
                Debug.WriteLine($"doesnt exist: {keyName}");
                return false;
            }
            catch
            {
                Debug.WriteLine($"error opening HKCU\\SOFTWARE\\DragWin");
                return false;
            }
        }


        public static void AddToAutoRun(string appName, string appPath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true)!)
                {
                    key.SetValue(appName, appPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Lang.Message_error_autorun_add + ": " + ex.Message);
            }
        }

        public static void RemoveFromAutoRun(string appName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true)!)
                {
                    key.DeleteValue(appName, false);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Lang.Message_error_autorun_remove + ": " + ex.Message);
            }
        }

        private void StartLogin_Checked(object sender, RoutedEventArgs e)
        {
            if (Autorun_Button.IsChecked)
            {
                startOnLogin = true;
                AddToAutoRun("DragWin", exePath ?? "");
            }
            else
            {
                startOnLogin = false;
                RemoveFromAutoRun("DragWin");
            }
            WriteToRegistry("startOnLogin", startOnLogin);
        }

        private void Exit_Button_MouseEnter(object sender, MouseEventArgs e)
        {
            Exit_Button.Foreground = new SolidColorBrush(Color.FromArgb(255, 254, 107, 107));
        }

        private void TrayIcon_RightClick(Wpf.Ui.Tray.Controls.NotifyIcon sender, RoutedEventArgs e)
        {
            AutoFancyZones_Button.Visibility = Process.GetProcessesByName("PowerToys.FancyZones").Any(p => !p.HasExited) ? Visibility.Visible : Visibility.Collapsed;
            if (KeyExists("AutoFancyZones") && Process.GetProcessesByName("PowerToys.FancyZones") != null)
            {
                AutoFancyZones = (bool)ReadKeyValue("AutoFancyZones");
                AutoFancyZones_Button.IsChecked = AutoFancyZones;
            }
        }


        private void Update_Button_Click(object sender, RoutedEventArgs e)
        {
            Update();
        }

        private void Settings_Button_Click(object sender, RoutedEventArgs e)
        {
            if (SWindow != null)
            {
                SWindow.Close();
            }
            SWindow = new SettingsWindow();
            SWindow.Show();
        }

        private static bool CheckBlacklisted()
        {
            if (blockedParts == null || blockedParts.Length < 3) return false;
            List<string[]> loadedBlocklist = LoadBlockedListFromRegistry();
            return loadedBlocklist.Any(item =>
                (item[0] == "*" || item[0] == blockedParts[0]) &&
                (item[1] == "*" || item[1] == blockedParts[1]) &&
                (item[2] == "*" || item[2] == blockedParts[2]));
        }

        private static List<string[]> LoadBlockedListFromRegistry()
        {
            var result = new List<string[]>();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\DragWin"))
                {
                    if (key == null) return result;

                    var blockedItems = key.GetValue("BlockedItems") as string;
                    if (string.IsNullOrEmpty(blockedItems)) return result;

                    var panels = blockedItems.Split(new[] { "|,|" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var panelData in panels)
                    {
                        var texts = panelData.Split(new[] { "<|>" }, StringSplitOptions.None);
                        if (texts.Length < 3) continue;

                        result.Add(new[] { texts[0], texts[1], texts[2] });
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        private void Exit_Button_MouseLeave(object sender, MouseEventArgs e)
        {
            Exit_Button.Foreground = Brushes.White;
        }
    }
}