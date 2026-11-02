using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using QuickAction;
using QuickAction.Core;

internal static class BlurChecks
{
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd,nint dc);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd,uint command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X,Y; }
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc,int x,int y);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc,int width,int height);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc,nint obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint destination,int x,int y,int width,int height,nint source,int sourceX,int sourceY,uint operation);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;

    public static void Run(DockWindow dock,MainViewModel vm)
    {
        var service=typeof(DockWindow).Assembly.GetType("QuickAction.WindowsServices")!;
        if(!(bool)service.GetProperty("TransparencyEnabled")!.GetValue(null)!)
            throw new Exception("Blur check requires Windows transparency enabled and high contrast off.");
        var stripes=new DrawingGroup();
        stripes.Children.Add(new GeometryDrawing(Brushes.Black,null,new RectangleGeometry(new Rect(0,0,8,8))));
        stripes.Children.Add(new GeometryDrawing(Brushes.White,null,new RectangleGeometry(new Rect(0,0,4,8))));
        var pattern=new DrawingBrush(stripes) { TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,8,8),Stretch=Stretch.None };
        var background=new Window { WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,
            Left=SystemParameters.VirtualScreenLeft,Top=SystemParameters.VirtualScreenTop,
            Width=SystemParameters.VirtualScreenWidth,Height=SystemParameters.VirtualScreenHeight,Background=pattern,ShowActivated=false };
        var geometry=(RectangleGeometry)typeof(DockWindow).GetField("reveal",Flags)!.GetValue(dock)!;
        try
        {
            vm.Items.Add(new Shortcut { Name="Visual test shortcut",Path="visual-test.exe",Icon="builtin:star",PinOrder=0 });
            background.Show(); vm.Config.Opacity=.3; vm.Config.Topmost=true; dock.Topmost=false; dock.Topmost=true;
            dock.OpenAll(); Wait(650);
            if(typeof(DockWindow).GetField("compositorBackdrop",Flags)!.GetValue(dock)==null)
                throw new Exception("Native compositor was not initialized; an opaque fallback cannot pass this test.");
            SetBlur(false); Wait(150); var sharp=Contrast();
            SetBlur(true); Wait(150); var blurred=Contrast();
            if(sharp<15 || blurred>sharp*.5) throw new Exception($"Desktop blur not visible: sharp={sharp:F2}, blurred={blurred:F2}");
            Console.WriteLine($"PASS native desktop blur: stripe contrast {sharp:F2} -> {blurred:F2}");
            CheckContent();
            SaveScreenshot(dock,Path.Combine("dist","visual-checks","dock-content.png"));
            foreach(var edge in Enum.GetValues<Edge>())
            {
                vm.Config.Edge=edge; Invoke("Render"); Wait(450);
                CheckOutside($"{edge}/all");
                CheckContent();
                foreach(var expanding in new[]{false,true})
                {
                    Invoke("ChangeState",expanding,expanding);
                    ((DispatcherTimer)typeof(DockWindow).GetField("animation",Flags)!.GetValue(dock)!).Stop();
                    foreach(var fraction in new[]{.1,.35,.7})
                    {
                        Invoke("ApplyResizeProgress",fraction); dock.UpdateLayout();
                        CheckOutside($"{edge}/{(expanding?"expand":"collapse")}/{fraction}");
                    }
                    Invoke("ApplyResizeProgress",1.0);
                    CheckOutside($"{edge}/{(expanding?"all":"idle")}");
                }
            }
            var backgroundHandle=GetWindow(new WindowInteropHelper(dock).Handle,4);
            if(backgroundHandle==nint.Zero) throw new Exception("The separate backdrop owner window is missing.");
            dock.Hide(); Wait(100);
            if(IsWindowVisible(backgroundHandle)) throw new Exception("Backdrop remained visible after hiding dock.");
            dock.Show(); dock.OpenAll(); Wait(500); CheckContent();
            if(!IsWindowVisible(backgroundHandle)) throw new Exception("Backdrop did not return with dock.");
            background.Topmost=false; vm.Config.Topmost=false; Invoke("Render"); dock.Activate(); Wait(250); CheckContent();
            vm.Config.Topmost=true; Invoke("Render"); Wait(250); CheckContent();
            Console.WriteLine("PASS hide/show and topmost changes preserve visible content and backdrop lifetime");
        }
        finally { background.Close(); }

        void Invoke(string name,params object[] args)=>typeof(DockWindow).GetMethod(name,Flags|BindingFlags.DeclaredOnly)!.Invoke(dock,args);
        void CheckContent()
        {
            var elements=Descendants(dock).ToArray();
            var header=elements.OfType<TextBlock>().First(t=>t.Text=="Ứng dụng của bạn");
            var settings=elements.OfType<Button>().First(b=>b.ToolTip?.ToString()=="Settings");
            var pin=elements.OfType<Button>().First(b=>b.Tag is Guid);
            var hit=settings.PointToScreen(new Point(settings.ActualWidth/2,settings.ActualHeight/2));
            if(WindowFromPoint(new NativePoint { X=(int)hit.X,Y=(int)hit.Y })!=new WindowInteropHelper(dock).Handle)
                throw new Exception("The background intercepts Settings button input.");
            if(Pixels(header).Count(c=>(c&255)<80)<30) throw new Exception("Title text is obscured on the desktop.");
            if(Pixels(settings).Count(c=>(c&255)<80)<10) throw new Exception("Settings glyph is obscured on the desktop.");
            if(Pixels(pin).Count(c=>(c&255)<70 && ((c>>8)&255)>60 && ((c>>8)&255)<170 && ((c>>16)&255)>200)<50)
                throw new Exception("Pinned shortcut icon is obscured on the desktop.");
            Console.WriteLine("PASS desktop title, Settings glyph and blue pinned icon are visible above blur");
        }
        uint[] Pixels(FrameworkElement element)
        {
            var a=element.PointToScreen(new Point(0,0)); var b=element.PointToScreen(new Point(element.ActualWidth,element.ActualHeight));
            return SampleColors(from y in Enumerable.Range((int)a.Y,Math.Max(1,(int)(b.Y-a.Y)))
                from x in Enumerable.Range((int)a.X,Math.Max(1,(int)(b.X-a.X))) select new Point(x,y));
        }
        void SetBlur(bool enabled)
        {
            typeof(DockWindow).GetField("blurEnabled",Flags)!.SetValue(dock,enabled);
            Invoke("SetReveal",geometry.Rect.Width,geometry.Rect.Height);
        }
        void CheckOutside(string label)
        {
            var boundary=geometry.GetWidenedPathGeometry(new Pen(Brushes.Black,2));
            var points=new List<Point>();
            for(double y=1;y<dock.ActualHeight;y+=8)
            for(double x=1;x<dock.ActualWidth;x+=8) Add(new Point(x,y));
            foreach(var x in new[]{geometry.Rect.Left+.25,geometry.Rect.Right-.25})
            foreach(var y in new[]{geometry.Rect.Top+.25,geometry.Rect.Bottom-.25}) Add(new Point(x,y));
            if(points.Count==0) throw new Exception($"No exterior sample points for {label}");
            SetBlur(false); Wait(100); var before=Sample(points);
            SetBlur(true); Wait(100); var after=Sample(points);
            var difference=before.Zip(after,(a,b)=>Math.Abs(a-b)).Max();
            if(difference>3) throw new Exception($"Backdrop escaped {label}: difference={difference}, points={points.Count}");
            Console.WriteLine($"PASS {label}: {points.Count} exterior pixels unchanged (max {difference})");
            void Add(Point p) { if(!geometry.FillContains(p)&&!boundary.FillContains(p)) points.Add(dock.PointToScreen(p)); }
        }
        double Contrast()
        {
            var origin=dock.PointToScreen(new Point(dock.ActualWidth/2,dock.ActualHeight*.75));
            var values=Sample(Enumerable.Range(0,128).Select(i=>new Point(origin.X+i,origin.Y)));
            var mean=values.Average(); return Math.Sqrt(values.Average(v=>(v-mean)*(v-mean)));
        }
        void Wait(int milliseconds)
        {
            var clock=System.Diagnostics.Stopwatch.StartNew(); var frame=new DispatcherFrame();
            var timer=new DispatcherTimer(DispatcherPriority.Send) { Interval=TimeSpan.FromMilliseconds(5) };
            timer.Tick+=(_,_)=>
            {
                foreach(var name in new[]{"hover","collapse"}) ((DispatcherTimer)typeof(DockWindow).GetField(name,Flags)!.GetValue(dock)!).Stop();
                if(clock.ElapsedMilliseconds>=milliseconds) frame.Continue=false;
            };
            timer.Start(); try { Dispatcher.PushFrame(frame); } finally { timer.Stop(); }
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i); yield return child;
            foreach(var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static double[] Sample(IEnumerable<Point> points)=>SampleColors(points).Select(c=>(double)(c&255)).ToArray();
    private static uint[] SampleColors(IEnumerable<Point> points)
    {
        var samples=points.ToArray();
        int left=(int)samples.Min(p=>p.X),top=(int)samples.Min(p=>p.Y);
        int width=(int)samples.Max(p=>p.X)-left+1,height=(int)samples.Max(p=>p.Y)-top+1;
        var dc=GetDC(nint.Zero);
        var memory=CreateCompatibleDC(dc); var bitmap=CreateCompatibleBitmap(dc,width,height); var previous=SelectObject(memory,bitmap);
        try
        {
            if(!BitBlt(memory,0,0,width,height,dc,left,top,0x00CC0020)) throw new Exception("Desktop capture failed.");
            return samples.Select(p=>GetPixel(memory,(int)p.X-left,(int)p.Y-top)).ToArray();
        }
        finally { SelectObject(memory,previous); DeleteObject(bitmap); DeleteDC(memory); ReleaseDC(nint.Zero,dc); }
    }
    private static void SaveScreenshot(DockWindow dock,string path)
    {
        var a=dock.PointToScreen(new Point()); var b=dock.PointToScreen(new Point(dock.ActualWidth,dock.ActualHeight));
        int width=(int)(b.X-a.X),height=(int)(b.Y-a.Y);
        var dc=GetDC(nint.Zero); var memory=CreateCompatibleDC(dc); var bitmap=CreateCompatibleBitmap(dc,width,height); var previous=SelectObject(memory,bitmap);
        try
        {
            if(!BitBlt(memory,0,0,width,height,dc,(int)a.X,(int)a.Y,0x00CC0020)) throw new Exception("Desktop capture failed.");
            var image=Imaging.CreateBitmapSourceFromHBitmap(bitmap,nint.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); using var stream=File.Create(path); encoder.Save(stream);
        }
        finally { SelectObject(memory,previous); DeleteObject(bitmap); DeleteDC(memory); ReleaseDC(nint.Zero,dc); }
    }
}
