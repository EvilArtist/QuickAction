using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickAction;
using QuickAction.Core;

internal static class IconChecks
{
    [STAThread]
    public static int Run()
    {
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        var root=Path.Combine(Path.GetTempPath(),"QuickAction-icons-"+Guid.NewGuid());
        SettingsWindow? window=null;
        try
        {
            var vm=new MainViewModel(root);
            window=new SettingsWindow(vm);
            var render=typeof(SettingsWindow).Assembly.GetType("QuickAction.Ui")!.GetMethod("Icon")!;
            byte[] Pixels(Shortcut item,string font)
            {
                var host=new ContentControl { FontFamily=new FontFamily(font),Content=render.Invoke(null,new object[]{item,vm}),Width=32,Height=32 };
                host.Measure(new Size(32,32)); host.Arrange(new Rect(0,0,32,32)); host.UpdateLayout();
                var bitmap=new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32); bitmap.Render(host);
                var pixels=new byte[32*32*4]; bitmap.CopyPixels(pixels,128,0); return pixels;
            }
            var modes=new[]{"auto","app","terminal","folder","star","tools"};
            var rendered=new List<byte[]>();
            foreach(var mode in modes)
            {
                var item=new Shortcut { Name="Icon test",Path=Path.Combine(Environment.SystemDirectory,"cmd.exe"),Icon="builtin:"+mode };
                var normal=Pixels(item,"Segoe UI");
                if(!normal.SequenceEqual(Pixels(item,"Segoe Fluent Icons"))) throw new Exception($"Icon {mode} changes with host font");
                if(mode!="auto") rendered.Add(normal);
                typeof(SettingsWindow).GetMethod("Load",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object[]{item});
                if(((ComboBox)window.FindName("IconModeCombo")).SelectedIndex!=Array.IndexOf(modes,mode)) throw new Exception("Wrong icon selection");
                ((Button)window.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(vm.Store.Load().Shortcuts.Single(s=>s.Id==item.Id).Icon!=item.Icon) throw new Exception("Saved icon differs from selection");
            }
            for(int i=0;i<rendered.Count;i++) for(int j=i+1;j<rendered.Count;j++)
                if(rendered[i].SequenceEqual(rendered[j])) throw new Exception("Built-in icons must be visually distinct");
            var control=new ContentControl();
            var changed=typeof(SettingsWindow).GetMethod("ShortcutIconDataContextChanged",BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach(var mode in modes.Skip(1))
            {
                var item=new Shortcut { Icon="builtin:"+mode };
                changed.Invoke(window,new object[]{control,new DependencyPropertyChangedEventArgs(FrameworkElement.DataContextProperty,null,item)});
                var expected=(Border)render.Invoke(null,new object[]{item,vm})!;
                if(control.Content is not Border actual || ((TextBlock)actual.Child).Text!=((TextBlock)expected.Child).Text) throw new Exception("Reused row did not update icon");
            }
            Console.WriteLine("PASS six icon selections persist; built-in icons are distinct and render identically across host fonts; reused rows refresh");
            return 0;
        }
        finally { window?.Close(); app.Shutdown(); if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
}
