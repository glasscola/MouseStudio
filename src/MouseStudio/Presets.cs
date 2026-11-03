using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Text.Json;
namespace MouseLab5;
public partial class StudioWindow
{
 PresetStore presetStore=new();
 readonly List<Button> presetButtons=[],showButtons=[];
 readonly Dictionary<int,string> runningNames=[];
 readonly bool[] pointerPinned=new bool[5];
 string? editingPresetId;string cleanEditor="";bool storeReady;
 Popup? presetPopup;
 string PresetPath=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","프리셋.json"));
 void LoadPresetStore()
 {
  try{presetStore=PresetStore.Load(PresetPath,DataPath);storeReady=true;}
  catch(Exception ex){Notify("프리셋 읽기 실패: "+ex.Message,true);}
 }
 bool SavePresetStore()
 {
  if(!storeReady){Notify("프리셋 파일을 읽지 못해 저장할 수 없습니다",true);return false;}
  try{presetStore.Save(PresetPath);return true;}catch(Exception ex){Notify("저장 실패: "+ex.Message,true);return false;}
 }
 void TogglePointer(int id)
 {
  pointerPinned[id-1]=!pointerPinned[id-1];
  showButtons[id-1].Content=pointerPinned[id-1]?"표시 켜짐":"표시";
  showButtons[id-1].Background=Hex(pointerPinned[id-1]?"#EEF1FF":"#FFFFFF");
  Notify(pointerPinned[id-1]?$"마우스 {id} 위치 표시":$"마우스 {id} 대기 위치 숨김 · 실행 중에는 표시");
 }
 void RefreshPresetCards()
 {
  for(int i=0;i<presetButtons.Count;i++)
  {
   var p=presetStore.Find(presetStore.Assignments[i]);
   presetButtons[i].Content=new TextBlock{Text=(p?.Name??"실행할 매크로")+" ▾",TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=155};
   presetButtons[i].ToolTip=p==null?"저장한 프리셋 선택":$"{p.Name} · {p.Macro.Steps.Count}단계 · {(p.Macro.Repeat==0?"무한 반복":p.Macro.Repeat+"회")}";
   runButtons[i].IsEnabled=p!=null||slots[i].Running||paused.GetValueOrDefault(i+1);
  }
 }
 long lastDirtyCheck;
 string EditorSignature()
 {
  int current=editingStep==null?-1:steps.IndexOf(editingStep);
  static string Number(string value)=>int.TryParse(value,out int n)?n.ToString():"invalid:"+value;
  var values=steps.Select((a,i)=>new{a.Action,a.Button,a.UseMousePosition,a.TargetHandle,a.TargetPid,a.TargetStart,a.ClientX,a.ClientY,
   X=i==current?Number(EditX.Text):a.X.ToString(),Y=i==current?Number(EditY.Text):a.Y.ToString(),
   EndX=i==current?Number(EditEndX.Text):a.EndX.ToString(),EndY=i==current?Number(EditEndY.Text):a.EndY.ToString(),
   Value=i==current?Number(EditValue.Text):a.Value.ToString(),Before=i==current?Number(DelayBefore.Text):a.DelayBefore.ToString(),After=i==current?Number(DelayAfter.Text):a.DelayAfter.ToString()});
  return JsonSerializer.Serialize(new{Id=editingPresetId,Name=ProfileName.Text,Steps=values,Repeat=Number(RepeatCount.Text),Infinite=Infinite.IsChecked,Basis=PositionBasis.SelectedIndex});
 }
 void ResetBaseline(){cleanEditor=EditorSignature();UpdatePresetDirty();}
 void UpdatePresetDirty()
 {
  if(PresetState==null||cleanEditor.Length==0)return;
  bool dirty=EditorSignature()!=cleanEditor;
  PresetState.Text=(editingPresetId==null?"새 프리셋":presetStore.Find(editingPresetId)?.Name??"새 프리셋")+(dirty?"  ·  수정 중":"  ·  저장됨");
  if(editingPresetId==null&&!dirty)PresetState.Text="새 프리셋";
 }
 bool ConfirmEditorChange()
 {
  if(cleanEditor.Length==0||EditorSignature()==cleanEditor)return true;
  var answer=MessageBox.Show(this,"수정한 프리셋을 저장할까요?","저장하지 않은 변경",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
  return answer==MessageBoxResult.No||answer==MessageBoxResult.Yes&&SaveCurrentPreset();
 }
 bool SaveCurrentPreset()
 {
  var job=ReadJob();if(job==null)return false;
  string name=ProfileName.Text.Trim();if(name.Length==0||name.Length>80){Notify("프리셋 이름을 1~80자로 입력해 주세요",true);return false;}
  var existing=presetStore.Presets.FirstOrDefault(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase)&&p.Id!=editingPresetId);
  if(existing!=null)
  {
   if(editingPresetId!=null){Notify("같은 이름이 있습니다. 다른 이름을 입력해 주세요",true);return false;}
   if(MessageBox.Show(this,$"‘{name}’ 프리셋을 덮어쓸까요? 다른 이름으로 저장하려면 아니요를 누르세요.","같은 이름의 프리셋",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return false;
  }
  var preset=presetStore.Find(editingPresetId)??existing??new MacroPreset();
  string oldName=preset.Name;var oldJob=preset.Macro;var oldTime=preset.Updated;bool added=!presetStore.Presets.Contains(preset);
  preset.Name=name;preset.Macro=job;preset.Updated=DateTime.Now;if(added)presetStore.Presets.Add(preset);
  if(!SavePresetStore()){if(added)presetStore.Presets.Remove(preset);else{preset.Name=oldName;preset.Macro=oldJob;preset.Updated=oldTime;}return false;}
  editingPresetId=preset.Id;ProfileName.Text=name;ResetBaseline();RefreshPresetCards();Notify("프리셋 저장 완료 · 다음 실행부터 적용");return true;
 }
 void SaveProfile(object sender,RoutedEventArgs e)=>SaveCurrentPreset();
 void LoadProfile(object sender,RoutedEventArgs e)=>OpenPresetPicker((Button)sender,null);
 void NewProfile(object sender,RoutedEventArgs e)
 {
  if(!ConfirmEditorChange())return;editingPresetId=null;editingStep=null;steps.Clear();ProfileName.Text="새 프리셋";
  Infinite.IsChecked=false;RepeatCount.Text="1";PositionBasis.SelectedIndex=1;RefreshSteps();ResetBaseline();Notify("동작을 추가하고 이름을 정해 저장하세요");
 }
 void DuplicateProfile(object sender,RoutedEventArgs e)
 {
  if(!Commit())return;editingPresetId=null;string root=ProfileName.Text.Trim()+" 복사본",name=root;int n=2;
  while(presetStore.Presets.Any(p=>p.Name==name))name=root+" "+n++;
  ProfileName.Text=name;Notify("복사본의 이름과 동작을 수정한 뒤 저장하세요");UpdatePresetDirty();
 }
 void DeleteProfile(object sender,RoutedEventArgs e)
 {
  var preset=presetStore.Find(editingPresetId);if(preset==null){Notify("삭제할 저장 프리셋을 먼저 불러오세요");return;}
  var ids=Enumerable.Range(1,5).Where(i=>presetStore.Assignments[i-1]==preset.Id).ToArray();
  if(ids.Any(i=>slots[i-1].Running||paused.GetValueOrDefault(i))){Notify("연결된 마우스의 실행을 중단한 뒤 삭제하세요",true);return;}
  string linked=ids.Length>0?"\n연결 해제: 마우스 "+string.Join(", ",ids):"";
  if(MessageBox.Show(this,$"‘{preset.Name}’ 프리셋을 삭제할까요?{linked}","프리셋 삭제",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
  var backup=JsonSerializer.Serialize(presetStore);presetStore.Delete(preset.Id);
  if(!SavePresetStore()){presetStore=JsonSerializer.Deserialize<PresetStore>(backup)!;return;}
  editingPresetId=null;editingStep=null;steps.Clear();ProfileName.Text="새 프리셋";RefreshSteps();ResetBaseline();RefreshPresetCards();Notify("프리셋 삭제 완료");
 }
 void LoadPreset(MacroPreset preset)
 {
  if(recording||!ConfirmEditorChange())return;
  editingPresetId=preset.Id;editingStep=null;steps.Clear();steps.AddRange(preset.Macro.Steps.Select(Clone));
  ProfileName.Text=preset.Name;Infinite.IsChecked=preset.Macro.Repeat==0;RepeatCount.Text=Math.Max(1,preset.Macro.Repeat).ToString();
  PositionBasis.SelectedIndex=preset.Macro.PositionMode=="마우스 위치"?1:0;RefreshSteps();ResetBaseline();Notify("프리셋 불러옴 · 이름 수정 후 저장하면 이름도 변경됩니다");
 }
 void OpenPresetPicker(Button anchor,int? number)
 {
  if(presetPopup?.IsOpen==true){presetPopup.IsOpen=false;return;}
  var search=new TextBox{MinHeight=40,Margin=new(0,0,0,10),ToolTip="프리셋 이름 검색"};
  System.Windows.Automation.AutomationProperties.SetName(search,"프리셋 검색");
  var list=new ListBox{BorderThickness=new(0),Background=Brushes.Transparent,MaxHeight=280,MinHeight=50};
  ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
  var panel=new StackPanel();panel.Children.Add(new TextBlock{Text=number.HasValue?$"마우스 {number} · 실행할 프리셋":"프리셋 불러오기",FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,10)});panel.Children.Add(search);panel.Children.Add(list);
  var popup=new Popup{PlacementTarget=anchor,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,VerticalOffset=6,HorizontalOffset=number.HasValue?0:anchor.ActualWidth-340,
   Child=new Border{Background=Brushes.White,BorderBrush=Hex("#DCE3F3"),BorderThickness=new(1),CornerRadius=new(12),Padding=new(14),Width=340,Child=panel}};
  presetPopup=popup;
  void Refill()
  {
   list.Items.Clear();
   if(number.HasValue)list.Items.Add(new ListBoxItem{Content="선택 없음",Tag=""});
   foreach(var p in presetStore.Presets.Where(p=>p.Name.Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p.Name))
   {
    var row=new StackPanel();row.Children.Add(new TextBlock{Text=p.Name,TextTrimming=TextTrimming.CharacterEllipsis,FontWeight=FontWeights.SemiBold});
    row.Children.Add(new TextBlock{Text=$"{p.Macro.Steps.Count}단계 · {(p.Macro.Repeat==0?"무한 반복":p.Macro.Repeat+"회")} · {p.Macro.PositionMode}",FontSize=11,Foreground=Hex("#8290A8"),Margin=new(0,5,0,0)});
    list.Items.Add(new ListBoxItem{Content=row,Tag=p.Id});
   }
   if(list.Items.Count==0)list.Items.Add(new ListBoxItem{Content="저장한 프리셋이 없습니다",IsEnabled=false});
  }
  search.TextChanged+=(_,_)=>Refill();Refill();
  list.SelectionChanged+=(_,_)=>
  {
   if(list.SelectedItem is not ListBoxItem item||item.Tag is not string id)return;
   popup.IsOpen=false;
   if(number is int n)
   {
    if(slots[n-1].Running||paused.GetValueOrDefault(n)){Notify("실행을 중단한 뒤 변경하세요",true);return;}
    string? previous=presetStore.Assignments[n-1];presetStore.Assignments[n-1]=id.Length==0?null:id;
    if(!SavePresetStore())presetStore.Assignments[n-1]=previous;
    RefreshPresetCards();Notify($"마우스 {n} · "+(presetStore.Find(id)?.Name??"연결 해제"));
   }
   else if(presetStore.Find(id) is {} preset)LoadPreset(preset);
  };
  popup.IsOpen=true;search.Focus();
 }
}
