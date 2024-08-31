using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsInput;
using WindowsInput.Native;
using static Interop;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace DragWinWPF
{
    public partial class MainWindow
    {

        // mutex
        // -----------------
        private const string mutexId = "{47bb6j6d-l38e-4bb5-92jb-a239cr17bj9e}";
        private Mutex mutex; // it is used 

        // mouse
        // -----------------
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
        private static int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
        private static int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

        private static IntPtr hookIdMouse = IntPtr.Zero;
        private static IntPtr hWnd;
        private static IntPtr resizeHwnd;
        private static IntPtr prevhWnd;
        private static IntPtr prevMovedhWnd;
        private static IntPtr prevMovedInnerhWnd;
        private static IntPtr prevMovedlParam;
        private static IntPtr prevInnerHwnd;
        private static List<IntPtr> windowOrder = new List<IntPtr>();
        private static List<IntPtr> windowHandles = new List<IntPtr>();

        private static MSLLHOOKSTRUCT hookStruct;

        private static RECT rect;
        private static Point initialMouseClickPosition;
        private static POINT previousMousePosition;
        private static Interop.LowLevelMouseProc mouseProc = MouseHookCallback;

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);


        //ui buttons
        // -----------------
        private static bool bringToFront = true;
        private static bool canOverflow = true;
        private static bool startOnLogin = false;
        private static bool canResizeCorners = false;
        private static bool canScrollWindows = false;


        //logic 
        // -----------------
        private static bool hwndCheck = false;
        private static bool rightMouseDown = false;
        private static bool middleMouseDown = false;
        // private static bool leftdown = false;
        private static bool doOnce = true;
        private static bool doOnce2 = true;
        // private static bool doneMove = false;
        private static bool movingWindow = false;
        private static bool movingExplorerFix = true;
        private static bool enabled = true;
        // private static bool outsideFix = false;
        private static bool didScrollWindows = false;

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
        private static int gap = 20;
        private static string exePath = "";
        private static string[] dontMove = { /*"",*/"", "Quick settings", "EarTrumpet", "Notification Overflow", "Task Switching", "Program Manager", "Start", "Notification Center", "Ear Trumpet", "Task Manager","Windows Input Experience" };






        public MainWindow()
        {
            mutex = new Mutex(true, mutexId, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Application is already running.", "DragWin", MessageBoxButton.OK);
                Application.Current.Shutdown();
            }
            hookIdMouse = SetHook(WH_MOUSE_LL, mouseProc);
            try
            {
                exePath = Process.GetCurrentProcess().MainModule!.FileName;
            }
            catch
            {
                MessageBox.Show("Couldn't get the executable's path.");
            }
           
           
            InitializeComponent();


            if (KeyExists("canOverflow"))       canOverflow = (bool)ReadKeyValue("canOverflow"); 
            else canOverflow = true;
            if (KeyExists("bringToFront"))      bringToFront = (bool)ReadKeyValue("bringToFront");
            if (KeyExists("enabled"))           enabled = (bool)ReadKeyValue("enabled");
            if (KeyExists("canResizeCorners"))  canResizeCorners = (bool)ReadKeyValue("canResizeCorners");
            if (KeyExists("canScrollWindows"))  canScrollWindows = (bool)ReadKeyValue("canScrollWindows");
            if (KeyExists("startOnLogin"))      startOnLogin = (bool)ReadKeyValue("startOnLogin");


            bringToFront = true; // Other option isn't available
            Resize_Button.IsChecked = canResizeCorners;
            Corner_Button.IsChecked = canOverflow;
            ScrollWindows_Button.IsChecked = canScrollWindows;
            SetTop_Button.IsChecked = bringToFront;
            Autorun_Button.IsChecked = startOnLogin;
            Enable_Button.IsChecked = enabled;
        }


        private static void SimulateKeyPress(params VirtualKeyCode[] keys)
        {
            InputSimulator simulator = new InputSimulator();

            foreach (var key in keys)
            {
                simulator.Keyboard.KeyDown(key);
            }
            foreach (var key in keys)
            {
                simulator.Keyboard.KeyUp(key);
            }
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


        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {


            if (!enabled) return 0;
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
                MessageBox.Show("error setting hookStruct");
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
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);
                            MouseEvent(MouseEventFlags.RightUp);
                            Thread.Sleep(10); // otherwise context menu can pop up
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);

                        });

                        return 0;
                    }

                    if (prevInnerHwnd != innerHwnd(lParam) && !mouseOutsideHwndWindow(rect) && innerMovedHwndTitle.Contains("Chrome Legacy Window") && legacyFixOnceOnly && parentHwnd(innerHwnd(lParam)) == prevMovedhWnd)
                    {
                        fixChromiumWindow = false;
                        Debug.WriteLine("Chrome Legacy Window -> Chrome Legacy Window");
                        legacyFixOnceOnly = false;
                        Task.Run(() =>
                        {
                            if (!mouseOutsideHwndWindow(rect))
                            {
                                MouseEvent(MouseEventFlags.LeftDown);
                                MouseEvent(MouseEventFlags.LeftUp);
                            }

                        });
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
                if (GetFileDescription(hWnd) == "Windows Explorer" && fixExplorerInside)
                {
                    //  Debug.WriteLine("clickfix explorer | ", innerHwndTitle(lParam));
                    fixExplorerInside = false;
                    clickFix = false;
                    Task.Run(() =>
                    {
                        Debug.WriteLine("Windows Explorer ClickFix");
                        //  MouseEvent(MouseEventFlags.LeftDown);
                        //  MouseEvent(MouseEventFlags.LeftUp);
                        new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);
                        Thread.Sleep(10);
                        MouseEvent(MouseEventFlags.RightUp);
                        new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);
                        new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);
                        new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);
                    });
                    //hWnd = GetAncestor(innerHwnd(lParam), 2); // idk its not needed here at all
                    return 0;
                }

                if (bringToFront && !leftWindow /*  '!' ??, but works */ && clickFix)
                {
                    //Debug.WriteLine("in list : " + dontMove.Contains(GetAncestor(innerHwnd(lParam), 2).ToString()));
                    // TODO: Checks whether right click event happened after left the window (bool)
                    // GetWindowThreadProcessId(prevMovedhWnd, out processId1);
                    Thread.Sleep(10);
                    GetWindowThreadProcessId(WindowFromPoint(hookStruct.pt), out processId2);
                    if (processId1 != processId2 /*&& !isChromium(hWnd, lParam)*/)
                    {
                        Debug.WriteLine("Out of window BTF Click fix");
                        clickFix = false;
                        //  Debug.WriteLine($"leftd: {(GetAsyncKeyState(0x01) & 0x8000)}\trightd: {(GetAsyncKeyState(0x02) & 0x8000)}");
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




                // if (!movingWindow && dontMove.Contains(parentHwndTitle(lParam)))  // example: moving a window under the taskbar releasing the mouse
                // INFO: changed from the above ^ 
                if (!movingWindow && dontMove.Contains(GetWindowTitle(hWnd)))  // example: moving a window under the taskbar releasing the mouse
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

                    clickFix = false; // No need for ClickFix as it releases the right mouse button
                    Debug.WriteLine($"fix inner hwnd: {fixChromiumWindow}");
                    Task.Run(() =>
                    {
                        Thread.Sleep(10);
                        new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);
                        new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);
                        MouseEvent(MouseEventFlags.RightUp);
                        Thread.Sleep(10); // Otherwise context menu can pop up
                        new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);
                        new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);
                    });
                    return 0;
                }
            }




            prevInnerHwnd = innerHwnd(lParam);

            // if (cursorPos.y >= SystemParameters.WorkArea.Height || dontMove.Contains(t))

            //if (hwndCheck) // this is not neccessery gethere
            //{
            //   // Debug.WriteLine("CHEC");
            //  //  hWnd = GetAncestor(innerHwnd(lParam), 2);
            //}

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
                //  middleMouseDown = false;
            }

            if (wParam == (IntPtr)WM_MBUTTONUP && canScrollWindows && !ignoreMB /*&& didScrollWindows*/) // TODO: Logic might be faulty check later! Looks like it WORKS perfectly tho.
            {

                middleMouseDown = false;
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
                ///////// TODO: this is wrong so commented
                //if (((style & unchecked((int)0x80000000)) != 0 && (style & 0x00800000) == 0 && (style & 0x00C00000) == 0))
                //{
                //    Debug.WriteLine("[MB 1st check] Window is borderless fulscreen.");
                //    return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);
                //}
                /////////
                if ((startRect.right - startRect.left) >= (int)SystemParameters.PrimaryScreenWidth &&
                    (startRect.bottom - startRect.top) >= (int)SystemParameters.PrimaryScreenHeight &&
                    startRect.top <= 0 && startRect.right >= (int)SystemParameters.PrimaryScreenWidth)
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
                        if (!string.IsNullOrWhiteSpace(windowTitle) && !dontMove.Contains(GetWindowTitle(_hWnd)))
                        {
                            if (startRect.left < _rect.right &&
                                startRect.right > _rect.left &&
                                startRect.top < _rect.bottom &&
                                startRect.bottom > _rect.top
                                )
                            {
                                windowOrder.Add(GetAncestor(_hWnd, 2));
                            }
                        }
                    }

                    _hWnd = GetWindow(_hWnd, 2);
                }
                while (_hWnd != IntPtr.Zero);





                if (windowOrder.Count > 0)
                {
                    middleMouseDown = true;
                }
                Debug.WriteLine("done gathering window hwnds\n");
                foreach (var item in windowOrder)
                {
                    Debug.WriteLine($"{item}:\t{parentHwndTitle(item)}");
                }
                Debug.WriteLine("\n");
                return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);

            }



            if (wParam == (IntPtr)WM_MOUSEWHEEL && middleMouseDown && windowOrder.Count > 0 && canScrollWindows)
            {
                didScrollWindows = true;
                int currentIndex = windowOrder.IndexOf(GetForegroundWindow());
                // Debug.WriteLine($"index: {currentIndex}");
                //  Debug.WriteLine(currentIndex);
                if (currentIndex == -1)
                {
                    return -1;
                }
                if ((short)((hookStruct.mouseData >> 16) & 0xffff) < 0 && (currentIndex + 1) <= windowOrder.Count - 1) // Scroll down
                {
                    // Debug.WriteLine($"down\t{GetWindowTitle(windowOrder[currentIndex + 1])}");

                    SetForegroundWindow(windowOrder[currentIndex + 1]);
                    Debug.WriteLine($"set +1: {windowOrder[currentIndex + 1]}");

                }
                else if ((short)((hookStruct.mouseData >> 16) & 0xffff) > 0 && currentIndex - 1 != -1) // Scroll up
                {
                    //Debug.WriteLine($"up\t\t{GetWindowTitle(windowOrder[currentIndex - 1])}");
                    SetForegroundWindow(windowOrder[currentIndex - 1]);
                    Debug.WriteLine($"set -1: {windowOrder[currentIndex - 1]}");

                }
                return -1;
            }


            if (wParam == (IntPtr)WM_MOUSEWHEEL) // Virtual desktop change in the top corners of the screen
            {

                if ((hookStruct.pt.x <= 0 || hookStruct.pt.x >= screenWidth - 1) && hookStruct.pt.y <= 0)
                {
                    //  Debug.WriteLine("OK");
                    if ((short)(hookStruct.mouseData >> 16) < 0)
                    {
                        SimulateKeyPress(VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL, VirtualKeyCode.LEFT);
                    }
                    else
                    {
                        SimulateKeyPress(VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL, VirtualKeyCode.RIGHT);
                    }
                }
            }
            if (wParam == (IntPtr)WM_LBUTTONDOWN)

            {
                if (!bringToFront && !clickFixNoTop)
                {
                    Debug.WriteLine("leftwin és nem front");
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
                // leftdown = false;
            }

            if (wParam == (IntPtr)WM_RBUTTONDOWN)
            {


                if (ignoreRB)
                {
                    // fixExplorerInside = false;
                    return 0;
                }
                hitTestCode = HitTest(rect, hookStruct.pt);
                GetWindowRect(GetAncestor(innerHwnd(lParam), 2), out RECT tempRect);

                if (hwndCheck)
                {
                    hWnd = GetAncestor(innerHwnd(lParam), 2);
                }

                int style = GetWindowLong(hWnd, -16);

                if ((tempRect.right - tempRect.left) >= (int)SystemParameters.PrimaryScreenWidth &&
                    (tempRect.bottom - tempRect.top) >= (int)SystemParameters.PrimaryScreenHeight &&
                    tempRect.top <= 0 && tempRect.right >= (int)SystemParameters.PrimaryScreenWidth)
                {
                    string s = parentHwndTitle(lParam);
                    Debug.WriteLine($"ok: {s}");
                    if (string.IsNullOrEmpty(parentHwndTitle(lParam)))
                    {

                        Debug.WriteLine($"ummm: {GetAncestor(innerHwnd(lParam), 2)}");
                    }
                    else
                    {
                        Debug.WriteLine($"ummm: {GetAncestor(innerHwnd(lParam), 2)}");
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
                Debug.WriteLine("whatiffffffff");

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
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);
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

                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.ESCAPE);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.ESCAPE);

                        });
                    }
                    else if (innerHwndTitle(lParam) == "Navigation Pane")
                    {
                        Task.Run(() =>
                        {
                            Debug.WriteLine("MOKI");

                            MouseEvent(MouseEventFlags.RightUp);
                            Thread.Sleep(5);
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.ESCAPE);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.ESCAPE); // NOTE: temp, but seem good
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

                    if (isChromium(hWnd, lParam))
                    {
                        Debug.WriteLine("FIX CHROME INSIDE PLS");
                        fixChromiumWindow = true;
                        legacyFixOnceOnly = true;
                    }
                    else
                    {
                        fixChromiumWindow = false;
                    }
                    clickFix = true;
                    if ((int)GetMousePosition().Y <= 1 | rect.top < 0)
                    {
                        Debug.WriteLine("set full");
                        Task.Run(() =>
                        {
                            ShowWindow(hWnd, 3);
                            Thread.Sleep(10);
                            GetWindowRect(hWnd, out rect);
                        });
                    }
                    else if ((int)GetMousePosition().X <= 1)
                    {
                        Debug.WriteLine("set full left");
                        Task.Run(() =>
                        {
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.LWIN);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.LEFT);
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.LEFT);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.LWIN);
                            Thread.Sleep(10);
                            GetWindowRect(hWnd, out rect);
                        });
                    }
                    else if ((int)GetMousePosition().X + 1 >= screenWidth)
                    {
                        Debug.WriteLine("set full right");
                        Task.Run(() =>
                        {
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.LWIN);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.RIGHT);
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.RIGHT);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.LWIN);
                            Thread.Sleep(10);
                            GetWindowRect(hWnd, out rect);
                        });
                    }

                    return -1;
                }

                if (mouseOutsideHwndWindow(rect))
                {
                    Debug.WriteLine("RB UP");
                    return CallNextHookEx(hookIdMouse, nCode, wParam, lParam);
                }
                else
                {
                    Debug.WriteLine("RB UP cancelright");
                    movingWindow = false;
                }
            }


            if (wParam == (IntPtr)WM_MOUSEMOVE && rightMouseDown && (
                    Math.Abs(initialMouseClickPosition.Y - (int)GetMousePosition().Y) > 3 |
                    Math.Abs(initialMouseClickPosition.X - (int)GetMousePosition().X) > 3))
            {

                if (hwndCheck)
                {
                    hWnd = GetAncestor(innerHwnd(lParam), 2);
                    hwndCheck = false;
                }

                if (dontMove.Contains(GetWindowTitle(hWnd)))
                {
                    // doneMove = true;
                    return 0;  // Won't move if hwnd is in the list
                }


                ShowWindow(hWnd, 9); // Set to normal from maximized
                GetWindowRect(hWnd, out rect);

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
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);
                            SendMessageTimeout(resizeHwnd, 0x00A1, (IntPtr)hitTestCode, IntPtr.Zero, 0, 5, out result);
                            SendMessageTimeout(resizeHwnd, 0x00A1, (IntPtr)hitTestCode, IntPtr.Zero, 0, 5, out result);
                            // MouseEvent(MouseEventFlags.RightUp);
                            Debug.WriteLine($"{GetWindowTitle(GetAncestor(innerHwnd(lParam), 2))}\t{GetWindowTitle(innerHwnd(lParam))}");
                            if (innerHwndSpecialResize.Contains(GetWindowTitle(innerHwnd(lParam)))) //  A list for browser inner HWNDs that are right clicking before it even moves, this fixes that issue
                            {
                                Debug.WriteLine("escape");
                                Thread.Sleep(5);
                                new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);
                                new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);
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
                    System.Drawing.Point currentMousePosition = new System.Drawing.Point(hookStruct.pt.x, hookStruct.pt.y);
                    deltaX = currentMousePosition.X - previousMousePosition.x;
                    deltaY = currentMousePosition.Y - previousMousePosition.y;
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
                            MoveWindow(hWnd, (int)GetMousePosition().X - width / 2, 0, width, height, true);
                        }
                        else
                        {
                            Debug.WriteLine("normalize: 2");
                            MoveWindow(hWnd, (int)GetMousePosition().X - width / 2, (int)GetMousePosition().Y - height / 2, width, height, true);
                        }
                    }

                    if (rect.top <= 0 && rect.left >= 1)
                    {
                        Debug.WriteLine("normalize: 3");
                        MoveWindow(hWnd, (int)GetMousePosition().X - width / 2, 0, width, height, true);
                    }
                    if (rect.top <= 1 && rect.left <= 0)
                    {
                        Debug.WriteLine("normalize: 4");
                        MoveWindow(hWnd, 0, 0, width, height, true);
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
                        Debug.WriteLine("moving exp papirWM fix");
                        Task.Run(() =>
                        {
                            Thread.Sleep(5);
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);

                            Thread.Sleep(5);
                            MouseEvent(MouseEventFlags.LeftDown);
                            MouseEvent(MouseEventFlags.LeftUp);
                            Thread.Sleep(5);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);
                            new InputSimulator().Keyboard.KeyDown(VirtualKeyCode.MENU);
                            new InputSimulator().Keyboard.KeyUp(VirtualKeyCode.MENU);
                        });
                    }
                    movingExplorerFix = false;
                }


                if (!reSizing)
                {
                    System.Drawing.Point currentMousePosition = new System.Drawing.Point(hookStruct.pt.x, hookStruct.pt.y);
                    deltaX = currentMousePosition.X - previousMousePosition.x;
                    deltaY = currentMousePosition.Y - previousMousePosition.y;

                    newX = rect.left + deltaX;
                    newY = rect.top + deltaY;
                    width = rect.right - rect.left;
                    height = rect.bottom - rect.top;

                    if (!canOverflow)
                    {
                        if (newX + width > SystemParameters.WorkArea.Width) newX = (int)SystemParameters.WorkArea.Width - width;
                        if (newX < SystemParameters.WorkArea.Left) newX = (int)SystemParameters.WorkArea.Left;
                        if (newY + height > SystemParameters.WorkArea.Height) newY = (int)(SystemParameters.WorkArea.Height - height);
                        if (newY < SystemParameters.WorkArea.Top) newY = (int)SystemParameters.WorkArea.Top;
                    }

                    if ((int)GetMousePosition().X == 0 || (int)GetMousePosition().X == screenWidth - 1) newX = rect.left;
                    if ((int)GetMousePosition().Y == 0 || (int)GetMousePosition().Y == screenHeight - 1) newY = rect.top;

                    MoveWindow(hWnd, newX, newY, width, height, true);

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
            bool left = pt.x >= windowRect.left && pt.x < windowRect.left + edgeSize;
            bool right = pt.x < windowRect.right && pt.x >= windowRect.right - edgeSize;
            bool top = pt.y >= windowRect.top && pt.y < windowRect.top + edgeSize;
            bool bottom = pt.y < windowRect.bottom && pt.y >= windowRect.bottom - edgeSize;

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
                MessageBox.Show("error getting window handle's title");
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
                MessageBox.Show("Error getting window handle");
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
                    if (key != null )
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
                MessageBox.Show("Error reading key: " + ex.Message);
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
                MessageBox.Show("Error adding to autorun: " + ex.Message);
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
                MessageBox.Show("Error removing from autorun: " + ex.Message);
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



        private void Exit_Button_MouseLeave(object sender, MouseEventArgs e)
        {
            Exit_Button.Foreground = Brushes.White;
        }
    }
}