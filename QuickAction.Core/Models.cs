using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickAction.Core;

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
public sealed class Shortcut : Observable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = L10n.T("Shortcut mới");
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string Icon { get; set; } = "builtin:app";
    private bool admin;
    public bool Admin { get => admin; set { admin = value; Changed(); } }
    public int? PinOrder { get; set; }
    public List<GroupStep>? Steps { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsGroup => Steps != null;
    public Shortcut Copy() { var copy=(Shortcut)MemberwiseClone(); copy.Steps=Steps?.Select(s=>s.Copy()).ToList(); return copy; }
    public override string ToString() => Name;
}
public enum DockIconSize { Small, Medium, Big }
public enum Edge { Left, Right, Top, Bottom }
public sealed class Configuration
{
    public string Language { get; set; } = "vi";
    public int Version { get; set; } = 1;
    public List<Shortcut> Shortcuts { get; set; } = [];
    public string Monitor { get; set; } = "";
    public Edge Edge { get; set; } = Edge.Left;
    public DockIconSize IconSize { get; set; } = DockIconSize.Small;
    public double Position { get; set; } = .5;
    public bool Topmost { get; set; } = true;
    public double Opacity { get; set; } = .82;
    public int CollapseMilliseconds { get; set; } = 600;
    public bool Startup { get; set; }
    public void Validate()
    {
        if (Language is not ("en" or "vi")) Language = "vi";
        if (Version != 1 || Shortcuts == null || Shortcuts.Count > 10000) throw new InvalidDataException(L10n.T("Cấu hình không được hỗ trợ."));
        if (!Enum.IsDefined(IconSize) || !Enum.IsDefined(Edge) || !double.IsFinite(Position) || Position < 0 || Position > 1 || !double.IsFinite(Opacity) || Opacity < .3 || Opacity > 1 || CollapseMilliseconds < 200 || CollapseMilliseconds > 5000) throw new InvalidDataException(L10n.T("Thiết lập dock không hợp lệ."));
        if (Shortcuts.Any(s => s == null || s.Id == Guid.Empty || string.IsNullOrWhiteSpace(s.Name) || s.Path == null || s.Arguments == null || s.WorkingDirectory == null || s.Icon == null) || Shortcuts.Select(s => s.Id).Distinct().Count() != Shortcuts.Count) throw new InvalidDataException(L10n.T("Shortcut không hợp lệ."));
        if (Shortcuts.Count(s => s.PinOrder.HasValue) > 5) throw new InvalidDataException(L10n.T("Chỉ được ghim tối đa 5 app."));
        foreach (var s in Shortcuts) if(s.IsGroup) GroupRules.Validate(s.Steps!);
        foreach (var s in AllShortcuts()) if (!s.Icon.StartsWith("builtin:") && (System.IO.Path.GetFileName(s.Icon) != s.Icon || s.Icon.Contains(':'))) throw new InvalidDataException(L10n.T("Đường dẫn icon không hợp lệ."));
    }
    public IEnumerable<Shortcut> AllShortcuts() => Shortcuts.SelectMany(s => new[]{s}.Concat(s.Steps?.Select(step=>step.App) ?? []));
}
public static class Pins
{
    public static void Toggle(Configuration config, Shortcut item)
    {
        if (item.PinOrder.HasValue) item.PinOrder = null;
        else { if (config.Shortcuts.Count(s => s.PinOrder.HasValue) >= 5) throw new InvalidOperationException(L10n.T("Đã ghim 5 app. Hãy bỏ ghim một app trước.")); item.PinOrder = 100; }
        Normalize(config);
    }
    public static void Normalize(Configuration config) { int n = 0; foreach (var s in config.Shortcuts.Where(s => s.PinOrder.HasValue).OrderBy(s => s.PinOrder)) s.PinOrder = n++; }
    public static void Move(Configuration config, Guid source, Guid target)
    {
        var list = config.Shortcuts.Where(s => s.PinOrder.HasValue).OrderBy(s => s.PinOrder).ToList();
        var from = list.FindIndex(s => s.Id == source); var to = list.FindIndex(s => s.Id == target);
        if (from < 0 || to < 0) return;
        var item = list[from]; list.RemoveAt(from); list.Insert(to, item);
        for (int i = 0; i < list.Count; i++) list[i].PinOrder = i;
    }
}
public readonly record struct Area(double X, double Y, double Width, double Height);
public static class DockGeometry
{
    public static Edge Nearest(Area a, double x, double y) => new[] { (Edge.Left, Math.Abs(x-a.X)), (Edge.Right, Math.Abs(x-a.X-a.Width)), (Edge.Top, Math.Abs(y-a.Y)), (Edge.Bottom, Math.Abs(y-a.Y-a.Height)) }.MinBy(e => e.Item2).Item1;
    public static (double X, double Y) Place(Area a, Edge edge, double position, double width, double height)
    {
        var x = a.X + (a.Width-width)*Math.Clamp(position,0,1); var y = a.Y + (a.Height-height)*Math.Clamp(position,0,1);
        if (edge == Edge.Left) x=a.X; if (edge == Edge.Right) x=a.X+a.Width-width;
        if (edge == Edge.Top) y=a.Y; if (edge == Edge.Bottom) y=a.Y+a.Height-height;
        return (Math.Clamp(x,a.X,a.X+Math.Max(0,a.Width-width)), Math.Clamp(y,a.Y,a.Y+Math.Max(0,a.Height-height)));
    }
}
