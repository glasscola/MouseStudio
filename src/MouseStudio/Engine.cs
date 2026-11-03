using System.Drawing;
namespace MouseLab5;
internal sealed class MacroStep
{
 public bool UseMousePosition{get;set;}
 public string Action{get;set;}="클릭";public string Button{get;set;}="왼쪽";
 public int X{get;set;} public int Y{get;set;} public int Value{get;set;}
 public int EndX{get;set;} public int EndY{get;set;}
 public int DelayBefore{get;set;} public int DelayAfter{get;set;}=100;
 public long TargetHandle{get;set;} public int TargetPid{get;set;} public long TargetStart{get;set;}
 public string TargetTitle{get;set;}="";public string TargetProcess{get;set;}="";
 public int ClientX{get;set;} public int ClientY{get;set;}
 public int RootX{get;set;} public int RootY{get;set;} public string TargetClass{get;set;}="";
}
internal sealed class MacroJob
{
 public List<MacroStep> Steps{get;set;}=[];public int Repeat{get;set;}=1;
 public string PositionMode{get;set;}="저장한 위치";
 internal static bool TryRepeat(bool infinite,string text,out int repeat)
 {repeat=0;if(infinite)return true;return int.TryParse(text,out repeat)&&repeat>=1&&repeat<=100000;}
 internal int Index,Completed,Phase;internal long Due;long pausedAt;internal bool Held;
 internal Func<PointerSlot,MacroStep,bool>? Resolver;
 MacroStep? current;bool moving,dragging;long motionStart,motionEnd;Point from,to;int upMessage=Native.WM_LBUTTONUP;nint heldWindow;
 internal void Reset(){Index=Completed=Phase=0;Due=0;Held=false;current=null;moving=false;pausedAt=0;}
 internal void Release(PointerSlot s){if(Held&&Native.IsWindow(heldWindow))Native.PostMessage(heldWindow,upMessage,0,Native.Pack(s.Client.X,s.Client.Y));Held=false;}
 internal void Pause(PointerSlot s,long now){pausedAt=now;if(Held){Release(s);Phase=0;moving=false;Due=now;}}
 internal void Resume(long now){long delta=Math.Max(0,now-pausedAt);Due+=delta;if(moving){motionStart+=delta;motionEnd+=delta;}pausedAt=0;}
 void Fail(PointerSlot s,string message){Release(s);s.Running=false;s.Status=message;}
 static (int down,int up,int dbl,int flag) Messages(string button)=>button switch{"오른쪽"=>(0x204,0x205,0x206,2),"가운데"=>(0x207,0x208,0x209,16),_=>(0x201,0x202,0x203,1)};
 bool Pair(PointerSlot s,(int down,int up,int dbl,int flag) m,int first=0){bool ok=Native.PostMessage(s.Target,first==0?m.down:first,m.flag,Native.Pack(s.Client.X,s.Client.Y));if(ok)ok=Native.PostMessage(s.Target,m.up,0,Native.Pack(s.Client.X,s.Client.Y));if(ok)s.Sent++;return ok;}
 void After(long now){Phase=3;Due=now+Math.Max(0,current!.DelayAfter);}
 internal void Tick(PointerSlot s,long now)
 {
  if(now<Due)return;
  if(Phase==2)
  {
   if(!s.Valid()){Fail(s,"대상이 닫혔습니다");return;}
   if(moving){double t=Math.Clamp((double)(now-motionStart)/Math.Max(1,motionEnd-motionStart),0,1);s.Client=new((int)Math.Round(from.X+(to.X-from.X)*t),(int)Math.Round(from.Y+(to.Y-from.Y)*t));s.UpdateScreen();var msg=Messages(current!.Button);if(!Native.PostMessage(s.Target,Native.WM_MOUSEMOVE,dragging?msg.flag:0,Native.Pack(s.Client.X,s.Client.Y))){Fail(s,"이동 전달 실패");return;}if(t<1){Due=now+16;return;}}
   if(Held)Release(s);moving=false;After(now);return;
  }
  if(Phase==3){Index++;Phase=0;}
  if(Phase==0)
  {
   if(Steps.Count==0){Fail(s,"동작이 없습니다");return;}
   if(Index>=Steps.Count){Completed++;Index=0;if(Repeat>0&&Completed>=Repeat){Release(s);s.Running=false;s.Status="매크로 완료";return;}}
   current=Steps[Index];Phase=1;Due=now+Math.Max(0,current.DelayBefore);if(current.DelayBefore>0)return;
  }
  var a=current!;if(a.Action is "딜레이" or "대기"){Phase=3;Due=now+Math.Max(0,a.Value)+Math.Max(0,a.DelayAfter);return;}
  Point previous=s.Screen;
  if(Resolver!=null){if(!Resolver(s,a)){Fail(s,"마우스 위치나 좌표를 확인해 주세요");return;}}else s.Client=new(a.X,a.Y);
  if(!s.Valid()||!Native.GetClientRect(s.Target,out var r)){Fail(s,"대상 입력을 준비하지 못했습니다");return;}
  if(s.Client.X<0||s.Client.Y<0||s.Client.X>=r.Right||s.Client.Y>=r.Bottom){Fail(s,"저장한 위치가 창 밖입니다");return;}
  s.UpdateScreen();var m=Messages(a.Action=="우클릭"?"오른쪽":a.Action=="가운데 클릭"?"가운데":a.Button);bool ok=true;
  if(a.Action is "클릭" or "우클릭" or "가운데 클릭")ok=Pair(s,m);
  else if(a.Action=="더블 클릭")ok=Pair(s,m)&&Pair(s,m,m.dbl);
  else if(a.Action=="스크롤"){var p=new Native.Pt(s.Client.X,s.Client.Y);Native.ClientToScreen(s.Target,ref p);ok=Native.PostMessage(s.Target,0x20a,(nint)((Math.Clamp(a.Value,-32768,32767)&0xffff)<<16),Native.Pack(p.X,p.Y));}
  else if(a.Action is "누르기" or "드래그")
  {
   ok=Native.PostMessage(s.Target,m.down,m.flag,Native.Pack(s.Client.X,s.Client.Y));if(ok){Held=true;heldWindow=s.Target;upMessage=m.up;}
   if(a.Action=="누르기"&&ok){Phase=2;moving=false;Due=now+Math.Max(1,a.Value);return;}
  }
  if(!ok){Fail(s,"입력 전달 실패");return;}
  if(a.Action is "이동" or "드래그")
  {
   dragging=a.Action=="드래그";from=s.Client;
   if(dragging)to=Resolver==null?new(a.EndX,a.EndY):new(s.Client.X+a.EndX-a.X,s.Client.Y+a.EndY-a.Y);
   else{to=s.Client;var p=new Native.Pt(previous.X,previous.Y);Native.ScreenToClient(s.Target,ref p);from=new(Math.Clamp(p.X,0,r.Right-1),Math.Clamp(p.Y,0,r.Bottom-1));}
   s.Client=from;motionStart=now;motionEnd=now+Math.Max(1,a.Value);moving=true;Phase=2;Due=now+1;return;
  }
  s.Status=$"{Completed+1}회차 · {Index+1}/{Steps.Count}";After(now);
 }
}
