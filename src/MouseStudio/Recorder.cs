using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
namespace MouseLab5;
public partial class StudioWindow
{
 bool recording;long lastRecorded,lastMove,pressedAt;MacroStep? pressed;
 void RecordToggle(object sender,RoutedEventArgs e)=>ToggleRecording();
 void ToggleRecording()
 {
  if(recording){EndRecording();return;}
  if(!ConfirmEditorChange())return;editingPresetId=null;StopSlot(selected);recording=true;steps.Clear();editingStep=null;lastRecorded=time.ElapsedMilliseconds;lastMove=0;pressed=null;TargetSlot.SelectedIndex=selected-1;
  mouseCallback=RecordMouse;mouseHook=Native.SetWindowsHookEx(14,mouseCallback,Native.GetModuleHandle(null),0);
  if(mouseHook==0){recording=false;Notify("녹화를 시작하지 못했습니다",true);return;}
  RecordButton.Content="■ 녹화 종료 F9";RecordButton.Foreground=Hex("#D65064");Notify("녹화 중 · F9 종료");WindowState=WindowState.Minimized;
 }
 void EndRecording()
 {
  if(!recording)return;recording=false;pressed=null;
  if(mouseHook!=0){Native.UnhookWindowsHookEx(mouseHook);mouseHook=0;}
  RecordButton.Content="● 녹화 F9";RecordButton.Foreground=Hex("#34415A");ProfileName.Text="녹화 "+DateTime.Now.ToString("HH:mm:ss");
  WindowState=WindowState.Normal;Activate();SwitchPage(true);RefreshSteps();Notify($"녹화 완료 · {steps.Count}개 동작");
 }
 MacroStep? RecordedPoint(Native.Pt p)
 {
  var a=new MacroStep{DelayAfter=0};return Routing.Capture(a,p.X,p.Y)?a:null;
 }
 void AppendRecording(MacroStep a,long eventStart,long eventEnd)
 {
  if(steps.Count>=10000)return;
  if(steps.Count>0&&a.Action is "클릭" or "우클릭" or "가운데 클릭")
  {
   var previous=steps[^1];if(previous.Action==a.Action&&Math.Abs(previous.X-a.X)+Math.Abs(previous.Y-a.Y)<=3&&eventStart-lastRecorded<=System.Windows.Forms.SystemInformation.DoubleClickTime){previous.Action="더블 클릭";lastRecorded=eventEnd;return;}
  }
  a.DelayBefore=steps.Count==0?0:(int)Math.Clamp(eventStart-lastRecorded,0,int.MaxValue);a.DelayAfter=0;steps.Add(a);lastRecorded=eventEnd;
 }
 nint RecordMouse(int code,nint wp,nint lp)
 {
  if(code<0||!recording||closed)return Native.CallNextHookEx(mouseHook,code,wp,lp);
  try
  {
   var data=Marshal.PtrToStructure<Native.MouseEvent>(lp);int message=(int)wp;long now=time.ElapsedMilliseconds;
   if(message is 0x201 or 0x204 or 0x207)
   {
    pressed=RecordedPoint(data.Point);if(pressed!=null){pressed.Button=message==0x204?"오른쪽":message==0x207?"가운데":"왼쪽";pressedAt=now;}
   }
   else if(message is 0x202 or 0x205 or 0x208)
   {
    if(pressed!=null)
    {
     var a=pressed;int distance=Math.Abs(data.Point.X-a.X)+Math.Abs(data.Point.Y-a.Y);
     a.Action=distance>5?"드래그":now-pressedAt>450?"누르기":a.Button=="오른쪽"?"우클릭":a.Button=="가운데"?"가운데 클릭":"클릭";
     a.EndX=data.Point.X;a.EndY=data.Point.Y;a.Value=a.Action is "드래그" or "누르기"?(int)Math.Clamp(now-pressedAt,1,int.MaxValue):0;
     AppendRecording(a,pressedAt,now);pressed=null;
    }
   }
   else if(message==0x20a)
   {
    var a=RecordedPoint(data.Point);if(a!=null){a.Action="스크롤";a.Value=(short)(data.Data>>16);AppendRecording(a,now,now);}
   }
   else if(message==Native.WM_MOUSEMOVE&&pressed==null&&now-lastMove>=100)
   {
    var a=RecordedPoint(data.Point);if(a!=null){a.Action="이동";a.Value=1;AppendRecording(a,now,now);lastMove=now;}
   }
  }
  catch{ /* Recording never suppresses physical input. */ }
  return Native.CallNextHookEx(mouseHook,code,wp,lp);
 }
}
