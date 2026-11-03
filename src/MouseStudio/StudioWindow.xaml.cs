using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.Json;
using System.Runtime.InteropServices;
using DPoint=System.Drawing.Point;

namespace MouseLab5;
public partial class StudioWindow:Window
{
 readonly PointerSlot[] slots=Enumerable.Range(1,5).Select(i=>new PointerSlot(i)).ToArray();
 readonly PointerOverlay[] pointers=new PointerOverlay[5];
 readonly Dictionary<int,MacroJob> jobs=[];
 readonly Dictionary<int,bool> paused=[];
 readonly List<MacroStep> steps=[];
 readonly List<TextBlock> targets=[],states=[],counts=[];
 readonly List<Button> runButtons=[];
 readonly List<Border> cards=[];
 readonly DispatcherTimer clock=new(){Interval=TimeSpan.FromMilliseconds(30)};
 readonly Stopwatch time=Stopwatch.StartNew();
 nint hwnd,mouseHook;Native.MouseHook? mouseCallback;
 int selected=1,mode=1,hotkeyConflicts;bool inputHookAdded;bool binding,closed,manualUsed;
 readonly PointerBank bank=new();
 readonly MacroStep[] memories=new MacroStep[5];
 readonly bool[] positioned=new bool[5];
 Native.Pt lastPhysical;long lastRemember;
 MacroStep? editingStep;
 string DataPath=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","매크로 보관함.json"));
 public StudioWindow()
 {
  for(int i=0;i<5;i++){memories[i]=new MacroStep{X=bank.Positions[i].X,Y=bank.Positions[i].Y,UseMousePosition=true};Routing.Capture(memories[i],bank.Positions[i].X,bank.Positions[i].Y);}
  InitializeComponent();
  MaxHeight=SystemParameters.WorkArea.Height;MaxWidth=SystemParameters.WorkArea.Width;
  Width=Math.Min(1180,SystemParameters.WorkArea.Width-24);Height=Math.Min(820,SystemParameters.WorkArea.Height-24);
  LoadPresetStore();BuildCards();BuildHelp();
  for(int i=0;i<5;i++){pointers[i]=new PointerOverlay(i+1,Palette.Colors[i]);pointers[i].Prepare();slots[i].Screen=bank.Positions[i];}
  SourceInitialized+=(_,_)=>RegisterInput();
  Activated+=(_,_)=>{if(hwnd!=0&&hotkeyConflicts>0){for(int id=100;id<=405;id++)Native.UnregisterHotKey(hwnd,id);RegisterInput();}};
  Loaded+=(_,_)=>{steps.Add(Clone(memories[0]));RefreshSteps();SelectPointer(1,false);SetMode(presetStore.PointerMode);ResetBaseline();RefreshPresetCards();clock.Start();};
  Closing+=(_,e)=>{if(!closed&&!ConfirmEditorChange())e.Cancel=true;};
  clock.Tick+=(_,_)=>Tick();Closed+=(_,_)=>Cleanup();
 }
 static Brush Hex(string s)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(s));
 static TextBlock Text(string text,double size=14,string color="#202B40")=>new(){Text=text,FontSize=size,Foreground=Hex(color),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
 Button MakeButton(string label,Action action,bool primary=false)
 {
  var b=new Button{Content=label,Style=(Style)FindResource(primary?"Primary":typeof(Button)),MinHeight=42};b.Click+=(_,_)=>action();return b;
 }
 void BuildCards()
 {
  for(int i=0;i<5;i++)
  {
   int id=i+1;var card=new Border{Style=(Style)FindResource("Card"),Margin=new Thickness(0,0,i%2==0?10:0,14),Padding=new Thickness(16)};
   var stack=new StackPanel();var head=new DockPanel();
   var show=MakeButton("표시",()=>TogglePointer(id));show.Padding=new(12,5,12,5);show.MinHeight=32;DockPanel.SetDock(show,Dock.Right);head.Children.Add(show);showButtons.Add(show);
   var badge=MakeButton($"●  마우스 {id}",()=>SelectPointer(id));badge.Foreground=new SolidColorBrush(Color.FromRgb(Palette.Colors[i].R,Palette.Colors[i].G,Palette.Colors[i].B));badge.Style=(Style)FindResource("Quiet");badge.MinHeight=32;badge.Padding=new Thickness(0);badge.HorizontalAlignment=HorizontalAlignment.Left;head.Children.Add(badge);stack.Children.Add(head);
   var status=Text("준비",12,"#7C8AA0");status.Margin=new(0,6,0,12);stack.Children.Add(status);states.Add(status);
   var bottom=new Grid();bottom.ColumnDefinitions.Add(new(){Width=GridLength.Auto});bottom.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});bottom.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
   var use=MakeButton("사용",()=>SelectPointer(id));use.Padding=new(12,8,12,8);bottom.Children.Add(use);
   var picker=MakeButton("실행할 매크로 ▾",()=>{});picker.Click+=(_,_)=>OpenPresetPicker(picker,id);picker.Padding=new(10,8,10,8);picker.Margin=new(8,0,8,0);Grid.SetColumn(picker,1);bottom.Children.Add(picker);presetButtons.Add(picker);
   var run=MakeButton("실행",()=>ToggleSlot(id),true);run.Padding=new(12,8,12,8);Grid.SetColumn(run,2);bottom.Children.Add(run);runButtons.Add(run);
   stack.Children.Add(bottom);card.Child=stack;MouseCards.Children.Add(card);cards.Add(card);
  }
 }
 void BuildHelp()
 {
  string[] titles=["① 1~5 수동 조작","② 동작 순서 만들기","③ 반복과 실행","④ 포인터 표시"];
  string[] descriptions=["Alt+1~5 또는 카드의 사용 버튼으로 수동 마우스를 전환합니다. 모두 동일한 일반 마우스입니다. 번호마다 위치를 기억하며, 전환한 번호의 매크로만 멈춥니다. 다른 번호의 매크로는 유지됩니다.","새로 만들기로 시작해 클릭·드래그·이동·휠·누르기·딜레이를 추가하고 이름을 정해 저장합니다. 불러오기는 검색·스크롤 목록입니다. 이름 수정 후 저장하면 이름도 바뀝니다. 복제와 프리셋 삭제도 지원합니다. 녹화는 F9로 시작·종료합니다.","마우스 카드에서 실행할 프리셋을 각각 선택한 뒤 실행합니다. 같은 프리셋도 독립 실행되며 편집·저장해도 현재 실행은 유지됩니다. 반복과 좌표 기준은 프리셋에 저장합니다. 저장한 위치 기준은 좌표를 그대로 사용하며, 각 마우스 위치 기준은 첫 동작을 마우스 위치로 옮기고 나머지 경로 간격을 유지합니다. F8은 실행 중단입니다.","색상·숫자·기본형을 선택합니다. 기본형은 색상과 숫자 없이 Windows 기본 화살표로 표시합니다. 대기 중인 포인터는 숨깁니다. 카드의 표시 버튼으로 대기 위치를 확인할 수 있고, 실행 중에는 자동으로 표시합니다. 표시를 꺼도 실제 시스템 커서는 유지됩니다. 수동 조작은 일반 Windows 입력을 쓰므로 연동이 필요 없습니다. 동시에 도는 백그라운드 매크로는 대상 앱의 창 메시지 지원 여부에 따라 동작하며, 모든 게임에서 지원되는 것은 아닙니다."];
  for(int i=0;i<titles.Length;i++){var p=new StackPanel{Margin=new(10,4,8,26)};p.Children.Add(new TextBlock{Text=titles[i],FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=Hex("#344BC1")});p.Children.Add(new TextBlock{Text=descriptions[i],TextWrapping=TextWrapping.Wrap,FontSize=14,LineHeight=25,Foreground=Hex("#7B879D"),Margin=new(0,12,0,0)});HelpText.Children.Add(p);}
 }
 void Notify(string message,bool error=false){Status.Text=message+(hotkeyConflicts>0?$" · 단축키 {hotkeyConflicts}개 중복":"");Status.ToolTip=Status.Text;Status.MaxWidth=330;Status.TextTrimming=TextTrimming.CharacterEllipsis;Status.Foreground=Hex(error?"#D85B6C":"#62718A");}
 void DragHeader(object sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Left&&e.Source is not Button)try{DragMove();}catch{}}
 void Minimize(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
 void Exit(object sender,RoutedEventArgs e)=>Close();
 void Help(object sender,RoutedEventArgs e)=>HelpLayer.Visibility=HelpLayer.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;
 void MouseTab(object sender,RoutedEventArgs e)=>SwitchPage(false);
 void MacroTab(object sender,RoutedEventArgs e)=>SwitchPage(true);
 void SwitchPage(bool macro){HelpLayer.Visibility=Visibility.Collapsed;MousePage.Visibility=macro?Visibility.Collapsed:Visibility.Visible;MacroPage.Visibility=macro?Visibility.Visible:Visibility.Collapsed;MouseNav.Background=Hex(macro?"#FFFFFF":"#EEF1FF");MacroNav.Background=Hex(macro?"#EEF1FF":"#FFFFFF");}
 void SetColor(object sender,RoutedEventArgs e)=>SetMode(0);
 void SetNumber(object sender,RoutedEventArgs e)=>SetMode(1);
 void SetNone(object sender,RoutedEventArgs e)=>SetMode(2);
 void SetMode(int value)
 {
  mode=value;presetStore.PointerMode=mode;for(int i=0;i<5;i++){var p=pointers[i];p.Mode=mode;p.Invalidate();}
  SavePresetStore();Button[] b=[ColorMode,NumberMode,NoMode];for(int i=0;i<b.Length;i++){b[i].Background=Hex(i==mode?"#EEF1FF":"#FFFFFF");b[i].Foreground=Hex(i==mode?"#5267EB":"#6C7890");b[i].BorderBrush=Hex(i==mode?"#AFBAF8":"#E3E8F1");}
 }
 void SelectPointer(int id,bool move=true)
 {
  if(id<1||id>5)return;
  if(move&&!bank.Switch(id,!OverStudio())){Notify("번호를 전환하지 못했습니다",true);return;}
  selected=id;if(move)manualUsed=true;StopSlot(id);Native.GetCursorPos(out lastPhysical);
  for(int i=0;i<cards.Count;i++){cards[i].BorderBrush=Hex(i+1==id?"#AAB8FA":"#E7EBF3");cards[i].BorderThickness=new(i+1==id?2:1);}
  Notify($"마우스 {id} 수동 조작");
 }
 bool OverStudio()
 {
  Native.GetCursorPos(out var p);var h=Native.WindowFromPoint(p);Native.GetWindowThreadProcessId(h,out var pid);return pid==Environment.ProcessId;
 }
 void StopSlot(int id){var s=slots[id-1];s.Running=false;if(jobs.TryGetValue(id,out var job))job.Release(s);paused[id]=false;}
 void StopEverything(object sender,RoutedEventArgs e)=>StopAll();
 void StopAll(){if(recording)EndRecording();for(int i=1;i<=5;i++)StopSlot(i);Notify("자동 실행 중단 · 수동 조작 유지");}
 bool Prepare(int id,MacroJob job)
 {
  foreach(var pointer in pointers)pointer.Hide();
  if(MacroPreparation.TryPrepare(job,positioned[id-1]?Clone(memories[id-1]):null,bank.Positions[id-1],out string error))return true;
  slots[id-1].Status=error;Notify($"마우스 {id} · "+error,true);return false;
 }
 void ToggleSlot(int id)
 {
  var s=slots[id-1];if(s.Running||paused.GetValueOrDefault(id)){StopSlot(id);return;}
  var preset=presetStore.Find(presetStore.Assignments[id-1]);
  if(preset==null){Notify($"마우스 {id}에서 실행할 매크로를 선택해 주세요",true);return;}
  var job=presetStore.CreateRun(id)!;
  if(!Prepare(id,job))return;jobs[id]=job;runningNames[id]=preset.Name;StartJob(id,job);
 }
 void StartJob(int id,MacroJob job){job.Reset();var s=slots[id-1];s.NextDue=0;s.Running=true;s.Status="실행 중";paused[id]=false;RefreshPresetCards();Notify($"마우스 {id} · {runningNames.GetValueOrDefault(id,"매크로")} 실행");}
 void RunAll(object sender,RoutedEventArgs e){for(int id=1;id<=5;id++)if(presetStore.Find(presetStore.Assignments[id-1])!=null&&!slots[id-1].Running)ToggleSlot(id);}
 void Tick()
 {
  if(closed)return;long now=time.ElapsedMilliseconds;Native.GetCursorPos(out var point);
  bool changed=point.X!=lastPhysical.X||point.Y!=lastPhysical.Y;
  if(changed&&!OverStudio()&&MacroPreparation.RememberPhysical(slots[selected-1].Running,paused.GetValueOrDefault(selected)))
  {
   bank.Positions[selected-1]=new(point.X,point.Y);
   if(now-lastRemember>=80){if(Routing.Capture(memories[selected-1],point.X,point.Y))positioned[selected-1]=true;lastRemember=now;}
  }
  lastPhysical=point;
  foreach(var s in slots)
  {
   if(s.Running&&now>=s.NextDue){if(jobs.TryGetValue(s.Id,out var j))j.Tick(s,now);if(!s.Running)Notify($"마우스 {s.Id} · "+(s.Status=="매크로 완료"?"재생 완료":s.Status),s.Status!="매크로 완료");s.NextDue=now+16;bank.Positions[s.Id-1]=s.Screen;}
   if(!s.Running&&jobs.TryGetValue(s.Id,out var stopped))stopped.Release(s);

   states[s.Id-1].Text=s.Running&&jobs.TryGetValue(s.Id,out var progress)?$"{runningNames.GetValueOrDefault(s.Id)} · {progress.Completed+1}/{(progress.Repeat==0?"∞":progress.Repeat.ToString())}회 · {Math.Min(progress.Index+1,progress.Steps.Count)}단계":paused.GetValueOrDefault(s.Id)?"일시 정지":s.Status=="매크로 완료"?"매크로 완료":s.Status!="준비"&&s.Status!="실행 중"&&s.Status!="대상 미연결"?s.Status:$"X {bank.Positions[s.Id-1].X} · Y {bank.Positions[s.Id-1].Y}";
   states[s.Id-1].ToolTip=states[s.Id-1].Text;states[s.Id-1].Foreground=Hex(s.Running?"#24A276":"#64748B");runButtons[s.Id-1].Content=s.Running||paused.GetValueOrDefault(s.Id)?"중단":"실행";runButtons[s.Id-1].IsEnabled=s.Running||paused.GetValueOrDefault(s.Id)||presetStore.Find(presetStore.Assignments[s.Id-1])!=null;presetButtons[s.Id-1].IsEnabled=!s.Running&&!paused.GetValueOrDefault(s.Id);
   int i=s.Id-1;bool native=s.Id==selected&&!s.Running;
   if(pointers[i].IsNative!=native){pointers[i].IsNative=native;pointers[i].Invalidate();}
   pointers[i].Place(native?new DPoint(point.X,point.Y):s.Running?s.Screen:bank.Positions[i],native);
   bool visible=s.Running||pointerPinned[i]||(manualUsed&&s.Id==selected);
   if(native&&mode==2)visible=false;
   if(pointers[i].Visible!=visible){if(visible)pointers[i].Show();else pointers[i].Hide();}
  }
  if(now-lastDirtyCheck>=350){lastDirtyCheck=now;UpdatePresetDirty();}int t=TargetSlot.SelectedIndex+1;if(jobs.TryGetValue(t,out var job)){Progress.Maximum=Math.Max(1,job.Steps.Count);Progress.Value=slots[t-1].Status=="매크로 완료"?job.Steps.Count:Math.Min(job.Index,job.Steps.Count);}
 }
 void RegisterInput()
 {
  hwnd=new WindowInteropHelper(this).Handle;if(!inputHookAdded){HwndSource.FromHwnd(hwnd)?.AddHook(Messages);inputHookAdded=true;}
  int conflicts=0;
  for(int i=1;i<=5;i++)if(!Native.RegisterHotKey(hwnd,100+i,Native.ALT|Native.NOREPEAT,(uint)(0x30+i)))conflicts++;
  for(int i=1;i<=5;i++){if(!Native.RegisterHotKey(hwnd,300+i,Native.ALT|Native.CONTROL|Native.NOREPEAT,(uint)(0x30+i)))conflicts++;}
  if(!Native.RegisterHotKey(hwnd,400,Native.NOREPEAT,0x77))conflicts++;
  if(!Native.RegisterHotKey(hwnd,403,Native.NOREPEAT,0x78))conflicts++;
  hotkeyConflicts=conflicts;
  if(conflicts>0)Notify($"단축키 {conflicts}개 중복 · 이전 버전을 종료해 주세요",true);
 }
 nint Messages(nint h,int message,nint wp,nint lp,ref bool handled)
 {
  if(message==Native.ActivateMain){WindowState=WindowState.Normal;Activate();handled=true;}
  if(message==Native.WM_HOTKEY){int id=(int)wp;if(id is >=101 and <=105)SelectPointer(id-100);else if(id is >=301 and <=305)ToggleSlot(id-300);else if(id==400)StopAll();else if(id==403)ToggleRecording();handled=true;}
  return 0;
 }
 void Cleanup(){closed=true;clock.Stop();if(mouseHook!=0)Native.UnhookWindowsHookEx(mouseHook);for(int id=100;id<=405;id++)Native.UnregisterHotKey(hwnd,id);foreach(var s in slots){if(jobs.TryGetValue(s.Id,out var j))j.Release(s);}foreach(var p in pointers)p.Dispose();}

 string ActionName=>((ComboBoxItem)AddType.SelectedItem).Content.ToString()??"클릭";
 void AddStep(object sender,RoutedEventArgs e){if(!Commit())return;var a=Clone(memories[TargetSlot.SelectedIndex]);a.Action=(sender as Button)?.Tag?.ToString()??ActionName;a.DelayBefore=0;a.DelayAfter=a.Action=="딜레이"?0:100;a.Value=a.Action=="스크롤"?120:a.Action is "드래그" or "누르기"?500:a.Action is "이동" or "딜레이"?300:0;a.EndX=a.X+100;a.EndY=a.Y+100;steps.Add(a);RefreshSteps(steps.Count-1);}
 void RefreshSteps(int index=0)
 {
  binding=true;StepList.Items.Clear();for(int i=0;i<steps.Count;i++)
  {
   var a=steps[i];var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(32)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
   row.Children.Add(Text((i+1).ToString("00"),12,"#95A0B6"));var stack=new StackPanel();stack.Children.Add(new TextBlock{Text=a.Action,FontWeight=FontWeights.SemiBold});stack.Children.Add(new TextBlock{Text=Describe(a),FontSize=11,Foreground=Hex("#8C98AC"),Margin=new(0,5,0,0),TextTrimming=TextTrimming.CharacterEllipsis});Grid.SetColumn(stack,1);row.Children.Add(stack);StepList.Items.Add(row);
  }
  StepEditor.IsEnabled=steps.Count>0;binding=false;if(steps.Count>0)StepList.SelectedIndex=Math.Clamp(index,0,steps.Count-1);else{editingStep=null;EditKind.SelectedIndex=-1;}
 }
 static string Describe(MacroStep a)=>a.Action is "딜레이" or "대기"?$"{a.Value:N0} ms 딜레이":$"{a.X}, {a.Y} · 전 {a.DelayBefore} / 후 {a.DelayAfter} ms";
 bool Commit()
 {
  if(binding||editingStep==null)return true;
  if(!int.TryParse(EditX.Text,out int x)||!int.TryParse(EditY.Text,out int y)||!int.TryParse(EditValue.Text,out int value)||!int.TryParse(EditEndX.Text,out int ex)||!int.TryParse(EditEndY.Text,out int ey)||!int.TryParse(DelayBefore.Text,out int before)||!int.TryParse(DelayAfter.Text,out int after)||before<0||after<0||(editingStep.Action!="스크롤"&&value<0)){Notify("좌표와 시간은 숫자로 입력해 주세요",true);return false;}
  if(editingStep.X!=x||editingStep.Y!=y){editingStep.UseMousePosition=false;Routing.Capture(editingStep,x,y,true);}
  editingStep.X=x;editingStep.Y=y;editingStep.Value=value;editingStep.EndX=ex;editingStep.EndY=ey;editingStep.DelayBefore=before;editingStep.DelayAfter=after;return true;
 }
 void CommitEditor(object sender,RoutedEventArgs e){if(!binding&&Commit()){int i=StepList.SelectedIndex;if(i>=0&&StepList.Items[i] is Grid g&&g.Children[1] is StackPanel p&&p.Children[1] is TextBlock t) {t.Text=Describe(steps[i]);((TextBlock)p.Children[0]).Text=steps[i].Action;}}}
 void FillEditor()
 {
  if(editingStep==null)return;binding=true;var a=editingStep;
  EditKind.SelectedIndex=Enumerable.Range(0,EditKind.Items.Count).FirstOrDefault(i=>((ComboBoxItem)EditKind.Items[i]).Content.ToString()==a.Action);
  EditX.Text=a.X.ToString();EditY.Text=a.Y.ToString();EditValue.Text=a.Value.ToString();EditEndX.Text=a.EndX.ToString();EditEndY.Text=a.EndY.ToString();DelayBefore.Text=a.DelayBefore.ToString();DelayAfter.Text=a.DelayAfter.ToString();
  ButtonFields.Visibility=a.Action is "클릭" or "더블 클릭" or "우클릭" or "가운데 클릭" or "누르기" or "드래그"?Visibility.Visible:Visibility.Collapsed;ButtonChoice.IsEnabled=a.Action is not "우클릭" and not "가운데 클릭";ButtonChoice.SelectedIndex=a.Action=="우클릭"||a.Button=="오른쪽"?1:a.Action=="가운데 클릭"||a.Button=="가운데"?2:0;
  PositionFields.Visibility=a.Action is "대기" or "딜레이"?Visibility.Collapsed:Visibility.Visible;EndFields.Visibility=a.Action=="드래그"?Visibility.Visible:Visibility.Collapsed;ValueFields.Visibility=a.Action is "이동" or "드래그" or "누르기" or "대기" or "딜레이" or "스크롤"?Visibility.Visible:Visibility.Collapsed;
  ValueLabel.Text=a.Action=="스크롤"?"휠 변화량 (120 = 한 칸)":a.Action=="이동"?"이동 시간 (ms)":a.Action is "누르기" or "드래그"?"누름 / 드래그 시간 (ms)":"딜레이 시간 (ms)";binding=false;
 }
 void SelectStep(object sender,SelectionChangedEventArgs e){if(binding||StepList.SelectedIndex<0)return;Commit();editingStep=steps[StepList.SelectedIndex];FillEditor();}
 void ChangeButton(object sender,SelectionChangedEventArgs e){if(!binding&&editingStep!=null&&ButtonChoice.SelectedIndex>=0)editingStep.Button=((ComboBoxItem)ButtonChoice.SelectedItem).Content.ToString()!;}
 void ChangeKind(object sender,SelectionChangedEventArgs e){if(binding||editingStep==null||EditKind.SelectedIndex<0)return;editingStep.Action=((ComboBoxItem)EditKind.SelectedItem).Content.ToString()!;if(editingStep.Action=="우클릭")editingStep.Button="오른쪽";else if(editingStep.Action=="가운데 클릭")editingStep.Button="가운데";if(editingStep.Action=="스크롤")editingStep.Value=120;else if(editingStep.Action is "이동" or "드래그" or "누르기" or "딜레이")editingStep.Value=Math.Max(300,editingStep.Value);FillEditor();CommitEditor(sender,e);}
 void UpStep(object s,RoutedEventArgs e)=>MoveStep(-1);
 void DownStep(object s,RoutedEventArgs e)=>MoveStep(1);
 void MoveStep(int delta){if(!Commit())return;int i=StepList.SelectedIndex,j=i+delta;if(i<0||j<0||j>=steps.Count)return;(steps[i],steps[j])=(steps[j],steps[i]);RefreshSteps(j);}
 static MacroStep Clone(MacroStep a)=>JsonSerializer.Deserialize<MacroStep>(JsonSerializer.Serialize(a))!;
 void DuplicateStep(object s,RoutedEventArgs e){if(!Commit()||editingStep==null)return;int i=StepList.SelectedIndex;steps.Insert(i+1,Clone(editingStep));RefreshSteps(i+1);}
 void DeleteStep(object s,RoutedEventArgs e){int i=StepList.SelectedIndex;if(i<0)return;editingStep=null;steps.RemoveAt(i);RefreshSteps(Math.Max(0,i-1));}
 void UsePosition(object s,RoutedEventArgs e){if(editingStep==null)return;var m=memories[TargetSlot.SelectedIndex];int i=steps.IndexOf(editingStep);var a=Clone(m);a.Action=editingStep.Action;a.Value=editingStep.Value;a.DelayBefore=editingStep.DelayBefore;a.DelayAfter=editingStep.DelayAfter;a.EndX=a.X+editingStep.EndX-editingStep.X;a.EndY=a.Y+editingStep.EndY-editingStep.Y;a.UseMousePosition=true;steps[i]=a;editingStep=a;FillEditor();RefreshSteps(i);}
 void MouseTargetChanged(object s,SelectionChangedEventArgs e){}
 void RepeatChanged(object s,RoutedEventArgs e){if(RepeatCount!=null)RepeatCount.IsEnabled=Infinite.IsChecked!=true;}
 MacroJob? ReadJob()
 {
  if(!Commit())return null;if(steps.Count==0){Notify("실행할 동작을 추가해 주세요",true);return null;}
  if(!MacroJob.TryRepeat(Infinite.IsChecked==true,RepeatCount.Text,out int repeat)){Notify("반복 횟수는 1~100000으로 입력해 주세요",true);return null;}
  return new(){Steps=steps.Select(Clone).ToList(),Repeat=repeat,PositionMode=PositionBasis.SelectedIndex==1?"마우스 위치":"저장한 위치"};
 }
 void RunMacro(object sender,RoutedEventArgs e)
 {
  if(!SaveCurrentPreset())return;int id=TargetSlot.SelectedIndex+1;
  if(slots[id-1].Running||paused.GetValueOrDefault(id)){Notify("해당 마우스를 중단한 뒤 프리셋을 적용해 주세요",true);return;}
  presetStore.Assignments[id-1]=editingPresetId;SavePresetStore();RefreshPresetCards();ToggleSlot(id);
 }
 void PauseMacro(object s,RoutedEventArgs e)
 {
  int id=TargetSlot.SelectedIndex+1;var slot=slots[id-1];if(!jobs.TryGetValue(id,out var job)){Notify("실행 중인 매크로가 없습니다");return;}
  if(slot.Running){job.Pause(slot,time.ElapsedMilliseconds);slot.Running=false;paused[id]=true;Notify("일시 정지됨");}
  else if(paused.GetValueOrDefault(id)){slot.Running=true;paused[id]=false;slot.NextDue=0;job.Resume(time.ElapsedMilliseconds);Notify("이어서 실행합니다");}
 }
 void StopMacro(object s,RoutedEventArgs e){StopSlot(TargetSlot.SelectedIndex+1);Notify("매크로 실행 종료");}
}
