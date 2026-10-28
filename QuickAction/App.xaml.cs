using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace QuickAction;
public partial class App : Application
{
    private Mutex? mutex;
    private readonly CancellationTokenSource shutdown = new();
    private const string Pipe = "QuickAction-Desktop-v1";
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        void Trace(string text) { if(e.Args.Length==2 && e.Args[0]=="--diagnostics") File.AppendAllText(e.Args[1],DateTimeOffset.Now+" "+text+Environment.NewLine); }
        Trace("Startup");
        mutex=new Mutex(true,"Local\\QuickAction-Desktop-v1",out var first);
        Trace("First instance: "+first);
        if(!first)
        {
            try { using var client=new NamedPipeClientStream(".",Pipe,PipeDirection.Out); client.Connect(1500); client.WriteByte(1); } catch(IOException) { } catch(TimeoutException) { }
            Shutdown(); return;
        }
        try
        {
            var vm=new MainViewModel(); Trace("ViewModel ready"); var dock=new DockWindow(vm); MainWindow=dock; dock.Show(); Trace("Dock visible: "+dock.IsVisible);
            if(vm.Items.Count==0) dock.OpenSettings();
            if(e.Args.Length==2 && e.Args[0]=="--diagnostics") { dock.ShowInTaskbar=true; dock.OpenAll(); dock.Activate(); }
            _=Task.Run(async()=>
            {
                while(!shutdown.IsCancellationRequested)
                {
                    try { using var server=new NamedPipeServerStream(Pipe,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly); await server.WaitForConnectionAsync(shutdown.Token); await Dispatcher.InvokeAsync(()=> { dock.Show(); dock.OpenAll(); dock.Activate(); }); }
                    catch(OperationCanceledException) { break; } catch(IOException) { }
                }
            });
        }
        catch(Exception ex) { Trace(ex.ToString()); MessageBox.Show("Không thể khởi động QuickAction: "+ex.Message,"QuickAction"); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { shutdown.Cancel(); mutex?.Dispose(); base.OnExit(e); }
}
