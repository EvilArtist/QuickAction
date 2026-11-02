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
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out PointNative point);

    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(PointNative point,uint flags);
    [StructLayout(LayoutKind.Sequential)] internal struct PointNative { public int X,Y; }

    public static PointNative Cursor { get { GetCursorPos(out var p); return p; } }
    public static Forms.Screen Screen(Configuration c)=>Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==c.Monitor) ?? Forms.Screen.PrimaryScreen!;
    public static double Scale(Forms.Screen screen)
    {
        var monitor=MonitorFromPoint(new(){ X=screen.Bounds.Left+screen.Bounds.Width/2,Y=screen.Bounds.Top+screen.Bounds.Height/2 },2);
        return GetDpiForMonitor(monitor,0,out var x,out _)==0 ? x/96.0 : 1;
    }
    public static bool TransparencyEnabled
    {
        get
        {
            using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return !SystemParameters.HighContrast && !(key?.GetValue("EnableTransparency") is int enabled && enabled==0);
        }
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
