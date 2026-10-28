using QuickAction.Core;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace QuickAction;
public sealed class DockWindow : Window
{
    private readonly MainViewModel vm;
    private readonly Border surface=new() { CornerRadius=new(28),BorderThickness=new(1),BorderBrush=new SolidColorBrush(Color.FromArgb(150,255,255,255)) };
    private readonly DispatcherTimer hover=new() { Interval=TimeSpan.FromMilliseconds(150) };
    private readonly DispatcherTimer collapse=new();
    private readonly Forms.NotifyIcon tray;
    private bool expanded,all,dragging,blur,placing,suppressShortcutClick;
    private bool shapeUpdatePending;
    private SettingsWindow? settings;
    private Point? dragStart;
    private Guid? dragId;
    private StackPanel? rows;
    private string search="";
    private bool Vertical=>vm.Config.Edge is Edge.Left or Edge.Right;
    public DockWindow(MainViewModel viewModel)
    {
        vm=viewModel; DataContext=vm; SetResourceReference(StyleProperty,typeof(Window)); Title="QuickAction"; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; ShowInTaskbar=false; ShowActivated=false; Background=Brushes.Transparent; Content=surface; AllowDrop=true;
        Width=10; Height=100;
        tray=new Forms.NotifyIcon { Icon=System.Drawing.SystemIcons.Application,Text="QuickAction",Visible=true };
        var menu=new Forms.ContextMenuStrip(); menu.Items.Add("Hiện / ẩn dock",null,(_,_)=>Dispatcher.Invoke(()=> { if(IsVisible) Hide(); else { Show(); Render(); } })); menu.Items.Add("Settings",null,(_,_)=>Dispatcher.Invoke(()=>OpenSettings())); menu.Items.Add("Thoát",null,(_,_)=>Dispatcher.Invoke(()=>Application.Current.Shutdown())); tray.ContextMenuStrip=menu;
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=> { Show(); OpenAll(); Activate(); });
        vm.Notification+=Notify; vm.Updated+=Refresh;
        hover.Tick+=(_,_)=> { hover.Stop(); if(!dragging) { expanded=true; Render(); } };
        collapse.Tick+=(_,_)=> { collapse.Stop(); if(!IsMouseOver&&!all&&!dragging) { expanded=false; Keyboard.ClearFocus(); Render(); } };
        MouseEnter+=(_,_)=> { collapse.Stop(); if(!expanded) hover.Start(); };
        MouseLeave+=(_,_)=> { hover.Stop(); ScheduleCollapse(); };
        Deactivated+=(_,_)=> { if(!dragging) { all=false; Keyboard.ClearFocus(); ScheduleCollapse(); Render(); } };
        PreviewKeyDown+=(_,e)=> { if(e.Key==Key.Escape) { all=false; expanded=false; Keyboard.ClearFocus(); Render(); e.Handled=true; } };
        PreviewMouseLeftButtonDown+=StartDrag; PreviewMouseMove+=MoveDrag; PreviewMouseLeftButtonUp+=(_,_)=> { dragStart=null; dragId=null; };
        DragOver+=(_,e)=> { e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:e.Data.GetDataPresent("QuickAction.Pin")?DragDropEffects.Move:DragDropEffects.None; e.Handled=true; };
        Drop+=(_,e)=> { if(e.Data.GetData(DataFormats.FileDrop) is string[] files) foreach(var f in files.Where(LaunchBuilder.Supported)) OpenSettings(f); e.Handled=true; };
        SourceInitialized+=(_,_)=> { blur=WindowsServices.Backdrop(this); HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(Hook); Render(); if(vm.Store.RecoveryMessage!=null) Notify(vm.Store.RecoveryMessage); };
        SizeChanged+=(_,_)=>QueueShapeUpdate();
        ContentRendered+=(_,_)=>QueueShapeUpdate();
        SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        SystemEvents.UserPreferenceChanged+=PreferencesChanged;
        Closed+=(_,_)=> { hover.Stop(); collapse.Stop(); tray.Dispose(); vm.Notification-=Notify; vm.Updated-=Refresh; SystemEvents.DisplaySettingsChanged-=DisplayChanged; SystemEvents.UserPreferenceChanged-=PreferencesChanged; };
    }
    private void PreferencesChanged(object sender,UserPreferenceChangedEventArgs e)=>Dispatcher.BeginInvoke(()=> { blur=WindowsServices.Backdrop(this); Render(); });
    private void DisplayChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(()=> { if(!Forms.Screen.AllScreens.Any(s=>s.DeviceName==vm.Config.Monitor)) { vm.Config.Monitor=Forms.Screen.PrimaryScreen!.DeviceName; vm.Guard(vm.Save); } Render(); });
    private IntPtr Hook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled)
    {
        if(message==0x02E0&&!placing) Dispatcher.BeginInvoke(Render);
        // Reapply after WPF has finished its native resize/style processing.
        if(message is 0x0047 or 0x02E0 or 0x007D) QueueShapeUpdate();
        return IntPtr.Zero;
    }
    private void QueueShapeUpdate()
    {
        if(shapeUpdatePending) return;
        shapeUpdatePending=true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>
        {
            shapeUpdatePending=false;
            if(!IsVisible) return;
            var radius=Math.Min(28,Math.Min(ActualWidth,ActualHeight)/2);
            surface.CornerRadius=new(radius);
            WindowsServices.Round(this,radius);
        });
    }
    private void Refresh() { if(!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(Refresh); return; } Render(); }
    private void Notify(string message)
    {
        if(!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(()=>Notify(message)); return; }
        tray.BalloonTipTitle="QuickAction"; tray.BalloonTipText=message; tray.ShowBalloonTip(4000);
    }
    private void ScheduleCollapse() { collapse.Interval=TimeSpan.FromMilliseconds(vm.Config.CollapseMilliseconds); collapse.Start(); }
    public void OpenAll() { expanded=true; all=true; Render(); }
    public void OpenSettings(string? file=null)
    {
        if(settings==null) { settings=new SettingsWindow(vm); settings.Closed+=(_,_)=>settings=null; }
        settings.Show(); settings.Activate(); if(file!=null) settings.AddFile(file);
    }
    private void StartDrag(object sender,MouseButtonEventArgs e)
    {
        var source=e.OriginalSource as DependencyObject;
        while(source!=null && source is not Button && source is not TextBox && source is not CheckBox) source=VisualTreeHelper.GetParent(source);
        if(source is Button button && button.Tag is Guid id) { dragId=id; dragStart=e.GetPosition(this); }
        else if(source==null) { dragId=null; dragStart=e.GetPosition(this); }
    }
    private void MoveDrag(object sender,MouseEventArgs e)
    {
        if(e.LeftButton!=MouseButtonState.Pressed||dragStart==null||dragging) return;
        var point=e.GetPosition(this); if((point-dragStart.Value).Length<6) return;
        dragging=true; hover.Stop(); collapse.Stop();
        try
        {
            if(dragId is Guid id) { suppressShortcutClick=true; DragDrop.DoDragDrop(this,new DataObject("QuickAction.Pin",id),DragDropEffects.Move); }
            else
            {
                DragMove(); var cursor=WindowsServices.Cursor; var screen=Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X,cursor.Y)); var rect=screen.WorkingArea;
                vm.Config.Monitor=screen.DeviceName; vm.Config.Edge=DockGeometry.Nearest(new(rect.X,rect.Y,rect.Width,rect.Height),cursor.X,cursor.Y);
                vm.Config.Position=Math.Clamp(Vertical?(cursor.Y-rect.Y)/(double)rect.Height:(cursor.X-rect.X)/(double)rect.Width,0,1);
                all=false; expanded=false; vm.Guard(vm.Save);
            }
        }
        finally { dragging=false; dragStart=null; dragId=null; Render(); ScheduleCollapse(); Dispatcher.BeginInvoke(DispatcherPriority.Background,()=>suppressShortcutClick=false); }
    }
    private Button DockButton(string text,Action action,string hint)
    {
        var b=Ui.Button(text,action,hint); b.Width=48; b.Height=48; b.Padding=new(4); b.Margin=new(2); b.FontSize=24; b.FontFamily=new FontFamily("Segoe Fluent Icons"); b.Background=Brushes.Transparent; return b;
    }
    private void Render()
    {
        if(!IsInitialized) return;
        Topmost=vm.Config.Topmost;
        surface.Background=SystemParameters.HighContrast?SystemColors.WindowBrush:new SolidColorBrush(Color.FromArgb(blur?(byte)(vm.Config.Opacity*255):(byte)255,248,250,255));
        if(!expanded) { surface.Child=null; Place(Vertical?10:100,Vertical?100:10); return; }
        var root=new DockPanel { Margin=new(5),LastChildFill=true };
        var strip=new Grid();
        for(int i=0;i<3;i++) { var size=i==1?new GridLength(1,GridUnitType.Star):GridLength.Auto; if(Vertical) strip.RowDefinitions.Add(new(){Height=size}); else strip.ColumnDefinitions.Add(new(){Width=size}); }
        strip.Children.Add(DockButton("\uE713",()=>OpenSettings(),"Settings"));
        var pins=new StackPanel { Orientation=Vertical?Orientation.Vertical:Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center };
        if(Vertical) Grid.SetRow(pins,1); else Grid.SetColumn(pins,1); strip.Children.Add(pins);
        foreach(var item in vm.Pinned)
        {
            var b=DockButton("",()=> { if(!dragging&&!suppressShortcutClick) _ = vm.Run(item); },item.Name+(item.Admin?" • Admin":"")); b.Content=Ui.Icon(item,vm); b.Tag=item.Id;
            b.Drop+=(_,e)=> { if(e.Data.GetData("QuickAction.Pin") is Guid source) { vm.Reorder(source,item.Id); e.Handled=true; } };
            pins.Children.Add(b);
        }
        var allButton=DockButton("\uE71D",()=> { all=!all; Render(); if(all) Activate(); },"Tất cả ứng dụng");
        if(Vertical) Grid.SetRow(allButton,2); else Grid.SetColumn(allButton,2); strip.Children.Add(allButton);
        DockPanel.SetDock(strip,vm.Config.Edge switch { Edge.Right=>Dock.Right,Edge.Bottom=>Dock.Bottom,Edge.Top=>Dock.Top,_=>Dock.Left }); root.Children.Add(strip);
        if(all)
        {
            var content=new DockPanel { Margin=new(12) };
            var header=new StackPanel(); header.Children.Add(Ui.Text("Ứng dụng của bạn",22));
            var searchBox=new TextBox { Text=search,ToolTip="Tìm theo tên, chương trình hoặc parameters" }; System.Windows.Automation.AutomationProperties.SetName(searchBox,"Tìm shortcut"); searchBox.TextChanged+=(_,_)=> { search=searchBox.Text; FillRows(); }; header.Children.Add(searchBox);
            DockPanel.SetDock(header,Dock.Top); content.Children.Add(header); rows=new StackPanel(); content.Children.Add(new ScrollViewer { Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }); root.Children.Add(content); FillRows();
        }
        surface.Child=root;
        var count=vm.Pinned.Count()+2;
        Place(all?650:Vertical?62:count*52+10,all?Math.Max(440,Vertical?count*52+10:440):Vertical?count*52+10:62);
        if(SystemParameters.ClientAreaAnimation) { root.BeginAnimation(OpacityProperty,new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(180))); }
    }
    private void FillRows()
    {
        if(rows==null) return; rows.Children.Clear();
        foreach(var item in vm.Items.Where(s=>(s.Name+" "+s.Path+" "+s.Arguments).Contains(search,StringComparison.CurrentCultureIgnoreCase)))
        {
            var grid=new Grid { Margin=new(0,5,0,8) }; grid.ColumnDefinitions.Add(new(){ Width=new(42) }); grid.ColumnDefinitions.Add(new(){ Width=new(1,GridUnitType.Star) }); grid.ColumnDefinitions.Add(new(){ Width=GridLength.Auto });
            var icon=Ui.Icon(item,vm); grid.Children.Add(icon);
            var detail=new StackPanel { Margin=new(6,0,8,0) }; detail.Children.Add(Ui.Text(item.Name,17)); detail.Children.Add(Ui.Text(item.Path,11,Brushes.DimGray)); detail.Children.Add(Ui.Text(item.Arguments,12,Brushes.DimGray)); Grid.SetColumn(detail,1); grid.Children.Add(detail);
            var actions=new StackPanel(); var admin=new CheckBox { Content="Admin",IsChecked=item.Admin,ToolTip="Quyền admin mặc định" }; admin.Click+=(_,_)=> { item.Admin=admin.IsChecked==true; vm.Guard(vm.Save); }; actions.Children.Add(admin);
            actions.Children.Add(Ui.Button("▶ Run",()=>_ = vm.Run(item),"Chạy "+item.Name)); actions.Children.Add(Ui.Button(item.PinOrder.HasValue?"Bỏ ghim":"Ghim",()=>vm.TogglePin(item))); Grid.SetColumn(actions,2); grid.Children.Add(actions);
            rows.Children.Add(new Border { Child=grid,BorderBrush=new SolidColorBrush(Color.FromRgb(225,231,242)),BorderThickness=new(0,0,0,1) });
        }
        if(rows.Children.Count==0) rows.Children.Add(Ui.Text("Chưa có shortcut phù hợp. Mở Settings hoặc kéo file vào dock để thêm.",14,Brushes.DimGray));
    }
    private void Place(double width,double height)
    {
        var hwnd=new WindowInteropHelper(this).Handle; if(hwnd==IntPtr.Zero) return;
        placing=true;
        try
        {
            var screen=WindowsServices.Screen(vm.Config); var scale=WindowsServices.Scale(screen); var work=screen.WorkingArea;
            width=Math.Min(width,work.Width/scale); height=Math.Min(height,work.Height/scale); Width=width; Height=height;
            var p=DockGeometry.Place(new(work.X,work.Y,work.Width,work.Height),vm.Config.Edge,vm.Config.Position,width*scale,height*scale);
            WindowsServices.SetWindowPos(hwnd,IntPtr.Zero,(int)Math.Round(p.X),(int)Math.Round(p.Y),(int)Math.Round(width*scale),(int)Math.Round(height*scale),0x0014);
            WindowsServices.Round(this,Math.Min(28,Math.Min(width,height)/2));
            QueueShapeUpdate();
        }
        finally { placing=false; }
    }
}
