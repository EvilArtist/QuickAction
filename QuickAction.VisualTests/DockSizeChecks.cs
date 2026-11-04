using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickAction;
using QuickAction.Core;

internal static class DockSizeChecks
{
    public static void Run(DockWindow dock,MainViewModel vm)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly;
        foreach(var name in new[]{"Visual Studio Code","Terminal","Docker Desktop","Tiếng Việt có dấu","👨‍👩‍👧‍👦 Family app with long name"})
            vm.Items.Add(new Shortcut { Name=name,Path="test.exe" });
        foreach(var size in Enum.GetValues<DockIconSize>())
        foreach(var edge in Enum.GetValues<Edge>())
        {
            vm.Config.IconSize=size; vm.Config.Edge=edge;
            typeof(DockWindow).GetField("expanded",flags)!.SetValue(dock,true);
            typeof(DockWindow).GetField("all",flags)!.SetValue(dock,false);
            typeof(DockWindow).GetMethod("Render",flags)!.Invoke(dock,null);
            typeof(DockWindow).GetMethod("FinishResize",flags)!.Invoke(dock,null);
            dock.UpdateLayout();
            var reveal=(RectangleGeometry)typeof(DockWindow).GetField("reveal",flags)!.GetValue(dock)!;
            if(size==DockIconSize.Small && (Math.Abs(reveal.RadiusX-Math.Min(reveal.Rect.Width,reveal.Rect.Height)/2)>.01 || reveal.RadiusY!=reveal.RadiusX))
                throw new Exception($"Small dock ends are not semicircles on {edge}");
            var buttons=Descendants(dock).OfType<Button>().Where(b=>b.Tag is Guid).ToArray();
            if(buttons.Length!=5) throw new Exception("Dock app count changed");
            var expectedIcon=new[]{26,36,48}[(int)size];
            var limit=new[]{9,12,15}[(int)size];
            foreach(var button in buttons)
            {
                var content=(StackPanel)button.Content;
                if(content.Children[0] is not Viewbox icon || content.Children[1] is not TextBlock label) throw new Exception("Label must follow icon");
                if(icon.Width!=expectedIcon || label.FontSize!=10 || StringInfo.ParseCombiningCharacters(label.Text).Length>limit) throw new Exception("Wrong icon size or text limit");
                var item=vm.Items.Single(i=>i.Id==(Guid)button.Tag);
                var chars=StringInfo.ParseCombiningCharacters(item.Name);
                var expected=chars.Length>limit?item.Name[..chars[limit-1]]+"…":item.Name;
                if(label.Text!=expected) throw new Exception("Name was truncated incorrectly");
                var labelBounds=label.TransformToAncestor(button).TransformBounds(new Rect(label.RenderSize));
                if(labelBounds.Bottom>button.ActualHeight || labelBounds.Right>button.ActualWidth || labelBounds.Top<0 || labelBounds.Left<0) throw new Exception("Label exceeds app button bounds");
                var bounds=button.TransformToAncestor(dock).TransformBounds(new Rect(button.RenderSize));
                if(bounds.Right>dock.ActualWidth || bounds.Bottom>dock.ActualHeight) throw new Exception($"App exceeds dock bounds: {size}/{edge} {bounds} dock {dock.ActualWidth}x{dock.ActualHeight}");
            }
            if(edge==Edge.Top)
            {
                var content=(FrameworkElement)dock.Content;
                var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);
                bitmap.Render(content);
                Directory.CreateDirectory("dist/settings-preview");
                using var file=File.Create($"dist/settings-preview/dock-{size}.png");
                var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
            }
        }
        Console.WriteLine("PASS Dock sizes: 3 sizes x 4 edges, five apps fit, labels below icons, constant font size, Unicode-safe truncation");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i); yield return child;
            foreach(var nested in Descendants(child)) yield return nested;
        }
    }
}


