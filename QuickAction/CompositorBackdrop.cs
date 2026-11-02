using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace QuickAction;

internal sealed class CompositorBackdrop : IDisposable
{
    [DllImport("QuickAction.Native.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int CreateBackdrop(IntPtr hwnd,out IntPtr state);
    [DllImport("QuickAction.Native.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int UpdateBackdrop(IntPtr state,float x,float y,float width,float height,float radius,float opacity,bool enabled);
    [DllImport("QuickAction.Native.dll",CallingConvention=CallingConvention.Cdecl)] private static extern void DestroyBackdrop(IntPtr state);
    [DllImport("QuickAction.Native.dll",CallingConvention=CallingConvention.Cdecl)] private static extern void SyncBackdrop(IntPtr state);
    private IntPtr state;
    public CompositorBackdrop(IntPtr hwnd) { Marshal.ThrowExceptionForHR(CreateBackdrop(hwnd,out state)); }
    public void Sync() { if(state!=IntPtr.Zero) SyncBackdrop(state); }
    public void Update(Rect rect,double radius,double scale,double opacity,bool enabled) =>
        Marshal.ThrowExceptionForHR(UpdateBackdrop(state,(float)(rect.X*scale),(float)(rect.Y*scale),(float)(rect.Width*scale),(float)(rect.Height*scale),(float)(radius*scale),(float)opacity,enabled));
    public void Dispose() { var value=state; state=IntPtr.Zero; if(value!=IntPtr.Zero) DestroyBackdrop(value); }
}
