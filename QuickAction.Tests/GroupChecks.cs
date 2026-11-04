using QuickAction.Core;

internal static class GroupChecks
{
    public static async Task Run(Action<bool,string> check,Action<Action,string> throws,string root)
    {
        var first=new GroupStep { App=new Shortcut { Name="First",Path=Path.Combine(root,"first.exe") } };
        var together=new GroupStep { App=new Shortcut { Name="Together",Path=first.App.Path },DependsOn=first.Id,Condition=GroupCondition.Together };
        var idle=new GroupStep { App=new Shortcut { Name="Idle",Path=first.App.Path },DependsOn=first.Id,Condition=GroupCondition.InputIdle };
        var exit=new GroupStep { App=new Shortcut { Name="Exit",Path=first.App.Path },DependsOn=first.Id,Condition=GroupCondition.Finished };
        var steps=new List<GroupStep> { first,together,idle,exit };
        var group=new Shortcut { Name="Group",Icon="builtin:group",Steps=steps };
        var copy=group.Copy(); copy.Steps![0].App.Name="Changed";
        check(first.App.Name=="First","Group copy isolates nested app edits");
        var store=new ConfigurationStore(Path.Combine(root,"groups"));
        var iconFile=Path.Combine(root,"group-icon.png"); File.WriteAllBytes(iconFile,[137,80,78,71]); first.App.Icon=store.ImportIcon(iconFile);
        var config=new Configuration { Shortcuts=[group] }; store.Save(config);
        check(store.Load().Shortcuts[0].Steps![3].DependsOn==first.Id,"Group dependencies survive save and load");
        var backup=Path.Combine(root,"group-backup.json"); store.Export(config,backup); var restored=store.Import(backup);
        check(File.Exists(Path.Combine(store.Icons,restored.Shortcuts[0].Steps![0].App.Icon)),"Group app icons survive backup and restore");
        var reordered=steps.Select(s=>s.Copy()).ToList(); (reordered[0],reordered[1])=(reordered[1],reordered[0]);
        GroupRules.ResetInvalidDependencies(reordered);
        check(reordered[0].DependsOn==null && reordered[0].Condition==null && reordered[0].DelaySeconds==0 && reordered[2].DependsOn==first.Id,"Reorder clears only invalid forward dependencies");
        reordered.RemoveAt(1); GroupRules.ResetInvalidDependencies(reordered);
        check(reordered[0].DependsOn==null && reordered.Skip(1).Select((s,i)=>s.DependsOn==reordered[i].Id && s.Condition==GroupCondition.Together && s.DelaySeconds==0).All(v=>v),"Removing an app resets its dependents to Together with the nearest previous app");
        var fresh=Enumerable.Range(0,3).Select(_=>new GroupStep()).ToList();
        GroupRules.ResetInvalidDependencies(fresh);
        check(fresh[0].DependsOn==null && fresh[1].DependsOn==fresh[0].Id && fresh[2].DependsOn==fresh[1].Id && fresh.Skip(1).All(s=>s.Condition==GroupCondition.Together),"New steps default to Together with the nearest previous app");
        fresh[1].DependsOn=fresh[2].Id; fresh[1].Condition=GroupCondition.Finished; fresh[1].DelaySeconds=8;
        GroupRules.ResetInvalidDependencies(fresh);
        check(fresh[1].DependsOn==fresh[0].Id && fresh[1].Condition==GroupCondition.Together && fresh[1].DelaySeconds==0,"Invalid forward dependency resets condition, target and delay");
        together.DelaySeconds=-1; throws(()=>GroupRules.Validate(steps),"Reject negative group delay"); together.DelaySeconds=0;
        first.DependsOn=exit.Id; first.Condition=GroupCondition.Finished; throws(()=>GroupRules.Validate(steps),"Reject cyclic and forward dependencies"); first.DependsOn=null; first.Condition=null;
        var process=new FakeProcess(); var launched=new List<string>();
        var run=GroupLauncher.RunAsync(steps,app=> { launched.Add(app.Name); return Task.FromResult<IGroupProcess?>(app.Name=="First"?process:new FakeProcess()); });
        check(launched.SequenceEqual(new[]{"First","Together"}),"Together launches without waiting for ready or exit");
        process.Idle.SetResult();
        check(launched.Contains("Idle") && !launched.Contains("Exit"),"Input-idle releases only the ready branch");
        process.Exit.SetResult(); await run;
        check(launched.Contains("Exit") && process.Disposed,"Finished branch waits for exit and process handles are released");
        var independent=new GroupStep { App=new Shortcut { Name="Independent" } }; launched.Clear();
        try { await GroupLauncher.RunAsync([first,together,independent],app=> { launched.Add(app.Name); return app.Name=="First"?Task.FromException<IGroupProcess?>(new IOException("Failed")):Task.FromResult<IGroupProcess?>(null); }); }
        catch(IOException) { }
        check(launched.SequenceEqual(new[]{"First","Independent"}),"Launch failure skips dependents but preserves independent branches");
        together.DelaySeconds=1; var clock=System.Diagnostics.Stopwatch.StartNew();
        await GroupLauncher.RunAsync([first,together],app=>Task.FromResult<IGroupProcess?>(null));
        check(clock.ElapsedMilliseconds>=900,"Group delay uses seconds"); together.DelaySeconds=0;
    }
    private sealed class FakeProcess : IGroupProcess
    {
        public TaskCompletionSource Idle { get; }=new();
        public TaskCompletionSource Exit { get; }=new();
        public bool Disposed { get; private set; }
        public Task WaitForInputIdleAsync()=>Idle.Task;
        public Task WaitForExitAsync()=>Exit.Task;
        public void Dispose()=>Disposed=true;
    }
}
