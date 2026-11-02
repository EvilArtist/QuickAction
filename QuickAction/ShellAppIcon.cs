using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace QuickAction;

internal static class ShellAppIcon
{
    [StructLayout(LayoutKind.Sequential)] private struct Size { public int X,Y; }
    [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageFactory
    {
        [PreserveSig] int GetImage(Size size,uint flags,out IntPtr bitmap);
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]
    private static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,out IImageFactory factory);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);

    public static BitmapSource? Load(string path)
    {
        IImageFactory? factory=null; IntPtr bitmap=IntPtr.Zero;
        try
        {
            var iid=typeof(IImageFactory).GUID;
            if(SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out factory)<0) return null;
            // AppsFolder supplies packaged-app artwork as a thumbnail; ICONONLY
            // can return a generic document icon for Microsoft Store apps.
            if(factory.GetImage(new Size { X=64,Y=64 },0,out bitmap)<0 || bitmap==IntPtr.Zero) return null;
            var image=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
            image.Freeze(); return image;
        }
        catch(COMException) { return null; }
        finally
        {
            if(bitmap!=IntPtr.Zero) DeleteObject(bitmap);
            if(factory!=null) Marshal.ReleaseComObject(factory);
        }
    }
}
