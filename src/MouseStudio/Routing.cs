using System.Drawing;
namespace MouseLab5;
internal static class Routing
{
 internal static bool Capture(MacroStep a,int x,int y,bool preserve=false)
 {
  nint h=preserve&&(a.TargetHandle!=0)&&Native.IsWindow((nint)a.TargetHandle)?(nint)a.TargetHandle:Native.WindowFromPoint(new(x,y));
  Native.GetWindowThreadProcessId(h,out var pid);if(h==0||pid==Environment.ProcessId)return false;
  string cls=Native.Class(Native.GetAncestor(h,2));if(cls is "Progman" or "WorkerW" or "Shell_TrayWnd")return false;
  var p=new Native.Pt(x,y);if(!Native.ScreenToClient(h,ref p))return false;
  try{using var process=Process.GetProcessById((int)pid);a.X=x;a.Y=y;a.TargetHandle=(long)h;a.TargetPid=(int)pid;a.TargetStart=process.StartTime.ToUniversalTime().Ticks;a.TargetProcess=process.ProcessName;a.TargetTitle=Native.Title(Native.GetAncestor(h,2));a.ClientX=p.X;a.ClientY=p.Y;a.TargetClass=Native.Class(h);var rp=new Native.Pt(x,y);Native.ScreenToClient(Native.GetAncestor(h,2),ref rp);a.RootX=rp.X;a.RootY=rp.Y;return true;}catch{return false;}
 }
 internal static bool Resolve(PointerSlot s,MacroStep a)
 {
  nint h=(nint)a.TargetHandle;Native.GetWindowThreadProcessId(h,out var pid);
  bool fallback=false;bool valid=Native.IsWindow(h)&&pid==a.TargetPid&&(a.TargetClass.Length==0||Native.Class(h)==a.TargetClass);
  if(valid){try{using var p=Process.GetProcessById((int)pid);valid=p.StartTime.ToUniversalTime().Ticks==a.TargetStart;}catch{valid=false;}}
  if(!valid)
  {
   if(a.TargetTitle.Length>0){h=Native.FindWindow(null,a.TargetTitle);Native.GetWindowThreadProcessId(h,out pid);try{using var p=Process.GetProcessById((int)pid);valid=h!=0&&p.ProcessName==a.TargetProcess;fallback=valid;}catch{valid=false;}}
   else if(Capture(a,a.X,a.Y)){h=(nint)a.TargetHandle;pid=(uint)a.TargetPid;valid=true;}
  }
  if(!valid||pid==Environment.ProcessId){s.Status="마우스 위치나 좌표를 확인해 주세요";return false;}
  try
  {
   using var process=Process.GetProcessById((int)pid);s.Target=h;s.TargetPid=pid;s.ProcessStart=process.StartTime.ToUniversalTime().Ticks;s.Delivery=Delivery.WindowMessage;s.TargetName=Native.Title(Native.GetAncestor(h,2));
   s.Client=fallback?new(a.RootX,a.RootY):new(a.ClientX,a.ClientY);s.UpdateScreen();return true;
  }
  catch{return false;}
 }
}
internal sealed class PointerBank
{
 internal readonly Point[] Positions=new Point[5];internal int Selected=1;
 internal PointerBank()
 {
  Native.GetCursorPos(out var p);Positions[0]=new(p.X,p.Y);
  var area=System.Windows.Forms.Screen.FromPoint(Positions[0]).WorkingArea;
  int x=Math.Clamp(p.X,area.Left+12,Math.Max(area.Left+12,area.Right-200));int y=Math.Clamp(p.Y,area.Top+12,Math.Max(area.Top+12,area.Bottom-150));
  for(int i=1;i<5;i++)Positions[i]=new(x+60+((i-1)%2)*65,y+35+((i-1)/2)*60);
 }
 internal bool Switch(int id,bool remember=true)
 {
  if(id<1||id>5)return false;Native.GetCursorPos(out var p);if(remember)Positions[Selected-1]=new(p.X,p.Y);
  var destination=Positions[id-1];if(!Native.SetCursorPos(destination.X,destination.Y))return false;Selected=id;return true;
 }
}
