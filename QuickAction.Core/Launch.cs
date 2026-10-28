using System.Diagnostics;

namespace QuickAction.Core;
public sealed record LaunchRequest(string FileName, string Arguments, string WorkingDirectory, bool Admin);
public static class LaunchBuilder
{
    public static bool Supported(string path) => new[] { ".exe", ".bat", ".cmd", ".ps1", ".msi" }.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());
    public static LaunchRequest Build(Shortcut item, string systemDirectory)
    {
        var path = Environment.ExpandEnvironmentVariables(item.Path);
        if (!System.IO.Path.IsPathFullyQualified(path) || path.Contains('"') || !Supported(path)) throw new ArgumentException("Chọn đường dẫn tuyệt đối đến EXE, BAT, CMD, PS1 hoặc MSI.");
        var work = string.IsNullOrWhiteSpace(item.WorkingDirectory) ? System.IO.Path.GetDirectoryName(path)! : Environment.ExpandEnvironmentVariables(item.WorkingDirectory);
        var args = item.Arguments.Trim(); var quoted = "\""+path+"\"";
        return System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".bat" or ".cmd" => new(System.IO.Path.Combine(systemDirectory,"cmd.exe"), "/d /s /c \""+quoted+(args.Length>0?" "+args:"")+"\"",work,item.Admin),
            ".ps1" => new(System.IO.Path.Combine(systemDirectory,"WindowsPowerShell","v1.0","powershell.exe"), "-NoProfile -File "+quoted+(args.Length>0?" "+args:""),work,item.Admin),
            ".msi" => new(System.IO.Path.Combine(systemDirectory,"msiexec.exe"), "/i "+quoted+(args.Length>0?" "+args:""),work,item.Admin),
            _ => new(path,args,work,item.Admin)
        };
    }
    public static ProcessStartInfo StartInfo(LaunchRequest request) => new(request.FileName, request.Arguments) { WorkingDirectory = request.WorkingDirectory, UseShellExecute = true, Verb = request.Admin ? "runas" : "open" };
}
