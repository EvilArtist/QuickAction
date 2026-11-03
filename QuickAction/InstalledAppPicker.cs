using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;
using System.Windows.Media;
using System.Windows.Markup;
using QuickAction.Core;

namespace QuickAction;

internal sealed record InstalledApp(string Name,string Id,string Target,string Arguments,string WorkingDirectory,ImageSource? Icon);

internal static class InstalledApps
{
    public static Task<InstalledApp[]> LoadAsync()
    {
        var completion=new TaskCompletionSource<InstalledApp[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            object? shell=null,folder=null,items=null;
            try
            {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application",true)!);
                folder=((dynamic)shell!).NameSpace("shell:AppsFolder");
                if(folder==null) throw new InvalidOperationException(L10n.T("Windows không cung cấp danh sách ứng dụng."));
                items=((dynamic)folder).Items();
                var result=new List<InstalledApp>();
                int count=((dynamic)items).Count;
                for(var i=0;i<count;i++)
                {
                    object? item=null;
                    try
                    {
                        item=((dynamic)items).Item(i);
                        string name=((dynamic)item!).Name;
                        string id=((dynamic)item).Path;
                        if(!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id) && id.IndexOfAny(new[]{'"','\r','\n'})<0)
                        {
                            string target=Property(item,"System.Link.TargetParsingPath");
                            string args=Property(item,"System.Link.Arguments");
                            string work=Property(item,"System.Link.WorkingDirectory");
                            if(string.IsNullOrEmpty(target) && Path.IsPathFullyQualified(id)) target=id;
                            target=Environment.ExpandEnvironmentVariables(target);
                            if(!Path.IsPathFullyQualified(target) || !LaunchBuilder.Supported(target) || !File.Exists(target))
                            {
                                target=LaunchBuilder.AppPrefix+id; args=""; work="";
                            }
                            result.Add(new(name,id,target,args,work,ShellAppIcon.Load(LaunchBuilder.AppPrefix+id)));
                        }
                    }
                    catch(COMException) { /* One unavailable app must not hide the rest. */ }
                    finally { Release(item); }
                }
                completion.SetResult(result.DistinctBy(a=>a.Id,StringComparer.OrdinalIgnoreCase)
                    .OrderBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).ToArray());
            }
            catch(Exception error) { completion.SetException(error); }
            finally { Release(items); Release(folder); Release(shell); }
        }) { IsBackground=true,Name="Installed app discovery" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static void Release(object? value)
    {
        if(value!=null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }
    private static string Property(object item,string key)
    {
        try { return ((dynamic)item).ExtendedProperty(key) as string ?? ""; }
        catch(COMException) { return ""; }
    }
}

internal sealed class InstalledAppPicker : Window
{
    private InstalledApp[] apps=Array.Empty<InstalledApp>();
    private readonly TextBox search=new() { Margin=new(0,8,0,12),Tag=L10n.T("Tìm ứng dụng…") };
    private readonly ListBox list=new();
    private readonly TextBlock status=new() { Text=L10n.T("Đang tải danh sách ứng dụng…"),Margin=new(0,10,0,10),TextWrapping=TextWrapping.Wrap };
    private readonly Button choose=new() { Content=L10n.T("Chọn ứng dụng"),IsDefault=true,IsEnabled=false,MinWidth=130 };
    public InstalledApp? SelectedApp { get; private set; }

    public InstalledAppPicker()
    {
        Title=L10n.T("Chọn ứng dụng đã cài"); Width=720; Height=660; MinWidth=460; MinHeight=420;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("/QuickAction;component/Themes/Colors.xaml",UriKind.Relative) });
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("/QuickAction;component/Themes/Controls.xaml",UriKind.Relative) });
        Style=(Style)FindResource("Window.Base");
        var panel=new DockPanel { Margin=new(20),Background=(System.Windows.Media.Brush)FindResource("Brush.Bg") }; Content=panel;
        var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); panel.Children.Add(header);
        header.Children.Add(new TextBlock { Text=L10n.T("Tìm ứng dụng đã cài"),FontSize=20,FontWeight=FontWeights.SemiBold });
        header.Children.Add(search);
        var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); panel.Children.Add(footer);
        footer.Children.Add(status);
        footer.Children.Add(new TextBlock { Text=L10n.T("Chọn ứng dụng để điền đường dẫn và icon. Không thấy ứng dụng? Dùng Chọn file trong Settings."),TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,12) });
        var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
        var cancel=new Button { Content=L10n.T("Hủy"),IsCancel=true,Margin=new(0,0,8,0) };
        buttons.Children.Add(cancel); buttons.Children.Add(choose); footer.Children.Add(buttons);
        panel.Children.Add(list);
        ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(list,ScrollBarVisibility.Auto);
        list.ItemsPanel=(ItemsPanelTemplate)XamlReader.Parse("""
            <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><WrapPanel/></ItemsPanelTemplate>
            """);
        list.ItemTemplate=(DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Width="126" Height="108" Margin="4" ToolTip="{Binding Target}">
                <Grid Width="48" Height="48" Margin="0,6,0,8">
                  <TextBlock Text="▦" FontSize="36" HorizontalAlignment="Center" Foreground="#2563EB">
                    <TextBlock.Style><Style TargetType="TextBlock"><Setter Property="Visibility" Value="Collapsed"/>
                      <Style.Triggers><DataTrigger Binding="{Binding Icon}" Value="{x:Null}" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers>
                    </Style></TextBlock.Style>
                  </TextBlock>
                  <Image Source="{Binding Icon}" Stretch="Uniform"/>
                </Grid>
                <TextBlock Text="{Binding Name}" TextAlignment="Center" TextWrapping="Wrap" TextTrimming="CharacterEllipsis" MaxHeight="38"/>
              </StackPanel>
            </DataTemplate>
            """);
        AutomationProperties.SetName(search,L10n.T("Tìm ứng dụng đã cài"));
        AutomationProperties.SetName(list,L10n.T("Danh sách ứng dụng đã cài"));
        search.TextChanged+=(_,_)=>Filter();
        list.SelectionChanged+=(_,_)=>choose.IsEnabled=list.SelectedItem is InstalledApp;
        choose.Click+=(_,_)=>Accept();
        list.MouseDoubleClick+=(_,e)=> { if(ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) is ListBoxItem) Accept(); };
        Loaded+=async (_,_)=>
        {
            search.Focus();
            try { apps=await InstalledApps.LoadAsync(); if(IsVisible) Filter(); }
            catch(Exception) { if(IsVisible) status.Text=L10n.T("Không thể tải danh sách ứng dụng. Hãy đóng và thử lại hoặc dùng Chọn file."); }
        };
    }
    private void Filter()
    {
        var filtered=apps.Where(a=>a.Name.Contains(search.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)).ToArray();
        list.ItemsSource=filtered;
        status.Text=filtered.Length==0 ? L10n.T("Không tìm thấy ứng dụng.") : string.Format(L10n.T("{0} ứng dụng"),filtered.Length);
    }
    private void Accept()
    {
        if(list.SelectedItem is not InstalledApp selected) return;
        SelectedApp=selected; DialogResult=true;
    }
}
