using System.Drawing;
namespace MouseLab5;

internal static class MacroPreparation
{
 // Bind against the remembered window identity; the settings window may cover its screen position.
 // An unused pointer inherits the preset's first position instead of an arbitrary startup position.
 internal static bool TryPrepare(MacroJob job,MacroStep? memory,Point position,out string error)
 {
  error="";var actions=job.Steps.Where(a=>a.Action is not "딜레이" and not "대기").ToList();
  if(job.PositionMode=="마우스 위치"&&actions.Count>0)
  {
   var first=actions[0];var anchor=memory??first;var resolved=new PointerSlot(0);
   if(!Routing.Resolve(resolved,anchor)){error="대상 창을 찾지 못했습니다. 창을 열거나 다시 녹화해 주세요";return false;}
   Point origin=memory==null?resolved.Screen:position;
   int dx=origin.X-first.X,dy=origin.Y-first.Y;
   foreach(var a in actions)
   {
    a.X+=dx;a.Y+=dy;a.EndX+=dx;a.EndY+=dy;a.TargetHandle=(long)resolved.Target;
    if(!Routing.Capture(a,a.X,a.Y,true)){error="기억한 마우스 위치를 적용하지 못했습니다";return false;}
    a.UseMousePosition=false;
   }
  }
  foreach(var a in actions)
  {
   a.UseMousePosition=false;var target=new PointerSlot(0);
   if(!Routing.Resolve(target,a)){error="대상 창을 찾지 못했습니다. 창을 열거나 다시 녹화해 주세요";return false;}
   if(!Native.GetClientRect(target.Target,out var bounds)||target.Client.X<0||target.Client.Y<0||target.Client.X>=bounds.Right||target.Client.Y>=bounds.Bottom)
   {error="저장한 위치가 대상 창 밖입니다. 위치 기준을 확인해 주세요";return false;}
  }
  job.Resolver=Routing.Resolve;return true;
 }
 internal static bool RememberPhysical(bool running,bool paused)=>!running&&!paused;
}
