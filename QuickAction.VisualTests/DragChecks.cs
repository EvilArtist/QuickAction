using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using QuickAction;
using QuickAction.Core;

internal static class DragChecks
{
    public static void Run(DockWindow dock,MainViewModel vm)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        for(var i=0;i<20;i++) vm.Items.Add(new Shortcut { Name=$"App {i}",Path="test.exe",PinOrder=i==0?0:null });
        dock.OpenAll();
        typeof(DockWindow).GetMethod("FinishResize",flags)!.Invoke(dock,null);
        dock.UpdateLayout();
        var pending=typeof(DockWindow).GetField("dragStart",flags)!;
        var dragId=typeof(DockWindow).GetField("dragId",flags)!;
        void Press(UIElement target)
        {
            // PreviewMouseLeftButtonDown is re-raised directly by WPF for each
            // element. Exercise the window handler with the original hit target.
            var e=new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left)
                { RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent,Source=target };
            typeof(DockWindow).GetMethod("StartDrag",flags)!.Invoke(dock,new object[]{dock,e});
        }
        var nodes=Descendants(dock).ToArray();
        var scrollbar=nodes.OfType<ScrollBar>().First(s=>s.Orientation==Orientation.Vertical && s.IsVisible && Descendants(s).OfType<Thumb>().Any());
        var thumb=Descendants(scrollbar).OfType<Thumb>().Single();
        var thumbSurface=Descendants(thumb).OfType<Border>().First();
        var search=nodes.OfType<TextBox>().Single();
        var ordinaryButton=nodes.OfType<Button>().First(b=>b.Tag is not Guid);
        foreach(var target in new UIElement[]{scrollbar,thumb,thumbSurface,search,ordinaryButton})
        {
            pending.SetValue(dock,new Point(10,10));
            Press(target);
            if(pending.GetValue(dock)!=null || dragId.GetValue(dock)!=null)
                throw new Exception($"{target.GetType().Name} armed a dock drag.");
        }
        var pin=nodes.OfType<Button>().First(b=>b.Tag is Guid);
        Press(pin);
        if(pending.GetValue(dock)==null || !Equals(dragId.GetValue(dock),pin.Tag)) throw new Exception("Pinned shortcut drag was blocked.");
        Press((UIElement)dock.Content);
        if(pending.GetValue(dock)==null || dragId.GetValue(dock)!=null) throw new Exception("Background dock drag was blocked.");
        Console.WriteLine("PASS scrollbar, thumb, thumb content and controls cannot start dock movement; pin and background dragging remain available");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i); yield return child;
            foreach(var nested in Descendants(child)) yield return nested;
        }
    }
}
