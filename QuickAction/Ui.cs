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
    public static FrameworkElement Icon(Shortcut s,MainViewModel vm)
    {
        var image=WindowsServices.Icon(s,vm.Store);
        if(image!=null) return new Image { Source=image,Width=32,Height=32 };
        var glyph=s.Icon switch { "builtin:terminal"=>">_", "builtin:folder"=>"▰", "builtin:star"=>"★", "builtin:tools"=>"⚙", _=>"▦" };
        return new Border { Width=32,Height=32,CornerRadius=new(9),Background=new SolidColorBrush(Color.FromRgb(36,107,253)),Child=new TextBlock { Text=glyph,Foreground=Brushes.White,FontSize=23,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center } };
    }
    public static TextBlock Text(string text,double size=14,Brush? color=null)=>new() { Text=text,FontSize=size,Foreground=color??Brushes.Black,TextWrapping=TextWrapping.Wrap,Margin=new(0,3,0,3) };
    public static void Label(Panel panel,string text)=>panel.Children.Add(Text(text,12,Brushes.DimGray));
}
