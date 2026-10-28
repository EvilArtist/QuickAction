using QuickAction.Core;
using System.IO.Compression;
using System.Diagnostics;
using System.Text.Json;

if(args.Length>=2 && args[0]=="--capture") { File.WriteAllText(args[1],JsonSerializer.Serialize(args.Skip(2))); return; }

var passed=0;
void Check(bool condition,string name) { if(!condition) throw new Exception("FAIL: "+name); Console.WriteLine("PASS: "+name); passed++; }
void Throws(Action action,string name) { try { action(); } catch { Check(true,name); return; } throw new Exception("FAIL: "+name); }
var root=Path.Combine(Path.GetTempPath(),"QuickAction-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
try
{
    var file=Path.Combine(root,"Ứng dụng có dấu.exe"); var item=new Shortcut { Name="Demo",Path=file,Arguments="--name \"Nguyễn Văn A\"",Admin=true };
    var exe=LaunchBuilder.Build(item,root); Check(exe.FileName==file && exe.Arguments==item.Arguments && exe.WorkingDirectory==root && exe.Admin,"EXE: Unicode, spaces, arguments, admin, default working directory");
    Check(LaunchBuilder.StartInfo(exe).Verb=="runas" && LaunchBuilder.StartInfo(exe).UseShellExecute,"Elevation uses runas");
    item.Admin=false; item.Path=Path.ChangeExtension(file,"bat"); var bat=LaunchBuilder.Build(item,root); Check(bat.Arguments=="/d /s /c \"\""+item.Path+"\" "+item.Arguments+"\"","BAT quoting");
    item.Path=Path.ChangeExtension(file,"cmd"); Check(LaunchBuilder.Build(item,root).FileName==Path.Combine(root,"cmd.exe"),"CMD dispatch");
    item.Path=Path.ChangeExtension(file,"ps1"); var ps=LaunchBuilder.Build(item,root); Check(ps.Arguments.StartsWith("-NoProfile -File \"") && !ps.Arguments.Contains("Bypass"),"PS1 preserves execution policy");
    item.Path=Path.ChangeExtension(file,"msi"); Check(LaunchBuilder.Build(item,root).Arguments.StartsWith("/i \""),"MSI install dispatch");
    item.WorkingDirectory=root; Check(LaunchBuilder.Build(item,root).WorkingDirectory==root,"Explicit working directory");
    item.Path="relative.exe"; Throws(()=>LaunchBuilder.Build(item,root),"Reject relative executable");
    item.Path=Path.Combine(root,"test.txt"); Throws(()=>LaunchBuilder.Build(item,root),"Reject unsupported extension");
    var config=new Configuration(); for(int i=0;i<6;i++) config.Shortcuts.Add(new(){Name="Same application "+i,Path=file});
    foreach(var s in config.Shortcuts.Take(5)) Pins.Toggle(config,s);
    Throws(()=>Pins.Toggle(config,config.Shortcuts[5]),"Sixth pin rejected");
    Pins.Move(config,config.Shortcuts[0].Id,config.Shortcuts[4].Id); Check(config.Shortcuts[0].PinOrder==4 && config.Shortcuts[4].PinOrder==3,"Reorder pinned apps");
    Pins.Toggle(config,config.Shortcuts[0]); Pins.Toggle(config,config.Shortcuts[5]); Check(config.Shortcuts.Count(s=>s.PinOrder.HasValue)==5,"Unpin then pin");
    var store=new ConfigurationStore(Path.Combine(root,"store")); store.Save(config); config.Position=.25; store.Save(config);
    Check(store.Load().Position==.25 && File.Exists(store.FilePath+".bak"),"Atomic save and backup");
    File.WriteAllText(store.FilePath,"{broken"); var recovered=store.Load(); Check(recovered.Position==.5 && store.RecoveryMessage!=null,"Recover corrupted JSON");
    var icon=Path.Combine(root,"sample.png"); File.WriteAllBytes(icon,[137,80,78,71]); config.Shortcuts[0].Icon=store.ImportIcon(icon);
    var archive=Path.Combine(root,"backup.zip"); store.Export(config,archive); var imported=store.Import(archive); Check(imported.Shortcuts.Count==6 && File.Exists(Path.Combine(store.Icons,imported.Shortcuts[0].Icon)),"Backup restores shortcuts and icons");
    Check(imported.Shortcuts[0].Icon!=config.Shortcuts[0].Icon,"Import keeps old assets intact");
    var hostile=Path.Combine(root,"hostile.zip"); using(var zip=ZipFile.Open(hostile,ZipArchiveMode.Create)) { zip.CreateEntry("../outside.txt"); }
    Throws(()=>store.Import(hostile),"Reject ZIP traversal");
    var before=File.ReadAllText(store.FilePath); var invalid=Path.Combine(root,"invalid.zip"); using(var zip=ZipFile.Open(invalid,ZipArchiveMode.Create)) { using var w=new StreamWriter(zip.CreateEntry("config.json").Open()); w.Write("{\"Version\":999}"); }
    Throws(()=>store.Import(invalid),"Reject future config version"); Check(before==File.ReadAllText(store.FilePath),"Invalid import preserves current configuration");
    var a=new Area(-1920,-200,1920,1080); Check(DockGeometry.Nearest(a,-1919,300)==Edge.Left,"Negative monitor coordinates");
    foreach(var edge in Enum.GetValues<Edge>()) { var p=DockGeometry.Place(a,edge,.5,650,440); Check(p.X>=a.X && p.Y>=a.Y && p.X+650<=a.X+a.Width && p.Y+440<=a.Y+a.Height,"Dock inside work area: "+edge); }
    var physical=DockGeometry.Place(new(0,0,3840,2160),Edge.Right,.5,650*1.5,440*1.5); Check(physical.X==2865 && physical.Y==750,"Placement with 150% pixel dimensions");
    var tiny=DockGeometry.Place(new(0,0,100,100),Edge.Bottom,.5,200,200); Check(tiny==(0d,0d),"Oversized panel anchor clamped");
    config.Opacity=double.NaN; Throws(config.Validate,"Reject nonfinite preferences");
    if(OperatingSystem.IsWindows())
    {
        var captured=Path.Combine(root,"captured arguments.json");
        var sample=new Shortcut { Path=Environment.ProcessPath!,Arguments=$"--capture \"{captured}\" \"Nguyễn Văn A\" \"two words\"" };
        using(var p=Process.Start(LaunchBuilder.StartInfo(LaunchBuilder.Build(sample,Environment.SystemDirectory)))) { Check(p!=null && p.WaitForExit(10000) && p.ExitCode==0,"Launch actual EXE"); }
        var received=JsonSerializer.Deserialize<string[]>(File.ReadAllText(captured)); Check(received!.SequenceEqual(new[]{"Nguyễn Văn A","two words"}),"Actual EXE receives Unicode and quoted arguments");
        var script=Path.Combine(root,"sample script.cmd"); var cmdOutput=Path.Combine(root,"cmd result.txt"); File.WriteAllText(script,"@echo off\r\necho %~2>\"%~1\"\r\n");
        var cmdItem=new Shortcut { Path=script,Arguments=$"\"{cmdOutput}\" \"two words\"" };
        using(var p=Process.Start(LaunchBuilder.StartInfo(LaunchBuilder.Build(cmdItem,Environment.SystemDirectory)))) { Check(p!=null && p.WaitForExit(10000) && p.ExitCode==0,"Launch actual CMD file with spaces"); }
        Check(File.ReadAllText(cmdOutput).Trim()=="two words","Actual CMD receives quoted arguments");
    }
    Console.WriteLine($"\n{passed} tests passed.");
}
finally { Directory.Delete(root,true); }
