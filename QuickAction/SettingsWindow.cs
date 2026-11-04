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
public sealed partial class SettingsWindow : Window
{
    private readonly MainViewModel vm;
    private ListBox list => ShortcutList;
    private TextBox name => NameBox;
    private TextBox path => PathBox;
    private TextBox arguments => ArgsBox;
    private TextBox work => WorkDirBox;
    private CheckBox admin => AdminCheck;
    private ComboBox icons => IconModeCombo;
    private TextBlock iconStatus => IconStatus;
    private readonly System.Windows.Data.CollectionViewSource shortcutSource = new();
    private Guid? editing;
    private GroupEditor? groupEditor;
    private GroupStep? editingGroupStep;
    private string icon="builtin:auto";
    private bool dirty,loading;
    private string displayedLanguage;
    private TabControl tabs => Pages;
    public SettingsWindow(MainViewModel model)
    {
        vm=model; displayedLanguage=vm.Config.Language; InitializeComponent(); DataContext=vm;
        PreferencesHost.Content=BuildPreferences(); BackupHost.Content=BuildBackup();
        shortcutSource.Source=vm.Items;
        shortcutSource.Filter+=(_,e)=>e.Accepted=e.Item is Shortcut item &&
            (item.Name.Contains(SearchBox.Text,StringComparison.OrdinalIgnoreCase) || item.Path.Contains(SearchBox.Text,StringComparison.OrdinalIgnoreCase) || item.Arguments.Contains(SearchBox.Text,StringComparison.OrdinalIgnoreCase));
        list.ItemsSource=shortcutSource.View;
        SearchBox.TextChanged+=(_,_)=> {
            // Filtering must not discard an unsaved editor.
            shortcutSource.View.Refresh(); UpdateEmptyState();
        };
        AddShortcutButton.Click+=(_,_)=> { if(CanDiscard()) { SearchBox.Clear(); list.SelectedItem=null; Load(null); ShowDetails(); } };
        AddGroupButton.Click+=(_,_)=> { if(CanDiscard()) OpenGroup(null); };
        BrowsePathButton.Click+=(_,_)=> { var dialog=new OpenFileDialog { Filter=L10n.T("Chương trình|*.exe;*.bat;*.cmd;*.ps1;*.msi") }; if(dialog.ShowDialog(this)==true) { path.Text=dialog.FileName; if(string.IsNullOrWhiteSpace(name.Text)) name.Text=Path.GetFileNameWithoutExtension(dialog.FileName); } };
        BrowseWorkDirButton.Click+=(_,_)=> { var dialog=new OpenFolderDialog(); if(dialog.ShowDialog(this)==true) work.Text=dialog.FolderName; };
        ChooseInstalledAppButton.Click+=(_,_)=>vm.Guard(()=>
        {
            var picker=new InstalledAppPicker { Owner=this };
            if(picker.ShowDialog()!=true || picker.SelectedApp is not { } selected) return;
            name.Text=selected.Name;
            path.Text=selected.Target;
            arguments.Text=selected.Arguments;
            work.Text=selected.WorkingDirectory; admin.IsChecked=false; icons.SelectedIndex=0; dirty=true;
            if(selected.Icon is System.Windows.Media.Imaging.BitmapSource bitmap)
            {
                var iconName=Guid.NewGuid().ToString("N")+".png";
                var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using(var output=File.Create(Path.Combine(vm.Store.Icons,iconName))) encoder.Save(output);
                icon=iconName; loading=true; icons.SelectedIndex=6; loading=false; iconStatus.Text=L10n.T("Icon của ")+selected.Name; iconStatus.Visibility=Visibility.Visible;
            }
        });
        BackButton.Content="← "+L10n.T("Quay lại");
        BackButton.Click+=(_,_)=> { if(editingGroupStep!=null) { if(CanDiscardForm()) ReturnToGroup(); } else if(CanDiscard()) ShowList(); };
        SaveButton.Click+=(_,_)=>vm.Guard(SaveItem);
        RunButton.Click+=(_,_)=>vm.Guard(()=>_ = vm.Run(ReadForm()));
        DeleteButton.Click+=(_,_)=>Delete();
        var menu=new ContextMenu(); var duplicate=new MenuItem { Header=L10n.T("Nhân bản shortcut") };
        duplicate.Click+=(_,_)=> { if(CanDiscard() && list.SelectedItem is Shortcut item) { if(item.IsGroup) { var copy=item.Copy(); copy.Id=Guid.NewGuid(); copy.PinOrder=null; copy.Name+=L10n.T(" — bản sao"); OpenGroup(copy); return; } Load(item); editing=null; name.Text=item.Name+L10n.T(" — bản sao"); dirty=true; ShowDetails(); } };
        menu.Items.Add(duplicate); list.ContextMenu=menu;
        foreach(var (box,label) in new[]{(name,L10n.T("Tên shortcut")),(path,L10n.T("Đường dẫn chương trình")),(arguments,L10n.T("Tham số")),(work,L10n.T("Thư mục làm việc")),(SearchBox,L10n.T("Tìm shortcut"))}) System.Windows.Automation.AutomationProperties.SetName(box,label);
        foreach(var box in new[]{name,path,arguments,work}) box.TextChanged+=(_,_)=> { if(!loading) dirty=true; };
        admin.Click+=(_,_)=>dirty=true;
        icons.SelectionChanged+=(_,e)=>
        {
            if(loading) return;
            if(icons.SelectedIndex==6)
            {
                int previous=e.RemovedItems.Count>0?icons.Items.IndexOf(e.RemovedItems[0]):0;
                vm.Guard(()=>
                {
                    bool selected=false; loading=true;
                    try
                    {
                        icons.IsDropDownOpen=false;
                        var dialog=new OpenFileDialog { Filter="Icon|*.ico;*.png;*.jpg;*.jpeg" };
                        if(dialog.ShowDialog(this)==true) { icon=vm.Store.ImportIcon(dialog.FileName); iconStatus.Text=Path.GetFileName(dialog.FileName); dirty=true; selected=true; }
                    }
                    finally { if(!selected) icons.SelectedIndex=previous; loading=false; }
                });
            }
            else dirty=true;
            iconStatus.Visibility=icons.SelectedIndex==6 && !icon.StartsWith("builtin:")?Visibility.Visible:Visibility.Collapsed;
        };
        vm.Updated+=RefreshList;
        AllowDrop=true; DragOver+=(_,e)=> { e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None; e.Handled=true; };
        Drop+=(_,e)=> { if(e.Data.GetData(DataFormats.FileDrop) is string[] files) foreach(var f in files.Where(LaunchBuilder.Supported)) AddFile(f); e.Handled=true; };
        Closing+=(_,e)=> { if(!CanDiscard()) e.Cancel=true; }; Closed+=(_,_)=>vm.Updated-=RefreshList;
        Load(null); UpdateEmptyState();
    }
    private void RefreshList()
    {
        if(displayedLanguage!=vm.Config.Language)
        {
            displayedLanguage=vm.Config.Language;
            BackButton.Content="← "+L10n.T("Quay lại");
            PreferencesHost.Content=BuildPreferences(); BackupHost.Content=BuildBackup();
            ((MenuItem)list.ContextMenu.Items[0]).Header=L10n.T("Nhân bản shortcut");
            foreach(var (box,label) in new[]{(name,"Tên shortcut"),(path,"Đường dẫn chương trình"),(arguments,"Tham số"),(work,"Thư mục làm việc"),(SearchBox,"Tìm shortcut")})
                System.Windows.Automation.AutomationProperties.SetName(box,L10n.T(label));
        }
        shortcutSource.View.Refresh(); UpdateEmptyState();
    }
    private void UpdateEmptyState()
    {
        EmptyState.Visibility=list.Items.IsEmpty?Visibility.Visible:Visibility.Collapsed;
        EmptyTitle.Text=vm.Items.Count==0?L10n.T("Chưa có shortcut nào"):L10n.T("Không tìm thấy shortcut");
    }
    private void ShortcutRowLoaded(object sender,RoutedEventArgs e)
    {
        if(sender is ContentControl control && control.DataContext is Shortcut item) control.Content=Ui.AppRow(item,vm,()=>OpenDetails(item));
    }
    private void ShortcutRowDataContextChanged(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(sender is ContentControl control) control.Content=e.NewValue is Shortcut item?Ui.AppRow(item,vm,()=>OpenDetails(item)):null;
    }
    private void ShowDetails()
    {
        GroupDetailsHost.Visibility=Visibility.Collapsed;
        ShortcutListPage.Visibility=Visibility.Collapsed;
        ShortcutDetailsPage.Visibility=Visibility.Visible;
        name.Focus();
    }
    private void ShowList()
    {
        groupEditor=null; editingGroupStep=null; GroupDetailsHost.Content=null; GroupDetailsHost.Visibility=Visibility.Collapsed;
        DeleteButton.Visibility=Visibility.Visible;
        list.SelectedItem=null;
        Load(null);
        ShortcutDetailsPage.Visibility=Visibility.Collapsed;
        ShortcutListPage.Visibility=Visibility.Visible;
        SearchBox.Focus();
    }
    private bool ConfirmDiscard()=>MessageBox.Show(this,L10n.T("Bỏ các thay đổi chưa lưu?"),"QuickAction",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
    private bool CanDiscardForm()=>!dirty||ConfirmDiscard();
    private bool CanDiscard()=>!(dirty || groupEditor?.IsDirty==true)||ConfirmDiscard();
    private void OpenDetails(Shortcut item)
    {
        if(!CanDiscard()) return;
        if(item.IsGroup) { OpenGroup(item); return; }
        list.SelectedItem=item; Load(item); ShowDetails();
    }
    private void OpenGroup(Shortcut? item)
    {
        Load(null); editingGroupStep=null;
        groupEditor=new GroupEditor(vm,item);
        groupEditor.BackRequested+=()=> { if(CanDiscard()) ShowList(); };
        groupEditor.EditAppRequested+=step=>
        {
            editingGroupStep=step; Load(step.App); ShowDetails(); DeleteButton.Visibility=Visibility.Collapsed;
            BackButton.Content="← Nhóm app";
        };
        GroupDetailsHost.Content=groupEditor;
        ReturnToGroup();
    }
    private void ReturnToGroup()
    {
        editingGroupStep=null; Load(null); DeleteButton.Visibility=Visibility.Visible;
        BackButton.Content="← "+L10n.T("Quay lại");
        ShortcutListPage.Visibility=Visibility.Collapsed; ShortcutDetailsPage.Visibility=Visibility.Collapsed;
        GroupDetailsHost.Visibility=Visibility.Visible;
    }
    private void Load(Shortcut? item)
    {
        loading=true; editing=item?.Id; name.Text=item?.Name??""; path.Text=item?.Path??""; arguments.Text=item?.Arguments??""; work.Text=item?.WorkingDirectory??""; admin.IsChecked=item?.Admin??false; icon=item?.Icon??"builtin:auto";
        icons.SelectedIndex=Array.IndexOf(new[]{"builtin:auto","builtin:app","builtin:terminal","builtin:folder","builtin:star","builtin:tools"},icon); if(icons.SelectedIndex<0) icons.SelectedIndex=6; iconStatus.Text=icon.StartsWith("builtin:")?"":icon;
        iconStatus.Visibility=icon.StartsWith("builtin:")?Visibility.Collapsed:Visibility.Visible;
        loading=false; dirty=false;
    }
    public void AddFile(string file)
    {
        if(!CanDiscard()) return; ShowList(); tabs.SelectedIndex=0; list.SelectedItem=null; Load(null); path.Text=file; name.Text=Path.GetFileNameWithoutExtension(file); dirty=true; ShowDetails();
    }
    private Shortcut ReadForm()
    {
        if(string.IsNullOrWhiteSpace(name.Text)) throw new ArgumentException(L10n.T("Nhập tên shortcut."));
        var selectedIcon=icons.SelectedIndex is >=0 and <6 ? new[]{"builtin:auto","builtin:app","builtin:terminal","builtin:folder","builtin:star","builtin:tools"}[icons.SelectedIndex] : icon;
        if(icons.SelectedIndex==6 && selectedIcon.StartsWith("builtin:")) throw new ArgumentException("Chọn Icon riêng từ danh sách để chọn file icon.");
        var item=new Shortcut { Id=editing??Guid.NewGuid(),Name=name.Text.Trim(),Path=path.Text.Trim(),Arguments=arguments.Text,WorkingDirectory=work.Text.Trim(),Admin=admin.IsChecked==true,Icon=selectedIcon,PinOrder=vm.Items.FirstOrDefault(s=>s.Id==editing)?.PinOrder };
        LaunchBuilder.Build(item,Environment.SystemDirectory); return item;
    }
    private void SaveItem()
    {
        if(editingGroupStep is { } step && groupEditor!=null)
        {
            groupEditor.UpdateApp(step,ReadForm()); ReturnToGroup(); return;
        }
        var item=ReadForm(); var old=vm.Items.FirstOrDefault(s=>s.Id==item.Id);
        if(old!=null) vm.Items[vm.Items.IndexOf(old)]=item; else vm.Items.Add(item);
        vm.Save(); dirty=false; SearchBox.Clear(); list.SelectedItem=item; Load(item);
    }
    private void Delete()
    {
        var item=vm.Items.FirstOrDefault(s=>s.Id==editing); if(item==null) return;
        if(MessageBox.Show(this,L10n.T("Xóa shortcut “")+item.Name+L10n.T("”? File chương trình vẫn được giữ nguyên."),"QuickAction",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
        vm.Guard(()=> { dirty=false; vm.Items.Remove(item); vm.Save(); ShowList(); });
    }
    private UIElement BuildPreferences()
    {
        var panel=new StackPanel(); panel.Children.Add(new TextBlock { Text=L10n.T("Dock & hệ thống"),Style=(Style)FindResource("TextBlock.SectionTitle") });
        Ui.Label(panel,L10n.T("Ngôn ngữ / Language"));
        var language=new Controls.SegmentedButtonGroup { Name="LanguageGroup",ItemsSource=new[]{"English","Tiếng Việt"},SelectedIndex=vm.Config.Language=="en"?0:1,Margin=new(0,5,0,16) };
        System.Windows.Automation.AutomationProperties.SetName(language,"Language / Ngôn ngữ"); panel.Children.Add(language);
        Ui.Label(panel,L10n.T("Màn hình"));
        var monitor=new Controls.SegmentedButtonGroup { Name="MonitorGroup",ItemsSource=Forms.Screen.AllScreens.Select(s=>s.DeviceName).ToArray(),SelectedItem=WindowsServices.Screen(vm.Config).DeviceName,Margin=new(0,5,0,16) }; panel.Children.Add(monitor);
        System.Windows.Automation.AutomationProperties.SetName(monitor,L10n.T("Màn hình"));
        Ui.Label(panel,L10n.T("Kích thước icon"));
        var iconSize=new Controls.SegmentedButtonGroup { Name="IconSizeGroup",ItemsSource=new[]{L10n.T("Nhỏ"),"Medium","Big"},SelectedIndex=(int)vm.Config.IconSize,Margin=new(0,5,0,16) };
        System.Windows.Automation.AutomationProperties.SetName(iconSize,L10n.T("Kích thước icon")); panel.Children.Add(iconSize);
        Ui.Label(panel,L10n.T("Cạnh ghim")); var edge=new Controls.SegmentedButtonGroup { ItemsSource=new[]{L10n.T("Trái"),L10n.T("Phải"),L10n.T("Trên"),L10n.T("Dưới")},SelectedIndex=(int)vm.Config.Edge,Margin=new(0,5,0,16) }; panel.Children.Add(edge);
        System.Windows.Automation.AutomationProperties.SetName(edge,L10n.T("Cạnh ghim"));
        var topmost=new CheckBox { Margin=new(0,8,0,12),Content=L10n.T("Luôn nổi trên các cửa sổ khác"),IsChecked=vm.Config.Topmost }; panel.Children.Add(topmost);
        var startup=new CheckBox { Margin=new(0,0,0,16),Content=L10n.T("Khởi động cùng Windows"),IsChecked=vm.Config.Startup }; panel.Children.Add(startup);
        Ui.Label(panel,L10n.T("Độ đục nền (30–100%)")); var opacity=new Slider { Minimum=.3,Maximum=1,Value=vm.Config.Opacity,TickFrequency=.1,Margin=new(0,8,0,18) }; panel.Children.Add(opacity);
        Ui.Label(panel,L10n.T("Thời gian thu gọn (200–5000 ms)")); var delay=new TextBox { Margin=new(0,4,0,16),Text=vm.Config.CollapseMilliseconds.ToString() }; panel.Children.Add(delay);
        var delayError=Ui.Text(L10n.T("Thời gian phải từ 200 đến 5000 ms."),12,Brushes.Firebrick);
        delayError.Visibility=Visibility.Collapsed; panel.Children.Add(delayError);
        // Subscribe after initialization so opening/rebuilding this page never writes settings.
        void Apply<T>(T current,T value,Action<T> set)
        {
            if(System.Collections.Generic.EqualityComparer<T>.Default.Equals(current,value)) return;
            vm.Guard(()=>
            {
                try { set(value); vm.Save(); }
                catch
                {
                    try { set(current); }
                    finally { PreferencesHost.Content=BuildPreferences(); }
                    throw;
                }
            });
        }
        language.SelectionChanged+=(_,_)=> { if(language.SelectedIndex>=0) Apply(vm.Config.Language,language.SelectedIndex==0?"en":"vi",value=>vm.Config.Language=value); };
        monitor.SelectionChanged+=(_,_)=> { if(monitor.SelectedItem is string selected) Apply(vm.Config.Monitor,selected,value=>vm.Config.Monitor=value); };
        iconSize.SelectionChanged+=(_,_)=> { if(iconSize.SelectedIndex>=0) Apply(vm.Config.IconSize,(DockIconSize)iconSize.SelectedIndex,value=>vm.Config.IconSize=value); };
        edge.SelectionChanged+=(_,_)=> { if(edge.SelectedIndex>=0) Apply(vm.Config.Edge,(Edge)edge.SelectedIndex,value=>vm.Config.Edge=value); };
        topmost.Click+=(_,_)=>Apply(vm.Config.Topmost,topmost.IsChecked==true,value=>vm.Config.Topmost=value);
        startup.Click+=(_,_)=>Apply(vm.Config.Startup,startup.IsChecked==true,value=> { WindowsServices.Startup(value); vm.Config.Startup=value; });
        opacity.ValueChanged+=(_,_)=>Apply(vm.Config.Opacity,opacity.Value,value=>vm.Config.Opacity=value);
        delay.TextChanged+=(_,_)=>
        {
            bool valid=int.TryParse(delay.Text,out var ms)&&ms>=200&&ms<=5000;
            delayError.Visibility=valid?Visibility.Collapsed:Visibility.Visible;
            if(valid) Apply(vm.Config.CollapseMilliseconds,ms,value=>vm.Config.CollapseMilliseconds=value);
        };
        delay.LostKeyboardFocus+=(_,_)=> { if(delayError.Visibility==Visibility.Visible) delay.Text=vm.Config.CollapseMilliseconds.ToString(); };
        panel.Children.Add(Ui.Text(L10n.T("Thiết lập được áp dụng và lưu tự động. Bạn có thể kéo nền dock để đổi cạnh và màn hình. Nếu chuyển thư mục ứng dụng, hãy tắt rồi bật lại Khởi động cùng Windows."),12,Brushes.DimGray)); return panel;
    }
    private UIElement BuildBackup()
    {
        var panel=new StackPanel(); panel.Children.Add(Ui.Text(L10n.T("Sao lưu shortcut & icon"),22)); panel.Children.Add(Ui.Text(L10n.T("Tệp JSON chứa đường dẫn, parameters và icon; không chứa chương trình đích. Parameters có thể chứa thông tin riêng tư."),14,Brushes.DimGray));
        panel.Children.Add(Ui.Button(L10n.T("Xuất bản sao lưu…"),()=>vm.Guard(()=> { var dialog=new SaveFileDialog { Filter="QuickAction JSON backup|*.json",DefaultExt=".json",FileName="QuickAction-backup-"+DateTime.Now.ToString("yyyyMMdd")+".json" }; if(dialog.ShowDialog(this)==true) { var temporary=dialog.FileName+"."+Guid.NewGuid().ToString("N")+".tmp"; try { vm.Store.Export(vm.Config,temporary); File.Move(temporary,dialog.FileName,true); } finally { if(File.Exists(temporary)) File.Delete(temporary); } } })));
        panel.Children.Add(Ui.Button(L10n.T("Khôi phục từ JSON…"),()=>vm.Guard(()=>
        {
            var dialog=new OpenFileDialog { Filter="QuickAction JSON backup|*.json|Legacy ZIP backup|*.zip" }; if(dialog.ShowDialog(this)!=true||!CanDiscard()) return;
            if(MessageBox.Show(this,L10n.T("Thay thế toàn bộ shortcut và thiết lập hiện tại bằng bản sao lưu?"),L10n.T("Khôi phục"),MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes) return;
            dirty=false; vm.ReplaceFrom(dialog.FileName); WindowsServices.Startup(vm.Config.Startup); ShowList(); PreferencesHost.Content=BuildPreferences();
        })));
        panel.Children.Add(Ui.Text(L10n.T("Dữ liệu cục bộ: ")+vm.Store.Root,12,Brushes.DimGray)); return panel;
    }
}

