using System.Windows.Forms;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace MouseLab5;
internal sealed class Verification:Form
{
 readonly Label qaLabel=new(){Dock=DockStyle.Fill,TextAlign=System.Drawing.ContentAlignment.MiddleCenter,Font=new System.Drawing.Font("맑은 고딕",12),Text="녹화 검증 영역"};
 readonly bool receiver;readonly string path,token;Process? child;
 readonly System.Windows.Forms.Timer timer=new(){Interval=20};readonly Stopwatch watch=Stopwatch.StartNew();
 PointerSlot? slot;MacroJob? job;int phase;long due,delayStart;bool basicPassed;JsonObject? pendingReport;int down,up,right,scroll;Native.Pt initialCursor; nint initialForeground;
 readonly int[] repeatCases=[1,2,5,0];int repeatCase,baseline,expected;bool repeatsPassed=true,infiniteKeptRunning;readonly JsonArray repeatResults=[];
 PointerSlot[] parallelSlots=[];MacroJob[] parallelJobs=[];Native.Pt parallelCursor;
 protected override bool ShowWithoutActivation=>true;
 internal Verification(bool receiver,string value)
 {
  this.receiver=receiver;path=value;token=receiver?value:Guid.NewGuid().ToString("N");Text=(receiver?"MouseStudio.Check.":"MouseStudio.Coordinator.")+token;Opacity=0;ShowInTaskbar=false;ClientSize=new(300,220);
  Controls.Add(qaLabel);qaLabel.Enabled=false;
  Shown+=(_,_)=>{Native.GetCursorPos(out initialCursor);initialForeground=Native.GetForegroundWindow();if(!receiver)child=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--receiver "+token){UseShellExecute=false,CreateNoWindow=true});};
  timer.Tick+=(_,_)=>Tick();timer.Start();FormClosed+=(_,_)=>{timer.Dispose();if(child!=null&&!child.HasExited){var h=Native.FindWindow(null,"MouseStudio.Check."+token);if(h!=0)Native.PostMessage(h,0x10,0,0);}child?.Dispose();};
 }
 protected override void WndProc(ref Message m)
 {
  if(receiver){if(m.Msg is Native.WM_LBUTTONDOWN or 0x203){down++;qaLabel.Text=$"누름 {down} / 놓음 {up}";Native.SetProp(Handle,"Down",down);Native.SetProp(Handle,"LastDownAt",(nint)Environment.TickCount64);m.Result=0;return;}if(m.Msg==Native.WM_LBUTTONUP){up++;qaLabel.Text=$"누름 {down} / 놓음 {up}";Native.SetProp(Handle,"Up",up);m.Result=0;return;}if(m.Msg==0x204){right++;Native.SetProp(Handle,"Right",right);m.Result=0;return;}if(m.Msg==0x20a){scroll++;Native.SetProp(Handle,"Scroll",scroll);m.Result=0;return;}if(m.Msg==Native.WM_MOUSEMOVE){m.Result=0;return;}}
  base.WndProc(ref m);
 }
 void Tick()
 {
  if(receiver)return;long now=watch.ElapsedMilliseconds;
  if(phase==0)
  {
   nint h=Native.FindWindow(null,"MouseStudio.Check."+token);if(h==0){if(now>10000)Finish(false,"receiver_timeout");return;}
   slot=new(2);slot.Bind(h,new(80,50),Delivery.WindowMessage,false);
   job=new(){Repeat=3,Steps=[new(){Action="이동",X=80,Y=50,Value=20},new(){Action="대기",Value=40},new(){Action="클릭",X=80,Y=50,Value=30},new(){Action="더블 클릭",X=80,Y=50,Value=30},new(){Action="우클릭",X=80,Y=50,Value=30},new(){Action="스크롤",X=80,Y=50,Value=120},new(){Action="누르기",X=80,Y=50,Value=50},new(){Action="드래그",X=80,Y=50,EndX=120,EndY=90,Value=50}]};
   foreach(var action in job.Steps.Where(a=>a.Action!="대기"))
   {
    var point=new Native.Pt(action.X,action.Y);var end=new Native.Pt(action.EndX,action.EndY);Native.ClientToScreen(h,ref point);Native.ClientToScreen(h,ref end);action.TargetHandle=(long)h;
    if(!Routing.Capture(action,point.X,point.Y,true)){Finish(false,"capture_failed");return;}action.EndX=end.X;action.EndY=end.Y;
   }
   job.Resolver=Routing.Resolve;slot.Running=true;phase=1;
  }
  else if(phase==1)
  {
   job!.Tick(slot!,now);if(!slot!.Running){due=now+200;phase=2;}else if(now>15000)Finish(false,"macro_timeout");
  }
  else if(phase==2&&now>=due)
  {
   int d=(int)Native.GetProp(slot!.Target,"Down"),u=(int)Native.GetProp(slot.Target,"Up"),r=(int)Native.GetProp(slot.Target,"Right"),w=(int)Native.GetProp(slot.Target,"Scroll");
   Native.GetCursorPos(out var cursor);bool ok=d==15&&u==15&&r==3&&w==3&&slot.Client==new System.Drawing.Point(120,90)&&job!.Completed==3;
   basicPassed=ok;
   var click=new MacroStep{Action="클릭",X=80,Y=50,DelayBefore=150,DelayAfter=250,TargetHandle=(long)slot.Target};
   var point=new Native.Pt(80,50);Native.ClientToScreen(slot.Target,ref point);Routing.Capture(click,point.X,point.Y,true);
   job=new(){Repeat=1,Resolver=Routing.Resolve,Steps=[new(){Action="딜레이",Value=200,DelayAfter=0},click]};slot.Running=true;delayStart=Environment.TickCount64;phase=3;return;

  }
  else if(phase==3)
  {
   job!.Tick(slot!,now);if(!slot!.Running){due=now+100;phase=4;}
  }
  else if(phase==4&&now>=due)
  {
   long clickDelay=(long)Native.GetProp(slot!.Target,"LastDownAt")-delayStart;
   bool delays=clickDelay>=350&&Environment.TickCount64-delayStart>=600&&(int)Native.GetProp(slot.Target,"Down")==16;
   Native.GetCursorPos(out var saved);var bank=new PointerBank();var manual=new List<bool>();
   try{for(int i=0;i<5;i++){bank.Positions[i]=new(saved.X+i,saved.Y+i);bool changed=bank.Switch(i+1,false);Native.GetCursorPos(out var actual);manual.Add(changed&&actual.X==saved.X+i&&actual.Y==saved.Y+i);}}finally{Native.SetCursorPos(saved.X,saved.Y);}
   Native.GetCursorPos(out var finalCursor);
   var result=new{passed=basicPassed&&delays&&manual.All(v=>v),mouseActionsPassed=basicPassed,standaloneDelayAndBeforeAfterPassed=delays,actualClickDelayMs=clickDelay,manualSwitch1to5=manual,manualSwitchRequiresTarget=false,separateProcess=child!.Id!=Environment.ProcessId,cursorRestoredAfterManualCheck=finalCursor.X==saved.X&&finalCursor.Y==saved.Y,scope="Real interprocess mouse messages and five native cursor position switches. No game or user app was clicked."};
   pendingReport=JsonSerializer.SerializeToNode(result)!.AsObject();
   var click=new MacroStep{Action="클릭",DelayAfter=300,TargetHandle=(long)slot.Target};var p=new Native.Pt(80,50);Native.ClientToScreen(slot.Target,ref p);Routing.Capture(click,p.X,p.Y,true);
   job=new(){Repeat=1,Resolver=Routing.Resolve,Steps=[click,JsonSerializer.Deserialize<MacroStep>(JsonSerializer.Serialize(click))!]};slot.Running=true;phase=5;

  }

  else if(phase==5)
  {
   job!.Tick(slot!,now);if(job.Phase==3&&job.Index==0){job.Pause(slot!,now);due=now+400;phase=6;}
  }
  else if(phase==6&&now>=due){job!.Resume(now);phase=7;}
  else if(phase==7)
  {
   job!.Tick(slot!,now);if(!slot!.Running){due=now+100;phase=8;}
  }
  else if(phase==8&&now>=due)
  {
   bool pauseOk=(int)Native.GetProp(slot!.Target,"Down")==18&&(int)Native.GetProp(slot.Target,"Up")==18;
   pendingReport!["pauseResumeWithoutDuplicateClickPassed"]=pauseOk;pendingReport["passed"]=pendingReport["passed"]!.GetValue<bool>()&&pauseOk;
   phase=9;
  }
  else if(phase==9)
  {
   baseline=(int)Native.GetProp(slot!.Target,"Down");
   var click=new MacroStep{Action="클릭",X=80,Y=50,DelayAfter=20};
   job=new(){Repeat=repeatCases[repeatCase],Steps=[click]};slot.Running=true;phase=10;
  }
  else if(phase==10)
  {
   job!.Tick(slot!,now);
   if(job.Repeat==0&&job.Completed>=4){infiniteKeptRunning=slot!.Running;expected=(int)slot.Sent;slot.Running=false;job.Release(slot);due=now+120;phase=11;}
   else if(!slot!.Running){expected=job.Repeat;due=now+120;phase=11;}
  }
  else if(phase==11&&now>=due)
  {
   int clicks=(int)Native.GetProp(slot!.Target,"Down")-baseline;
   bool ok=job!.Repeat==0?infiniteKeptRunning&&clicks>=4:clicks==job.Repeat&&job.Completed==job.Repeat;
   repeatsPassed&=ok;repeatResults.Add(new JsonObject{["repeat"]=job.Repeat,["clicks"]=clicks,["passed"]=ok});
   if(++repeatCase<repeatCases.Length){phase=9;return;}
   bool input=MacroJob.TryRepeat(true,"invalid",out int inf)&&inf==0&&MacroJob.TryRepeat(false,"5",out int finite)&&finite==5
     &&!MacroJob.TryRepeat(false,"0",out _)&&!MacroJob.TryRepeat(false,"-1",out _)&&!MacroJob.TryRepeat(false,"abc",out _)&&!MacroJob.TryRepeat(false,"100001",out _);
   pendingReport!["repeatCases"]=repeatResults;pendingReport["repeatInputValidationPassed"]=input;
   pendingReport["infiniteContinuedUntilStopped"]=infiniteKeptRunning;
   pendingReport["passed"]=pendingReport["passed"]!.GetValue<bool>()&&repeatsPassed&&input;
   var presetResult=JsonSerializer.SerializeToNode(PresetChecks.Run())!;pendingReport["presetChecks"]=presetResult;
   pendingReport["passed"]=pendingReport["passed"]!.GetValue<bool>()&&presetResult["passed"]!.GetValue<bool>();
   baseline=(int)Native.GetProp(slot.Target,"Down");Native.GetCursorPos(out parallelCursor);
   parallelSlots=Enumerable.Range(1,5).Select(i=>{var s=new PointerSlot(i);s.Bind(slot.Target,new(80,50),Delivery.WindowMessage,false);s.Running=true;return s;}).ToArray();
   parallelJobs=Enumerable.Range(1,5).Select(i=>new MacroJob{Repeat=i,Steps=Enumerable.Range(0,i).Select(_=>new MacroStep{Action="클릭",X=80,Y=50,DelayAfter=20}).ToList()}).ToArray();
   phase=12;
  }
  else if(phase==12)
  {
   for(int i=0;i<5;i++)if(parallelSlots[i].Running)parallelJobs[i].Tick(parallelSlots[i],now);
   if(parallelSlots.All(s=>!s.Running)){due=now+150;phase=13;}
  }
  else if(phase==13&&now>=due)
  {
   int clicks=(int)Native.GetProp(slot!.Target,"Down")-baseline;Native.GetCursorPos(out var cursor);
   bool ok=clicks==55&&Enumerable.Range(0,5).All(i=>parallelJobs[i].Completed==i+1);
   pendingReport!["fiveDifferentMacrosParallelPassed"]=ok;pendingReport["parallelActualClicks"]=clicks;
   pendingReport["parallelCursorUnchanged"]=cursor.X==parallelCursor.X&&cursor.Y==parallelCursor.Y;
   pendingReport["passed"]=pendingReport["passed"]!.GetValue<bool>()&&ok;
   baseline=(int)Native.GetProp(slot.Target,"Down");up=(int)Native.GetProp(slot.Target,"Up");
   MacroStep Captured(string action,int x,int y)
   {
    var a=new MacroStep{Action=action,X=x,Y=y,EndX=x+20,EndY=y+20,Value=40,DelayAfter=30,TargetHandle=(long)slot.Target};
    var p=new Native.Pt(x,y);var end=new Native.Pt(a.EndX,a.EndY);Native.ClientToScreen(slot.Target,ref p);Native.ClientToScreen(slot.Target,ref end);
    Routing.Capture(a,p.X,p.Y,true);a.EndX=end.X;a.EndY=end.Y;return a;
   }
   var second=new MacroJob{Repeat=3,Steps=[Captured("클릭",80,50),Captured("이동",100,60)]};
   var third=new MacroJob{Repeat=4,PositionMode="마우스 위치",Steps=[Captured("클릭",100,70),Captured("드래그",110,80)]};
   var memory=JsonSerializer.Deserialize<MacroStep>(JsonSerializer.Serialize(third.Steps[0]))!;
   bool remembered=MacroPreparation.TryPrepare(PresetStore.Copy(third),memory,new(memory.X,memory.Y),out _);
   bool prepared2=MacroPreparation.TryPrepare(second,null,new(0,0),out string error2);
   bool prepared3=MacroPreparation.TryPrepare(third,null,new(0,0),out string error3);
   bool movement=!MacroPreparation.RememberPhysical(true,false)&&!MacroPreparation.RememberPhysical(false,true)&&MacroPreparation.RememberPhysical(false,false);
   pendingReport["startPreparation"]=new JsonObject{["savedPosition"]=prepared2,["unusedThirdPointer"]=prepared3,["rememberedWindow"]=remembered,["physicalMovementKeepsMacros"]=movement,["error2"]=error2,["error3"]=error3};
   pendingReport["passed"]=pendingReport["passed"]!.GetValue<bool>()&&prepared2&&prepared3&&remembered&&movement;
   if(!prepared2||!prepared3){WriteReport();return;}
   parallelJobs=[second,third];parallelSlots=[new(2),new(3)];foreach(var s in parallelSlots)s.Running=true;phase=14;
  }
  else if(phase==14)
  {
   for(int i=0;i<2;i++)if(parallelSlots[i].Running)parallelJobs[i].Tick(parallelSlots[i],now);
   if(parallelSlots.All(s=>!s.Running)){due=now+150;phase=15;}
  }
  else if(phase==15&&now>=due)
  {
   int clicks=(int)Native.GetProp(slot!.Target,"Down")-baseline,releases=(int)Native.GetProp(slot.Target,"Up")-up;
   bool ok=clicks==11&&releases==11&&parallelJobs[0].Completed==3&&parallelJobs[1].Completed==4;
   pendingReport!["mouse2And3PreparedParallelPassed"]=ok;pendingReport["mouse2And3ActualPresses"]=clicks;pendingReport["mouse2And3ActualReleases"]=releases;
   pendingReport["passed"]=pendingReport["passed"]!.GetValue<bool>()&&ok;WriteReport();
  }
 }
 void WriteReport(){File.WriteAllText(path,pendingReport!.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));Close();}
 void Finish(bool ok,string reason){File.WriteAllText(path,JsonSerializer.Serialize(new{passed=ok,reason}));Close();}
}
