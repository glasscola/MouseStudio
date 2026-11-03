using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace MouseLab5;

internal sealed class PointerOverlay(int id, Color color) : Form
{
    private bool selected;
    internal bool IsNative;
    internal int Mode=1;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000020 | 0x00000080 | 0x00080000; return cp; }
    }
    internal void Prepare()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        BackColor = Color.Magenta; TransparencyKey = Color.Magenta;
        StartPosition = FormStartPosition.Manual; Size = new(84, 94);
        DoubleBuffered = true; Text = $"MouseLab5 Pointer {id}";
    }
    internal void Place(Point point, bool active)
    {
        Location = IsNative ? new(point.X + 16, point.Y + 18) : new(point.X - 2, point.Y - 2);
        if (selected != active) { selected = active; Invalidate(); }
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x84) { m.Result = -1; return; }
        if (m.Msg == 0x21) { m.Result = 3; return; }
        base.WndProc(ref m);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if(Mode==2){if(!IsNative)Cursors.Arrow.Draw(e.Graphics,new Rectangle(2,2,Cursors.Arrow.Size.Width,Cursors.Arrow.Size.Height));return;}
        if (!IsNative)
        {
            Point[] arrow = [new(2, 2), new(3, 33), new(12, 25), new(19, 39), new(26, 35), new(19, 22), new(31, 21)];
            using var fill = new SolidBrush(color); using var border = new Pen(Color.FromArgb(10, 16, 27), 2);
            e.Graphics.FillPolygon(fill, arrow); e.Graphics.DrawPolygon(border, arrow);
        }
        if(Mode==1)
        {
            using var font=new Font("Segoe UI",16,FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics,id.ToString(),font,new Rectangle(IsNative?0:28,IsNative?0:34,52,52),color,TextFormatFlags.NoPadding);
        }
        else if(IsNative)
        {
            using var b=new SolidBrush(color);e.Graphics.FillPolygon(b,[new Point(2,2),new Point(2,24),new Point(19,16)]);
        }
    }
}
