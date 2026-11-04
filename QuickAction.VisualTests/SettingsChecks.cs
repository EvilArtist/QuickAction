using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickAction;
using QuickAction.Core;

internal static class SettingsChecks
{
    public static int Run()
    {
        var data=Path.Combine(Path.GetTempPath(),"QuickAction-settings-"+Guid.NewGuid());
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        SettingsWindow? window=null;
        try
        {
            var group=new QuickAction.Controls.SegmentedButtonGroup();
            foreach(var count in new[]{1,2,3,4,2,1})
            {
                group.ItemsSource=Enumerable.Range(1,count).Select(i=>$"Option {i}").ToArray();
                group.Measure(new Size(800,40)); group.Arrange(new Rect(0,0,800,40)); group.UpdateLayout();
                var surface=(Border)group.Template.FindName("GroupSurface",group);
                if(Math.Abs(surface.ActualWidth-count*200)>1)
                    throw new Exception($"Segment group with {count} options: expected width {count*200}, got {surface.ActualWidth}");
            }
            Console.WriteLine("PASS Segment widths: 1/4, 1/2, 3/4, full width and dynamic item changes");
            var vm=new MainViewModel(data);
            foreach(var title in new[]{"Visual Studio","Dọn cache","Docker Desktop","SQL Server Studio"})
                vm.Items.Add(new Shortcut { Name=title,Path=@"C:\Program Files\"+title+@"\app.exe",PinOrder=vm.Items.Count==0?0:null });
            window=new SettingsWindow(vm); window.Show(); Pump();
            T Find<T>(string name) where T:class => (T)window.FindName(name);
            StackPanel Preferences()=>(StackPanel)Find<ContentControl>("PreferencesHost").Content;
            QuickAction.Controls.SegmentedButtonGroup Language()=>Preferences().Children.OfType<QuickAction.Controls.SegmentedButtonGroup>().Single(c=>c.Name=="LanguageGroup");
            IEnumerable<T> Children<T>(DependencyObject parent) where T:DependencyObject
            {
                for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
                {
                    var child=VisualTreeHelper.GetChild(parent,i);
                    if(child is T match) yield return match;
                    foreach(var nested in Children<T>(child)) yield return nested;
                }
            }
            Button RowButton(string label)=>Children<Button>(Find<ListBox>("ShortcutList")).First(b=>System.Windows.Automation.AutomationProperties.GetName(b)==label);
            void Execute(Button button) { button.Command.Execute(null); Pump(); }
            if(Find<Grid>("ShortcutDetailsPage").Visibility!=Visibility.Collapsed) throw new Exception("Details visible on startup");
            Capture(window,"settings-list");
            Execute(RowButton("Quyền admin mặc định"));
            if(!vm.Items[0].Admin || !vm.Store.Load().Shortcuts[0].Admin || Find<Grid>("ShortcutDetailsPage").IsVisible) throw new Exception("List admin action failed or opened details");
            Execute(RowButton("Bỏ ghim"));
            if(vm.Items[0].PinOrder!=null || Find<Grid>("ShortcutDetailsPage").IsVisible) throw new Exception("List unpin failed or opened details");
            Execute(RowButton("Ghim"));
            if(vm.Items[0].PinOrder==null) throw new Exception("List pin failed");
            string? runNotice=null; vm.Notification+=message=>runNotice=message;
            Execute(RowButton("Chạy Visual Studio"));
            if(runNotice==null || Find<Grid>("ShortcutDetailsPage").IsVisible) throw new Exception("List Run did not reach launch handler or opened details");
            Execute(RowButton("Visual Studio"));
            if(!Find<Grid>("ShortcutDetailsPage").IsVisible || Find<Border>("ShortcutListPage").IsVisible) throw new Exception("App click did not open details");
            Find<Button>("BackButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            if(!Find<Border>("ShortcutListPage").IsVisible || Find<Grid>("ShortcutDetailsPage").IsVisible) throw new Exception("Back did not restore list");
            Execute(RowButton("Visual Studio"));
            Console.WriteLine("PASS Shortcut list: shared actions, saved admin/pin, app details and back navigation");
            Find<TextBox>("NameBox").Text="Nội dung chưa lưu";
            Language().SelectedIndex=0; Pump();
            if((string)Find<Button>("SaveButton").Content!="Save shortcut") throw new Exception("English resources did not update");
            if(vm.Store.Load().Language!="en") throw new Exception("Language was not saved automatically");
            if(Find<TextBox>("NameBox").Text!="Nội dung chưa lưu") throw new Exception("Language switch discarded editor content");
            Capture(window,"settings-english");
            Language().SelectedIndex=1; Pump();
            if((string)Find<Button>("SaveButton").Content!="Lưu shortcut") throw new Exception("Vietnamese resources did not update");
            typeof(SettingsWindow).GetMethod("Load",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object?[]{null});
            Console.WriteLine("PASS Language: live English/Vietnamese switch preserves unsaved editor");
            var preferences=Preferences();
            if(preferences.Children.OfType<Button>().Any()) throw new Exception("Preferences still has a save button");
            var edges=preferences.Children.OfType<QuickAction.Controls.SegmentedButtonGroup>().Last(); edges.SelectedIndex=(int)Edge.Right;
            var opacity=preferences.Children.OfType<Slider>().Single(); opacity.Value=.55;
            var topmost=preferences.Children.OfType<CheckBox>().First(); topmost.IsChecked=false; topmost.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var delay=preferences.Children.OfType<TextBox>().Single(); delay.Text="1200";
            var saved=vm.Store.Load();
            if(saved.Edge!=Edge.Right || saved.Opacity!=.55 || saved.Topmost || saved.CollapseMilliseconds!=1200) throw new Exception("Preferences were not applied and saved immediately");
            foreach(var invalidDelay in new[]{"","abc","199","5001"})
            {
                delay.Text=invalidDelay;
                if(vm.Config.CollapseMilliseconds!=1200 || vm.Store.Load().CollapseMilliseconds!=1200) throw new Exception("Invalid delay changed saved settings");
            }
            delay.Text="5000";
            if(vm.Store.Load().CollapseMilliseconds!=5000) throw new Exception("Valid delay did not recover after invalid input");
            Console.WriteLine("PASS Preferences: immediate updates, persistence and delay validation without a save button");
            var catalogType=typeof(SettingsWindow).Assembly.GetType("QuickAction.InstalledApps")!;
            var catalogTask=(System.Threading.Tasks.Task)catalogType.GetMethod("LoadAsync")!.Invoke(null,null)!;
            if(!catalogTask.Wait(TimeSpan.FromSeconds(30))) throw new Exception("App discovery timed out");
            var installed=(Array)catalogTask.GetType().GetProperty("Result")!.GetValue(catalogTask)!;
            if(installed.Length==0) throw new Exception("No installed applications discovered on test machine");
            var entries=installed.Cast<object>().ToArray();
            string Target(object entry)=>(string)entry.GetType().GetProperty("Target")!.GetValue(entry)!;
            if(!entries.Any(entry=>Path.IsPathFullyQualified(Target(entry)) && Target(entry).EndsWith(".exe",StringComparison.OrdinalIgnoreCase))) throw new Exception("No native executable targets resolved");
            if(!entries.Any(entry=>entry.GetType().GetProperty("Icon")!.GetValue(entry) is System.Windows.Media.ImageSource)) throw new Exception("No application icons loaded");
            foreach(var entry in entries)
                LaunchBuilder.Build(new Shortcut { Path=Target(entry),Arguments=(string)entry.GetType().GetProperty("Arguments")!.GetValue(entry)! },Environment.SystemDirectory);
            Console.WriteLine($"PASS Installed app discovery: {installed.Length} entries");
            var pickerType=typeof(SettingsWindow).Assembly.GetType("QuickAction.InstalledAppPicker")!;
            var picker=(Window)Activator.CreateInstance(pickerType)!;
            picker.Owner=window; picker.Show();
            var deadline=DateTime.UtcNow.AddSeconds(30);
            var pickerList=(ListBox)pickerType.GetField("list",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(picker)!;
            while(pickerList.Items.Count==0 && DateTime.UtcNow<deadline) { Pump(); System.Threading.Thread.Sleep(20); }
            if(pickerList.Items.Count==0) throw new Exception("Picker did not load apps");
            var pickerSearch=(TextBox)pickerType.GetField("search",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(picker)!;
            pickerSearch.Text="no-app-"+Guid.NewGuid(); Pump();
            if(pickerList.Items.Count!=0) throw new Exception("App search did not filter");
            pickerSearch.Clear(); Pump();
            if(pickerList.Items.Count==0) throw new Exception("Clearing app search did not restore entries");
            Capture(picker,"installed-app-picker"); picker.Close();
            var list=Find<ListBox>("ShortcutList"); Execute(RowButton("Visual Studio")); Pump();
            if(Find<TextBox>("NameBox").Text!="Visual Studio") throw new Exception($"Selection failed: count={list.Items.Count}, selected={list.SelectedItem}, name={Find<TextBox>("NameBox").Text}, page={Find<TabControl>("Pages").SelectedIndex}");
            Capture(window,"settings");
            Find<Button>("BackButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Find<TextBox>("SearchBox").Text="Docker"; Pump();
            if(list.Items.Count!=1) throw new Exception("Search failed");
            Find<TextBox>("SearchBox").Text="missing"; Pump();
            if(Find<Border>("EmptyState").Visibility!=Visibility.Visible) throw new Exception("Empty state failed");
            Find<TextBox>("SearchBox").Clear(); Pump(); Execute(RowButton("Visual Studio"));
            Find<TextBox>("NameBox").Text="Updated";
            Find<Button>("SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            if(vm.Items[0].Name!="Updated") throw new Exception("Save failed");
            Find<Button>("PinButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            if(vm.Items[0].PinOrder!=null) throw new Exception("Pin toggle failed");
            window.Width=1000; window.Height=600; Pump(); Capture(window,"settings-small");
            var pages=Find<TabControl>("Pages");
            pages.SelectedIndex=1; Pump(); Capture(window,"settings-dock");
            pages.SelectedIndex=2; Pump(); Capture(window,"settings-backup");
            Find<Button>("BackButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); vm.Items.Clear(); vm.Save(); pages.SelectedIndex=0; Pump(); Capture(window,"settings-empty");
            Console.WriteLine("PASS Settings: render, selection, search, empty state, save, pin, minimum size, dock and backup pages.");
            return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { window?.Close(); if(Directory.Exists(data)) Directory.Delete(data,true); }
    }
    private static void Pump()
    {
        var frame=new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,()=>frame.Continue=false);
        Dispatcher.PushFrame(frame);
    }
    private static void Capture(Window window,string name)
    {
        window.UpdateLayout();
        var content=(FrameworkElement)window.Content;
        var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);
        bitmap.Render(content);
        Directory.CreateDirectory("dist/settings-preview");
        using var output=File.Create("dist/settings-preview/"+name+".png");
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(output);
    }
}


