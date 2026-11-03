global using System;
global using System.Linq;
global using System.IO;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Threading;
namespace MouseLab5;
internal static class Palette
{
 internal static readonly System.Drawing.Color[] Colors=[System.Drawing.Color.FromArgb(239,68,68),System.Drawing.Color.FromArgb(249,143,44),System.Drawing.Color.FromArgb(210,164,0),System.Drawing.Color.FromArgb(34,166,107),System.Drawing.Color.FromArgb(79,111,246)];
}
internal static class Program
{
 [STAThread]static void Main(string[] args)
 {
  if(args.Length==2&&args[0] is "--verify" or "--receiver" or "--record-area"){System.Windows.Forms.Application.Run(new Verification(args[0]!="--verify",args[1]){Opacity=args[0]=="--record-area"?1:0,ShowInTaskbar=args[0]=="--record-area"});return;}
  using var mutex=new Mutex(true,@"Local\MouseStudio.Next3",out bool fresh);
  if(!fresh){var hwnd=Native.FindWindow(null,"Mouse Studio");if(hwnd!=0)Native.PostMessage(hwnd,Native.ActivateMain,0,0);return;}
  System.Windows.Forms.Application.EnableVisualStyles();
  var app=new System.Windows.Application();app.Run(new StudioWindow());
 }
}
