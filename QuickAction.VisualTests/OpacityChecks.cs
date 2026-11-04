using System.Reflection;
using System.Windows.Media;
using System.Windows.Shapes;
using QuickAction;

internal static class OpacityChecks
{
    public static void Run(DockWindow dock,MainViewModel vm)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var native=typeof(DockWindow).GetField("compositorBackdrop",flags)!;
        if(native.GetValue(dock) is not IDisposable compositor)
            throw new Exception("Packaged native backdrop did not load.");
        Console.WriteLine("PASS packaged native backdrop loads from application output");
        compositor.Dispose(); native.SetValue(dock,null);
        var path=(Path)typeof(DockWindow).GetField("backdrop",flags)!.GetValue(dock)!;
        var service=typeof(DockWindow).Assembly.GetType("QuickAction.WindowsServices")!;
        var transparency=(bool)service.GetProperty("TransparencyEnabled")!.GetValue(null)!;
        foreach(var opacity in new[]{.3,.65,1.0,.3})
        {
            vm.Config.Opacity=opacity; vm.Save();
            var expected=transparency?(byte)Math.Round(opacity*255):(byte)255;
            if(((SolidColorBrush)path.Fill).Color.A!=expected)
                throw new Exception($"Fallback background did not update to {opacity:P0}.");
            if(dock.Opacity!=1) throw new Exception("Background opacity must not fade foreground content.");
        }
        Console.WriteLine("PASS live fallback background opacity updates; foreground stays opaque; Windows transparency preference respected");
    }
}
