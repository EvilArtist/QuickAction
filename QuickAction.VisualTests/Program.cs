using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickAction;
using QuickAction.Core;

internal static class Program
{
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd,int index);
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Contains("--settings")) return SettingsChecks.Run();
        var root=Path.Combine(Path.GetTempPath(),"QuickAction-visual-"+Guid.NewGuid().ToString("N"));
        // Use a plain test host: App.OnStartup would run the production
        // single-instance handshake and could close this harness during Pump.
        var app=new App { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent(); // Load production resources without calling Run/OnStartup.
        if(app.TryFindResource("AllApps.ScrollBar") is not Style) throw new Exception("All Apps scrollbar resource is missing from application scope.");
        DockWindow? dock=null;
        try
        {
            var vm=new MainViewModel(root); dock=new DockWindow(vm); dock.ShowInTaskbar=true; dock.Title="QuickAction · Transparency test"; dock.Show();
            Pump();
            if(args.Contains("--blur")) { CheckBlur(dock,vm); return 0; }
            if(!dock.AllowsTransparency || (GetWindowLongPtr(new WindowInteropHelper(dock).Handle,-20).ToInt64()&0x80000)==0) throw new Exception("Dock must use native layered per-pixel alpha.");
            int passed=0;
            foreach(var edge in Enum.GetValues<Edge>())
            foreach(var state in new[]{"idle","pill","all"})
            {
                vm.Config.Edge=edge;
                // Drive the real view's states without pointer timing or user settings.
                typeof(DockWindow).GetField("expanded",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(dock,state!="idle");
                typeof(DockWindow).GetField("all",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(dock,state=="all");
                typeof(DockWindow).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)!.Invoke(dock,null);
                if(SystemParameters.ClientAreaAnimation && state!="idle")
                {
                    var geometry=(RectangleGeometry)typeof(DockWindow).GetField("reveal",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
                    var start=geometry.Rect;
                    var nativeWidth=dock.Width; var nativeHeight=dock.Height;
                    var calls=(int)typeof(DockWindow).GetField("nativePlacementCount",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
                    if(!(bool)typeof(DockWindow).GetField("resizing",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!) throw new Exception("Expected active resize animation.");
                    // Sample a deterministic animation frame; wall-clock sampling
                    // is unreliable when the desktop/compositor stalls under load.
                    typeof(DockWindow).GetMethod("ApplyResizeProgress",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dock,new object[]{.25});
                    var targetWidth=(double)typeof(DockWindow).GetField("resizeToWidth",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
                    var targetHeight=(double)typeof(DockWindow).GetField("resizeToHeight",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
                    if(geometry.Rect.Size==start.Size) throw new Exception("Reveal animation did not advance.");
                    if(Math.Abs(geometry.Rect.Width-targetWidth)<.01 && Math.Abs(geometry.Rect.Height-targetHeight)<.01) throw new Exception("Reveal animation finished too early.");
                    for(int frame=30;frame<100;frame+=5) typeof(DockWindow).GetMethod("ApplyResizeProgress",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dock,new object[]{frame/100.0});
                    if(dock.Width!=nativeWidth || dock.Height!=nativeHeight || calls!=(int)typeof(DockWindow).GetField("nativePlacementCount",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!) throw new Exception("Native resize occurred during animation frames.");
                    Console.WriteLine($"PASS {edge}/{state}: reveal interpolates; no native resizes across 15 frames");
                }
                Wait(dock,450); dock.UpdateLayout();
                if((bool)typeof(DockWindow).GetField("resizing",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!) throw new Exception("Resize animation did not complete.");
                foreach(var scale in new[]{1.0,1.5,2.0})
                {
                    var width=(int)Math.Ceiling(dock.ActualWidth*scale); var height=(int)Math.Ceiling(dock.ActualHeight*scale);
                    var bitmap=new RenderTargetBitmap(width,height,96*scale,96*scale,PixelFormats.Pbgra32); bitmap.Render(dock);
                    var pixels=new byte[width*height*4]; bitmap.CopyPixels(pixels,width*4,0);
                    foreach(var (x,y) in new[]{(0,0),(width-1,0),(0,height-1),(width-1,height-1)})
                        if(pixels[(y*width+x)*4+3]!=0) throw new Exception($"Opaque corner: {edge}/{state}/{scale} at {x},{y}");
                    if(pixels[((height/2)*width+width/2)*4+3]==0) throw new Exception("Dock center must remain visible.");
                    Console.WriteLine($"PASS {edge}/{state} {scale*100}%: all four corner alpha values = 0; center visible"); passed++;
                }
            }
            Console.WriteLine($"{passed} render checks passed; native layered HWND verified.");
            if(SystemParameters.ClientAreaAnimation)
            {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var render=typeof(DockWindow).GetMethod("Render",flags|BindingFlags.DeclaredOnly)!;
                var sample=typeof(DockWindow).GetMethod("ApplyResizeProgress",flags)!;
                var geometry=(RectangleGeometry)typeof(DockWindow).GetField("reveal",flags)!.GetValue(dock)!;
                for(int i=0;i<20;i++)
                {
                    var before=geometry.Rect.Size;
                    typeof(DockWindow).GetField("all",flags)!.SetValue(dock,i%2!=0);
                    typeof(DockWindow).GetField("expanded",flags)!.SetValue(dock,true);
                    render.Invoke(dock,null);
                    if(geometry.Rect.Size!=before) throw new Exception("Retargeting jumped the visible size.");
                    sample.Invoke(dock,new object[]{.35});
                }
                sample.Invoke(dock,new object[]{1.0}); dock.UpdateLayout();
                if((bool)typeof(DockWindow).GetField("resizing",flags)!.GetValue(dock)! || Math.Abs(dock.Width-geometry.Rect.Width)>.01 || Math.Abs(dock.Height-geometry.Rect.Height)>.01) throw new Exception("Interrupted transition did not settle.");
                Console.WriteLine("PASS 20 rapid reversals preserve visible size and settle to the final native bounds");
                typeof(DockWindow).GetMethod("ChangeState",flags)!.Invoke(dock,new object[]{true,false});
                Wait(dock,450);
                var surface=(Border)typeof(DockWindow).GetField("surface",flags)!.GetValue(dock)!;
                var pillContent=surface.Child;
                typeof(Window).GetMethod("OnDeactivated",flags)!.Invoke(dock,new object[]{EventArgs.Empty});
                if(!ReferenceEquals(pillContent,surface.Child)) throw new Exception("Focus loss rebuilt a settled pill.");
                dock.OpenAll();
                var allContent=surface.Child;
                for(int i=0;i<20;i++) dock.OpenAll();
                if(!ReferenceEquals(allContent,surface.Child)) throw new Exception("Repeated state requests rebuilt the tree.");
                // Simulate the UI being busy beyond the animation duration.
                // The next dispatcher tick must commit the endpoint directly.
                Thread.Sleep(550); Wait(dock,80);
                var animation=(DispatcherTimer)typeof(DockWindow).GetField("animation",flags)!.GetValue(dock)!;
                if(animation.IsEnabled || (bool)typeof(DockWindow).GetField("resizing",flags)!.GetValue(dock)!) throw new Exception("Animation remained stuck after delayed dispatch.");
                typeof(DockWindow).GetMethod("ChangeState",flags)!.Invoke(dock,new object[]{true,false});
                dock.Hide();
                if(animation.IsEnabled) throw new Exception("Hidden dock retained an animation timer.");
                dock.Show(); Wait(dock,450);
                if((bool)typeof(DockWindow).GetField("resizing",flags)!.GetValue(dock)! || Math.Abs(dock.Width-geometry.Rect.Width)>.01) throw new Exception("Show did not settle the interrupted animation.");
                Console.WriteLine("PASS focus-loss, duplicate state requests, stalled dispatcher, and hide/show interruption");
            }
            if(args.Contains("--preview"))
            {
                vm.Config.Edge=Edge.Left;
                typeof(DockWindow).GetField("expanded",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(dock,true);
                typeof(DockWindow).GetField("all",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(dock,false);
                typeof(DockWindow).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)!.Invoke(dock,null);
                // Keep preview expanded while inspecting; production timers are unchanged.
                var hover=(DispatcherTimer)typeof(DockWindow).GetField("hover",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
                var collapse=(DispatcherTimer)typeof(DockWindow).GetField("collapse",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!;
                var end=DateTime.UtcNow.AddMinutes(3);
                while(DateTime.UtcNow<end && dock.IsVisible) { hover.Stop(); collapse.Stop(); Pump(); Thread.Sleep(20); }
            }
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { dock?.Close(); if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
    private static void CheckBlur(DockWindow dock,MainViewModel vm) => BlurChecks.Run(dock,vm);
    private static void Pump()
    {
        var frame=new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,()=>frame.Continue=false); Dispatcher.PushFrame(frame);
    }
    private static void Wait(DockWindow dock,int milliseconds)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        var frame=new DispatcherFrame();
        var timer=new DispatcherTimer(DispatcherPriority.Send) { Interval=TimeSpan.FromMilliseconds(5) };
        timer.Tick+=(_,_)=>
        {
            foreach(var name in new[]{"hover","collapse"}) ((DispatcherTimer)typeof(DockWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dock)!).Stop();
            if(clock.ElapsedMilliseconds>=milliseconds) frame.Continue=false;
        };
        timer.Start(); try { Dispatcher.PushFrame(frame); } finally { timer.Stop(); }
    }
}




