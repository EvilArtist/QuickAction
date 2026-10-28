using QuickAction.Core;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace QuickAction;
internal static class WindowsServices
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int l,int t,int r,int b,int ew,int eh);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] private static extern bool EqualRgn(IntPtr first,IntPtr second);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr hwnd,IntPtr region);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left,Right,Top,Bottom; }
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out PointNative point);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out RectNative rect);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(PointNative point,uint flags);
    [StructLayout(LayoutKind.Sequential)] internal struct PointNative { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RectNative { public int Left,Top,Right,Bottom; }
    public static PointNative Cursor { get { GetCursorPos(out var p); return p; } }
    public static Forms.Screen Screen(Configuration c)=>Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==c.Monitor) ?? Forms.Screen.PrimaryScreen!;
    public static double Scale(Forms.Screen screen)
    {
        var monitor=MonitorFromPoint(new(){ X=screen.Bounds.Left+screen.Bounds.Width/2,Y=screen.Bounds.Top+screen.Bounds.Height/2 },2);
        return GetDpiForMonitor(monitor,0,out var x,out _)==0 ? x/96.0 : 1;
    }
    public static bool Backdrop(Window window)
    {
        var hwnd=new WindowInteropHelper(window).Handle;
        HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor=Colors.Transparent;
        // The client area must participate in DWM composition; a transparent
        // WPF brush alone does not remove the native window's opaque backing.
        var margins=new Margins { Left=-1,Right=-1,Top=-1,Bottom=-1 };
        DwmExtendFrameIntoClientArea(hwnd,ref margins);
        int borderColor=unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        DwmSetWindowAttribute(hwnd,34,ref borderColor,4);
        int cornerPreference=1; // The custom region owns the entire silhouette.
        DwmSetWindowAttribute(hwnd,33,ref cornerPreference,4);
        using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        if(SystemParameters.HighContrast || (key?.GetValue("EnableTransparency") is int enabled && enabled==0)) { int none=1; DwmSetWindowAttribute(hwnd,38,ref none,4); return false; }
        int kind=3; return DwmSetWindowAttribute(hwnd,38,ref kind,4)==0;
    }
    public static void Round(Window window,double radius)
    {
        var handle=new WindowInteropHelper(window).Handle;
        if(handle==IntPtr.Zero || !GetWindowRect(handle,out var rect)) return;
        var dpi=VisualTreeHelper.GetDpi(window).DpiScaleX;
        var region=CreateRoundRectRgn(0,0,rect.Right-rect.Left,rect.Bottom-rect.Top,(int)Math.Round(radius*2*dpi),(int)Math.Round(radius*2*dpi));
        if(region==IntPtr.Zero) return;
        var current=CreateRectRgn(0,0,0,0);
        try
        {
            // SetWindowRgn itself sends WM_WINDOWPOSCHANGED. Avoid an endless
            // message/reclip loop when the region already matches.
            if(GetWindowRgn(handle,current)>0 && EqualRgn(current,region)) { DeleteObject(region); return; }
        }
        finally { DeleteObject(current); }
        if(SetWindowRgn(handle,region,true)==0) DeleteObject(region);
    }
    public static void Startup(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if(enabled) key.SetValue("QuickAction","\""+Environment.ProcessPath+"\""); else key.DeleteValue("QuickAction",false);
    }
    public static ImageSource? Icon(Shortcut item,ConfigurationStore store)
    {
        try
        {
            if(!item.Icon.StartsWith("builtin:"))
            {
                var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.UriSource=new Uri(Path.Combine(store.Icons,item.Icon)); image.DecodePixelWidth=64; image.EndInit(); image.Freeze(); return image;
            }
            if(item.Icon=="builtin:auto" && File.Exists(Environment.ExpandEnvironmentVariables(item.Path)))
            {
                using var icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ExpandEnvironmentVariables(item.Path));
                if(icon!=null) { var image=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(32,32)); image.Freeze(); return image; }
            }
        } catch(Exception e) when(e is IOException or ArgumentException or NotSupportedException or System.Runtime.InteropServices.ExternalException or System.IO.FileFormatException) { }
        return null;
    }
}
