using QuickAction.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace QuickAction;
public sealed class RelayCommand(Action<object?> execute) : ICommand
{
    public bool CanExecute(object? parameter)=>true;
    public void Execute(object? parameter)=>execute(parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
public sealed class MainViewModel : Observable
{
    public ConfigurationStore Store { get; }
    public Configuration Config { get; private set; }
    public ObservableCollection<Shortcut> Items { get; }=[];
    public IEnumerable<Shortcut> DockItems => Items.Take(5);
    public event Action? Updated;
    public event Action<string>? Notification;
    private readonly HashSet<Guid> running=[];
    public MainViewModel(string? dataDirectory=null) { Store=new(dataDirectory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QuickAction")); Config=Store.Load(); Reload(); }
    private void ApplyLanguage()
    {
        L10n.Language=Config.Language;
        if(Application.Current is not { } app) return;
        int index=0;
        foreach(var entry in L10n.English) app.Resources["Loc."+index++]=L10n.T(entry.Key);
    }
    public void Reload() { ApplyLanguage(); Items.Clear(); foreach(var s in Config.Shortcuts) Items.Add(s); Changed(nameof(DockItems)); Updated?.Invoke(); }
    public void Save() { Config.Shortcuts=Items.ToList(); Store.Save(Config); ApplyLanguage(); Changed(nameof(DockItems)); Updated?.Invoke(); }
    public void Notify(string message)=>Notification?.Invoke(message);
    public void Guard(Action action)
    {
        try { action(); } catch(Exception e) { Log(e); MessageBox.Show(e.Message,"QuickAction",MessageBoxButton.OK,MessageBoxImage.Warning); }
    }
    public void Reorder(Guid source,Guid target,bool? after=null)=>Guard(()=>
    {
        var item=Items.FirstOrDefault(s=>s.Id==source);
        var destination=Items.FirstOrDefault(s=>s.Id==target);
        if(item==null || destination==null || item==destination) return;
        int from=Items.IndexOf(item),to=Items.IndexOf(destination);
        if(after.HasValue) { if(after.Value) to++; if(from<to) to--; }
        if(from==to) return;
        Items.Move(from,to); Save();
    });
    public void ReplaceFrom(string path) { Config=Store.Import(path); Reload(); }
    public async Task Run(Shortcut item)
    {
        if(!running.Add(item.Id)) return;
        try
        {
            if(item.IsGroup)
            {
                await GroupLauncher.RunAsync(item.Copy().Steps!,async app=>
                {
                    if(!LaunchBuilder.IsInstalledApp(app.Path) && !File.Exists(Environment.ExpandEnvironmentVariables(app.Path))) throw new FileNotFoundException("Không tìm thấy chương trình: "+app.Name);
                    var launch=LaunchBuilder.Build(app,Environment.SystemDirectory);
                    if(!Directory.Exists(launch.WorkingDirectory)) throw new DirectoryNotFoundException(launch.WorkingDirectory);
                    var child=await Task.Run(()=>Process.Start(LaunchBuilder.StartInfo(launch)));
                    Notify(L10n.T("Đã gửi yêu cầu chạy: ")+app.Name);
                    return child==null?null:new GroupProcess(child);
                });
                return;
            }
            if(!LaunchBuilder.IsInstalledApp(item.Path) && !File.Exists(Environment.ExpandEnvironmentVariables(item.Path))) throw new FileNotFoundException(L10n.T("Không tìm thấy chương trình. Hãy sửa đường dẫn trong Settings."));
            var request=LaunchBuilder.Build(item,Environment.SystemDirectory);
            if(!Directory.Exists(request.WorkingDirectory)) throw new DirectoryNotFoundException(L10n.T("Thư mục làm việc không tồn tại."));
            var process=await Task.Run(()=>Process.Start(LaunchBuilder.StartInfo(request)));
            Notify(L10n.T("Đã gửi yêu cầu chạy: ")+item.Name);
            if(process!=null)
            {
                if(Path.GetExtension(item.Path).Equals(".ps1",StringComparison.OrdinalIgnoreCase)) _=ObserveScript(process,item.Name);
                else process.Dispose();
            }
            await Task.Delay(700);
        }
        catch(Win32Exception e) when(e.NativeErrorCode==1223) { Notify(L10n.T("Đã hủy yêu cầu quyền admin.")); }
        catch(Exception e) { Log(e); Notify(L10n.T("Không thể chạy: ")+e.Message); }
        finally { running.Remove(item.Id); }
    }
    private async Task ObserveScript(Process process,string name)
    {
        try { await process.WaitForExitAsync(); if(process.ExitCode!=0) Notify(string.Format(L10n.T("Script {0} kết thúc với mã {1}. Kiểm tra nội dung script và ExecutionPolicy trong PowerShell; QuickAction không thay đổi chính sách này."),name,process.ExitCode)); }
        catch(Exception e) { Log(e); } finally { process.Dispose(); }
    }
    private void Log(Exception exception)
    {
        try { var path=Path.Combine(Store.Root,"errors.log"); if(File.Exists(path)&&new FileInfo(path).Length>1024*1024) File.Move(path,path+".old",true); File.AppendAllText(path,$"{DateTimeOffset.Now:O} {exception.GetType().Name} HRESULT={exception.HResult}\n"); } catch(IOException) { }
    }
}
