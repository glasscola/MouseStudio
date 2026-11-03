using System.Drawing;
using System.Diagnostics;

namespace MouseLab5;

internal enum Delivery { Data, WindowMessage }

internal sealed class PointerSlot(int id)
{
    internal int Id { get; } = id;
    internal nint Target;
    internal uint TargetPid;
    internal long ProcessStart;
    internal Point Client;
    internal Point Screen;
    internal string TargetName = "대상 미연결";
    internal Delivery Delivery = Delivery.Data;
    internal bool Running;
    internal int Interval = 300;
    internal long NextDue;
    internal long Sent, ForegroundChanges, CursorChanges;
    internal string Status = "대상 미연결";
    internal bool IsTest;

    internal bool Valid()
    {
        if (Target == 0 || !Native.IsWindow(Target)) return false;
        Native.GetWindowThreadProcessId(Target, out uint pid);
        if (pid != TargetPid) return false;
        try { using var p = Process.GetProcessById((int)pid); return p.StartTime.ToUniversalTime().Ticks == ProcessStart; }
        catch { return false; }
    }

    internal void Bind(nint hwnd, Point client, Delivery method, bool test)
    {
        Running = false; Target = hwnd; Client = client; Delivery = method; IsTest = test;
        Native.GetWindowThreadProcessId(hwnd, out TargetPid);
        using var p = Process.GetProcessById((int)TargetPid);
        ProcessStart = p.StartTime.ToUniversalTime().Ticks;
        TargetName = test ? $"별도 테스트 프로세스 · #{Id}" : Native.Title(Native.GetAncestor(hwnd, 2));
        Status = "준비"; Sent = 0; UpdateScreen();
    }

    internal void UpdateScreen()
    {
        if (Target == 0 || !Native.IsWindow(Target)) return;
        var point = new Native.Pt(Client.X, Client.Y);
        if (Native.ClientToScreen(Target, ref point)) Screen = new(point.X, point.Y);
    }

    internal bool Click()
    {
        if (!Valid()) { Running = false; Status = "대상 종료/변경 · 중지"; return false; }
        if (!Native.GetClientRect(Target, out var rect) || Client.X < 0 || Client.Y < 0 || Client.X >= rect.Right || Client.Y >= rect.Bottom || Client.X > 32767 || Client.Y > 32767)
        { Running = false; Status = "좌표가 대상 영역 밖 · 재지정 필요"; return false; }
        Native.GetCursorPos(out var before);
        var foreground = Native.GetForegroundWindow();
        bool ok;
        if (Delivery == Delivery.Data)
            ok = Native.PostMessage(Target, Native.DataClick, Id, Native.Pack(Client.X, Client.Y));
        else
        {
            bool down = Native.PostMessage(Target, Native.WM_LBUTTONDOWN, 1, Native.Pack(Client.X, Client.Y));
            bool up = down && Native.PostMessage(Target, Native.WM_LBUTTONUP, 0, Native.Pack(Client.X, Client.Y));
            ok = down && up;
            if (down && !up) Native.PostMessage(Target, Native.WM_LBUTTONUP, 0, Native.Pack(Client.X, Client.Y));
        }
        Native.GetCursorPos(out var after);
        if (before.X != after.X || before.Y != after.Y) CursorChanges++;
        if (foreground != Native.GetForegroundWindow()) ForegroundChanges++;
        if (!ok) { Running = false; Status = "메시지 전달 실패 · 권한/대상 확인"; return false; }
        Sent++;
        Status = IsTest ? "전달됨 · 수신 수는 테스트 창 확인" : "전달됨 · 앱 실행 여부는 직접 확인";
        return true;
    }
}
