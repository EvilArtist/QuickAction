using QuickAction.Core;
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace QuickAction;
internal static class Ui
{
    internal const string ShortcutDragFormat="QuickAction.Shortcut";
    public static Button Button(string text,Action action,string? hint=null)
    {
        var b=new Button { Content=text,ToolTip=hint??text }; AutomationProperties.SetName(b,hint??text); b.Command=new RelayCommand(_=>action()); return b;
    }
    public static Border AppRow(Shortcut item,MainViewModel vm,Action? openDetails=null)
    {
            var grid=new Grid { Margin=new(14,16,14,16) }; grid.ColumnDefinitions.Add(new(){ Width=new(52) }); grid.ColumnDefinitions.Add(new(){ Width=new(1,GridUnitType.Star) }); grid.ColumnDefinitions.Add(new(){ Width=GridLength.Auto });
            var icon=Ui.Icon(item,vm); grid.Children.Add(icon);
            var detail=new StackPanel { Margin=new(6,0,8,0) }; detail.Children.Add(Ui.Text(item.Name,17));
            var path=Ui.Text(item.IsGroup?$"Nhóm app · {item.Steps!.Count} bước":item.Path,13,Brushes.SlateGray); path.TextWrapping=TextWrapping.NoWrap;
            path.TextTrimming=TextTrimming.CharacterEllipsis; path.ToolTip=item.Path; detail.Children.Add(path); Grid.SetColumn(detail,1); grid.Children.Add(detail);
            if(openDetails!=null)
            {
                grid.Children.Remove(icon); grid.Children.Remove(detail);
                var content=new Grid { Background=Brushes.Transparent }; content.ColumnDefinitions.Add(new(){Width=new(52)}); content.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
                content.Children.Add(icon); content.Children.Add(detail);
                var open=Button(item.Name,openDetails); open.Content=content;
                open.Height=double.NaN; open.Padding=new(0); open.HorizontalContentAlignment=HorizontalAlignment.Stretch;
                var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
                open.Template=new ControlTemplate(typeof(Button)) { VisualTree=presenter };
                open.Background=Brushes.Transparent; Grid.SetColumnSpan(open,2); grid.Children.Add(open);
            }
            var actions=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center }; var admin=Ui.Button("♢ Admin",()=> { item.Admin=!item.Admin; vm.Guard(vm.Save); },L10n.T("Quyền admin mặc định")); admin.Foreground=item.Admin?Brushes.RoyalBlue:Brushes.SlateGray; admin.Background=item.Admin?Brushes.Lavender:Brushes.Transparent; actions.Children.Add(admin);
            var run=Ui.Button(L10n.T("▶ Run"),()=>_ = vm.Run(item),L10n.T("Chạy ")+item.Name); run.Background=Brushes.RoyalBlue; run.Foreground=Brushes.White; run.Margin=new(12,0,12,0); actions.Children.Add(run); Grid.SetColumn(actions,2); grid.Children.Add(actions);
            if(item.IsGroup) admin.Visibility=Visibility.Collapsed;
            var layout=new Grid(); layout.ColumnDefinitions.Add(new(){Width=GridLength.Auto}); layout.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
            var handle=Button("☰",()=>{},L10n.T("Kéo để đổi thứ tự")); handle.Tag=item.Id;
            handle.Width=28; handle.Margin=new(8,0,0,0); handle.Padding=new(0); handle.VerticalAlignment=VerticalAlignment.Center;
            handle.Background=Brushes.Transparent; handle.BorderThickness=new(0); handle.Foreground=Brushes.SlateGray; handle.Cursor=Cursors.SizeAll;
            layout.Children.Add(handle); Grid.SetColumn(grid,1); layout.Children.Add(grid);
            Point? start=null;
            handle.PreviewMouseLeftButtonDown+=(_,e)=>start=e.GetPosition(handle);
            handle.PreviewMouseLeftButtonUp+=(_,_)=>start=null;
            handle.PreviewMouseMove+=(_,e)=>
            {
                // DockWindow already coordinates dragging with its collapse/resize timers.
                if(Window.GetWindow(handle) is DockWindow || start==null || e.LeftButton!=MouseButtonState.Pressed) return;
                var delta=e.GetPosition(handle)-start.Value;
                if(Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance) return;
                start=null; handle.ReleaseMouseCapture(); e.Handled=true;
                DragDrop.DoDragDrop(handle,new DataObject(ShortcutDragFormat,item.Id),DragDropEffects.Move);
            };
            var row=new Border { Child=layout,BorderBrush=new SolidColorBrush(Color.FromRgb(225,231,242)),CornerRadius=new(14),Margin=new(0,0,8,8),BorderThickness=new(1),AllowDrop=true };
            var normalBorder=row.BorderBrush;
            bool CanMove(DragEventArgs e)=>e.Data.GetData(ShortcutDragFormat) is Guid source && source!=item.Id;
            row.DragOver+=(_,e)=>
            {
                if(!e.Data.GetDataPresent(ShortcutDragFormat)) return;
                e.Effects=CanMove(e)?DragDropEffects.Move:DragDropEffects.None;
                row.BorderBrush=CanMove(e)?Brushes.RoyalBlue:normalBorder;
                row.BorderThickness=CanMove(e)?(e.GetPosition(row).Y<row.ActualHeight/2?new Thickness(1,3,1,1):new Thickness(1,1,1,3)):new Thickness(1);
                e.Handled=true;
            };
            row.DragLeave+=(_,_)=> { row.BorderBrush=normalBorder; row.BorderThickness=new(1); };
            row.Drop+=(_,e)=>
            {
                row.BorderBrush=normalBorder; row.BorderThickness=new(1);
                if(e.Data.GetData(ShortcutDragFormat) is not Guid source) return;
                vm.Reorder(source,item.Id,e.GetPosition(row).Y>=row.ActualHeight/2); e.Handled=true;
            };
            return row;
    }
    public static FrameworkElement Icon(Shortcut s,MainViewModel vm)
    {
        var image=WindowsServices.Icon(s,vm.Store);
        if(image!=null) return new Image { Source=image,Width=32,Height=32 };
        var glyph=s.Icon switch { "builtin:group"=>"\uE81E", "builtin:terminal"=>"\uE756", "builtin:folder"=>"\uE8B7", "builtin:star"=>"\uE735", "builtin:tools"=>"\uE713", _=>"\uE71D" };
        // Dock buttons use an icon font; set our own font so every host renders the same glyph.
        return new Border { Width=32,Height=32,CornerRadius=new(9),Background=new SolidColorBrush(Color.FromRgb(36,107,253)),Child=new TextBlock { Text=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),Foreground=Brushes.White,FontSize=20,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center } };
    }
    public static TextBlock Text(string text,double size=14,Brush? color=null)=>new() { Text=text,FontSize=size,Foreground=color??Brushes.Black,TextWrapping=TextWrapping.Wrap,Margin=new(0,3,0,3) };
    public static void Label(Panel panel,string text)=>panel.Children.Add(Text(text,12,Brushes.DimGray));
}
