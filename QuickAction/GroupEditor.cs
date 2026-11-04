using Microsoft.Win32;
using QuickAction.Core;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickAction;

public sealed class GroupEditor : UserControl
{
    private readonly MainViewModel vm;
    private Shortcut draft;
    private readonly TextBox groupName=new();
    private readonly StackPanel rows=new(), timeline=new();
    private readonly ComboBox iconPicker=new();
    private bool dirty;
    public bool IsDirty => dirty;
    public event Action? BackRequested;
    public event Action<GroupStep>? EditAppRequested;
    public void UpdateApp(GroupStep step,Shortcut app) { app.PinOrder=null; step.App=app; if(!draft.Steps!.Contains(step)) draft.Steps.Add(step); dirty=true; Render(); ScrollToStep(step); }
    private void ScrollToStep(GroupStep step)=>Dispatcher.BeginInvoke(new Action(()=>
    {
        foreach(FrameworkElement row in rows.Children) if(row.Tag is Guid id && id==step.Id) row.BringIntoView();
    }),System.Windows.Threading.DispatcherPriority.Loaded);
    private const string DragFormat="QuickAction.GroupStep";
    public GroupEditor(MainViewModel model,Shortcut? source=null)
    {
        vm=model; draft=source?.Copy() ?? new Shortcut { Name="", Icon="builtin:group", Steps=[] };

        FontFamily=new FontFamily("Segoe UI"); FontSize=14; Background=new SolidColorBrush(Color.FromRgb(244,246,251));
        var layout=new DockPanel(); Content=layout;
        var back=Ui.Button("← "+L10n.T("Quay lại"),()=>BackRequested?.Invoke()); back.HorizontalAlignment=HorizontalAlignment.Left; DockPanel.SetDock(back,Dock.Top); layout.Children.Add(back);
        var header=new Grid(); header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width=new(200) });
        var naming=new StackPanel { Margin=new(0,0,24,0) }; Ui.Label(naming,"Tên nhóm"); groupName.Text=draft.Name; naming.Children.Add(groupName); header.Children.Add(naming);
        var iconArea=new StackPanel(); Ui.Label(iconArea,"Icon");
        var iconKeys=new[]{"builtin:group","builtin:app","builtin:terminal","builtin:folder","builtin:star","builtin:tools"};
        iconPicker.ItemsSource=new[]{"Xếp lớp","Ứng dụng","Terminal","Thư mục","Ngôi sao","Công cụ","Icon riêng…"};
        iconPicker.SelectedIndex=Array.IndexOf(iconKeys,draft.Icon); if(iconPicker.SelectedIndex<0) iconPicker.SelectedIndex=6;
        iconPicker.Height=34; iconArea.Children.Add(iconPicker);
        bool selectingIcon=false;
        iconPicker.SelectionChanged+=(_,_)=>
        {
            if(selectingIcon) return;
            if(iconPicker.SelectedIndex is >=0 and <6) { draft.Icon=iconKeys[iconPicker.SelectedIndex]; dirty=true; return; }
            vm.Guard(()=>
            {
                selectingIcon=true;
                try
                {
                    iconPicker.IsDropDownOpen=false;
                    var dialog=new OpenFileDialog { Filter="Icon|*.ico;*.png;*.jpg;*.jpeg" };
                    if(dialog.ShowDialog(Window.GetWindow(this))==true) { draft.Icon=vm.Store.ImportIcon(dialog.FileName); dirty=true; }
                }
                finally { var index=Array.IndexOf(iconKeys,draft.Icon); iconPicker.SelectedIndex=index<0?6:index; selectingIcon=false; }
            });
        };
        Grid.SetColumn(iconArea,1); header.Children.Add(iconArea); var top=Card(header); DockPanel.SetDock(top,Dock.Top); layout.Children.Add(top);
        var footer=new DockPanel { Margin=new(0,12,0,0) };
        var save=Ui.Button("Lưu nhóm",()=>vm.Guard(Save)); save.Background=Brushes.RoyalBlue; save.Foreground=Brushes.White; footer.Children.Add(save);
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
        actions.Children.Add(Ui.Button("Nhân bản",()=> { draft.Id=Guid.NewGuid(); draft.PinOrder=null; groupName.Text+=" — bản sao"; dirty=true; }));
        actions.Children.Add(Ui.Button("Xóa",()=>vm.Guard(()=>
        {
            var existing=vm.Items.FirstOrDefault(s=>s.Id==draft.Id); if(existing==null) return;
            if(MessageBox.Show(Window.GetWindow(this),"Xóa nhóm “"+existing.Name+"”?","QuickAction",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
            vm.Items.Remove(existing); vm.Save(); dirty=false; BackRequested?.Invoke();
        })));
        footer.Children.Add(actions); DockPanel.SetDock(footer,Dock.Bottom); layout.Children.Add(footer);
        var body=new Grid(); body.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)}); body.ColumnDefinitions.Add(new(){Width=new(245)});
        var stepsArea=new DockPanel();
        var tools=new StackPanel(); tools.Children.Add(Ui.Text("Các bước chạy",18)); tools.Children.Add(Ui.Text("Kéo để đổi thứ tự. Mặc định chạy cùng app ngay phía trước; phụ thuộc không hợp lệ sẽ về mặc định.",12,Brushes.SlateGray));
        DockPanel.SetDock(tools,Dock.Top); stepsArea.Children.Add(tools);
        stepsArea.Children.Add(new ScrollViewer { Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
        body.Children.Add(Card(stepsArea));
        var summary=Card(new ScrollViewer { Content=timeline,VerticalScrollBarVisibility=ScrollBarVisibility.Auto }); summary.Margin=new(12,8,0,0);
        SizeChanged+=(_,_)=> { bool wide=ActualWidth>=900; summary.Visibility=wide?Visibility.Visible:Visibility.Collapsed; body.ColumnDefinitions[1].Width=new GridLength(wide?245:0); }; Grid.SetColumn(summary,1); body.Children.Add(summary); layout.Children.Add(body);
        groupName.TextChanged+=(_,_)=>dirty=true;
        Render();
    }
    private static Border Card(UIElement content)=>new() { Child=content,Padding=new(14),Margin=new(0,8,0,0),CornerRadius=new(12),Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(220,226,239)),BorderThickness=new(1) };
    private void Add(Shortcut app)=>EditAppRequested?.Invoke(new GroupStep { App=app });
    private void Move(GroupStep step,int index)
    {
        var steps=draft.Steps!; var from=steps.IndexOf(step); if(from==index || index<0 || index>=steps.Count) return;
        steps.RemoveAt(from); steps.Insert(index,step); GroupRules.ResetInvalidDependencies(steps); dirty=true; Render();
    }
    private void Render()
    {
        rows.Children.Clear(); var steps=draft.Steps!;
        GroupRules.ResetInvalidDependencies(steps);
        if(steps.Count==0) rows.Children.Add(Ui.Text("Thêm shortcut hoặc file chương trình để bắt đầu.",14,Brushes.SlateGray));
        foreach(var step in steps)
        {
            int index=steps.IndexOf(step); var panel=new StackPanel(); var title=new DockPanel();
            var appIcon=Ui.Icon(step.App,vm); appIcon.Margin=new(0,0,10,0); appIcon.ToolTip="Kéo để đổi thứ tự"; appIcon.Cursor=Cursors.SizeAll; title.Children.Add(appIcon);
            Point? origin=null;
            appIcon.PreviewMouseLeftButtonDown+=(_,e)=>origin=e.GetPosition(appIcon);
            appIcon.PreviewMouseMove+=(_,e)=> { if(origin is not Point p || e.LeftButton!=MouseButtonState.Pressed) return; var delta=e.GetPosition(appIcon)-p; if(Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance && Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance) return; origin=null; DragDrop.DoDragDrop(appIcon,new DataObject(DragFormat,step.Id),DragDropEffects.Move); };
            var buttons=new StackPanel { Orientation=Orientation.Horizontal }; DockPanel.SetDock(buttons,Dock.Right);
            var up=Ui.Button("↑",()=>Move(step,index-1)); up.IsEnabled=index>0; buttons.Children.Add(up);
            var down=Ui.Button("↓",()=>Move(step,index+1)); down.IsEnabled=index<steps.Count-1; buttons.Children.Add(down);
            buttons.Children.Add(Ui.Button("✎",()=>EditAppRequested?.Invoke(step),"Chỉnh sửa app"));
            buttons.Children.Add(Ui.Button("×",()=> { steps.Remove(step); GroupRules.ResetInvalidDependencies(steps); dirty=true; Render(); })); title.Children.Add(buttons);
            var appTitle=new StackPanel(); appTitle.Children.Add(Ui.Text($"{index+1}. {step.App.Name}",15)); var path=Ui.Text(step.App.Path,11,Brushes.SlateGray); path.TextTrimming=TextTrimming.CharacterEllipsis; path.TextWrapping=TextWrapping.NoWrap; path.ToolTip=step.App.Path; appTitle.Children.Add(path); title.Children.Add(appTitle); panel.Children.Add(title);
            if(index==0) panel.Children.Add(Ui.Text("▶ Bắt đầu ngay khi chạy nhóm",13,Brushes.RoyalBlue));
            else
            {
                var conditionRow=new Grid { Margin=new(0,4,0,0) };
                conditionRow.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
                conditionRow.ColumnDefinitions.Add(new(){Width=new(160)});
                var dependencies=new ComboBox { Margin=new(8,0,0,0),DisplayMemberPath="Label",SelectedValuePath="Id",VerticalContentAlignment=VerticalAlignment.Center,ToolTip="App phụ thuộc" };
                foreach(var previous in steps.Take(index)) dependencies.Items.Add(new DependencyChoice(previous.Id,$"{steps.IndexOf(previous)+1}. {previous.App.Name}"));
                dependencies.SelectedValue=step.DependsOn;
                Grid.SetColumn(dependencies,1); conditionRow.Children.Add(dependencies);
                var conditions=new Controls.SegmentedButtonGroup { ItemsSource=new[]{"Cùng với","Khi sẵn sàng","Khi kết thúc"},SelectedIndex=step.Condition.HasValue?(int)step.Condition.Value:-1,IsEnabled=step.DependsOn.HasValue };
                conditionRow.Children.Add(conditions); panel.Children.Add(conditionRow);
                var delayPanel=new StackPanel { Orientation=Orientation.Horizontal,Margin=new(0,8,0,0) };
                var delay=new TextBox { Width=80,Text=step.DelaySeconds.ToString(),IsEnabled=step.DependsOn.HasValue,MaxLength=5,Margin=new(0),VerticalAlignment=VerticalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center };
                bool Valid(string text)=>text.Length>0 && text.All(c=>c>='0' && c<='9') && int.TryParse(text,out int n) && n<=86400;
                delay.PreviewTextInput+=(_,e)=>e.Handled=!Valid(delay.Text.Remove(delay.SelectionStart,delay.SelectionLength).Insert(delay.SelectionStart,e.Text));
                DataObject.AddPastingHandler(delay,(_,e)=> { if(e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !Valid(delay.Text.Remove(delay.SelectionStart,delay.SelectionLength).Insert(delay.SelectionStart,text))) e.CancelCommand(); });
                delay.TextChanged+=(_,_)=> { if(Valid(delay.Text)) { step.DelaySeconds=int.Parse(delay.Text); dirty=true; UpdateTimeline(); } };
                delay.LostKeyboardFocus+=(_,_)=> { if(!Valid(delay.Text)) delay.Text=step.DelaySeconds.ToString(); };
                var delayLabel=Ui.Text("Chờ thêm",12,Brushes.SlateGray); delayLabel.VerticalAlignment=VerticalAlignment.Center; delayLabel.Margin=new(0,0,8,0);
                var delayUnit=Ui.Text("giây (0–86400)",12,Brushes.SlateGray); delayUnit.VerticalAlignment=VerticalAlignment.Center; delayUnit.Margin=new(8,0,0,0);
                delayPanel.Children.Add(delayLabel); delayPanel.Children.Add(delay); delayPanel.Children.Add(delayUnit); panel.Children.Add(delayPanel);
                dependencies.SelectionChanged+=(_,_)=> { step.DependsOn=((DependencyChoice)dependencies.SelectedItem).Id; step.Condition=step.DependsOn.HasValue?step.Condition??GroupCondition.Together:null; if(!step.DependsOn.HasValue) step.DelaySeconds=0; dirty=true; Render(); };
                conditions.SelectionChanged+=(_,_)=> { if(conditions.SelectedIndex<0) return; step.Condition=(GroupCondition)conditions.SelectedIndex; dirty=true; UpdateTimeline(); };
            }
            var row=Card(panel); row.Tag=step.Id; row.Background=new SolidColorBrush(Color.FromRgb(249,250,254)); row.AllowDrop=true;
            row.DragOver+=(_,e)=> { e.Effects=e.Data.GetData(DragFormat) is Guid?DragDropEffects.Move:DragDropEffects.None; e.Handled=true; };
            row.Drop+=(_,e)=> { if(e.Data.GetData(DragFormat) is Guid id && steps.FirstOrDefault(s=>s.Id==id) is { } moving) Move(moving,index); e.Handled=true; };
            rows.Children.Add(row);
        }
        var add=Ui.Button("+ Thêm app vào nhóm",()=>Add(new Shortcut { Name="",Icon="builtin:auto" }));
        add.Margin=new(0,12,0,8); add.HorizontalAlignment=HorizontalAlignment.Stretch;
        rows.Children.Add(add);
        UpdateTimeline();
    }
    private void UpdateTimeline()
    {
        timeline.Children.Clear(); timeline.Children.Add(Ui.Text("Thứ tự thực tế",18));
        foreach(var step in draft.Steps!)
        {
            var parent=draft.Steps.FirstOrDefault(s=>s.Id==step.DependsOn);
            string trigger=parent==null?"Bắt đầu":step.Condition switch { GroupCondition.Together=>"Cùng với "+parent.App.Name,GroupCondition.InputIdle=>"Khi "+parent.App.Name+" sẵn sàng",_=>"Khi "+parent.App.Name+" kết thúc" };
            if(step.DelaySeconds>0) trigger+=$" (+{step.DelaySeconds} s)";
            timeline.Children.Add(Ui.Text("○ "+trigger,12,Brushes.RoyalBlue)); timeline.Children.Add(Ui.Text("    "+step.App.Name,13));
        }
        timeline.Children.Add(Ui.Text("Sẵn sàng: WaitForInputIdle, chờ tối đa 30 giây rồi tiếp tục. App không có giao diện sẽ tiếp tục ngay. Các nhánh chạy độc lập; thời điểm kết thúc tùy app.",12,Brushes.SlateGray));
    }
    private void Save()
    {
        if(string.IsNullOrWhiteSpace(groupName.Text)) throw new ArgumentException("Nhập tên nhóm.");
        draft.Name=groupName.Text.Trim(); GroupRules.Validate(draft.Steps!);
        foreach(var step in draft.Steps!) LaunchBuilder.Build(step.App,Environment.SystemDirectory);
        var item=draft.Copy(); var old=vm.Items.FirstOrDefault(s=>s.Id==item.Id);
        if(old==null) vm.Items.Add(item); else vm.Items[vm.Items.IndexOf(old)]=item;
        try { vm.Save(); }
        catch { if(old==null) vm.Items.Remove(item); else vm.Items[vm.Items.IndexOf(item)]=old; throw; }
        dirty=false; BackRequested?.Invoke();
    }
    private sealed record DependencyChoice(Guid? Id,string Label);
}
