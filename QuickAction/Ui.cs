using QuickAction.Core;
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickAction;
internal static class Ui
{
    public static Button Button(string text,Action action,string? hint=null)
    {
        var b=new Button { Content=text,ToolTip=hint??text }; AutomationProperties.SetName(b,hint??text); b.Command=new RelayCommand(_=>action()); return b;
    }
    public static Border AppRow(Shortcut item,MainViewModel vm,Action? openDetails=null)
    {
            var grid=new Grid { Margin=new(14,16,14,16) }; grid.ColumnDefinitions.Add(new(){ Width=new(52) }); grid.ColumnDefinitions.Add(new(){ Width=new(1,GridUnitType.Star) }); grid.ColumnDefinitions.Add(new(){ Width=GridLength.Auto });
            var icon=Ui.Icon(item,vm); grid.Children.Add(icon);
            var detail=new StackPanel { Margin=new(6,0,8,0) }; detail.Children.Add(Ui.Text(item.Name,17));
            var path=Ui.Text(item.Path,13,Brushes.SlateGray); path.TextWrapping=TextWrapping.NoWrap;
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
            var run=Ui.Button(L10n.T("▶ Run"),()=>_ = vm.Run(item),L10n.T("Chạy ")+item.Name); run.Background=Brushes.RoyalBlue; run.Foreground=Brushes.White; run.Margin=new(12,0,12,0); actions.Children.Add(run); var pin=Ui.Button(item.PinOrder.HasValue?"\uE77A":"\uE718",()=>vm.TogglePin(item),item.PinOrder.HasValue?L10n.T("Bỏ ghim"):L10n.T("Ghim")); pin.FontFamily=new FontFamily("Segoe Fluent Icons"); pin.Background=Brushes.Transparent; pin.BorderThickness=new(0); pin.Padding=new(8,0,8,0); actions.Children.Add(pin); Grid.SetColumn(actions,2); grid.Children.Add(actions);
            return new Border { Child=grid,BorderBrush=new SolidColorBrush(Color.FromRgb(225,231,242)),CornerRadius=new(14),Margin=new(0,0,8,8),BorderThickness=new(1) };
    }
    public static FrameworkElement Icon(Shortcut s,MainViewModel vm)
    {
        var image=WindowsServices.Icon(s,vm.Store);
        if(image!=null) return new Image { Source=image,Width=32,Height=32 };
        var glyph=s.Icon switch { "builtin:terminal"=>"\uE756", "builtin:folder"=>"\uE8B7", "builtin:star"=>"\uE735", "builtin:tools"=>"\uE713", _=>"\uE71D" };
        // Dock buttons use an icon font; set our own font so every host renders the same glyph.
        return new Border { Width=32,Height=32,CornerRadius=new(9),Background=new SolidColorBrush(Color.FromRgb(36,107,253)),Child=new TextBlock { Text=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),Foreground=Brushes.White,FontSize=20,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center } };
    }
    public static TextBlock Text(string text,double size=14,Brush? color=null)=>new() { Text=text,FontSize=size,Foreground=color??Brushes.Black,TextWrapping=TextWrapping.Wrap,Margin=new(0,3,0,3) };
    public static void Label(Panel panel,string text)=>panel.Children.Add(Text(text,12,Brushes.DimGray));
}
