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
    public ConfigurationStore Store { get; } = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QuickAction"));
    public Configuration Config { get; private set; }
    public ObservableCollection<Shortcut> Items { get; }=[];
    public IEnumerable<Shortcut> Pinned => Items.Where(s=>s.PinOrder.HasValue).OrderBy(s=>s.PinOrder);
    public event Action? Updated;
    public event Action<string>? Notification;
    private readonly HashSet<Guid> running=[];
    public MainViewModel() { Config=Store.Load(); Reload(); }
    public void Reload() { Items.Clear(); foreach(var s in Config.Shortcuts) Items.Add(s); Changed(nameof(Pinned)); Updated?.Invoke(); }
    public void Save() { Config.Shortcuts=Items.ToList(); Store.Save(Config); Changed(nameof(Pinned)); Updated?.Invoke(); }
    public void Notify(string message)=>Notification?.Invoke(message);
    public void Guard(Action action)
    {
        try { action(); } catch(Exception e) { Log(e); MessageBox.Show(e.Message,"QuickAction",MessageBoxButton.OK,MessageBoxImage.Warning); }
    }
    public void TogglePin(Shortcut s)=>Guard(()=> { Pins.Toggle(Config,s); Save(); });
    public void Reorder(Guid source,Guid target)=>Guard(()=> { Pins.Move(Config,source,target); Save(); });
    public void ReplaceFrom(string path) { Config=Store.Import(path); Reload(); }
    public async Task Run(Shortcut item)
    {
        if(!running.Add(item.Id)) return;
        try
        {
            if(!File.Exists(Environment.ExpandEnvironmentVariables(item.Path))) throw new FileNotFoundException("Không tìm thấy chương trình. Hãy sửa đường dẫn trong Settings.");
            var request=LaunchBuilder.Build(item,Environment.SystemDirectory);
            if(!Directory.Exists(request.WorkingDirectory)) throw new DirectoryNotFoundException("Thư mục làm việc không tồn tại.");
            var process=await Task.Run(()=>Process.Start(LaunchBuilder.StartInfo(request)));
            Notify("Đã gửi yêu cầu chạy: "+item.Name);
            if(process!=null)
            {
                if(Path.GetExtension(item.Path).Equals(".ps1",StringComparison.OrdinalIgnoreCase)) _=ObserveScript(process,item.Name);
                else process.Dispose();
            }
            await Task.Delay(700);
        }
        catch(Win32Exception e) when(e.NativeErrorCode==1223) { Notify("Đã hủy yêu cầu quyền admin."); }
        catch(Exception e) { Log(e); Notify("Không thể chạy: "+e.Message); }
        finally { running.Remove(item.Id); }
    }
    private async Task ObserveScript(Process process,string name)
    {
        try { await process.WaitForExitAsync(); if(process.ExitCode!=0) Notify($"Script {name} kết thúc với mã {process.ExitCode}. Kiểm tra nội dung script và ExecutionPolicy trong PowerShell; QuickAction không thay đổi chính sách này."); }
        catch(Exception e) { Log(e); } finally { process.Dispose(); }
    }
    private void Log(Exception exception)
    {
        try { var path=Path.Combine(Store.Root,"errors.log"); if(File.Exists(path)&&new FileInfo(path).Length>1024*1024) File.Move(path,path+".old",true); File.AppendAllText(path,$"{DateTimeOffset.Now:O} {exception.GetType().Name} HRESULT={exception.HResult}\n"); } catch(IOException) { }
    }
}
