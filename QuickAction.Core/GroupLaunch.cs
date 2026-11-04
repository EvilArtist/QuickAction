using System.Diagnostics;

namespace QuickAction.Core;

public enum GroupCondition { Together, InputIdle, Finished }
public sealed class GroupStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Shortcut App { get; set; } = new();
    public Guid? DependsOn { get; set; }
    public GroupCondition? Condition { get; set; }
    public int DelaySeconds { get; set; }
    public GroupStep Copy() => new() { Id=Id, App=App.Copy(), DependsOn=DependsOn, Condition=Condition, DelaySeconds=DelaySeconds };
}
public static class GroupRules
{
    public static void ResetInvalidDependencies(IList<GroupStep> steps)
    {
        var previous=new HashSet<Guid>();
        Guid? nearest=null;
        foreach(var step in steps)
        {
            if(step.DependsOn is not Guid id || !previous.Contains(id) || !step.Condition.HasValue)
            {
                step.DependsOn=nearest;
                step.Condition=nearest.HasValue?GroupCondition.Together:null;
                step.DelaySeconds=0;
            }
            previous.Add(step.Id);
            nearest=step.Id;
        }
    }
    public static void Validate(IReadOnlyList<GroupStep> steps)
    {
        if(steps.Count is 0 or >1000) throw new InvalidDataException("Nhóm cần từ 1 đến 1000 app.");
        var previous=new HashSet<Guid>();
        foreach(var step in steps)
        {
            if(step==null || step.Id==Guid.Empty || previous.Contains(step.Id) || step.App==null || step.App.IsGroup || string.IsNullOrWhiteSpace(step.App.Name) || step.App.Path==null || step.App.Arguments==null || step.App.WorkingDirectory==null || step.App.Icon==null)
                throw new InvalidDataException("App trong nhóm không hợp lệ.");
            if(step.DelaySeconds<0 || step.DelaySeconds>86400 || (step.DependsOn.HasValue != step.Condition.HasValue) || (step.Condition.HasValue && !Enum.IsDefined(step.Condition.Value)) || (step.DependsOn is Guid id && !previous.Contains(id)) || (!step.DependsOn.HasValue && step.DelaySeconds!=0))
                throw new InvalidDataException("Điều kiện chạy không hợp lệ. Chỉ chọn app đứng trước; thời gian là giây nguyên từ 0 đến 86400.");
            if(step.DependsOn is Guid target && step.Condition!=GroupCondition.Together && LaunchBuilder.IsInstalledApp(steps.First(s=>s.Id==target).App.Path))
                throw new InvalidDataException("Không thể theo dõi app mở qua Windows. Hãy dùng đường dẫn EXE cho app được chờ.");
            previous.Add(step.Id);
        }
    }
}
public interface IGroupProcess : IDisposable
{
    Task WaitForInputIdleAsync();
    Task WaitForExitAsync();
}
public sealed class GroupProcess(Process process) : IGroupProcess
{
    public Task WaitForInputIdleAsync() => Task.Run(()=>
    {
        try { process.WaitForInputIdle(30000); }
        catch(InvalidOperationException) { /* Console apps and exited processes have no input queue. */ }
    });
    public Task WaitForExitAsync() => process.WaitForExitAsync();
    public void Dispose() => process.Dispose();
}
public static class GroupLauncher
{
    public static async Task RunAsync(IReadOnlyList<GroupStep> steps, Func<Shortcut,Task<IGroupProcess?>> start)
    {
        GroupRules.Validate(steps);
        var tasks=new Dictionary<Guid,Task<IGroupProcess?>>();
        async Task<IGroupProcess?> RunStep(GroupStep step)
        {
            if(step.DependsOn is Guid id)
            {
                var process=await tasks[id];
                if(step.Condition!=GroupCondition.Together)
                {
                    if(process==null) throw new InvalidOperationException("Không thể theo dõi tiến trình của "+step.App.Name);
                    if(step.Condition==GroupCondition.InputIdle) await process.WaitForInputIdleAsync();
                    else await process.WaitForExitAsync();
                }
                if(step.DelaySeconds>0) await Task.Delay(TimeSpan.FromSeconds(step.DelaySeconds));
            }
            return await start(step.App);
        }
        try
        {
            foreach(var step in steps) tasks.Add(step.Id,RunStep(step));
            await Task.WhenAll(tasks.Values);
        }
        finally { foreach(var task in tasks.Values) if(task.IsCompletedSuccessfully) task.Result?.Dispose(); }
    }
}
