using System.IO.Compression;
using System.Text.Json;

namespace QuickAction.Core;
public sealed class ConfigurationStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string Root { get; }
    public string Icons => Path.Combine(Root,"icons");
    public string FilePath => Path.Combine(Root,"config.json");
    public string? RecoveryMessage { get; private set; }
    public ConfigurationStore(string root) { Root=root; Directory.CreateDirectory(Icons); }
    private static Configuration Read(string text) { var c=JsonSerializer.Deserialize<Configuration>(text) ?? throw new InvalidDataException("Cấu hình rỗng."); c.Validate(); return c; }
    public Configuration Load()
    {
        if (!File.Exists(FilePath)) return new();
        try { return Read(File.ReadAllText(FilePath)); }
        catch (Exception e) when (e is JsonException or InvalidDataException or IOException)
        {
            File.Copy(FilePath, Path.Combine(Root,"config.corrupt-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".json"),true);
            if (File.Exists(FilePath+".bak")) { try { var c=Read(File.ReadAllText(FilePath+".bak")); File.Copy(FilePath+".bak",FilePath,true); RecoveryMessage="Đã phục hồi cấu hình từ bản sao lưu."; return c; } catch (Exception ex) when(ex is JsonException or InvalidDataException or IOException) { } }
            RecoveryMessage="Cấu hình hỏng đã được giữ lại; đang dùng cấu hình mới."; File.Delete(FilePath); return new();
        }
    }
    public void Save(Configuration config)
    {
        config.Validate(); var temp=FilePath+".tmp"; File.WriteAllText(temp,JsonSerializer.Serialize(config,Options));
        if(File.Exists(FilePath)) File.Replace(temp,FilePath,FilePath+".bak"); else File.Move(temp,FilePath);
    }
    public string ImportIcon(string source)
    {
        var ext=Path.GetExtension(source).ToLowerInvariant();
        if(!new[]{".ico",".png",".jpg",".jpeg"}.Contains(ext) || new FileInfo(source).Length>10*1024*1024) throw new InvalidDataException("Icon phải là ICO, PNG hoặc JPG, tối đa 10 MB.");
        var name=Guid.NewGuid().ToString("N")+ext; File.Copy(source,Path.Combine(Icons,name)); return name;
    }
    public void Export(Configuration config,string destination)
    {
        config.Validate(); using var zip=ZipFile.Open(destination,ZipArchiveMode.Create);
        using(var w=new StreamWriter(zip.CreateEntry("config.json").Open())) w.Write(JsonSerializer.Serialize(config,Options));
        foreach(var name in config.Shortcuts.Select(s=>s.Icon).Where(s=>!s.StartsWith("builtin:")).Distinct())
            if(File.Exists(Path.Combine(Icons,name))) zip.CreateEntryFromFile(Path.Combine(Icons,name),"icons/"+name);
    }
    public Configuration Import(string source)
    {
        using var zip=ZipFile.OpenRead(source);
        if(zip.Entries.Count>10001 || zip.Entries.Sum(e=>e.Length)>100*1024*1024) throw new InvalidDataException("Gói sao lưu quá lớn.");
        if(zip.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=zip.Entries.Count) throw new InvalidDataException("Gói chứa mục trùng lặp.");
        foreach(var e in zip.Entries)
            if(e.FullName!="config.json" && !(e.FullName.StartsWith("icons/") && e.FullName.Length>6 && Path.GetFileName(e.FullName)==e.FullName[6..] && !e.FullName.Contains('\\') && !e.FullName.Contains(':') && !e.FullName.Contains(".."))) throw new InvalidDataException("Gói chứa đường dẫn không hợp lệ.");
        var entry=zip.GetEntry("config.json") ?? throw new InvalidDataException("Thiếu config.json.");
        using var reader=new StreamReader(entry.Open()); var config=Read(reader.ReadToEnd());
        // Unique asset names keep the old configuration and its backup intact.
        foreach(var group in config.Shortcuts.Where(s=>!s.Icon.StartsWith("builtin:")).GroupBy(s=>s.Icon).ToList())
        {
            var asset=zip.GetEntry("icons/"+group.Key) ?? throw new InvalidDataException("Thiếu icon trong bản sao lưu.");
            var ext=Path.GetExtension(group.Key).ToLowerInvariant();
            if(!new[]{".ico",".png",".jpg",".jpeg"}.Contains(ext) || asset.Length>10*1024*1024) throw new InvalidDataException("Icon không hợp lệ.");
            var name=Guid.NewGuid().ToString("N")+ext; asset.ExtractToFile(Path.Combine(Icons,name)); foreach(var s in group) s.Icon=name;
        }
        Save(config); return config;
    }
}
