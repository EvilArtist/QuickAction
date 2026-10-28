using QuickAction.Core;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace QuickAction;
public sealed class SettingsWindow : Window
{
    private readonly MainViewModel vm;
    private readonly ListBox list=new() { DisplayMemberPath="Name",MinWidth=210,Margin=new(0,8,16,0) };
    private readonly TextBox name=new(), path=new(), arguments=new(), work=new();
    private readonly CheckBox admin=new() { Content="Chạy với quyền admin (UAC)" };
    private readonly ComboBox icons=new() { Margin=new(0,5,0,8),ItemsSource=new[]{"Tự lấy từ chương trình","Ứng dụng","Terminal","Thư mục","Ngôi sao","Công cụ","Icon tùy chỉnh"},SelectedIndex=0 };
    private readonly TextBlock iconStatus=Ui.Text("",12,Brushes.DimGray);
    private Guid? editing;
    private string icon="builtin:auto";
    private bool dirty,loading,changingSelection;
    private Shortcut? previous;
    private readonly TabControl tabs=new();
    public SettingsWindow(MainViewModel model)
    {
        vm=model; DataContext=vm; SetResourceReference(StyleProperty,typeof(Window)); Title="QuickAction · Settings"; Width=940; Height=760; MinWidth=740; MinHeight=580; WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var root=new DockPanel { Margin=new(24) }; var title=new StackPanel(); title.Children.Add(Ui.Text("QuickAction",28)); title.Children.Add(Ui.Text("Shortcut của bạn, luôn trong tầm tay.",14,Brushes.DimGray)); DockPanel.SetDock(title,Dock.Top); root.Children.Add(title); root.Children.Add(tabs); Content=root;
        tabs.Items.Add(new TabItem { Header="Shortcuts",Content=BuildEditor() }); tabs.Items.Add(new TabItem { Header="Dock & hệ thống",Content=BuildPreferences() }); tabs.Items.Add(new TabItem { Header="Sao lưu",Content=BuildBackup() });
        list.ItemsSource=vm.Items; list.SelectionChanged+=SelectionChanged;
        foreach(var box in new[]{name,path,arguments,work}) box.TextChanged+=(_,_)=> { if(!loading) dirty=true; };
        admin.Click+=(_,_)=>dirty=true;
        icons.SelectionChanged+=(_,_)=> { if(!loading) dirty=true; };
        vm.Updated+=RefreshList;
        AllowDrop=true; DragOver+=(_,e)=> { e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None; e.Handled=true; };
        Drop+=(_,e)=> { if(e.Data.GetData(DataFormats.FileDrop) is string[] files) foreach(var f in files.Where(LaunchBuilder.Supported)) AddFile(f); e.Handled=true; };
        Closing+=(_,e)=> { if(!CanDiscard()) e.Cancel=true; }; Closed+=(_,_)=>vm.Updated-=RefreshList;
        Load(null);
    }
    private void RefreshList()=>list.Items.Refresh();
    private bool CanDiscard()=>!dirty||MessageBox.Show(this,"Bỏ các thay đổi chưa lưu?","QuickAction",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
    private UIElement BuildEditor()
    {
        var grid=new Grid { Margin=new(0,16,0,0) }; grid.ColumnDefinitions.Add(new(){ Width=new(235) }); grid.ColumnDefinitions.Add(new(){ Width=new(1,GridUnitType.Star) });
        var sidebar=new DockPanel(); var add=Ui.Button("＋ Thêm shortcut",()=> { if(CanDiscard()) { changingSelection=true; list.SelectedItem=null; changingSelection=false; previous=null; Load(null); } }); DockPanel.SetDock(add,Dock.Top); sidebar.Children.Add(add); sidebar.Children.Add(list); grid.Children.Add(sidebar);
        var fields=new StackPanel { Margin=new(8,0,4,0) };
        Ui.Label(fields,"Tên hiển thị"); fields.Children.Add(name); Ui.Label(fields,"Chương trình · EXE / BAT / CMD / PS1 / MSI"); fields.Children.Add(BrowseRow(path,()=> { var dialog=new OpenFileDialog { Filter="Chương trình|*.exe;*.bat;*.cmd;*.ps1;*.msi" }; if(dialog.ShowDialog(this)==true) { path.Text=dialog.FileName; if(string.IsNullOrWhiteSpace(name.Text)) name.Text=Path.GetFileNameWithoutExtension(dialog.FileName); } }));
        Ui.Label(fields,"Parameters · giữ dấu ngoặc kép cho đối số có khoảng trắng"); fields.Children.Add(arguments); Ui.Label(fields,"Thư mục làm việc · để trống dùng thư mục chương trình"); fields.Children.Add(BrowseRow(work,()=> { var dialog=new OpenFolderDialog(); if(dialog.ShowDialog(this)==true) work.Text=dialog.FolderName; }));
        fields.Children.Add(admin); Ui.Label(fields,"Icon"); fields.Children.Add(icons); fields.Children.Add(Ui.Button("Browse icon…",()=>vm.Guard(()=> { var dialog=new OpenFileDialog { Filter="Icon|*.ico;*.png;*.jpg;*.jpeg" }; if(dialog.ShowDialog(this)==true) { icon=vm.Store.ImportIcon(dialog.FileName); icons.SelectedIndex=6; iconStatus.Text=Path.GetFileName(dialog.FileName); dirty=true; } }))); fields.Children.Add(iconStatus);
        var actions=new WrapPanel(); actions.Children.Add(Ui.Button("Lưu shortcut",()=>vm.Guard(SaveItem))); actions.Children.Add(Ui.Button("▶ Run thử",()=>vm.Guard(()=>_ = vm.Run(ReadForm())))); actions.Children.Add(Ui.Button("Ghim / Bỏ ghim",()=> { if(editing.HasValue) { var s=vm.Items.FirstOrDefault(s=>s.Id==editing); if(s!=null) vm.TogglePin(s); } else MessageBox.Show(this,"Lưu shortcut trước khi ghim.","QuickAction"); })); fields.Children.Add(actions);
        var more=new WrapPanel(); more.Children.Add(Ui.Button("Nhân bản",()=> { if(CanDiscard()&&list.SelectedItem is Shortcut s) { Load(s); editing=null; name.Text=s.Name+" — bản sao"; dirty=true; } })); more.Children.Add(Ui.Button("Xóa",Delete)); fields.Children.Add(more);
        fields.Children.Add(Ui.Text("Kéo file vào đây để thêm. Script chạy theo chính sách PowerShell hiện tại. Admin sẽ hiển thị UAC của Windows.",12,Brushes.DimGray));
        foreach(var (box,label) in new[]{(name,"Tên shortcut"),(path,"Đường dẫn chương trình"),(arguments,"Parameters"),(work,"Thư mục làm việc")}) System.Windows.Automation.AutomationProperties.SetName(box,label);
        var scroll=new ScrollViewer { Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; Grid.SetColumn(scroll,1); grid.Children.Add(scroll); return grid;
    }
    private static UIElement BrowseRow(TextBox box,Action browse)
    { var grid=new DockPanel(); var button=Ui.Button("Browse…",browse); DockPanel.SetDock(button,Dock.Right); grid.Children.Add(button); grid.Children.Add(box); return grid; }
    private void SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(changingSelection) return;
        if(!CanDiscard()) { changingSelection=true; list.SelectedItem=previous; changingSelection=false; return; }
        previous=list.SelectedItem as Shortcut; Load(previous);
    }
    private void Load(Shortcut? item)
    {
        loading=true; editing=item?.Id; name.Text=item?.Name??""; path.Text=item?.Path??""; arguments.Text=item?.Arguments??""; work.Text=item?.WorkingDirectory??""; admin.IsChecked=item?.Admin??false; icon=item?.Icon??"builtin:auto";
        icons.SelectedIndex=Array.IndexOf(new[]{"builtin:auto","builtin:app","builtin:terminal","builtin:folder","builtin:star","builtin:tools"},icon); if(icons.SelectedIndex<0) icons.SelectedIndex=6; iconStatus.Text=icon.StartsWith("builtin:")?"":icon;
        loading=false; dirty=false;
    }
    public void AddFile(string file)
    {
        if(!CanDiscard()) return; tabs.SelectedIndex=0; changingSelection=true; list.SelectedItem=null; changingSelection=false; previous=null; Load(null); path.Text=file; name.Text=Path.GetFileNameWithoutExtension(file); dirty=true;
    }
    private Shortcut ReadForm()
    {
        if(string.IsNullOrWhiteSpace(name.Text)) throw new ArgumentException("Nhập tên shortcut.");
        var selectedIcon=icons.SelectedIndex is >=0 and <6 ? new[]{"builtin:auto","builtin:app","builtin:terminal","builtin:folder","builtin:star","builtin:tools"}[icons.SelectedIndex] : icon;
        if(icons.SelectedIndex==6 && selectedIcon.StartsWith("builtin:")) throw new ArgumentException("Chọn file icon bằng Browse icon.");
        var item=new Shortcut { Id=editing??Guid.NewGuid(),Name=name.Text.Trim(),Path=path.Text.Trim(),Arguments=arguments.Text,WorkingDirectory=work.Text.Trim(),Admin=admin.IsChecked==true,Icon=selectedIcon,PinOrder=vm.Items.FirstOrDefault(s=>s.Id==editing)?.PinOrder };
        LaunchBuilder.Build(item,Environment.SystemDirectory); return item;
    }
    private void SaveItem()
    {
        var item=ReadForm(); var old=vm.Items.FirstOrDefault(s=>s.Id==item.Id);
        changingSelection=true; if(old!=null) vm.Items[vm.Items.IndexOf(old)]=item; else vm.Items.Add(item); changingSelection=false;
        vm.Save(); dirty=false; list.SelectedItem=item; previous=item; Load(item);
    }
    private void Delete()
    {
        if(list.SelectedItem is not Shortcut item) return;
        if(MessageBox.Show(this,"Xóa shortcut “"+item.Name+"”? File chương trình vẫn được giữ nguyên.","QuickAction",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
        vm.Guard(()=> { dirty=false; vm.Items.Remove(item); vm.Config.Shortcuts=vm.Items.ToList(); Pins.Normalize(vm.Config); vm.Save(); Load(null); });
    }
    private UIElement BuildPreferences()
    {
        var panel=new StackPanel { Margin=new(12,20,12,12) }; Ui.Label(panel,"Màn hình");
        var monitor=new ComboBox { ItemsSource=Forms.Screen.AllScreens.Select(s=>s.DeviceName).ToArray(),SelectedItem=WindowsServices.Screen(vm.Config).DeviceName,Margin=new(0,5,0,16) }; panel.Children.Add(monitor);
        Ui.Label(panel,"Cạnh ghim"); var edge=new ComboBox { ItemsSource=new[]{"Trái","Phải","Trên","Dưới"},SelectedIndex=(int)vm.Config.Edge,Margin=new(0,5,0,16) }; panel.Children.Add(edge);
        var topmost=new CheckBox { Content="Luôn nổi trên các cửa sổ khác",IsChecked=vm.Config.Topmost }; panel.Children.Add(topmost);
        var startup=new CheckBox { Content="Khởi động cùng Windows",IsChecked=vm.Config.Startup }; panel.Children.Add(startup);
        Ui.Label(panel,"Độ đục nền (30–100%)"); var opacity=new Slider { Minimum=.3,Maximum=1,Value=vm.Config.Opacity,TickFrequency=.1,Margin=new(0,8,0,18) }; panel.Children.Add(opacity);
        Ui.Label(panel,"Thời gian thu gọn (200–5000 ms)"); var delay=new TextBox { Text=vm.Config.CollapseMilliseconds.ToString() }; panel.Children.Add(delay);
        panel.Children.Add(Ui.Button("Lưu thiết lập dock",()=>vm.Guard(()=>
        {
            if(!int.TryParse(delay.Text,out var ms)||ms<200||ms>5000) throw new ArgumentException("Thời gian phải từ 200 đến 5000 ms.");
            WindowsServices.Startup(startup.IsChecked==true); vm.Config.Monitor=monitor.SelectedItem as string??""; vm.Config.Edge=(Edge)Math.Max(0,edge.SelectedIndex); vm.Config.Topmost=topmost.IsChecked==true; vm.Config.Startup=startup.IsChecked==true; vm.Config.Opacity=opacity.Value; vm.Config.CollapseMilliseconds=ms; vm.Save();
        })));
        panel.Children.Add(Ui.Text("Bạn cũng có thể kéo nền dock để đổi cạnh và màn hình. Nếu chuyển thư mục ứng dụng, hãy lưu lại thiết lập khởi động cùng Windows.",12,Brushes.DimGray)); return panel;
    }
    private UIElement BuildBackup()
    {
        var panel=new StackPanel { Margin=new(12,20,12,12) }; panel.Children.Add(Ui.Text("Sao lưu shortcut & icon",22)); panel.Children.Add(Ui.Text("Gói ZIP chứa đường dẫn, parameters và icon; không chứa chương trình đích. Parameters có thể chứa thông tin riêng tư.",14,Brushes.DimGray));
        panel.Children.Add(Ui.Button("Xuất bản sao lưu…",()=>vm.Guard(()=> { var dialog=new SaveFileDialog { Filter="QuickAction backup|*.zip",FileName="QuickAction-backup-"+DateTime.Now.ToString("yyyyMMdd")+".zip" }; if(dialog.ShowDialog(this)==true) { var temporary=dialog.FileName+"."+Guid.NewGuid().ToString("N")+".tmp"; try { vm.Store.Export(vm.Config,temporary); File.Move(temporary,dialog.FileName,true); } finally { if(File.Exists(temporary)) File.Delete(temporary); } } })));
        panel.Children.Add(Ui.Button("Khôi phục từ ZIP…",()=>vm.Guard(()=>
        {
            var dialog=new OpenFileDialog { Filter="QuickAction backup|*.zip" }; if(dialog.ShowDialog(this)!=true||!CanDiscard()) return;
            if(MessageBox.Show(this,"Thay thế toàn bộ shortcut và thiết lập hiện tại bằng bản sao lưu?","Khôi phục",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes) return;
            dirty=false; vm.ReplaceFrom(dialog.FileName); WindowsServices.Startup(vm.Config.Startup); Load(null); ((TabItem)tabs.Items[1]).Content=BuildPreferences();
        })));
        panel.Children.Add(Ui.Text("Dữ liệu cục bộ: "+vm.Store.Root,12,Brushes.DimGray)); return panel;
    }
}
