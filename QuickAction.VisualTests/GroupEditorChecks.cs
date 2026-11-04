using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickAction;
using QuickAction.Core;

internal static class GroupEditorChecks
{
    public static int Run()
    {
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        var root=Path.Combine(Path.GetTempPath(),"QuickAction-group-ui-"+Guid.NewGuid());
        var vm=new MainViewModel(root);
        var steps=new[]{"Dọn cache","Docker Desktop","SQL Server Studio","Visual Studio"}.Select(name=>new GroupStep { App=new Shortcut { Name=name,Path=@"C:\Programs\"+name+".exe" } }).ToList();
        steps[1].DependsOn=steps[0].Id; steps[1].Condition=GroupCondition.Together;
        steps[2].DependsOn=steps[1].Id; steps[2].Condition=GroupCondition.InputIdle; steps[2].DelaySeconds=2;
        steps[3].DependsOn=steps[0].Id; steps[3].Condition=GroupCondition.Finished;
        var source=new Shortcut { Name="Dev buổi sáng",Icon="builtin:group",Steps=steps };
        var window=new SettingsWindow(vm);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(SettingsWindow).GetMethod("OpenGroup",flags)!.Invoke(window,[source]);
        var editor=(GroupEditor)((ContentControl)window.FindName("GroupDetailsHost")).Content;
        try
        {
            window.Show(); window.UpdateLayout();
            var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); image.Render(window);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            Directory.CreateDirectory("output/groups"); using(var file=File.Create("output/groups/group-editor.png")) encoder.Save(file);
            var iconPicker=(ComboBox)typeof(GroupEditor).GetField("iconPicker",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(editor)!;
            iconPicker.IsDropDownOpen=true;
            window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
            var popup=(System.Windows.Controls.Primitives.Popup)iconPicker.Template.FindName("PART_Popup",iconPicker);
            var menu=(FrameworkElement)popup.Child; menu.UpdateLayout();
            var menuImage=new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth),(int)Math.Ceiling(menu.ActualHeight),96,96,PixelFormats.Pbgra32); menuImage.Render(menu);
            var menuEncoder=new PngBitmapEncoder(); menuEncoder.Frames.Add(BitmapFrame.Create(menuImage));
            using(var file=File.Create("output/groups/dropdown-open.png")) menuEncoder.Save(file);
            iconPicker.IsDropDownOpen=false;
            IEnumerable<T> Children<T>(DependencyObject parent) where T:DependencyObject
            {
                for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
                {
                    var child=VisualTreeHelper.GetChild(parent,i);
                    if(child is T match) yield return match;
                    foreach(var nested in Children<T>(child)) yield return nested;
                }
            }
            var delay=Children<TextBox>(window).First(b=>b.MaxLength==5); delay.SelectAll();
            foreach(var invalid in new[]{"1.5","-2","abc","86401"})
            {
                var input=new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice,new TextComposition(InputManager.Current,delay,invalid)) { RoutedEvent=TextCompositionManager.PreviewTextInputEvent };
                delay.RaiseEvent(input); if(!input.Handled) throw new Exception("Invalid seconds accepted: "+invalid);
                var paste=new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText,invalid),false,DataFormats.UnicodeText) { RoutedEvent=DataObject.PastingEvent };
                delay.RaiseEvent(paste); if(!paste.CommandCancelled) throw new Exception("Invalid pasted seconds accepted: "+invalid);
            }

            var draft=(Shortcut)typeof(GroupEditor).GetField("draft",flags)!.GetValue(editor)!;
            typeof(GroupEditor).GetMethod("Move",flags)!.Invoke(editor,[draft.Steps![1],0]);
            if(draft.Steps[0].DependsOn!=null || draft.Steps[0].Condition!=null) throw new Exception("Reorder did not clear invalid dependency");
            if(steps[1].DependsOn==null) throw new Exception("Editor mutated the source before save");
            var host=(ContentControl)window.FindName("GroupDetailsHost");
            if(Application.Current.Windows.Count!=1 || host.Visibility!=Visibility.Visible) throw new Exception("Group must be embedded in Settings");
            var rows=(StackPanel)typeof(GroupEditor).GetField("rows",flags)!.GetValue(editor)!;
            var add=(Button)rows.Children[rows.Children.Count-1];
            add.Command.Execute(null); window.UpdateLayout();
            if(draft.Steps.Count!=4) throw new Exception("Adding must not insert an empty group row");
            if(((TextBox)window.FindName("PathBox")).Text!="") throw new Exception("New app details must have an empty path");
            ((Button)window.FindName("BackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(draft.Steps.Count!=4 || host.Visibility!=Visibility.Visible) throw new Exception("Back from new app must leave group unchanged");
            add.Command.Execute(null);
            var details=(Grid)window.FindName("ShortcutDetailsPage");
            if(details.Visibility!=Visibility.Visible || host.Visibility!=Visibility.Collapsed) throw new Exception("Edit must use single-app details");
            ((TextBox)window.FindName("NameBox")).Text="New child";
            ((TextBox)window.FindName("PathBox")).Text=@"C:\Programs\new.exe";
            ((TextBox)window.FindName("ArgsBox")).Text="--group-only";
            ((Button)window.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var newStep=draft.Steps.Last();
            if(newStep.DependsOn!=draft.Steps[^2].Id || newStep.Condition!=GroupCondition.Together) throw new Exception("New app must default to Together with previous app");
            if(Children<ComboBox>(editor).Where(c=>c.DisplayMemberPath=="Label").Any(c=>c.Items.Cast<object>().Any(item=>item.GetType().GetProperty("Id")!.GetValue(item)==null))) throw new Exception("Dependency choices must not contain an empty option");
            if(host.Visibility!=Visibility.Visible || details.Visibility!=Visibility.Collapsed || newStep.App.Arguments!="--group-only" || vm.Items.Count!=0) throw new Exception("Saving child must return to group without creating standalone shortcut");
            var pending=(FrameworkElement)rows.Children[rows.Children.Count-2];
            Children<Button>(pending).Single(b=>Equals(b.ToolTip,"Chỉnh sửa app")).Command.Execute(null);
            if(((TextBox)window.FindName("ArgsBox")).Text!="--group-only") throw new Exception("Reopening child must retain saved app options");
            ((Button)window.FindName("BackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(host.Visibility!=Visibility.Visible || draft.Steps.Count!=5 || !editor.IsDirty) throw new Exception("Back must preserve the unsaved parent group");
            if(rows.Children[rows.Children.Count-1] is not Button) throw new Exception("Add app must remain last");
            window.UpdateLayout();
            var updated=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); updated.Render(window);
            var updatedEncoder=new PngBitmapEncoder(); updatedEncoder.Frames.Add(BitmapFrame.Create(updated));
            using(var file=File.Create("output/groups/group-details.png")) updatedEncoder.Save(file);
            typeof(GroupEditor).GetMethod("Save",flags)!.Invoke(editor,null);
            if(vm.Store.Load().Shortcuts.Single().Steps!.Count!=5) throw new Exception("Group editor did not save all steps");
            Console.WriteLine("PASS Embedded group details: direct add-details navigation, cancel without empty rows, bottom add button, shared app form, save/back navigation, isolated drafts, integer delay, reorder and persistence");
            return 0;
        }
        finally { typeof(GroupEditor).GetField("dirty",flags)!.SetValue(editor,false); typeof(SettingsWindow).GetField("dirty",flags)!.SetValue(window,false); window.Close(); app.Shutdown(); Directory.Delete(root,true); }
    }
}
