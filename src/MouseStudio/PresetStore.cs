using System.Text.Json;
namespace MouseLab5;

internal sealed class MacroPreset
{
 public string Id{get;set;}=Guid.NewGuid().ToString("N");
 public string Name{get;set;}="";
 public MacroJob Macro{get;set;}=new();
 public DateTime Updated{get;set;}=DateTime.Now;
}
internal sealed class PresetStore
{
 public List<MacroPreset> Presets{get;set;}=[];
 public string?[] Assignments{get;set;}=new string?[5];
 public int PointerMode{get;set;}=1;
 internal MacroPreset? Find(string? id)=>Presets.FirstOrDefault(p=>p.Id==id);
 internal static MacroJob Copy(MacroJob job)=>JsonSerializer.Deserialize<MacroJob>(JsonSerializer.Serialize(job))!;
 internal MacroJob? CreateRun(int number)=>Find(Assignments[number-1]) is { } preset?Copy(preset.Macro):null;
 internal void Delete(string id){Presets.RemoveAll(p=>p.Id==id);for(int i=0;i<5;i++)if(Assignments[i]==id)Assignments[i]=null;}
 internal void Save(string path)
 {
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,path,true);
 }
 internal static PresetStore Load(string path,string? legacy=null)
 {
  PresetStore result;
  if(File.Exists(path))result=JsonSerializer.Deserialize<PresetStore>(File.ReadAllText(path))??throw new InvalidDataException("프리셋 파일이 비어 있습니다");
  else
  {
   result=new();
   if(legacy!=null&&File.Exists(legacy))
    foreach(var entry in JsonSerializer.Deserialize<Dictionary<string,MacroJob>>(File.ReadAllText(legacy))??[])
    {if(entry.Value.Steps.Any(s=>s.UseMousePosition))entry.Value.PositionMode="마우스 위치";result.Presets.Add(new(){Name=entry.Key,Macro=entry.Value});}
  }
  if(result.Assignments.Length!=5){var resized=result.Assignments;Array.Resize(ref resized,5);result.Assignments=resized;}
  for(int i=0;i<5;i++)if(result.Find(result.Assignments[i])==null)result.Assignments[i]=null;
  return result;
 }
}
