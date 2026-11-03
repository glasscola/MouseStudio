using System.Text.Json;
namespace MouseLab5;
internal static class PresetChecks
{
 internal static object Run()
 {
  string path=Path.Combine(Path.GetTempPath(),"MouseStudio-presets-"+Guid.NewGuid().ToString("N")+".json");
  string legacy=path+".legacy";
  try
  {
   var store=new PresetStore();var a=new MacroPreset{Name="클릭",Macro=new(){Repeat=2,Steps=[new(){Action="클릭"}]}};
   var b=new MacroPreset{Name="다른 순서",Macro=new(){Repeat=5,Steps=[new(){Action="딜레이",Value=100},new(){Action="스크롤",Value=120}]}};
   store.Presets.AddRange([a,b]);store.Assignments[0]=a.Id;store.Assignments[1]=b.Id;store.Assignments[2]=a.Id;store.Save(path);
   store=PresetStore.Load(path);bool retained=store.Presets.Count==2&&store.Assignments[0]==a.Id&&store.Assignments[1]==b.Id;
   var run1=store.CreateRun(1)!;var run3=store.CreateRun(3)!;store.Find(a.Id)!.Macro.Steps[0].Action="드래그";store.Find(a.Id)!.Macro.Repeat=10;
   run1.Steps[0].X=999;run1.Repeat=4;
   bool isolated=run3.Steps[0].Action=="클릭"&&run3.Steps[0].X==0&&run3.Repeat==2&&store.Find(a.Id)!.Macro.Repeat==10;
   store.Find(a.Id)!.Name="이름 변경";store.Save(path);store=PresetStore.Load(path);
   bool rename=store.Find(store.Assignments[0])?.Name=="이름 변경"&&store.Assignments[2]==a.Id;
   store.Delete(a.Id);store.Save(path);store=PresetStore.Load(path);
   bool deletion=store.Assignments[0]==null&&store.Assignments[2]==null&&store.Assignments[1]==b.Id&&store.Presets.Count==1;
   File.WriteAllText(legacy,JsonSerializer.Serialize(new Dictionary<string,MacroJob>{{"이전 매크로",new(){Steps=[new(){UseMousePosition=true}]}}}));
   var migrated=PresetStore.Load(path+".missing",legacy);bool migration=migrated.Presets.Count==1&&migrated.Presets[0].Macro.PositionMode=="마우스 위치"&&File.Exists(legacy);
   return new{passed=retained&&isolated&&rename&&deletion&&migration,assignmentsRestored=retained,runSnapshotsIndependent=isolated,renameKeepsAssignments=rename,deleteUnlinksOnlyMatchingSlots=deletion,legacyMigration=migration};
  }
  finally{if(File.Exists(path))File.Delete(path);if(File.Exists(legacy))File.Delete(legacy);}
 }
}
