using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

// 자동 종료 타이머 v5 — UI 간소화 / Pretendard 내장 / 툴팁 안내

// ───────── 폰트 ─────────
public static class Fonts
{
    private static PrivateFontCollection pfc = new PrivateFontCollection();
    private static FontFamily famR, famSB;
    private static List<IntPtr> blocks = new List<IntPtr>();

    public static bool UsingPretendard = false;

    // 내장 폰트를 GDI(시스템)에도 등록 → WinForms 컨트롤/TextRenderer가 이름으로 찾아 선명하게 렌더
    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, out uint pcFonts);

    static Fonts()
    {
        famR = Load("PretendardR.ttf", false);
        famSB = Load("PretendardSB.ttf", true);
        // 내장 폰트가 실제로 '측정/렌더링'까지 되는지 자가진단.
        // 하나라도 실패하면 시스템 폰트로 완전히 되돌려 어떤 환경에서도 죽지 않게 함.
        if (!SelfTest(famR) || !SelfTest(famSB))
        {
            famR = null;
            famSB = null;
            UsingPretendard = false;
        }
        else UsingPretendard = true;
    }

    private static bool SelfTest(FontFamily fam)
    {
        if (fam == null) return false;
        try
        {
            using (Font f = new Font(fam, 10F, FontStyle.Regular, GraphicsUnit.Point))
            using (Bitmap bmp = new Bitmap(8, 8))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                float h = f.GetHeight(g);
                if (h <= 0) return false;
                SizeF sz = g.MeasureString("가A1", f);
                if (sz.Width <= 0) return false;
                g.DrawString("가A1", f, Brushes.Black, 0, 0);
            }
            return true;
        }
        catch { return false; }
    }

    private static FontFamily Load(string res, bool semi)
    {
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(res))
            {
                if (s == null) return null;
                byte[] data = new byte[s.Length];
                s.Read(data, 0, data.Length);
                IntPtr p = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, p, data.Length);
                blocks.Add(p);
                pfc.AddMemoryFont(p, data.Length);
                try { uint c; AddFontMemResourceEx(p, (uint)data.Length, IntPtr.Zero, out c); } catch { }
            }
            FontFamily best = null;
            foreach (FontFamily f in pfc.Families)
            {
                bool isSemi = f.Name.IndexOf("Semi", StringComparison.OrdinalIgnoreCase) >= 0;
                if (semi == isSemi) best = f;
            }
            return best;
        }
        catch { return null; }
    }

    private static Font Make(FontFamily fam, float size, FontStyle style)
    {
        if (fam == null) return null;
        try
        {
            if (!fam.IsStyleAvailable(style))
            {
                if (fam.IsStyleAvailable(FontStyle.Regular)) style = FontStyle.Regular;
                else if (fam.IsStyleAvailable(FontStyle.Bold)) style = FontStyle.Bold;
                else return null;
            }
            // 픽셀 단위로 생성 → 디스플레이 배율(DPI)이 달라도 크기/선명도 일정
            return new Font(fam, size * PX * SCALE, style, GraphicsUnit.Pixel);
        }
        catch { return null; }
    }
    public const float PX = 4f / 3f;    // pt → px (96DPI 기준 동일 크기)
    public const float SCALE = 0.94f;   // 전체 UI 축소에 맞춘 글자 크기 비율
    // 지정한 '픽셀' 크기로 세미볼드 (캐시 — 절대 Dispose 하지 말 것)
    private static System.Collections.Generic.Dictionary<int, Font> semiPxCache = new System.Collections.Generic.Dictionary<int, Font>();
    public static Font SemiPx(float px)
    {
        int key = (int)Math.Round(px * 10);
        Font c;
        if (semiPxCache.TryGetValue(key, out c) && c != null) return c;
        Font made = null;
        try
        {
            if (famSB != null) made = new Font(famSB, px, FontStyle.Regular, GraphicsUnit.Pixel);
            else if (famR != null) made = new Font(famR, px, FontStyle.Bold, GraphicsUnit.Pixel);
        }
        catch { }
        if (made == null) { try { made = new Font("맑은 고딕", px, FontStyle.Bold, GraphicsUnit.Pixel); } catch { } }
        if (made == null) { try { made = new Font(FontFamily.GenericSansSerif, px, FontStyle.Bold, GraphicsUnit.Pixel); } catch { } }
        if (made != null) semiPxCache[key] = made;
        return made;
    }

    // 시스템 폰트 후보를 차례로 시도, 전부 실패하면 기본 폰트
    public static Font System(float size, FontStyle style)
    {
        string[] cands = { "맑은 고딕", "Malgun Gothic", "Segoe UI", "Tahoma", "DejaVu Sans", "Arial" };
        foreach (string n in cands)
        {
            try
            {
                Font f = new Font(n, size * PX * SCALE, style, GraphicsUnit.Pixel);
                if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return f;
                return f;
            }
            catch { }
        }
        try { return new Font(FontFamily.GenericSansSerif, size * PX * SCALE, style, GraphicsUnit.Pixel); }
        catch { try { return new Font(FontFamily.GenericSansSerif, size * PX * SCALE, style); } catch { return null; } }
    }

    public static Font System(float size) { return System(size, FontStyle.Regular); }

    public static Font Regular(float size)
    {
        Font f = Make(famR, size, FontStyle.Regular);
        if (f != null) return f;
        return System(size, FontStyle.Regular);
    }

    public static Font Semi(float size)
    {
        Font f = Make(famSB, size, FontStyle.Regular);
        if (f != null) return f;
        f = Make(famR, size, FontStyle.Bold);
        if (f != null) return f;
        return System(size, FontStyle.Bold);
    }
}

// ───────── 테마 (라이트/다크) ─────────
// 모든 색은 여기서만 정한다. 컨트롤은 그릴 때 Theme.* 를 읽거나, 생성 시 담아둔 색을 ApplyThemeToTree 로 바꿔 받는다.
public static class Theme
{
    public static bool Dark = false;
    public static Color Bg, Card, Ink, Muted, Accent, Danger, Ok, Bezel, Island;
    public static Color Border, PillBorder, GhostFill, GhostHover, GhostDown, GhostText, GhostBorder;
    public static Color SegFill, SegHover, SegDown, SegText;
    public static Color DisFill, DisBorder, DisText;
    public static Color WinHover, WinDown, WinIcon, CloseHover, CloseDown;
    public static Color RowSel, RowSelSoft, OffText, OffSub, Hint, Chevron, ToggleOff, Knob, ToggleLabel, ToggleDisabled;
    public static Color CancelFill, CancelHover, CancelDown, CancelText, CancelBorder;
    public static Color HomeBar, ScreenEdge, VerText, BetaBg, IslandText, BadgeOffFill;
    public static Color GearActiveFill, GearIcon;

    static Theme() { Apply(false); }

    public static void Apply(bool dark)
    {
        Dark = dark;
        if (!dark)
        {
            Bg = Color.FromArgb(247, 248, 250); Card = Color.White;
            Ink = Color.FromArgb(28, 32, 40); Muted = Color.FromArgb(130, 138, 150);
            Accent = Color.FromArgb(56, 118, 224); Danger = Color.FromArgb(214, 68, 68); Ok = Color.FromArgb(34, 154, 88);
            Bezel = Color.FromArgb(12, 13, 16); Island = Color.Black;
            Border = Color.FromArgb(184, 191, 204); PillBorder = Color.FromArgb(180, 188, 202);
            GhostFill = Color.FromArgb(244, 246, 250); GhostHover = Color.FromArgb(234, 238, 245); GhostDown = Color.FromArgb(222, 227, 235);
            GhostText = Color.FromArgb(52, 60, 74); GhostBorder = Color.FromArgb(182, 190, 204);
            SegFill = Color.FromArgb(236, 238, 243); SegHover = Color.FromArgb(228, 231, 238); SegDown = Color.FromArgb(220, 224, 230); SegText = Color.FromArgb(96, 104, 116);
            DisFill = Color.FromArgb(234, 237, 242); DisBorder = Color.FromArgb(186, 193, 206); DisText = Color.FromArgb(128, 135, 148);
            WinHover = Color.FromArgb(232, 235, 241); WinDown = Color.FromArgb(220, 224, 232); WinIcon = Color.FromArgb(112, 118, 130);
            CloseHover = Color.FromArgb(250, 226, 226); CloseDown = Color.FromArgb(244, 210, 210);
            RowSel = Color.FromArgb(224, 235, 252); RowSelSoft = Color.FromArgb(232, 240, 252);
            OffText = Color.FromArgb(160, 166, 178); OffSub = Color.FromArgb(178, 184, 195); Hint = Color.FromArgb(146, 153, 166);
            Chevron = Color.FromArgb(158, 165, 178); ToggleOff = Color.FromArgb(206, 210, 218); Knob = Color.White;
            ToggleLabel = Color.FromArgb(70, 78, 92); ToggleDisabled = Color.FromArgb(228, 230, 236);
            CancelFill = Color.FromArgb(238, 240, 245); CancelHover = Color.FromArgb(228, 231, 238); CancelDown = Color.FromArgb(216, 220, 228);
            CancelText = Color.FromArgb(74, 82, 96); CancelBorder = Color.FromArgb(196, 203, 215);
            HomeBar = Color.FromArgb(70, 75, 86); ScreenEdge = Color.FromArgb(120, 214, 219, 228);
            VerText = Color.FromArgb(168, 174, 184); BetaBg = Color.FromArgb(230, 238, 252); IslandText = Color.FromArgb(236, 240, 248);
            BadgeOffFill = Color.FromArgb(240, 242, 246);
            GearActiveFill = Color.FromArgb(226, 233, 246); GearIcon = Color.FromArgb(126, 132, 144);
        }
        else
        {
            Bg = Color.FromArgb(26, 28, 34); Card = Color.FromArgb(40, 43, 52);
            Ink = Color.FromArgb(236, 238, 243); Muted = Color.FromArgb(150, 156, 168);
            Accent = Color.FromArgb(82, 142, 240); Danger = Color.FromArgb(232, 92, 92); Ok = Color.FromArgb(62, 190, 112);
            Bezel = Color.FromArgb(6, 6, 8); Island = Color.Black;
            Border = Color.FromArgb(78, 83, 96); PillBorder = Color.FromArgb(80, 85, 98);
            GhostFill = Color.FromArgb(46, 50, 60); GhostHover = Color.FromArgb(56, 61, 72); GhostDown = Color.FromArgb(68, 73, 86);
            GhostText = Color.FromArgb(222, 226, 234); GhostBorder = Color.FromArgb(82, 87, 100);
            SegFill = Color.FromArgb(44, 48, 58); SegHover = Color.FromArgb(54, 58, 70); SegDown = Color.FromArgb(64, 69, 82); SegText = Color.FromArgb(176, 182, 194);
            DisFill = Color.FromArgb(40, 43, 52); DisBorder = Color.FromArgb(60, 64, 76); DisText = Color.FromArgb(118, 124, 136);
            WinHover = Color.FromArgb(50, 54, 64); WinDown = Color.FromArgb(62, 66, 78); WinIcon = Color.FromArgb(160, 166, 178);
            CloseHover = Color.FromArgb(92, 48, 52); CloseDown = Color.FromArgb(120, 56, 60);
            RowSel = Color.FromArgb(52, 64, 92); RowSelSoft = Color.FromArgb(48, 58, 82);
            OffText = Color.FromArgb(112, 118, 130); OffSub = Color.FromArgb(98, 104, 116); Hint = Color.FromArgb(118, 124, 138);
            Chevron = Color.FromArgb(150, 156, 170); ToggleOff = Color.FromArgb(90, 96, 110); Knob = Color.White;
            ToggleLabel = Color.FromArgb(214, 218, 226); ToggleDisabled = Color.FromArgb(58, 62, 74);
            CancelFill = Color.FromArgb(48, 52, 62); CancelHover = Color.FromArgb(58, 62, 74); CancelDown = Color.FromArgb(70, 75, 88);
            CancelText = Color.FromArgb(214, 218, 226); CancelBorder = Color.FromArgb(66, 70, 82);
            HomeBar = Color.FromArgb(150, 155, 166); ScreenEdge = Color.FromArgb(120, 60, 64, 76);
            VerText = Color.FromArgb(120, 126, 140); BetaBg = Color.FromArgb(40, 58, 92); IslandText = Color.FromArgb(236, 240, 248);
            BadgeOffFill = Color.FromArgb(52, 56, 66);
            GearActiveFill = Color.FromArgb(44, 58, 86); GearIcon = Color.FromArgb(150, 156, 170);
        }
    }

    // 현재 팔레트 스냅샷 (이름 → 색). 테마 전환 시 컨트롤에 담긴 옛 색을 새 색으로 바꾸는 데 사용.
    public static Dictionary<string, Color> Snapshot()
    {
        Dictionary<string, Color> d = new Dictionary<string, Color>();
        foreach (System.Reflection.FieldInfo f in typeof(Theme).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            if (f.FieldType == typeof(Color)) d[f.Name] = (Color)f.GetValue(null);
        return d;
    }
}

// ───────── 둥근 앱 버튼 ─────────
public class AppButton : Control
{
    public Color Fill = Color.FromArgb(60, 120, 220);
    public Color FillHover = Color.FromArgb(76, 136, 236);
    public Color FillDown = Color.FromArgb(46, 104, 200);
    public Color TextColor = Color.White;
    public Color BorderColor = Color.Empty;
    public int Radius = 10;
    public int IconKind = 0;   // 0=없음 1=팝업 2=종 3=둘다 (글자 앞 아이콘)
    private bool hover = false, down = false;

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    public AppButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        Cursor = Cursors.Hand;
        Font = Fonts.Semi(10F);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    public static GraphicsPath Round(Rectangle r, int rad)
    {
        GraphicsPath p = new GraphicsPath();
        if (rad <= 0) { p.AddRectangle(r); return p; }
        int d = rad * 2;
        if (d > r.Width) d = r.Width;
        if (d > r.Height) d = r.Height;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;   // 1px 선이 두 픽셀에 반씩 걸쳐 흐려지는 것 방지
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Color bg = Parent != null ? Parent.BackColor : SystemColors.Control;
        g.Clear(bg);

        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        Color f = Fill;
        if (!Enabled) f = Theme.DisFill;
        else if (down) f = FillDown;
        else if (hover) f = FillHover;

        using (GraphicsPath path = Round(r, Radius))
        {
            using (SolidBrush b = new SolidBrush(f)) g.FillPath(b, path);
            // 비활성화일 때도 테두리를 그린다 (예전엔 안 그려서 버튼 윤곽이 사라져 보였음)
            Color bc = Enabled ? BorderColor : Theme.DisBorder;
            if (bc != Color.Empty)
                using (Pen pen = new Pen(bc, 1F)) g.DrawPath(pen, path);
        }

        Color tc = Enabled ? TextColor : Theme.DisText;
        if (IconKind > 0)
        {   // 아이콘 + 글자를 한 덩어리로 가운데 정렬 (설정 알림방식 버튼용)
            int nseg = IconKind == 3 ? 2 : 1, iw = 14, gp = 3, tgap = 6;
            int iconsW = nseg * iw + (nseg - 1) * gp;
            Size ts = TextRenderer.MeasureText(g, Text, Font, new Size(400, Height), TextFormatFlags.NoPadding);
            int groupW = iconsW + tgap + ts.Width, gx = (Width - groupW) / 2, cy = Height / 2, ix = gx;
            using (Pen pn = new Pen(tc, 1.5F) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (IconKind == 1 || IconKind == 3) { DrawPopupIcon(g, pn, ix + iw / 2, cy); ix += iw + gp; }
                if (IconKind == 2 || IconKind == 3) { DrawBellIcon(g, pn, ix + iw / 2, cy); }
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(gx + iconsW + tgap, 0, ts.Width + 4, Height), tc,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
        else
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
    // 알림 표시방식 아이콘 (메모창 토글과 동일 모양) — 다른 곳에서도 쓰도록 static
    public static void DrawPopupIcon(Graphics g, Pen pn, int px, int cy)
    {
        using (GraphicsPath sp = Round(new Rectangle(px - 6, cy - 5, 12, 9), 3))
        { g.DrawPath(pn, sp); g.DrawLine(pn, px - 2, cy + 4, px - 2, cy + 7); g.DrawLine(pn, px - 2, cy + 7, px + 2, cy + 4); }
    }
    public static void DrawBellIcon(Graphics g, Pen pn, int px, int cy)
    {
        g.DrawLine(pn, px, cy - 7, px, cy - 5);
        using (GraphicsPath bell = new GraphicsPath())
        { bell.AddArc(px - 5, cy - 6, 10, 10, 180, 180); bell.AddLine(px + 5, cy + 1, px + 7, cy + 3); bell.AddLine(px + 7, cy + 3, px - 7, cy + 3); bell.AddLine(px - 7, cy + 3, px - 5, cy + 1); g.DrawPath(pn, bell); }
        g.DrawLine(pn, px - 2, cy + 5, px + 2, cy + 5);
    }
}


// ───────── 톱니(설정) 아이콘 버튼 ─────────
// ⚙ 글리프는 폰트에 따라 꽃처럼 보여서 쓰지 않고 벡터로 직접 그린다.
public class GearButton : Control
{
    public Color Fill = Color.White, FillHover = Color.White, FillDown = Color.White;
    public Color IconColor = Color.FromArgb(112, 118, 130);
    private bool hover = false, down = false;

    public GearButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

    private static PointF Pt(float cx, float cy, float r, double a)
    { return new PointF(cx + (float)(r * Math.Cos(a)), cy + (float)(r * Math.Sin(a))); }

    // 톱니 8개 · 이가 굵어 작은 크기에서도 뭉개지지 않는 비율
    public static GraphicsPath GearPath(float cx, float cy, float rOut, float rIn, int teeth)
    {
        GraphicsPath gp = new GraphicsPath();
        System.Collections.Generic.List<PointF> pts = new System.Collections.Generic.List<PointF>();
        double step = Math.PI * 2.0 / teeth, tw = step * 0.22, ga = step * 0.06;
        for (int i = 0; i < teeth; i++)
        {
            double a = i * step;
            pts.Add(Pt(cx, cy, rOut, a - tw));
            pts.Add(Pt(cx, cy, rOut, a + tw));
            pts.Add(Pt(cx, cy, rIn,  a + tw + ga));
            pts.Add(Pt(cx, cy, rIn,  a + step - tw - ga));
        }
        gp.AddPolygon(pts.ToArray());
        gp.CloseFigure();
        return gp;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color bg = Parent != null ? Parent.BackColor : SystemColors.Control;
        g.Clear(bg);

        Color f = down ? FillDown : (hover ? FillHover : Fill);
        using (GraphicsPath p = AppButton.Round(new Rectangle(0, 0, Width - 1, Height - 1), Math.Min(Width, Height) / 2))
        using (SolidBrush b = new SolidBrush(f)) g.FillPath(b, p);

        float cx = Width / 2f, cy = Height / 2f;
        float rOut = Math.Min(Width, Height) * 0.315f;
        float rIn  = rOut * 0.735f;
        float rHole = rOut * 0.33f;
        using (GraphicsPath gp = GearPath(cx, cy, rOut, rIn, 8))
        using (SolidBrush ib = new SolidBrush(IconColor)) g.FillPath(ib, gp);
        using (SolidBrush hb = new SolidBrush(f)) g.FillEllipse(hb, cx - rHole, cy - rHole, rHole * 2, rHole * 2);
    }
}

// ───────── 프로세스 항목 ─────────
// ───────── iOS 온/오프 토글 ─────────
public class Toggle : Control
{
    private bool chk = false, hover = false;
    public event EventHandler CheckedChanged;
    public Color OnColor = Color.FromArgb(52, 199, 89);
    public Color OffColor = Color.Empty;        // Empty = Theme.ToggleOff
    public Color LabelColor = Color.Empty;      // Empty = Theme.ToggleLabel
    public bool Checked { get { return chk; } set { if (chk != value) { chk = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } } }
    public Toggle()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand; Font = Fonts.Regular(9.5F); Height = 28;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Checked = !Checked; base.OnMouseDown(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(Parent != null ? Parent.BackColor : System.Drawing.SystemColors.Control);
        int tw = 46, th = 26, ty = (Height - th) / 2;
        Rectangle track = new Rectangle(0, ty, tw, th);
        Color tc = !Enabled ? Theme.ToggleDisabled : (chk ? OnColor : (OffColor == Color.Empty ? Theme.ToggleOff : OffColor));
        using (System.Drawing.Drawing2D.GraphicsPath tp = AppButton.Round(track, th / 2))
        using (SolidBrush b = new SolidBrush(tc)) g.FillPath(b, tp);
        int kd = th - 6, kx = chk ? tw - kd - 3 : 3;
        Rectangle knob = new Rectangle(kx, ty + 3, kd, kd);
        using (SolidBrush b = new SolidBrush(Theme.Knob)) g.FillEllipse(b, knob);
        using (Pen pn = new Pen(Color.FromArgb(30, 0, 0, 0), 1F)) g.DrawEllipse(pn, knob);
        Color lc = Enabled ? (LabelColor == Color.Empty ? Theme.ToggleLabel : LabelColor) : Theme.DisText;
        TextRenderer.DrawText(g, Text, Font, new Rectangle(tw + 8, 0, Width - tw - 8, Height), lc,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}


// ───────── 선명한 앱 아이콘 ─────────
public class IconView : Control
{
    public IconView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent != null ? Parent.BackColor : System.Drawing.SystemColors.Control);
        int s = Math.Min(Width, Height);
        Rectangle r = new Rectangle(0, 0, s - 1, s - 1);
        using (System.Drawing.Drawing2D.GraphicsPath p = AppButton.Round(r, s / 4))
        using (SolidBrush b = new SolidBrush(Color.FromArgb(58, 108, 224))) g.FillPath(b, p);
        float cx = s / 2f, cy = s / 2f, R = s * 0.28f, lw = Math.Max(2f, s * 0.058f);
        using (Pen pen = new Pen(Color.White, lw))
        {
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            g.DrawEllipse(pen, cx - R, cy - R, 2 * R, 2 * R);
            g.DrawLine(pen, cx, cy, cx, cy - R * 0.55f);
            g.DrawLine(pen, cx, cy, cx + R * 0.48f, cy + R * 0.14f);
        }
    }
}


// ───────── 포커스 안 뺏는 알림 폼 ─────────
public class AlertForm : Form
{
    public bool NoActivate = false;
    protected override bool ShowWithoutActivation { get { return NoActivate; } }
}

// 더블버퍼 화면 패널 (아일랜드/홈바 깜빡임 방지)
public class ScreenPanel : Panel
{
    public ScreenPanel()
    {
        this.DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
    }
}

// 깜빡임(플리커) 방지용 더블버퍼 리스트박스 — 1초마다 갱신돼도 화면이 떨리지 않게
public class DbListBox : ListBox
{
    public string EmptyHint = "";   // 항목이 없을 때 가운데 보여줄 안내
    public bool HintByParent = false;   // true: 부모 카드가 안내문을 그리고 리스트는 비어 있을 때 숨김(Region 0)
    public int ClipWidth = 0;           // >0: 이 폭까지만 그림 (스크롤바를 카드 밖으로 밀어 숨길 때 카드 오른쪽 테두리를 가리지 않게)
    public void ApplyRegion()
    {
        try
        {
            if (HintByParent && Items.Count == 0) Region = new Region(Rectangle.Empty);
            else if (ClipWidth > 0) Region = new Region(new Rectangle(0, 0, ClipWidth, Math.Max(Height, 4000)));
            else Region = null;
        }
        catch { }
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); ApplyRegion(); }
    public event EventHandler EmptyChanged;
    private int lastCount = -1; private Timer watch;
    public DbListBox()
    {
        try { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); } catch { }
        try { DoubleBuffered = true; } catch { }
        watch = new Timer(); watch.Interval = 250; watch.Tick += delegate { CheckEmpty(); }; watch.Start();
    }
    private void CheckEmpty()
    {
        int c = Items.Count;
        if (c == lastCount) return;
        lastCount = c;
        ApplyRegion();   // 비어 있으면 투명(카드가 안내문) · 아니면 ClipWidth 만큼만
        if (EmptyChanged != null) EmptyChanged(this, EventArgs.Empty);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && watch != null) { watch.Stop(); watch.Dispose(); watch = null; }
        base.Dispose(disposing);
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x000F /*WM_PAINT*/ && !HintByParent && Items.Count == 0 && EmptyHint.Length > 0)
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(Handle))
                {
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    // 스크롤바 숨김용으로 카드보다 넓게 깔린 경우를 고려해 부모(카드) 폭 기준으로 가운데
                    int w = Parent != null ? Math.Min(Width, Parent.Width - 10) : Width;
                    TextRenderer.DrawText(g, EmptyHint, Fonts.Regular(9.5F), new Rectangle(0, 0, w, Height),
                        Theme.Hint,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }
            catch { }
        }
    }
}

// iOS 스타일 숫자 스텝퍼 — 둥근 pill + 직접 타이핑 + 커스텀 ▲▼ (윈도우 화살표 없음)
public class IosStepper : Panel
{
    private TextBox tb;
    public decimal Minimum = 0, Maximum = 100;
    public bool Wrap = false;
    public bool Pad2 = false;   // 두 자리로 표시 (시:분)
    public int Step = 1;
    public event EventHandler ValueChanged;
    private decimal _val = 0;
    private const int CHV = 24;   // 우측 화살표 영역 폭
    private bool editing = false;

    public decimal Value
    {
        get
        {   // 입력창에 타이핑 중이면 그 값을 우선 반영 (엔터를 안 눌러도 됨)
            if (editing && tb != null)
            { decimal tv; if (decimal.TryParse(tb.Text, out tv)) { if (tv < Minimum) tv = Minimum; if (tv > Maximum) tv = Maximum; return tv; } }
            return _val;
        }
        set { SetVal(value, true, false); }
    }
    // 바깥에서 편집(커서) 상태를 끝내고 값을 확정시킬 때 사용
    public void CommitEdit() { EndEdit(); }

    public IosStepper()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
        tb = new TextBox();
        tb.BorderStyle = BorderStyle.None;
        tb.TextAlign = HorizontalAlignment.Center;
        tb.BackColor = Theme.Card; tb.ForeColor = Theme.Ink;
        tb.Visible = false;
        Controls.Add(tb);
        tb.KeyPress += delegate(object s, KeyPressEventArgs e) {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        };
        tb.KeyDown += delegate(object s, KeyEventArgs e) {
            if (e.KeyCode == Keys.Up) { Bump(true); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { Bump(false); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { EndEdit(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { tb.Text = ((int)_val).ToString(); EndEdit(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.A) { tb.SelectAll(); e.SuppressKeyPress = true; e.Handled = true; }
        };
        tb.DoubleClick += delegate { tb.SelectAll(); };
        tb.Leave += delegate { EndEdit(); };
    }

    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (tb != null) tb.Font = this.Font; LayoutTb(); Invalidate(); }
    public void RefreshTheme() { BackColor = Theme.Card; if (tb != null) { tb.BackColor = Theme.Card; tb.ForeColor = Theme.Ink; } Invalidate(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutTb(); UpdateRegion(); }
    private void UpdateRegion()
    {
        try { using (GraphicsPath gp = AppButton.Round(new Rectangle(0, 0, Width, Height), Math.Min(6, Height / 2))) Region = new Region(gp); } catch { }
    }
    private Rectangle TextArea { get { return new Rectangle(6, 0, Math.Max(10, Width - CHV - 6), Height); } }
    private void LayoutTb()
    {
        if (tb == null) return;
        Rectangle ta = TextArea;
        int th = tb.PreferredHeight;
        tb.SetBounds(ta.X, Math.Max(0, (Height - th) / 2), ta.Width, th);
    }
    private void BeginEdit()
    {
        if (editing) return;
        editing = true;
        tb.Text = ((int)_val).ToString();
        tb.Visible = true; LayoutTb();
        tb.Focus(); tb.SelectAll();
        Invalidate();
    }
    private void EndEdit()
    {
        if (!editing) return;
        decimal v;
        if (decimal.TryParse(tb.Text, out v)) SetVal(v, false, true);
        editing = false;
        tb.Visible = false;
        Invalidate();
    }
    private void SetVal(decimal v, bool updateText, bool raise)
    {
        if (v < Minimum) v = Minimum; if (v > Maximum) v = Maximum;
        bool ch = v != _val; _val = v;
        if (updateText && tb != null && editing) tb.Text = ((int)v).ToString();
        Invalidate();
        if (raise && ch && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
    }
    private void Bump(bool up)
    {
        decimal v = _val + (up ? Step : -Step);
        if (v > Maximum) v = Wrap ? Minimum : Maximum;
        if (v < Minimum) v = Wrap ? Maximum : Minimum;
        if (editing) { SetVal(v, true, true); }
        else SetVal(v, false, true);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.X >= Width - CHV) { if (editing) EndEdit(); Bump(e.Y < Height / 2); Focus(); }
        else BeginEdit();
        base.OnMouseDown(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Rectangle rr = new Rectangle(0, 0, Width - 1, Height - 1);
        int rad = Math.Min(6, Height / 2);
        using (GraphicsPath gp = AppButton.Round(rr, rad))
        {
            using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillPath(b, gp);
            using (Pen p = new Pen(Theme.PillBorder, 1F)) g.DrawPath(p, gp);
        }
        // 편집 중이 아닐 때만 숫자를 '전체 폭 기준 중앙'에 직접 그림 (정렬 정확)
        if (!editing)
        {
            // 값은 pill 전체 폭 기준 정중앙 + 세미볼드 (캐시 폰트 — Dispose 금지)
            Font vf = Fonts.SemiPx(this.Font.Size);
            if (vf != null)
                TextRenderer.DrawText(g, Pad2 ? ((int)_val).ToString("00") : ((int)_val).ToString(), vf,
                    new Rectangle(0, 0, Width - CHV + 8, Height), Theme.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
        // 우측 화살표 (얇고 은은하게)
        int cx = Width - CHV / 2 - 4;
        using (Pen p = new Pen(Theme.Chevron, 1.5F) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            int uy = Height / 2 - 6;
            g.DrawLines(p, new Point[] { new Point(cx - 4, uy + 3), new Point(cx, uy), new Point(cx + 4, uy + 3) });
            int dy = Height / 2 + 6;
            g.DrawLines(p, new Point[] { new Point(cx - 4, dy - 3), new Point(cx, dy), new Point(cx + 4, dy - 3) });
        }
    }
}

public class Sched
{
    public int H, M; public bool On = true;
    public string Key { get { return H.ToString("00") + ":" + M.ToString("00"); } }
    public override string ToString() { return H + ":" + M + ":" + (On ? 1 : 0); }
}

public class ProcItem
{
    public string Name;
    public string Display;
    public System.Drawing.Image Ico;
    public override string ToString() { return Display; }
}

// ───────── 메인 ─────────
public class MainForm : Form
{
    private AppButton segPower, segApp, segAlarm; // 모드 세그먼트
    private int mode = 0;                          // 0 전원, 1 프로그램, 2 알림
    private bool modePower { get { return mode == 0; } }
    private Panel panelAlarm;                      // 알림 영역
    private TextBox txtMsg;
    private IosStepper numAlarmMin, numAlarmH, numAlarmM;   // 분 뒤 / 시각(시:분)
    private AppButton btnAddAlarm, btnDelAlarm, segRel, segAbs;
    private Label la1, la1b, la2, lblColon, lblRelUnit;
    private ScreenPanel pillMsg;   // 문구 입력 pill
    private int alarmListY = 88;
    private int alarmMode = 0;   // 0=분 뒤, 1=특정 시각
    private ListBox lstAlarms;
    private Toggle chkGaming;
    private bool SilentMode { get { return chkGaming != null && chkGaming.Checked; } }
    private System.Collections.Generic.List<int> defMin = new System.Collections.Generic.List<int>();   // kind0=분, kind1=하루중분(H*60+M)
    private System.Collections.Generic.List<string> defMsg = new System.Collections.Generic.List<string>();
    private System.Collections.Generic.List<string> defNote = new System.Collections.Generic.List<string>();   // 알림 본문 메모(여러 줄)
    private System.Collections.Generic.List<int> defShow = new System.Collections.Generic.List<int>();   // 알림별 표시방식: -1=기본(전역) 0=팝업 1=윈도우 2=둘다
    private System.Collections.Generic.List<int> defDays = new System.Collections.Generic.List<int>();   // 시각 알림 요일 비트마스크(bit0=월..bit6=일), 0=매일
    private Timer almClickT; private int almPendIdx = -1; private bool almDbl = false;   // 단일/더블 클릭 판별
    private System.Collections.Generic.List<bool> defOn = new System.Collections.Generic.List<bool>();
    private System.Collections.Generic.List<int> defKind = new System.Collections.Generic.List<int>();   // 0=분뒤, 1=특정시각
    private int[] runRemain; private string[] runMsg; private bool alarmRunning = false;
    private System.Collections.Generic.List<DateTime> almTarget = new System.Collections.Generic.List<DateTime>();   // 각 알림의 울릴 시각 (MaxValue=꺼짐/미예약)
    private int editIndex = -1;   // 수정 중인 알림 인덱스 (-1=추가 모드)
    private Label lblAfterF, lblUnitF;             // 하단 타이머 라벨(알림 모드에서 숨김)

    private Panel panelApp;                      // 프로그램 종료 영역
    private Panel panelBottom;                   // 타이머/실행 영역
    private TextBox txtProg; private ScreenPanel pillProg; private Form procPop;
    private AppButton btnAdd, btnList, btnBrowse, btnRemove;
    private ListBox lstTargets;
    private Toggle chkAll, chkSeq, chkFallback;
    private IosStepper numGap;

    private IosStepper numMinutes;
    private AppButton btnStart, btnCancel, btnTest;

    private Label lblCountdown, lblStatus, lblEta; private Timer etaTimer; private bool presetCentered = false;
    // 설정 맨 아래 버전 줄 — 새 버전이 있으면 여기에 배지를 단다
    private Label lblVerSet;
    private string pendingTag = null;
    private bool verChecking = false;

    private Panel panelFail;                     // 실패 시에만 노출
    private Label lblFail;
    private AppButton btnShowLog, btnSaveLog;

    private Timer timer, killStepTimer;
    private List<string> killQueue = new List<string>();
    private int killIndex = 0, remainingSeconds = 0;
    private bool warned = false, anyFail = false, isTestRun = false;
    private bool powerRunning = false; private int powerKind = 0;
    private System.Collections.Generic.List<Sched> scheds = new System.Collections.Generic.List<Sched>();
    private string lastMin = "";
    private Panel panelSched; private NumericUpDown sHour, sMin; private ListBox schedBox; private Timer schedTimer;
    private Panel panelPresets; private System.Collections.Generic.List<int> presets = new System.Collections.Generic.List<int>();
    private Panel alarmCard; private Label lahHelp;
    // 알림 표시 방식: 0=앱 팝업만, 1=윈도우 알림만, 2=둘 다
    private int notifyKind = 2; private bool loadDone = false;
    private ScreenPanel setTogPu, setTogBe;   // 설정: 팝업/윈도우 각각 온오프
    private GearButton btnGear; private Panel panelSet; private int lastTab = 0;
    private AppButton btnAlarmTest;
    private Toggle chkAutoRun; private Label lblAutoRunHelp;

    // 알림 리스트는 고정 4줄 · 넘치면 줄 단위 스크롤
    private void RelayoutAlarms()
    {
        if (alarmCard == null || lahHelp == null) return;
        try
        {
            int h = 16 + 36 * 4;   // 카드 높이 = 4줄 (설정 항목을 톱니로 옮겨 확보한 공간)
            alarmCard.Height = h;
            try { alarmCard.Region = new Region(AppButton.Round(new Rectangle(0, 0, alarmCard.Width, h), 6)); } catch { }
            lstAlarms.Height = 36 * 4;
            int rowBase = alarmListY + h + 8;          // 안내·삭제 버튼 줄
            lahHelp.Location = new Point(2, rowBase);
            if (btnDelAlarm != null) btnDelAlarm.Location = new Point(panelAlarm.Width - 80, rowBase);

        }
        catch { }
    }

    // 알림 입력 모드 전환: 0=분 뒤, 1=특정 시각 (Tag로 관련 컨트롤 표시/숨김)
    private void SetAlarmMode(int m)
    {
        alarmMode = m;
        bool rel = (m == 0);
        if (panelAlarm != null)
            foreach (Control c in panelAlarm.Controls)
            {
                string t = c.Tag as string;
                if (t == "rel") c.Visible = rel;
                else if (t == "abs") c.Visible = !rel;
            }
        StyleSeg(segRel, rel);
        StyleSeg(segAbs, !rel);
    }
    private System.Collections.Generic.List<string> pendingTargets = new System.Collections.Generic.List<string>();
    private StringBuilder logBuf = new StringBuilder();
    private ToolTip tip;

    private static Color BG { get { return Theme.Bg; } }
    private static Color CARD { get { return Theme.Card; } }
    private static Color INK { get { return Theme.Ink; } }
    private static Color MUTED { get { return Theme.Muted; } }
    private static Color ACCENT { get { return Theme.Accent; } }
    private static Color DANGER { get { return Theme.Danger; } }
    private static Color OKC { get { return Theme.Ok; } }

    private const int SIDE = 11, TOP = 11, BOTTOM = 11, SCRW = 288, SCRH = 640;
    private const int cardYConst = 152;
    private static Color BEZEL { get { return Theme.Bezel; } }
    private static Color ISLAND { get { return Theme.Island; } }
    private string islandText = "";
    private int islandCount = 0;   // 아일랜드 알림 개수 배지
    private string islandSig = " ";   // 마지막으로 그린 아일랜드 내용 (중복 다시그리기 방지)
    private string alarmListSig = " "; // 마지막으로 그린 알림 목록 상태 (중복 다시그리기 방지)
    private bool draggingWin = false; private Point dragOff;

    public MainForm()
    {
        this.Text = "자동 종료 타이머";
        this.FormBorderStyle = FormBorderStyle.None;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = BEZEL;
        this.Font = Fonts.Regular(9.5F);
        this.ClientSize = new Size(SCRW + SIDE * 2, TOP + SCRH + BOTTOM);
        this.DoubleBuffered = true;
        this.MouseDown += WinDown; this.MouseMove += WinMove; this.MouseUp += WinUp;
        this.KeyPreview = true;
        this.KeyDown += delegate(object s2, KeyEventArgs k) {
            if (k.KeyCode == Keys.Escape && powerRunning && mode != 2) { CancelClick(null, null); return; }
            // 알림 탭에서 행을 고른 뒤 Delete → 알림 삭제 (문구 입력 중일 때는 글자 지우기 그대로)
            if (k.KeyCode == Keys.Delete && mode == 2 && txtMsg != null && !txtMsg.Focused
                && lstAlarms != null && lstAlarms.SelectedIndex >= 0)
            { k.SuppressKeyPress = true; DelAlarmClick(null, null); } };
        try {
            Stream _s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.ico");
            if (_s != null) { using (_s) this.Icon = new Icon(_s); }
        } catch { }

        tip = new ToolTip(); tip.InitialDelay = 300; tip.ReshowDelay = 100; tip.AutoPopDelay = 12000;

        // 화면 패널 (아일랜드·홈바·서명 모두 액정 안에서 그림)
        screen = new ScreenPanel();
        screen.Location = new Point(SIDE, TOP);
        screen.Size = new Size(SCRW, SCRH);
        screen.BackColor = BG;
        try { screen.Region = new Region(AppButton.Round(new Rectangle(0, 0, SCRW, SCRH), 25)); } catch { }
        screen.Paint += ScreenPaint;
        screen.MouseDown += ScreenMouseDown; screen.MouseMove += WinMove; screen.MouseUp += WinUp;
        Controls.Add(screen);

        int M = 20, CW = SCRW - M * 2;   // 콘텐츠 폭

        // ── 헤더 (아일랜드 아래) ──
        IconView pic = new IconView();
        pic.Size = new Size(38, 38); pic.Location = new Point(M, 52);
        pic.Cursor = Cursors.Hand;
        pic.Click += delegate { ShowGuide(); };
        tip.SetToolTip(pic, "사용법 보기");
        screen.Controls.Add(pic);

        Label title = new Label();
        title.Text = "자동 종료 타이머"; title.Font = Fonts.Semi(12.5F); title.ForeColor = INK;
        title.Location = new Point(M + 46, 52); title.AutoSize = true; title.BackColor = BG;
        screen.Controls.Add(title);

        // 버전 + BETA 배지 (타이틀 옆, 작고 연하게)
        Label ver = new Label();
        ver.Text = "v1.0"; ver.Font = Fonts.Regular(8F); ver.ForeColor = Theme.VerText;
        ver.AutoSize = true; ver.BackColor = BG;
        screen.Controls.Add(ver);
        Label beta = new Label();
        beta.Text = "BETA"; beta.Font = new Font("Segoe UI", 6.5F * Fonts.PX * Fonts.SCALE, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Pixel);
        beta.ForeColor = Theme.Accent; beta.BackColor = Theme.BetaBg;
        beta.AutoSize = true; beta.Padding = new Padding(4, 1, 4, 1);
        screen.Controls.Add(beta);
        // 타이틀 실제 폭 계산 후 배치
        using (Graphics gm = CreateGraphics())
        {
            int tw2 = TextRenderer.MeasureText(gm, title.Text, title.Font).Width;
            int vw = TextRenderer.MeasureText(gm, ver.Text, ver.Font).Width;
            int vx = M + 46 + tw2 + 4;
            ver.Location = new Point(vx, 57);
            beta.Location = new Point(vx + vw + 5, 56);   // ver 실제 폭 기준 → 잘림 없음
        }
        try { beta.Region = new Region(AppButton.Round(new Rectangle(0, 0, 34, 15), 4)); } catch { }

        Label subtitle = new Label();
        subtitle.Text = "Auto Shutdown"; subtitle.Font = Fonts.Regular(8F); subtitle.ForeColor = MUTED;
        subtitle.Location = new Point(M + 48, 72); subtitle.AutoSize = true; subtitle.BackColor = BG;
        screen.Controls.Add(subtitle);

        // ── 세그먼트 ──
        int segY = 104, sw = (CW - 8) / 3;
        segPower = MakeSeg("전원 끄기", M, segY, sw);
        segPower.Click += delegate { SetMode(0); };
        screen.Controls.Add(segPower);
        segApp = MakeSeg("프로그램", M + sw + 4, segY, sw);
        segApp.Click += delegate { SetMode(1); };
        screen.Controls.Add(segApp);
        segAlarm = MakeSeg("알림", M + (sw + 4) * 2, segY, CW - (sw + 4) * 2);
        segAlarm.Click += delegate { SetMode(2); };
        screen.Controls.Add(segAlarm);

        int cardY = cardYConst;

        // ── 프로그램 카드 ──
        panelApp = new Panel();
        panelApp.Location = new Point(M, cardY); panelApp.Size = new Size(CW, 172);
        panelApp.BackColor = BG; panelApp.Visible = false;
        screen.Controls.Add(panelApp);

        // 행1: 프로그램 이름 입력(pill) + 추가
        txtProg = new TextBox(); txtProg.Size = new Size(10, 22); EnableTextActions(txtProg);
        txtProg.Font = new Font("맑은 고딕", 10F * Fonts.PX * Fonts.SCALE, FontStyle.Regular, GraphicsUnit.Pixel);
        txtProg.KeyDown += delegate(object s2, KeyEventArgs k) { if (k.KeyCode == Keys.Enter) { k.SuppressKeyPress = true; AddClick(null, null); } };
        pillProg = MakePill(txtProg, 0, 0, CW - 88, 32, panelApp);
        tip.SetToolTip(txtProg, "예: chrome.exe  ·  아래 [앱 목록]에서 골라도 됩니다");

        btnAdd = new AppButton();
        btnAdd.Text = "추가"; btnAdd.Location = new Point(CW - 80, 0); btnAdd.Size = new Size(80, 32);
        btnAdd.Radius = 8; btnAdd.Font = Fonts.Semi(10F);
        btnAdd.Click += AddClick; panelApp.Controls.Add(btnAdd);

        // 행2: 실행 중인 앱 목록 열기 + 백그라운드 포함
        btnList = MakeGhost("앱 목록", 0, 40, 80, 28); btnList.Click += RefreshClick; panelApp.Controls.Add(btnList);
        tip.SetToolTip(btnList, "지금 실행 중인 프로그램에서 고르기");
        chkAll = new Toggle(); chkAll.Text = "백그라운드 포함"; chkAll.Size = new Size(150, 28); chkAll.Location = new Point(90, 40); panelApp.Controls.Add(chkAll);
        tip.SetToolTip(chkAll, "창이 없는 프로그램(백그라운드)도 목록에 보여줍니다");

        // 행3: 종료 대상 목록 + [삭제] [지금 종료]
        lstTargets = new DbListBox();
        lstTargets.Font = Fonts.Regular(9.5F);
        ((DbListBox)lstTargets).EmptyHint = "종료할 프로그램 없음";
        lstTargets.KeyDown += delegate(object s2, KeyEventArgs k) { if (k.KeyCode == Keys.Delete) RemoveClick(null, null); };
        lstTargets.MouseDown += delegate(object s2, MouseEventArgs me)   // 우클릭으로 삭제
        {
            if (me.Button == MouseButtons.Right)
            { int idx = lstTargets.IndexFromPoint(me.Location); if (idx >= 0) { lstTargets.SelectedIndex = idx; RemoveClick(null, null); } }
        };
        tip.SetToolTip(lstTargets, "우클릭 또는 [삭제]로 제거");
        MakeCard(lstTargets, 0, 76, CW - 88, 60, panelApp);
        btnRemove = MakeGhost("삭제", CW - 80, 76, 80, 28); btnRemove.Click += RemoveClick; panelApp.Controls.Add(btnRemove);
        tip.SetToolTip(btnRemove, "선택한 항목 삭제 (Delete)");
        btnTest = MakeGhost("지금 종료", CW - 80, 108, 80, 28); btnTest.Click += TestClick; panelApp.Controls.Add(btnTest);
        btnTest.TextColor = Color.FromArgb(206, 62, 62);
        tip.SetToolTip(btnTest, "타이머 없이 목록의 프로그램을 바로 종료");

        // 행4: 순차 종료 + 간격
        chkSeq = new Toggle(); chkSeq.Text = "순차 종료"; chkSeq.Size = new Size(116, 28); chkSeq.Location = new Point(0, 144); panelApp.Controls.Add(chkSeq);
        tip.SetToolTip(chkSeq, "한 번에 다 끄지 않고 하나씩, 아래 간격으로 종료");
        numGap = new IosStepper(); numGap.Location = new Point(120, 144); numGap.Size = new Size(60, 28);
        numGap.Minimum = 1; numGap.Maximum = 600; numGap.Value = 3; numGap.Font = Fonts.System(10F); panelApp.Controls.Add(numGap);
        Label lblSec = new Label(); lblSec.Text = "초 간격"; lblSec.Font = Fonts.Regular(10F); lblSec.Location = new Point(188, 150); lblSec.AutoSize = true; lblSec.ForeColor = MUTED; lblSec.BackColor = BG; panelApp.Controls.Add(lblSec);

        // "안 꺼지면 전원 끄기" 옵션은 제거 (사용자 요청).
        // 컨트롤 자체는 남겨두되 화면에 붙이지 않아 항상 꺼진 상태 → 관련 로직은 그대로 안전하게 동작.
        chkFallback = new Toggle(); chkFallback.Checked = false; chkFallback.Visible = false;

        // ── 알림 카드 ──
        panelAlarm = new Panel();
        panelAlarm.Location = new Point(M, cardY); panelAlarm.Size = new Size(CW, 380);
        panelAlarm.BackColor = BG; panelAlarm.Visible = false;
        screen.Controls.Add(panelAlarm);
        panelAlarm.MouseDown += delegate { ReleaseAlarmEdit(); };   // 알림 패널 빈 곳 클릭 → 해제

        // ── 모드 선택: [분 후] [시간] ──
        segRel = MakeGhost("분 후", 0, 0, 66, 28); segRel.Radius = 8;
        segRel.Click += delegate { SetAlarmMode(0); }; panelAlarm.Controls.Add(segRel);
        segAbs = MakeGhost("시간", 72, 0, 66, 28); segAbs.Radius = 8;
        segAbs.Click += delegate { SetAlarmMode(1); }; panelAlarm.Controls.Add(segAbs);
        tip.SetToolTip(segRel, "지금부터 N분 후에 한 번 알림");
        tip.SetToolTip(segAbs, "정해진 시각에 매일 알림");

        int rowY = 38;   // 입력 줄 y
        int PH = 32;     // 입력 높이

        // 분 후: 스텝퍼 + "분 후"
        la1 = new Label(); la1.Visible = false; la1.Size = new Size(0, 0);   // (캡션 제거 · 호환용)
        numAlarmMin = new IosStepper(); numAlarmMin.Location = new Point(0, rowY); numAlarmMin.Size = new Size(88, PH);
        numAlarmMin.Minimum = 1; numAlarmMin.Maximum = 1440; numAlarmMin.Value = 30; numAlarmMin.Font = Fonts.System(11F);
        panelAlarm.Controls.Add(numAlarmMin);
        Label lminAfter = new Label(); lminAfter.Text = "분 후"; lminAfter.Font = Fonts.Regular(10.5F); lminAfter.ForeColor = MUTED; lminAfter.Location = new Point(96, rowY + 8); lminAfter.AutoSize = true; lminAfter.BackColor = BG; panelAlarm.Controls.Add(lminAfter);
        numAlarmMin.Tag = "rel"; lminAfter.Tag = "rel"; lblRelUnit = lminAfter;

        // 시간: [시] : [분] (23→00 순환)
        la1b = new Label(); la1b.Visible = false; la1b.Size = new Size(0, 0);
        numAlarmH = new IosStepper(); numAlarmH.Wrap = true; numAlarmH.Pad2 = true; numAlarmH.Location = new Point(0, rowY); numAlarmH.Size = new Size(70, PH);
        numAlarmH.Minimum = 0; numAlarmH.Maximum = 23; numAlarmH.Value = 22; numAlarmH.Font = Fonts.System(11F);
        panelAlarm.Controls.Add(numAlarmH);
        lblColon = new Label(); lblColon.Text = ":"; lblColon.Font = Fonts.Semi(12F); lblColon.ForeColor = INK; lblColon.Location = new Point(75, rowY + 4); lblColon.AutoSize = true; lblColon.BackColor = BG; panelAlarm.Controls.Add(lblColon);
        numAlarmM = new IosStepper(); numAlarmM.Wrap = true; numAlarmM.Pad2 = true; numAlarmM.Location = new Point(88, rowY); numAlarmM.Size = new Size(70, PH);
        numAlarmM.Minimum = 0; numAlarmM.Maximum = 59; numAlarmM.Value = 0; numAlarmM.Font = Fonts.System(11F);
        panelAlarm.Controls.Add(numAlarmM);
        numAlarmH.Tag = "abs"; lblColon.Tag = "abs"; numAlarmM.Tag = "abs";

        // 알림 내용 + 추가
        int msgY = rowY + PH + 8;
        la2 = new Label(); la2.Visible = false; la2.Size = new Size(0, 0);
        txtMsg = new TextBox(); txtMsg.Size = new Size(10, 22); txtMsg.Font = new Font("맑은 고딕", 10F * Fonts.PX * Fonts.SCALE, FontStyle.Regular, GraphicsUnit.Pixel); txtMsg.Text = "";
        EnableTextActions(txtMsg);
        txtMsg.KeyDown += delegate(object s2, KeyEventArgs k)
        {
            if (k.KeyCode == Keys.Enter) { k.SuppressKeyPress = true; AddAlarmClick(null, null); }
            else if (k.KeyCode == Keys.Escape && editIndex >= 0)   // 수정 중 Esc → 취소(추가 모드로)
            { k.SuppressKeyPress = true; txtMsg.Text = ""; if (lstAlarms != null) lstAlarms.SelectedIndex = -1; ExitEditMode(); if (lstAlarms != null) lstAlarms.Invalidate(); }
        };
        pillMsg = MakePill(txtMsg, 0, msgY, CW - 88, PH, panelAlarm);
        tip.SetToolTip(txtMsg, "알림 내용 (비우면 \"시간이 되었습니다\")");
        btnAddAlarm = new AppButton(); btnAddAlarm.Text = "추가"; btnAddAlarm.Location = new Point(CW - 80, msgY); btnAddAlarm.Size = new Size(80, PH); btnAddAlarm.Radius = 8; btnAddAlarm.Font = Fonts.Semi(10F);
        btnAddAlarm.Click += AddAlarmClick; panelAlarm.Controls.Add(btnAddAlarm);

        int listY = msgY + PH + 10;
        lstAlarms = new DbListBox(); lstAlarms.Font = Fonts.Regular(9.5F);
        lstAlarms.DrawMode = DrawMode.OwnerDrawFixed; lstAlarms.ItemHeight = 36;
        lstAlarms.DrawItem += AlarmDraw; lstAlarms.MouseDown += AlarmMouse;
        lstAlarms.KeyDown += delegate(object s2, KeyEventArgs k)
        {
            if (k.KeyCode == Keys.Delete) DelAlarmClick(null, null);
            else if (k.KeyCode == Keys.Enter)
            { int si = lstAlarms.SelectedIndex; if (si >= 0 && si < defMin.Count) { k.SuppressKeyPress = true; OpenNoteEditor(si); } }
        };
        lstAlarms.DoubleClick += delegate
        {
            almDbl = true; if (almClickT != null) almClickT.Stop();   // 예약된 단일클릭(수정) 취소
            Point pp = lstAlarms.PointToClient(Cursor.Position);
            int di = lstAlarms.IndexFromPoint(pp);
            if (di < 0 || di >= defMin.Count) return;
            int visRight = (alarmCard != null ? alarmCard.Width - 10 : lstAlarms.ClientSize.Width);
            if (pp.X > visRight - 52) return;   // 토글 영역 무시
            int pcx2, bcx2; AlarmIconCenters(out pcx2, out bcx2);
            if (pp.X >= pcx2 - 9 && pp.X <= bcx2 + 9) return;   // 팝업/종 아이콘 영역 무시
            OpenNoteEditor(di);
        };
        lstAlarms.IntegralHeight = false;
        alarmListY = listY;
        alarmCard = MakeCard(lstAlarms, 0, listY, CW, 16 + 36 * 3, panelAlarm, true);   // 전체 폭 + 스크롤바 숨김 (4줄)
        ((DbListBox)lstAlarms).EmptyHint = "알림이 없습니다 · 위에서 추가하세요";
        lahHelp = new Label(); lahHelp.Text = "더블클릭·Enter: 메모 추가"; lahHelp.Font = Fonts.Regular(9F); lahHelp.ForeColor = MUTED;
        lahHelp.AutoSize = false; lahHelp.Size = new Size(CW - 88, 28); lahHelp.TextAlign = ContentAlignment.MiddleLeft; lahHelp.BackColor = BG;
        panelAlarm.Controls.Add(lahHelp);   // 수정 모드일 때만 안내 표시
        btnDelAlarm = MakeGhost("선택 삭제", 0, 0, 80, 28); btnDelAlarm.Click += DelAlarmClick; panelAlarm.Controls.Add(btnDelAlarm);
        tip.SetToolTip(btnDelAlarm, "선택한 알림 삭제 (Delete)");
        tip.SetToolTip(lstAlarms, "행 클릭: 수정  ·  오른쪽 토글: 켜기/끄기");

        // ── 설정 카드 (톱니 아이콘) : 탭 공용 설정 모음 ──
        panelSet = new Panel();
        panelSet.Location = new Point(M, cardY); panelSet.Size = new Size(CW, 400);
        panelSet.BackColor = BG; panelSet.Visible = false;
        screen.Controls.Add(panelSet);

        Label setTitle = new Label(); setTitle.Text = "설정"; setTitle.Font = Fonts.Semi(15F); setTitle.ForeColor = INK;
        setTitle.Location = new Point(0, 0); setTitle.AutoSize = true; setTitle.BackColor = BG; panelSet.Controls.Add(setTitle);

        // 1) 윈도우 시작 시 자동 실행
        Label sg1 = new Label(); sg1.Text = "일반"; sg1.Font = Fonts.Semi(9.5F); sg1.ForeColor = MUTED;
        sg1.Location = new Point(2, 42); sg1.AutoSize = true; sg1.BackColor = BG; panelSet.Controls.Add(sg1);
        chkAutoRun = new Toggle(); chkAutoRun.Text = "윈도우 시작 시 자동 실행"; chkAutoRun.Size = new Size(230, 28);
        chkAutoRun.Location = new Point(0, 60); panelSet.Controls.Add(chkAutoRun);
        lblAutoRunHelp = new Label(); lblAutoRunHelp.Text = "로그온하면 트레이에서 자동으로 실행됩니다";
        lblAutoRunHelp.Font = Fonts.Regular(9F); lblAutoRunHelp.ForeColor = MUTED;
        lblAutoRunHelp.Location = new Point(2, 88); lblAutoRunHelp.AutoSize = true; lblAutoRunHelp.BackColor = BG;
        panelSet.Controls.Add(lblAutoRunHelp);
        tip.SetToolTip(chkAutoRun, "작업 스케줄러에 등록/해제합니다. 관리자 권한 앱이라 시작프로그램 대신 이 방식을 씁니다.");

        chkDark = new Toggle(); chkDark.Text = "다크 모드"; chkDark.Size = new Size(230, 28);
        chkDark.Location = new Point(0, 108); panelSet.Controls.Add(chkDark);
        chkDark.CheckedChanged += delegate { if (!applyingTheme) SetDarkMode(chkDark.Checked, true); };

        // 2) 알림
        Label sg2 = new Label(); sg2.Text = "알림"; sg2.Font = Fonts.Semi(9.5F); sg2.ForeColor = MUTED;
        sg2.Location = new Point(2, 152); sg2.AutoSize = true; sg2.BackColor = BG; panelSet.Controls.Add(sg2);

        chkGaming = new Toggle(); chkGaming.Text = "무음 모드"; chkGaming.Size = new Size(220, 28);
        chkGaming.Location = new Point(0, 170); panelSet.Controls.Add(chkGaming);
        tip.SetToolTip(chkGaming, "알림 소리를 내지 않습니다. 전체화면을 쓰는 중에도 포커스를 빼앗지 않습니다.");

        Label sg3 = new Label(); sg3.Text = "알림 표시 방식"; sg3.Font = Fonts.Regular(10F); sg3.ForeColor = INK;
        sg3.Location = new Point(2, 206); sg3.AutoSize = true; sg3.BackColor = BG; panelSet.Controls.Add(sg3);
        // 팝업/윈도우 각각 온오프 (둘 다 켜면 둘 다 · 최소 하나는 유지)
        int stW = 108, stH = 32, stY = 226, stGap = 12;
        for (int s = 0; s < 2; s++)
        {
            ScreenPanel c = new ScreenPanel();
            c.Location = new Point(s * (stW + stGap), stY); c.Size = new Size(stW, stH); c.BackColor = BG;
            bool isPopup = (s == 0);
            c.Paint += delegate(object s2, PaintEventArgs pe)
            {
                Graphics g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.Half;
                bool onx = isPopup ? (notifyKind == 0 || notifyKind == 2) : (notifyKind == 1 || notifyKind == 2);
                Color fill = onx ? ACCENT : (Theme.Dark ? Color.FromArgb(52, 56, 66) : Color.FromArgb(236, 238, 243));
                Color fg = onx ? Color.White : Theme.OffText;
                using (GraphicsPath bp = AppButton.Round(new Rectangle(0, 0, stW - 1, stH - 1), 9))
                { using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, bp);
                  if (!onx) using (Pen pn = new Pen(Theme.PillBorder, 1F)) g.DrawPath(pn, bp); }
                string lab = isPopup ? "팝업" : "윈도우";
                Font lf = Fonts.Semi(9.5F);
                int iw = 14, gp = 5, tw2 = TextRenderer.MeasureText(g, lab, lf, new Size(200, stH), TextFormatFlags.NoPadding).Width;
                int grpW = iw + gp + tw2, gx = (stW - grpW) / 2, cy = stH / 2;
                using (Pen pn = new Pen(fg, 1.6F) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                { if (isPopup) AppButton.DrawPopupIcon(g, pn, gx + iw / 2, cy); else AppButton.DrawBellIcon(g, pn, gx + iw / 2, cy); }
                TextRenderer.DrawText(g, lab, lf, new Rectangle(gx + iw + gp, 0, tw2 + 4, stH), fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            };
            c.Click += delegate
            {
                bool pu = (notifyKind == 0 || notifyKind == 2), be = (notifyKind == 1 || notifyKind == 2);
                if (isPopup) { pu = !pu; if (!pu && !be) be = true; } else { be = !be; if (!be && !pu) pu = true; }
                SetNotifyKind(pu && be ? 2 : (pu ? 0 : 1));
            };
            tip.SetToolTip(c, isPopup ? "앱 팝업창" : "윈도우 기본 알림(트레이 풍선)");
            if (isPopup) setTogPu = c; else setTogBe = c;
            panelSet.Controls.Add(c);
        }

        // 3) 정보
        Label sg4 = new Label(); sg4.Text = "정보"; sg4.Font = Fonts.Semi(9.5F); sg4.ForeColor = MUTED;
        sg4.Location = new Point(2, 310); sg4.AutoSize = true; sg4.BackColor = BG; panelSet.Controls.Add(sg4);
        lblVerSet = new Label();
        lblVerSet.Text = "자동 종료 타이머 v" + VERSION;
        lblVerSet.Font = Fonts.Regular(8.5F); lblVerSet.ForeColor = MUTED;
        lblVerSet.Location = new Point(2, 328); lblVerSet.AutoSize = true; lblVerSet.BackColor = BG; panelSet.Controls.Add(lblVerSet);
        // 버전 줄을 누르면 수동으로 업데이트 확인 (새 버튼을 놓을 자리가 없어 라벨 자체를 버튼처럼 쓴다)
        lblVerSet.Cursor = Cursors.Hand;
        // 예약 중이라는 경고는 새 버전이 실제로 있을 때만, 업데이트 화면 안에서 보여준다.
        // (여기서 먼저 물으면 최신 여부만 확인하려는 사람도 경고를 받는다)
        lblVerSet.Click += delegate
        {
            verChecking = true;
            RefreshVerLabel();
            Updater.Check(this, VERSION, false, ShowUpdateBadge);
        };
        tip.SetToolTip(lblVerSet, "클릭하면 새 버전이 있는지 확인합니다");
        tip.SetToolTip(chkGaming, "켜면 알림 소리를 내지 않고 팝업만 조용히 띄웁니다. 전체화면을 쓰는 중에도 포커스를 빼앗지 않습니다.");

        // ── 타이머 입력 줄 (전원/프로그램 모드) ──
        lblAfterF = new Label(); lblAfterF.Text = "타이머"; lblAfterF.Font = Fonts.Semi(12F); lblAfterF.ForeColor = INK; lblAfterF.AutoSize = true; lblAfterF.BackColor = BG;
        screen.Controls.Add(lblAfterF);
        numMinutes = new IosStepper(); numMinutes.Size = new Size(96, 42); numMinutes.Minimum = 1; numMinutes.Maximum = 1440; numMinutes.Value = 30; numMinutes.Font = Fonts.System(13F);
        screen.Controls.Add(numMinutes);
        lblUnitF = new Label(); lblUnitF.Text = "분 후"; lblUnitF.Font = Fonts.Regular(11.5F); lblUnitF.ForeColor = MUTED; lblUnitF.AutoSize = true; lblUnitF.BackColor = BG;
        screen.Controls.Add(lblUnitF);
        // 실제 몇 시에 실행되는지 미리 보여주는 줄 ("→ 오후 6:41 전원 끄기 예정")
        lblEta = new Label(); lblEta.Font = Fonts.Semi(10F); lblEta.ForeColor = ACCENT; lblEta.AutoSize = false;
        lblEta.Size = new Size(CW, 20); lblEta.TextAlign = ContentAlignment.MiddleCenter; lblEta.BackColor = BG;
        screen.Controls.Add(lblEta);
        numMinutes.ValueChanged += delegate { UpdateEta(); };
        etaTimer = new Timer(); etaTimer.Interval = 15000; etaTimer.Tick += delegate { UpdateEta(); }; etaTimer.Start();

        // 자주 쓰는 타이머(분) 저장 목록 — 칩 형태
        panelPresets = new Panel(); panelPresets.Size = new Size(CW, 30); panelPresets.BackColor = BG;
        screen.Controls.Add(panelPresets);
        presets.Add(30); presets.Add(60); presets.Add(120);

        // ── 하단 버튼 (풀폭 스택) ──
        // 알림 탭 전용: 알림 미리보기 (중지 버튼 자리에 표시)
        lblNotiWarn = new Label(); lblNotiWarn.Text = "윈도우 알림 꺼짐 · 켜기"; lblNotiWarn.Font = Fonts.Semi(9.5F);
        lblNotiWarn.ForeColor = DANGER; lblNotiWarn.BackColor = BG; lblNotiWarn.AutoSize = true; lblNotiWarn.Cursor = Cursors.Hand;
        lblNotiWarn.Location = new Point(120, 272); lblNotiWarn.Visible = false;
        lblNotiWarn.Click += delegate { OpenWindowsNotificationSettings(); };
        tip.SetToolTip(lblNotiWarn, "윈도우 설정 → 시스템 → 알림 이 꺼져 있습니다. 눌러서 열기");
        panelSet.Controls.Add(lblNotiWarn);
        btnAlarmTest = MakeGhost("알림 미리보기", 0, 266, 112, 28);
        btnAlarmTest.Click += delegate { HideFail(); if (notifyKind != 0 && !WindowsToastEnabled()) PromptEnableWindowsNotifications(); string mm = (txtMsg.Text ?? "").Trim(); if (mm.Length == 0) mm = "시간이 되었습니다"; Log("알림 미리보기"); FireAlarm(mm); };
        panelSet.Controls.Add(btnAlarmTest);
        tip.SetToolTip(btnAlarmTest, "지금 설정(무음·표시 방식)대로 알림이 어떻게 뜨는지 확인");

        btnStart = new AppButton();
        btnStart.Text = "시작"; btnStart.Location = new Point(M, SCRH - 150); btnStart.Size = new Size(CW, 42);
        btnStart.Radius = 10; btnStart.Font = Fonts.Semi(12F); btnStart.Click += StartClick; screen.Controls.Add(btnStart);

        btnCancel = new AppButton();
        // 시작(테두리 없음)과 같은 크기인데 테두리가 있어 더 커 보이는 착시 → 1px씩 안쪽으로 (시각적 크기 동일)
        btnCancel.Text = "중지"; btnCancel.Location = new Point(M + 1, SCRH - 104 + 1); btnCancel.Size = new Size(CW - 2, 40);
        btnCancel.Radius = 10; btnCancel.Font = Fonts.Semi(12F);
        btnCancel.Fill = Theme.CancelFill; btnCancel.FillHover = Theme.CancelHover;
        btnCancel.FillDown = Theme.CancelDown; btnCancel.TextColor = Theme.CancelText;
        btnCancel.BorderColor = Theme.CancelBorder; btnCancel.Enabled = false; btnCancel.Click += CancelClick; screen.Controls.Add(btnCancel);

        lblStatus = new Label(); lblStatus.Text = "대기 중"; lblStatus.ForeColor = MUTED; lblStatus.TextAlign = ContentAlignment.MiddleCenter;
        lblStatus.Location = new Point(46, SCRH - 58); lblStatus.Size = new Size(SCRW - 92, 18); lblStatus.BackColor = BG; screen.Controls.Add(lblStatus);

        // 숨김 카운트다운(아일랜드 표시용)
        lblCountdown = new Label(); lblCountdown.Text = "--:--"; lblCountdown.Visible = false; lblCountdown.Location = new Point(-500, -500); lblCountdown.Size = new Size(10,10); screen.Controls.Add(lblCountdown);

        // 실패 안내
        panelFail = new Panel(); panelFail.Location = new Point(M, SCRH - 250); panelFail.Size = new Size(CW, 44);
        panelFail.BackColor = Color.FromArgb(253, 240, 240); panelFail.Visible = false; screen.Controls.Add(panelFail);
        lblFail = new Label(); lblFail.Text = "일부 종료 실패"; lblFail.ForeColor = DANGER; lblFail.Font = Fonts.Semi(9.5F); lblFail.Location = new Point(10, 14); lblFail.AutoSize = true; lblFail.BackColor = panelFail.BackColor; panelFail.Controls.Add(lblFail);
        btnShowLog = MakeGhost("자세히", CW - 168, 9, 76, 26); btnShowLog.Fill = Color.FromArgb(250,232,232); btnShowLog.Click += ShowLogClick; panelFail.Controls.Add(btnShowLog);
        btnSaveLog = MakeGhost("로그 저장", CW - 86, 9, 86, 26); btnSaveLog.Fill = Color.FromArgb(250,232,232); btnSaveLog.Click += SaveLogClick; panelFail.Controls.Add(btnSaveLog);

        timer = new Timer(); timer.Interval = 1000; timer.Tick += TimerTick;
        killStepTimer = new Timer(); killStepTimer.Tick += KillStep;

        // (예약 시각 목록 기능은 사용자 요청으로 제외 — 타이머 + 자주 쓰는 분 칩으로 대체.
        //  관련 메서드는 남겨두어 언제든 복원 가능)

        BuildPhoneButtons();
        SetNotifyKind(notifyKind);        // 기본 스타일(둘 다) 먼저 적용
        try { LoadSettings(); } catch { } // 저장된 값이 있으면 덮어씀
        loadDone = true;
        try { RebuildPresets(); } catch { }
        RelayoutAlarms();
        ArmLoadedAlarms();   // 켜진 채로 저장된 알림은 켜자마자 다시 예약
        // 설정 토글은 누르는 즉시 저장 (마지막 상태 유지)
        chkGaming.CheckedChanged += delegate { try { SaveSettings(); } catch { } };
        chkAutoRun.CheckedChanged += delegate { try { ApplyAutoRun(chkAutoRun.Checked); } catch { } };
        try { RefreshAutoRunToggle(); } catch { }
        try { EnsureAutoRunTray(); } catch { }
        chkSeq.CheckedChanged += delegate { try { SaveSettings(); } catch { } };
        chkFallback.CheckedChanged += delegate { try { SaveSettings(); } catch { } };
        SetAlarmMode(0);
        SetMode(0);
        SetupTray();   // 트레이 상주 (닫기·최소화 시 백그라운드 유지)
        if (startInTray) { trayTipShown = true; TrayNotify("자동 종료 타이머", "트레이에서 실행 중입니다. 아이콘을 더블클릭하면 열립니다."); }
        this.Shown += delegate
        {
            SetPlaceholder(txtMsg, "알림 내용 (비우면 \"시간이 되었습니다\")");
            SetPlaceholder(txtProg, "프로그램 이름 · 예: chrome.exe");
        };

        // 업데이트 확인은 창 표시와 분리한다.
        // --tray 로 자동 실행되면 Shown 이 아예 발생하지 않아(SetVisibleCore 가 막는다),
        // 트레이 상주로만 쓰는 사용자는 새 버전을 영영 못 받게 된다.
        // 첫 확인은 6초 뒤(백신 행위감시가 예민한 구간을 피한다), 이후 6시간마다.
        // Updater 는 MainForm 밖의 static 클래스라 private 필드를 못 본다. 훅으로 넘겨준다.
        Updater.Done = delegate { verChecking = false; RefreshVerLabel(); };
        Updater.PowerBusy = delegate { return powerRunning; };

        try
        {
            Timer upChk = new Timer();
            upChk.Interval = 6000;
            upChk.Tick += delegate
            {
                upChk.Interval = 6 * 60 * 60 * 1000;
                try { SelfUnblock(); } catch { }
                try { Updater.Check(this, VERSION, true, ShowUpdateBadge); } catch { }
            };
            upChk.Start();
        }
        catch { }
        if (loadedFrom != null)
        {
            // 하단에 "설정 불러옴 · 레지스트리 · 알림 N개" 같은 안내는 표시하지 않는다.
            // (진단창에서는 계속 확인 가능 — loadedFrom 값은 그대로 유지)
            lblStatus.Text = "대기 중"; lblStatus.ForeColor = MUTED;
        }
        else
        {
            lblStatus.Text = "대기 중"; lblStatus.ForeColor = MUTED;
            // 시작 직후 몇 초간 백신 행위감시가 파일/레지스트리 읽기를 막는 환경 대응:
            // 2·4·8·15·30초 시점에 성공할 때까지 재시도
            int[] delays = new int[] { 2000, 2000, 4000, 7000, 15000 };
            int attempt = 0;
            Timer lateLoad = new Timer(); lateLoad.Interval = delays[0];
            lateLoad.Tick += delegate
            {
                lateLoad.Stop();
                if (loadedFrom == null)
                {
                    try
                    {
                        LoadSettings();
                        if (loadedFrom != null)
                        {
                            RebuildPresets();
                            RelayoutAlarms();
                            ArmLoadedAlarms();
                            // 하단 안내 문구 없이 조용히 복구만 한다
                            if (!powerRunning && !alarmRunning) { lblStatus.Text = "대기 중"; lblStatus.ForeColor = MUTED; }
                        }
                    }
                    catch { }
                    attempt++;
                    if (loadedFrom == null)   // 성공할 때까지 무한 재시도 (이후 10초 간격)
                    { lateLoad.Interval = attempt < delays.Length ? delays[attempt] : 10000; lateLoad.Start(); }
                }
            };
            lateLoad.Start();
        }

        // 압축 안에서 바로 실행 감지 → 저장이 유지되지 않는 대표 원인
        try
        {
            string xp = Application.ExecutablePath;
            if (xp.IndexOf(@"\AppData\Local\Temp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                xp.IndexOf(@"\Temp1_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MessageBox.Show(
                    "압축(zip) 안에서 바로 실행한 것으로 보입니다.\r\n" +
                    "이 상태에서는 실행할 때마다 임시 폴더에 복사돼 저장이 유지되지 않습니다.\r\n\r\n" +
                    "zip을 마우스 우클릭 → '압축 풀기'로 폴더에 푼 다음,\r\n" +
                    "그 폴더 안의 exe를 실행해 주세요.",
                    "저장이 유지되지 않는 환경", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch { }
    }

    private Panel screen;
    private AppButton btnWinClose, btnWinMin;
    private void BuildPhoneButtons()
    {
        // 액정 우상단, 평소엔 배경에 녹아있다가 올리면 살짝 표시되는 원형 버튼
        btnWinMin = new AppButton();
        btnWinMin.Text = "—"; btnWinMin.Size = new Size(30, 30);
        btnWinMin.Location = new Point(SCRW - 74, 5); btnWinMin.Radius = 15;
        btnWinMin.Fill = BG; btnWinMin.FillHover = Theme.WinHover;
        btnWinMin.FillDown = Theme.WinDown;
        btnWinMin.TextColor = Theme.WinIcon; btnWinMin.BorderColor = BG;
        btnWinMin.Font = Fonts.Semi(14F);
        btnWinMin.Click += delegate { this.WindowState = FormWindowState.Minimized; };
        tip.SetToolTip(btnWinMin, "트레이로 최소화 (계속 실행)");
        screen.Controls.Add(btnWinMin); btnWinMin.BringToFront();

        btnWinClose = new AppButton();
        btnWinClose.Text = "✕"; btnWinClose.Size = new Size(30, 30);
        btnWinClose.Location = new Point(SCRW - 38, 5); btnWinClose.Radius = 15;
        btnWinClose.Fill = BG; btnWinClose.FillHover = Theme.CloseHover;
        btnWinClose.FillDown = Theme.CloseDown;
        btnWinClose.TextColor = Theme.WinIcon; btnWinClose.BorderColor = BG;
        btnWinClose.Font = Fonts.Semi(14F);
        btnWinClose.Click += delegate { this.Close(); };
        tip.SetToolTip(btnWinClose, "트레이로 숨기기 · 완전 종료는 트레이 아이콘 우클릭");
        screen.Controls.Add(btnWinClose); btnWinClose.BringToFront();

        // 우하단 톱니 = 설정 화면
        btnGear = new GearButton();
        btnGear.Size = new Size(26, 26);
        btnGear.Location = new Point(SCRW - 36, SCRH - 58);   // 취소 버튼 아래 · ©600g 위 · 우측 정렬
        btnGear.Fill = BG; btnGear.FillHover = Theme.WinHover;
        btnGear.FillDown = Theme.WinDown;
        btnGear.IconColor = Theme.GearIcon;
        btnGear.Click += delegate { SetMode(mode == 3 ? lastTab : 3); };
        tip.SetToolTip(btnGear, "설정");
        screen.Controls.Add(btnGear); btnGear.BringToFront();
    }

    // 화면 하단 서명(© 600g) 영역을 5번 연속 클릭하면 진단 창 (숨은 트리거)
    // 텍스트 입력칸에 윈도우 기본 액션 보강: 더블클릭=전체선택 · Ctrl+A=전체선택
    private static void EnableTextActions(TextBox tb)
    {
        if (tb == null) return;
        tb.DoubleClick += delegate { try { tb.SelectAll(); } catch { } };
        tb.KeyDown += delegate(object s, KeyEventArgs k)
        {
            if (k.Control && k.KeyCode == Keys.A) { try { tb.SelectAll(); } catch { } k.SuppressKeyPress = true; k.Handled = true; }
        };
    }

    // 알림 수정 선택 해제 (리스트 밖 아무 곳 클릭 시)
    private void ReleaseAlarmEdit()
    {
        if (mode != 2 || editIndex < 0) return;
        if (txtMsg != null) txtMsg.Text = "";
        if (lstAlarms != null) { lstAlarms.SelectedIndex = -1; lstAlarms.Invalidate(); }
        ExitEditMode();
    }
    private void ScreenMouseDown(object sender, MouseEventArgs e)
    {
        CommitAllSteppers();
        ReleaseAlarmEdit();
        // ⓒ600g 글자는 y 606~619 에 그려지는데, 예전 판정(SCRH-46~-26 = 594~614)은 위쪽이
        // lblStatus(582~600)에 가려 실효 띠가 14px 뿐이었고 글자 아래 5px 은 반응하지 않았다.
        // 글자에 맞춰 604~622 로 옮긴다.
        bool onSig = e.Y >= SCRH - 36 && e.Y <= SCRH - 18 && e.X > SCRW / 2 - 60 && e.X < SCRW / 2 + 60;
        if (onSig)
        {
            if ((DateTime.Now - sigFirst).TotalSeconds > 3) { sigClicks = 0; sigFirst = DateTime.Now; }
            sigClicks++;
            if (sigClicks >= 5) { sigClicks = 0; ShowDiag(); return; }
        }
        WinDown(sender, e);
    }
    private void WinDown(object sender, MouseEventArgs e) { draggingWin = true; dragOff = e.Location; }
    private void WinMove(object sender, MouseEventArgs e) { if (draggingWin) this.Location = new Point(this.Location.X + e.X - dragOff.X, this.Location.Y + e.Y - dragOff.Y); }
    private void WinUp(object sender, MouseEventArgs e) { draggingWin = false; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 베젤(폰 바디)만 그림 — 화면 내용물은 screen 패널이 그림
        Rectangle bodyR = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        using (GraphicsPath bp = AppButton.Round(bodyR, 32))
        {
            this.Region = new Region(bp);
            using (SolidBrush b = new SolidBrush(BEZEL)) g.FillPath(b, bp);
            // 클리핑 계단현상 완화: 같은 경로를 안티앨리어싱 테두리로 덧그림
            using (Pen p = new Pen(Color.FromArgb(150, 40, 44, 54), 1.6F)) g.DrawPath(p, bp);
        }
    }

    // 액정 내부: 다이내믹 아일랜드 · 서명 · 홈 인디케이터
    private void ScreenPaint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // 액정 모서리: 클리핑 계단현상 완화용 안티앨리어싱 테두리
        using (GraphicsPath sp = AppButton.Round(new Rectangle(0, 0, SCRW - 1, SCRH - 1), 25))
        using (Pen sepen = new Pen(Theme.ScreenEdge, 1.4F)) g.DrawPath(sepen, sp);

        int iw = 108, ih = 24, ix = (SCRW - iw) / 2, iy = 8;
        using (GraphicsPath ip = AppButton.Round(new Rectangle(ix, iy, iw, ih), 12))
        using (SolidBrush b = new SolidBrush(ISLAND)) g.FillPath(b, ip);
        if (islandText.Length > 0)
        {
            int badgeW = islandCount > 0 ? 26 : 0;   // 개수 배지 자리
            Rectangle txtR = new Rectangle(ix + 6, iy, iw - 12 - badgeW, ih);
            TextRenderer.DrawText(g, islandText, Fonts.Semi(11.5F), txtR,
                Theme.IslandText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            if (islandCount > 0)
            {
                // 알림 개수 = 초록 원형 배지 (숫자 정확 중앙정렬: DrawString + Center)
                int bs = 20, bx2 = ix + iw - bs - 8, by2 = iy + (ih - bs) / 2;
                using (SolidBrush gb = new SolidBrush(Color.FromArgb(52, 199, 89)))
                    g.FillEllipse(gb, bx2, by2, bs, bs);
                Font bf = Fonts.SemiPx(13F); if (bf != null)
                    TextRenderer.DrawText(g, islandCount.ToString(), bf, new Rectangle(bx2, by2 + 1, bs, bs), Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }

        // 서명 (대기 중 아래)
        TextRenderer.DrawText(g, "© 600g", Fonts.Semi(8F),
            new Rectangle(0, SCRH - 34, SCRW, 13),
            Theme.Hint,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        // 홈 인디케이터
        int hw = 98, hx = (SCRW - hw) / 2, hy = SCRH - 14;
        using (GraphicsPath hp = AppButton.Round(new Rectangle(hx, hy, hw, 5), 3))
        using (SolidBrush b = new SolidBrush(Theme.HomeBar)) g.FillPath(b, hp);
    }

    // 아일랜드만 정확히 다시 그린다. (예전엔 상단 46px 전체를 무효화해서 최소화/닫기 버튼까지
    //  1초마다 다시 그려지며 깜빡였음 → 아일랜드 사각형 + 여백 2px 만 무효화)
    private void SetIsland(string t)
    {
        islandText = t; if (t.Length == 0) islandCount = 0;
        string sig = islandText + "|" + islandCount;
        if (sig == islandSig) return;   // 표시 내용이 그대로면 다시 그리지 않음 (깜빡임 방지)
        islandSig = sig;
        if (screen != null)
        {
            int iw = 108, ih = 24, ix = (SCRW - iw) / 2, iy = 8;
            screen.Invalidate(new Rectangle(ix - 2, iy - 2, iw + 4, ih + 4));
        }
    }
    private void SetIslandPower(string t) { islandCount = 0; SetIsland(t); }

    // ── 연한 태그 스타일 토스트 (BETA 배지 느낌): 연초록/연빨강 바탕 + 같은 계열 글씨 ──
    private ScreenPanel toastP; private Timer toastT; private string toastMsg = ""; private Color toastTxt = Color.Gray;
    private void ShowToast(string msg, bool positive)
    {
        try
        {
            toastMsg = msg;
            Color bg2 = positive ? Color.FromArgb(223, 245, 231) : Color.FromArgb(253, 233, 233);
            toastTxt = positive ? Color.FromArgb(24, 138, 72) : Color.FromArgb(206, 62, 62);
            if (toastP == null)
            {
                toastP = new ScreenPanel();
                toastP.Paint += delegate(object s2, PaintEventArgs pe)
                {
                    pe.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    TextRenderer.DrawText(pe.Graphics, toastMsg, Fonts.Semi(9.5F), toastP.ClientRectangle,
                        toastTxt, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                };
                toastP.Click += delegate { toastP.Visible = false; };
                screen.Controls.Add(toastP);
                toastT = new Timer(); toastT.Interval = 2200;
                toastT.Tick += delegate { toastT.Stop(); toastP.Visible = false; };
            }
            toastP.BackColor = bg2;
            int w = TextRenderer.MeasureText(toastMsg, Fonts.Semi(9.5F)).Width + 40;
            if (w < 120) w = 120; if (w > SCRW - 36) w = SCRW - 36;
            int h = 32;
            toastP.Size = new Size(w, h);
            toastP.Location = new Point((SCRW - w) / 2, SCRH - 250);
            try { toastP.Region = new Region(AppButton.Round(new Rectangle(0, 0, w, h), 6)); } catch { }
            toastP.Visible = true; toastP.BringToFront(); toastP.Invalidate();
            toastT.Stop(); toastT.Start();
        }
        catch { }
    }

    private AppButton MakeSeg(string text, int x, int y, int w)
    {
        AppButton b = new AppButton();
        b.Text = text; b.Location = new Point(x, y); b.Size = new Size(w, 38); b.Radius = 8; b.Font = Fonts.Semi(10F);
        return b;
    }

    // 리스트를 iOS 카드(둥근 흰 박스, 무테두리)로 감싸기
    private Panel MakeCard(ListBox inner, int x, int y, int w, int h, Panel parent) { return MakeCard(inner, x, y, w, h, parent, false); }
    private Panel MakeCard(ListBox inner, int x, int y, int w, int h, Panel parent, bool hideScroll)
    {
        ScreenPanel card = new ScreenPanel();
        card.Location = new Point(x, y); card.Size = new Size(w, h);
        card.BackColor = Theme.Card;
        try { card.Region = new Region(AppButton.Round(new Rectangle(0, 0, w, h), 6)); } catch { }
        card.Paint += delegate(object s2, PaintEventArgs pe) {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (Pen p = new Pen(Theme.Border, 1F))
            using (GraphicsPath gp = AppButton.Round(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 6))
                g.DrawPath(p, gp);
            // 비어 있을 때 안내문: 리스트 대신 카드(더블버퍼·단색 배경)가 직접 그려 선명하게
            DbListBox dl = inner as DbListBox;
            if (dl != null && dl.Items.Count == 0 && dl.EmptyHint.Length > 0)
                TextRenderer.DrawText(g, dl.EmptyHint, Fonts.Regular(10F), new Rectangle(8, 0, card.Width - 16, card.Height),
                    Theme.Hint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        };
        DbListBox dbl = inner as DbListBox;
        if (dbl != null)
        {
            dbl.HintByParent = true;
            if (hideScroll) dbl.ClipWidth = w - 12 - 10;   // 카드 오른쪽 테두리(x=w-1) 앞까지만 그림
            dbl.EmptyChanged += delegate { card.Invalidate(); };
        }
        inner.BorderStyle = BorderStyle.None;
        inner.BackColor = Theme.Card; inner.ForeColor = Theme.Ink;
        // hideScroll: 리스트를 카드보다 넓게 만들어 윈도우 스크롤바가 카드 밖(둥근 클립 영역 밖)으로 밀려 안 보이게 함
        inner.Location = new Point(10, 8);
        inner.Size = new Size(hideScroll ? (w - 10 + 18) : (w - 20), h - 16);
        card.Controls.Add(inner);
        parent.Controls.Add(card);
        return card;
    }

    // iOS 느낌 입력 pill: 둥근 흰 박스에 입력 컨트롤을 넣고 테두리는 은은하게
    private ScreenPanel MakePill(Control inner, int x, int y, int w, int h, Panel parent)
    {
        ScreenPanel pill = new ScreenPanel();
        pill.Location = new Point(x, y); pill.Size = new Size(w, h);
        pill.BackColor = Theme.Card;
        int rad = Math.Min(6, h / 2);
        try { pill.Region = new Region(AppButton.Round(new Rectangle(0, 0, w, h), rad)); } catch { }
        pill.Paint += delegate(object s2, PaintEventArgs pe) {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; pe.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            using (Pen p = new Pen(Theme.PillBorder, 1F))
            using (GraphicsPath gp = AppButton.Round(new Rectangle(0, 0, pill.Width - 1, pill.Height - 1), rad))
                pe.Graphics.DrawPath(p, gp);
        };
        if (inner is TextBox) ((TextBox)inner).BorderStyle = BorderStyle.None;
        if (inner is NumericUpDown) ((NumericUpDown)inner).BorderStyle = BorderStyle.None;
        inner.BackColor = pill.BackColor; inner.ForeColor = Theme.Ink;
        int ih = inner.Height;
        inner.Location = new Point(12, Math.Max(0, (h - ih) / 2));
        inner.Width = w - 24;
        pill.Controls.Add(inner);
        parent.Controls.Add(pill);
        return pill;
    }

    private AppButton MakeGhost(string text, int x, int y, int w, int h)
    {
        AppButton b = new AppButton();
        b.Text = text; b.Location = new Point(x, y); b.Size = new Size(w, h); b.Radius = 7; b.Font = Fonts.Semi(9.5F);
        b.Fill = Theme.GhostFill; b.FillHover = Theme.GhostHover; b.FillDown = Theme.GhostDown;
        b.TextColor = Theme.GhostText; b.BorderColor = Theme.GhostBorder;
        return b;
    }

    private void SetMode(int m)
    {
        mode = m;
        if (m != 3) lastTab = m;   // 톱니를 다시 누르면 돌아올 탭
        if (m != 2 && editIndex >= 0) { txtMsg.Text = ""; ExitEditMode(); }   // 알림 탭 벗어나면 수정 취소
        StyleSeg(segPower, m == 0);
        StyleSeg(segApp, m == 1);
        StyleSeg(segAlarm, m == 2);
        panelApp.Visible = (m == 1);
        panelAlarm.Visible = (m == 2);
        if (panelSet != null) panelSet.Visible = (m == 3);
        if (btnGear != null)
        {   // 설정 화면일 때 톱니 강조
            btnGear.Fill = (m == 3) ? Theme.GearActiveFill : BG;
            btnGear.IconColor = (m == 3) ? ACCENT : Theme.GearIcon;
            btnGear.Invalidate();
        }
        if (m == 3)
        {   // 설정 화면: 타이머/버튼 전부 숨김
            btnStart.Visible = false; btnCancel.Visible = false;
            lblAfterF.Visible = false; numMinutes.Visible = false; lblUnitF.Visible = false; lblEta.Visible = false;
            if (panelPresets != null) panelPresets.Visible = false;
            RefreshAutoRunToggle();
            RefreshNotiWarn();
            RefreshStatus();
            return;
        }
        if (panelSched != null) panelSched.Visible = (m == 0);
        // 알림 탭은 토글=예약이라 시작/중지 버튼 불필요
        btnStart.Visible = (m != 2);
        btnCancel.Visible = (m != 2);

        bool showTimer = (m != 2);
        numMinutes.Visible = showTimer; lblEta.Visible = showTimer;
        if (m == 0)
        {
            // 전원 끄기: 이 탭의 주인공은 타이머 → 크게, 가운데
            numMinutes.Size = new Size(128, 50); numMinutes.Font = Fonts.System(19F);
            int unitW = 46, groupW = 128 + 10 + unitW, gx = (SCRW - groupW) / 2, gy = 190;
            numMinutes.Location = new Point(gx, gy);
            lblAfterF.Visible = false;
            lblUnitF.Font = Fonts.Semi(12.5F); lblUnitF.ForeColor = INK; lblUnitF.Visible = true;
            lblUnitF.Location = new Point(gx + 128 + 10, gy + 15);
            lblEta.Location = new Point(20, gy + 60); lblEta.TextAlign = ContentAlignment.MiddleCenter;
            presetCentered = true;
            if (panelPresets != null) { panelPresets.Visible = true; panelPresets.Location = new Point(20, gy + 92); }
        }
        else if (m == 1)
        {
            numMinutes.Size = new Size(88, 34); numMinutes.Font = Fonts.System(12F);
            int ty = cardYConst + panelApp.Height + 12;
            lblAfterF.Visible = true; lblAfterF.Location = new Point(20, ty + 8);
            numMinutes.Location = new Point(92, ty);
            lblUnitF.Font = Fonts.Regular(10.5F); lblUnitF.ForeColor = MUTED; lblUnitF.Visible = true;
            lblUnitF.Location = new Point(188, ty + 9);
            lblEta.Location = new Point(20, ty + 40); lblEta.TextAlign = ContentAlignment.MiddleLeft;
            presetCentered = false;
            if (panelPresets != null) { panelPresets.Visible = true; panelPresets.Location = new Point(20, ty + 62); }
        }
        else
        {
            lblAfterF.Visible = false; lblUnitF.Visible = false;
            if (panelPresets != null) panelPresets.Visible = false;
        }
        RebuildPresets();
        UpdateEta();

        RefreshStatus();
        UpdateButtons();
    }

    // ───── 윈도우 알림 켜짐 여부 확인 / 켜기 유도 ─────
    private Label lblNotiWarn;
    private static bool WindowsToastEnabled()
    {
        // HKCU\...\PushNotifications\ToastEnabled = 0 이면 윈도우 알림 전체가 꺼진 상태 (NotifyIcon 풍선도 안 뜸)
        try
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return true;
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\PushNotifications"))
            {
                if (k == null) return true;
                object v = k.GetValue("ToastEnabled");
                if (v is int) return (int)v != 0;
            }
        }
        catch { }
        return true;
    }
    private static void OpenWindowsNotificationSettings()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("ms-settings:notifications");
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        catch { try { Process.Start("explorer.exe", "ms-settings:notifications"); } catch { } }
    }
    // 설정 화면 경고 라벨 갱신 (윈도우 알림을 쓰는데 OS에서 꺼져 있으면 표시)
    private void RefreshNotiWarn()
    {
        if (lblNotiWarn == null) return;
        bool warn = notifyKind != 0 && !WindowsToastEnabled();
        lblNotiWarn.Visible = warn;
    }
    private void PromptEnableWindowsNotifications()
    {
        DialogResult r = MessageBox.Show(
            "윈도우 알림이 꺼져 있어 '윈도우 알림'이 표시되지 않습니다.\n\n윈도우 설정에서 알림을 켜시겠습니까?\n(설정 → 시스템 → 알림)",
            "윈도우 알림 꺼짐", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (r == DialogResult.Yes) OpenWindowsNotificationSettings();
    }

    // ───── 다크 모드 ─────
    private Toggle chkDark; private bool applyingTheme = false;
    private void SetDarkMode(bool dark, bool save)
    {
        if (Theme.Dark == dark && chkDark != null && chkDark.Checked == dark) { if (save) { try { SaveSettings(); } catch { } } return; }
        Dictionary<string, Color> oldP = Theme.Snapshot();
        Theme.Apply(dark);
        Dictionary<string, Color> newP = Theme.Snapshot();
        // 옛 색 → 새 색 매핑 (컨트롤이 생성 시 담아둔 색을 그대로 치환)
        Dictionary<int, Color> map = new Dictionary<int, Color>();
        foreach (KeyValuePair<string, Color> kv in oldP)
            if (newP.ContainsKey(kv.Key) && !map.ContainsKey(kv.Value.ToArgb())) map[kv.Value.ToArgb()] = newP[kv.Key];
        try
        {
            this.BackColor = BEZEL;
            RemapTree(this, map);
            if (chkDark != null) { applyingTheme = true; chkDark.Checked = dark; applyingTheme = false; }
            this.Invalidate(true);
        }
        catch { }
        if (save) { try { SaveSettings(); } catch { } }
    }
    private static Color Re(Color c, Dictionary<int, Color> map)
    {
        Color o; return (c != Color.Empty && map.TryGetValue(c.ToArgb(), out o)) ? o : c;
    }
    private void RemapTree(Control root, Dictionary<int, Color> map)
    {
        foreach (Control c in root.Controls)
        {
            try
            {
                if (!(c is AppButton) && !(c is GearButton) && !(c is Toggle) && !(c is IosStepper))
                {
                    c.BackColor = Re(c.BackColor, map);
                    c.ForeColor = Re(c.ForeColor, map);
                }
                AppButton ab = c as AppButton;
                if (ab != null)
                {
                    ab.Fill = Re(ab.Fill, map); ab.FillHover = Re(ab.FillHover, map); ab.FillDown = Re(ab.FillDown, map);
                    ab.BorderColor = Re(ab.BorderColor, map);
                    if (ab.TextColor.ToArgb() != Color.White.ToArgb()) ab.TextColor = Re(ab.TextColor, map);   // 주 버튼 흰 글씨는 유지
                }
                GearButton gb = c as GearButton;
                if (gb != null) { gb.Fill = Re(gb.Fill, map); gb.FillHover = Re(gb.FillHover, map); gb.FillDown = Re(gb.FillDown, map); gb.IconColor = Re(gb.IconColor, map); }
                IosStepper st = c as IosStepper;
                if (st != null) st.RefreshTheme();
                c.Invalidate();
            }
            catch { }
            if (c.Controls.Count > 0) RemapTree(c, map);
        }
    }

    // 알림 표시 방식 선택 (0=팝업만 · 1=윈도우 알림만 · 2=둘 다)
    // ───── 윈도우 시작 시 자동 실행 (작업 스케줄러) ─────
    // 이 앱은 관리자 권한(requireAdministrator)이라 시작프로그램(Run 키)에 넣으면
    // 로그온 때 UAC를 띄울 수 없어 윈도우가 조용히 무시한다.
    // 그래서 "가장 높은 권한으로 실행" 작업으로 등록한다.
    private const string TASK_NAME = "AutoShutdownTimer_600g";
    private bool autoRunBusy = false;

    private int RunWait(string file, string args, out string outp)
    {
        outp = "";
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args);
            psi.CreateNoWindow = true; psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            using (Process pr = Process.Start(psi))
            {
                outp = pr.StandardOutput.ReadToEnd() + pr.StandardError.ReadToEnd();
                pr.WaitForExit(8000);
                return pr.HasExited ? pr.ExitCode : -1;
            }
        }
        catch (Exception ex) { outp = ex.Message; return -1; }
    }

    private bool IsAutoRunOn()
    {
        string o;
        return RunWait("schtasks", "/query /TN \"" + TASK_NAME + "\"", out o) == 0;
    }

    // 예전 빌드에서 켠 자동 실행 작업은 명령에 --tray 가 없어 시작 시 창이 뜬다.
    // 켜져 있는데 --tray 가 빠져 있으면 조용히 다시 등록해 고친다. (관리자 권한이라 가능)
    private void EnsureAutoRunTray()
    {
        try
        {
            string o;
            if (RunWait("schtasks", "/query /TN \"" + TASK_NAME + "\" /V /FO LIST", out o) != 0) return;  // 작업 없음
            if (o != null && o.IndexOf("--tray", StringComparison.OrdinalIgnoreCase) >= 0) return;         // 이미 정상
            string exe = Application.ExecutablePath, o2;
            RunWait("schtasks",
                "/create /F /TN \"" + TASK_NAME + "\" /TR \"\\\"" + exe + "\\\" --tray\" /SC ONLOGON /RL HIGHEST", out o2);
            Log("자동 실행 작업 --tray 로 갱신");
        }
        catch { }
    }

    private void RefreshAutoRunToggle()
    {
        if (chkAutoRun == null) return;
        try
        {
            autoRunBusy = true;
            chkAutoRun.Checked = IsAutoRunOn();
        }
        catch { }
        finally { autoRunBusy = false; }
    }

    private void ApplyAutoRun(bool on)
    {
        if (autoRunBusy) return;
        string exe = Application.ExecutablePath, o;
        int rc;
        if (on)
        {
            rc = RunWait("schtasks",
                "/create /F /TN \"" + TASK_NAME + "\" /TR \"\\\"" + exe + "\\\" --tray\" /SC ONLOGON /RL HIGHEST", out o);
            if (rc == 0) { Log("자동 실행 등록됨"); ShowToast("자동 실행 켜짐", true); }
            else
            {
                Log("자동 실행 등록 실패(" + rc + "): " + o);
                ShowToast("자동 실행 등록 실패", false);
                autoRunBusy = true; chkAutoRun.Checked = false; autoRunBusy = false;
            }
        }
        else
        {
            rc = RunWait("schtasks", "/delete /F /TN \"" + TASK_NAME + "\"", out o);
            if (rc == 0) { Log("자동 실행 해제됨"); ShowToast("자동 실행 꺼짐", false); }
            else { Log("자동 실행 해제 실패(" + rc + "): " + o); ShowToast("자동 실행 해제 실패", false); }
        }
    }

    private void SetNotifyKind(int k)
    {
        if (k < 0 || k > 2) k = 2;
        notifyKind = k;
        if (setTogPu == null) return;
        setTogPu.Invalidate(); setTogBe.Invalidate();
        try { SaveSettings(); } catch { }
        RefreshNotiWarn();
        // 사용자가 직접 '윈도우 알림/둘 다'를 골랐는데 OS 알림이 꺼져 있으면 켜도록 안내 (초기 로드 시엔 안 띄움)
        if (k != 0 && loadDone && !WindowsToastEnabled()) PromptEnableWindowsNotifications();
    }

    private void StyleSeg(AppButton b, bool active)
    {
        if (active)
        {
            b.Fill = ACCENT; b.FillHover = Color.FromArgb(74, 134, 234); b.FillDown = Color.FromArgb(44, 102, 200);
            b.TextColor = Color.White; b.BorderColor = Color.Empty;
        }
        else
        {
            b.Fill = Theme.SegFill; b.FillHover = Theme.SegHover; b.FillDown = Theme.SegDown;
            b.TextColor = Theme.SegText; b.BorderColor = Color.Empty;
        }
        b.Invalidate();
    }

    // ───── 예약 시각(저장) ─────
    private string SchedPath
    {
        get
        {
            string d = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShutdownTimer");
            try { if (!System.IO.Directory.Exists(d)) System.IO.Directory.CreateDirectory(d); } catch { }
            return System.IO.Path.Combine(d, "scheds.txt");
        }
    }
    private void LoadScheds()
    {
        try
        {
            if (System.IO.File.Exists(SchedPath))
                foreach (string line in System.IO.File.ReadAllLines(SchedPath))
                {
                    string[] a = line.Split(new char[] { ':' });
                    if (a.Length >= 3) { Sched sc = new Sched(); int h, m; int.TryParse(a[0], out h); int.TryParse(a[1], out m); sc.H = h; sc.M = m; sc.On = a[2] == "1"; scheds.Add(sc); }
                }
            RefreshSchedBox();
        }
        catch { }
    }
    private void SaveScheds()
    {
        try
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (Sched sc in scheds) sb.AppendLine(sc.ToString());
            System.IO.File.WriteAllText(SchedPath, sb.ToString());
        }
        catch { }
    }

    // ───── 마지막 설정값 저장/복원 (타이머 분, 알림 문구, 토글, 대상 목록) ─────
    // exe 옆 저장 우선 (관리자 실행이라 항상 쓰기 가능 · UAC 계정과 무관하게 동일 위치)
    private string ExeSettingsPath
    {
        get
        {
            try { return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "settings.txt"); }
            catch { return null; }
        }
    }
    private string SettingsPath
    {
        get
        {
            string d = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShutdownTimer");
            try { if (!System.IO.Directory.Exists(d)) System.IO.Directory.CreateDirectory(d); } catch { }
            return System.IO.Path.Combine(d, "settings.txt");
        }
    }
    private const string VERSION = "1.0.0";   // 배포 버전 (semver) — 태그 v1.0.1 과 같은 값
    private int sigClicks = 0; private DateTime sigFirst = DateTime.MinValue;
    private string loadedFrom = null;   // 진단: 설정을 어디서 불러왔는지
    private bool saveErrShown = false;
    private string lastSaveInfo = "(이번 실행에서 저장 없음)";

    // v1.0 클릭 → 저장 위치 진단 창
    private void ShowDiag()
    {
        try
        {
            // 진단 시점엔 읽기가 되는 것이 확인됐으므로, 아직 못 불러온 상태면 여기서 즉시 복구
            if (loadedFrom == null)
            {
                try
                {
                    LoadSettings();
                    if (loadedFrom != null)
                    {
                        RebuildPresets(); RelayoutAlarms(); ArmLoadedAlarms();
                        ShowToast("설정 복구됨 · " + loadedFrom + (defMin.Count > 0 ? " · 알림 " + defMin.Count + "개" : ""), true);
                    }
                }
                catch { }
            }
            // ── 먼저 상태 판정 (정상/주의/문제) ──
            string exeP = "?"; try { exeP = Application.ExecutablePath; } catch { }
            bool temp = exeP.IndexOf(@"\AppData\Local\Temp", StringComparison.OrdinalIgnoreCase) >= 0
                     || exeP.IndexOf(@"\Temp\", StringComparison.OrdinalIgnoreCase) >= 0;
            bool regOk = false;
            try { using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ShutdownTimer")) { object rv = rk == null ? null : rk.GetValue("settings"); regOk = rv != null && rv.ToString().Trim().Length > 0; } } catch { }
            bool loadOk = loadedFrom != null;
            bool hasReadErr = loadErr.Length > 0;

            string verdict; string icon;
            if (temp)
            { verdict = "⚠ 주의: 압축을 풀지 않고 실행 중입니다.\r\n    zip을 폴더에 '압축 풀기' 한 뒤 실행하세요."; icon = "warn"; }
            else if (loadOk)
            { verdict = "✓ 정상입니다. 저장/불러오기가 잘 되고 있어요.\r\n    (오류가 아니라 상태 확인 화면입니다)"; icon = "ok"; }
            else if (regOk && !loadOk)
            { verdict = "△ 데이터는 저장돼 있는데 이번 실행에서 못 읽었습니다.\r\n    잠시 후 자동 복구되거나, 이 창을 열면 복구됩니다."; icon = "warn"; }
            else
            { verdict = "· 아직 저장된 설정이 없습니다. (알림/타이머를 추가하면 저장됩니다)"; icon = "info"; }

            MessageBoxIcon mi = icon == "ok" ? MessageBoxIcon.Information
                              : icon == "warn" ? MessageBoxIcon.Warning
                              : MessageBoxIcon.Information;

            System.Text.StringBuilder d = new System.Text.StringBuilder();
            d.AppendLine("[ 상태 ]");
            d.AppendLine(verdict);
            d.AppendLine();
            d.AppendLine("─────────────────────────────");
            d.AppendLine("아래는 참고용 상세 정보입니다 (오류 아님).");
            d.AppendLine();
            d.AppendLine("버전: " + VERSION);
            try
            {
                using (Graphics gg = CreateGraphics())
                    d.AppendLine("화면 DPI: " + (int)gg.DpiX + " (배율 " + Math.Round(gg.DpiX / 96.0 * 100) + "%)");
            }
            catch { }
            try
            {
                d.AppendLine("폰트: " + (Fonts.UsingPretendard ? "Pretendard 내장 사용 중" : "⚠ 시스템 폰트로 대체됨(내장 폰트 로드 실패)"));
                Font tf = Fonts.SemiPx(13F); if (tf != null) d.AppendLine("  적용 서체: " + tf.Name + " / " + Math.Round(tf.Size, 1) + "px");
            }
            catch { }
            d.AppendLine("실행 파일:");
            d.AppendLine("  " + exeP);
            d.AppendLine();
            try
            {
                using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ShutdownTimer"))
                {
                    object rv = rk == null ? null : rk.GetValue("settings");
                    d.AppendLine("레지스트리 설정: " + (rv != null ? "있음 (" + rv.ToString().Length + "자)" : "없음"));
                    d.AppendLine(@"  HKCU\Software\ShutdownTimer");
                }
            }
            catch (Exception ex) { d.AppendLine("레지스트리 확인 오류: " + ex.Message); }
            d.AppendLine();
            try
            {
                string es = ExeSettingsPath;
                bool ee = es != null && System.IO.File.Exists(es);
                d.AppendLine("exe 옆 설정(구버전): " + (ee ? "있음" : "없음"));
            }
            catch (Exception ex) { d.AppendLine("exe 옆 설정 확인 오류: " + ex.Message); }
            try
            {
                string ap = SettingsPath;
                bool ae = System.IO.File.Exists(ap);
                d.AppendLine("AppData 설정: " + (ae ? "있음 (" + new System.IO.FileInfo(ap).Length + "바이트)" : "없음"));
                d.AppendLine("  " + ap);
            }
            catch (Exception ex) { d.AppendLine("AppData 설정 확인 오류: " + ex.Message); }
            d.AppendLine();
            try { d.AppendLine("실행 계정: " + Environment.UserDomainName + @"\" + Environment.UserName + (IsAdmin() ? " (관리자)" : "")); } catch { }
            d.AppendLine("이번 실행에서 불러온 곳: " + (loadedFrom ?? "없음 (설정을 못 찾음)"));
            d.AppendLine("읽기 오류 기록: " + (hasReadErr ? loadErr : "(없음 — 정상)"));
            d.AppendLine("마지막 저장 결과: " + lastSaveInfo);
            d.AppendLine();
            d.AppendLine("── 최근 동작 로그 (참고용) ──");
            try
            {
                string[] ll = logBuf.ToString().Split(new char[] { '\n' });
                int st = ll.Length > 15 ? ll.Length - 15 : 0;
                for (int q = st; q < ll.Length; q++) if (ll[q].Trim().Length > 0) d.AppendLine(ll[q].TrimEnd());
            }
            catch { }
            // 클립보드에 자동 복사 → 채팅에 붙여넣기만 하면 됨
            bool copied = false;
            try { Clipboard.SetText(d.ToString()); copied = true; } catch { }
            // 화면은 앱 디자인 시트로 띄운다. 클립보드로 나가는 원문(d)은 위에서 이미 복사했고
            // 지원 요청에 그대로 쓰이므로 한 글자도 바꾸지 않는다.
            string dump = d.ToString();
            AppSheet ds = new AppSheet("상태 확인", "v" + VERSION,
                                       copied ? "내용이 클립보드에 복사됐습니다" : null);
            Color sc = icon == "ok" ? OKC : (icon == "warn" ? ACCENT : MUTED);
            if (temp) sc = DANGER;
            string headline = verdict;
            string detail = null;
            int nl = verdict.IndexOf("\r\n");
            if (nl > 0) { headline = verdict.Substring(0, nl).Trim(); detail = verdict.Substring(nl).Trim(); }
            if (headline.Length > 2 && (headline[0] == '✓' || headline[0] == '△' || headline[0] == '·' || headline[0] == '⚠'))
                headline = headline.Substring(1).Trim();
            ds.SetStatus(headline, sc);
            if (detail != null) ds.Body.AddSub(detail);
            ds.Body.AddHead("상세");
            FillDiagBody(ds.Body, dump);
            ds.Tell(this, "닫기");
        }
        catch (Exception ex)
        {
            MessageBox.Show("진단 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    // 진단 원문(클립보드용 텍스트)을 시트 블록으로 옮긴다.
    // 원문 형식을 바꾸지 않고 화면만 앱답게 보이게 하는 방식이라, 지원 워크플로가 그대로 유지된다.
    private void FillDiagBody(SheetBody body, string dump)
    {
        if (body == null || string.IsNullOrEmpty(dump)) return;
        try
        {
            string[] lines = dump.Replace("\r", "").Split('\n');
            bool inLog = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.Length == 0) continue;
                if (t.StartsWith("─") || t.StartsWith("[ 상태 ]")) continue;
                if (t.StartsWith("✓") || t.StartsWith("△") || t.StartsWith("⚠") || t.StartsWith("· 아직")) continue;
                if (t.StartsWith("아래는 참고용")) continue;
                if (t.StartsWith("(이 내용이 클립보드")) continue;
                if (t.StartsWith("──") || t.IndexOf("최근 동작 로그") >= 0)
                {
                    inLog = true;
                    body.AddHead("최근 기록");
                    continue;
                }
                if (inLog) { body.AddLog(t); continue; }
                int c = t.IndexOf(": ");
                if (c > 0 && c <= 14)
                {
                    string k = t.Substring(0, c), v = t.Substring(c + 2).Trim();
                    if (v.IndexOf("\\") >= 0 && v.Length > 24) body.AddPath(k, v);
                    else body.AddKV(k, v);
                    continue;
                }
                body.AddText(t);
            }
        }
        catch { }
    }

    private string loadErr = "";   // 진단: 읽기 실패의 실제 오류
    private void LoadSettings()
    {
        try
        {
            // 1순위: 레지스트리(파일 검사·OneDrive 영향 없음) → 2순위: 기존 파일(이전 버전 이관용)
            string[] lines = null; string src = null;
            try
            {
                using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ShutdownTimer"))
                {
                    if (rk != null)
                    {
                        object rv = rk.GetValue("settings");
                        if (rv != null && rv.ToString().Trim().Length > 0)
                        { lines = rv.ToString().Replace("\r", "").Split(new char[] { '\n' }); src = "레지스트리"; }
                    }
                }
            }
            catch (Exception ex) { loadErr += "[레지스트리] " + ex.GetType().Name + ": " + ex.Message + "  "; }
            if (lines == null)
            {
                string[] cands = new string[] { ExeSettingsPath, SettingsPath };
                string[] names = new string[] { "exe 옆", "AppData" };
                for (int c = 0; c < cands.Length; c++)
                {
                    if (cands[c] == null) continue;
                    try { lines = System.IO.File.ReadAllLines(cands[c]); src = names[c]; break; }
                    catch (System.IO.FileNotFoundException) { }
                    catch (System.IO.DirectoryNotFoundException) { }
                    catch (Exception ex) { loadErr += "[" + names[c] + "] " + ex.GetType().Name + ": " + ex.Message + "  "; }
                }
            }
            if (lines == null)
            { Log("설정 읽기 실패" + (loadErr.Length > 0 ? " · " + loadErr : " · 저장된 데이터 없음")); return; }
            loadedFrom = src;
            Log("설정 읽기 성공 · " + src + " · " + lines.Length + "줄");
            foreach (string line in lines)
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1);
                try
                {
                    if (k == "min") { decimal d; if (decimal.TryParse(v, out d) && d >= numMinutes.Minimum && d <= numMinutes.Maximum) numMinutes.Value = d; }
                    else if (k == "amin") { decimal d; if (decimal.TryParse(v, out d) && d >= numAlarmMin.Minimum && d <= numAlarmMin.Maximum) numAlarmMin.Value = d; }
                    else if (k == "gap") { decimal d; if (decimal.TryParse(v, out d) && d >= numGap.Minimum && d <= numGap.Maximum) numGap.Value = d; }
                    else if (k == "msg" && v.Length > 0) txtMsg.Text = v;
                    else if (k == "gaming") chkGaming.Checked = v == "1";
                    else if (k == "noti") { int nk; if (int.TryParse(v, out nk)) SetNotifyKind(nk); }
                    else if (k == "dark") { if (v == "1") SetDarkMode(true, false); }
                    else if (k == "seq") chkSeq.Checked = v == "1";
                    else if (k == "fb") chkFallback.Checked = v == "1";
                    else if (k == "presets")
                    {
                        presets.Clear();
                        foreach (string t in v.Split(new char[] { ',' }))
                        { int pv; if (int.TryParse(t.Trim(), out pv) && pv >= 1 && pv <= 1440 && !presets.Contains(pv)) presets.Add(pv); }
                    }
                    else if (k == "alarm")
                    {
                        // 최신: val|on|kind|msg|show|note · 이전1: val|on|kind|msg|note · 이전2: val|on|kind|msg · 구: val|on|msg · 초기: val|msg
                        int am; bool aon = true; int akind = 0; string amsg = null; string anote = ""; int ashow = -1; int adays = 0x7F;
                        string[] p7 = v.Split(new char[] { '|' }, 7);
                        if (p7.Length >= 4 && (p7[1] == "0" || p7[1] == "1") && (p7[2] == "0" || p7[2] == "1"))
                        {
                            aon = p7[1] == "1"; akind = int.Parse(p7[2]); amsg = p7[3];
                            if (p7.Length == 7)   // 최신: 5=show 6=days 7=note
                            { int sv; if (int.TryParse(p7[4], out sv) && sv >= -1 && sv <= 2) ashow = sv; int dv; if (int.TryParse(p7[5], out dv)) adays = dv & 0x7F; anote = DecNote(p7[6]); }
                            else if (p7.Length == 6)   // 이전: 5=show 6=note (days 없음)
                            { int sv; if (int.TryParse(p7[4], out sv) && sv >= -1 && sv <= 2) ashow = sv; anote = DecNote(p7[5]); }
                            else if (p7.Length == 5)   // 더 이전: 5=note (show·days 없음)
                            { anote = DecNote(p7[4]); }
                        }
                        else
                        {
                            string[] parts = v.Split(new char[] { '|' }, 3);
                            if (parts.Length == 3 && (parts[1] == "0" || parts[1] == "1"))
                            { aon = parts[1] == "1"; amsg = parts[2]; }
                            else if (parts.Length >= 2)
                                amsg = v.Substring(v.IndexOf('|') + 1);
                        }
                        string first = v.Split(new char[] { '|' })[0];
                        bool okVal = int.TryParse(first, out am) && ((akind == 1 && am >= 0 && am <= 1439) || (akind == 0 && am >= 1 && am <= 1440));
                        if (amsg != null && okVal && amsg.Length > 0)
                        {
                            bool dupA = false;   // 늦은 병합 시 중복 방지
                            for (int q = 0; q < defMin.Count && q < defMsg.Count; q++)
                                if (defMin[q] == am && defMsg[q] == amsg && q < defKind.Count && defKind[q] == akind) dupA = true;
                            if (!dupA)
                            { defMin.Add(am); defMsg.Add(amsg); defNote.Add(anote); defShow.Add(ashow); defDays.Add(adays); defOn.Add(aon); defKind.Add(akind); lstAlarms.Items.Add(AlarmLabel(am, akind, amsg)); }
                        }
                    }
                    else if (k == "target" && v.Length > 0)
                    {
                        bool dup = false;
                        foreach (object o in lstTargets.Items) if (o.ToString() == v) dup = true;
                        if (!dup) lstTargets.Items.Add(v);
                    }
                }
                catch { }
            }
            // 파일에서 불러왔다면 레지스트리로 이관 (다음부터는 레지스트리에서 바로)
            if (loadedFrom != null && loadedFrom != "레지스트리")
            { try { SaveSettings(); } catch { } }
        }
        catch { }
    }
    // 자주 쓰는 타이머 칩 다시 그리기 (클릭=적용 · 우클릭=삭제 · +=현재값 저장)
    private const int PRESET_ROWS = 2;   // 칩 최대 줄 수
    private int presetCap = 7;           // 실제 저장 가능 개수 (레이아웃에서 계산)
    // 열려있는 숫자 입력 커서를 모두 확정/종료 (엔터 안 눌러도 값이 반영되게)
    private void CommitAllSteppers()
    {
        try { if (numMinutes != null) numMinutes.CommitEdit(); } catch { }
        try { if (numGap != null) numGap.CommitEdit(); } catch { }
        try { if (numAlarmMin != null) numAlarmMin.CommitEdit(); } catch { }
        try { if (numAlarmH != null) numAlarmH.CommitEdit(); } catch { }
        try { if (numAlarmM != null) numAlarmM.CommitEdit(); } catch { }
    }
    // "→ 오후 6:41 전원 끄기 예정" 미리보기 갱신
    private void UpdateEta()
    {
        if (lblEta == null || numMinutes == null) return;
        try
        {
            DateTime now = DateTime.Now, eta;
            if (powerRunning) eta = now.AddSeconds(remainingSeconds);
            else eta = now.AddMinutes((double)numMinutes.Value);
            System.Globalization.CultureInfo ko = System.Globalization.CultureInfo.GetCultureInfo("ko-KR");
            string t = eta.ToString("tt h:mm", ko);
            if (eta.Date > now.Date) t = "내일 " + t;
            string what = (mode == 1 || (powerRunning && powerKind == 1)) ? "프로그램 종료" : "전원 끄기";
            lblEta.Text = "→ " + t + " " + what + (powerRunning ? " 예정 · 진행 중" : " 예정");
        }
        catch { lblEta.Text = ""; }
    }

    private void RebuildPresets()
    {
        if (panelPresets == null) return;
        panelPresets.Controls.Clear();

        int cw = 54, gap = 6, stride = cw + gap, addW = 32;
        int maxW = panelPresets.Width;
        int perRow = (maxW + gap) / stride; if (perRow < 1) perRow = 1;
        presetCap = perRow * PRESET_ROWS - 1;      // 마지막 칸은 + 버튼 자리
        if (presetCap < 1) presetCap = 1;

        int shown = presets.Count; if (shown > presetCap) shown = presetCap;
        // 줄별 시작 x (가운데 정렬 옵션)
        int total = shown + 1;   // + 버튼 포함
        int[] rowOff = new int[PRESET_ROWS + 1];
        for (int r = 0; r <= PRESET_ROWS; r++)
        {
            int first = r * perRow, cnt = Math.Max(0, Math.Min(perRow, total - first));
            if (cnt == 0) { rowOff[r] = 0; continue; }
            bool hasAdd = (first + cnt == total);
            int rw = cnt * stride - gap - (hasAdd ? (cw - addW) : 0);
            rowOff[r] = presetCentered ? Math.Max(0, (maxW - rw) / 2) : 0;
        }
        for (int i = 0; i < shown; i++)
        {
            int cx = rowOff[i / perRow] + (i % perRow) * stride, cy = (i / perRow) * 30;
            AppButton c = MakeGhost(presets[i] + "분", cx, cy, cw, 26); c.Radius = 7; c.Font = Fonts.Semi(9.5F);
            int pv = presets[i];
            c.Click += delegate { if (pv >= 1 && pv <= 1440) numMinutes.Value = pv; };
            c.MouseUp += delegate(object s2, MouseEventArgs me) {
                if (me.Button == MouseButtons.Right) { presets.Remove(pv); RebuildPresets(); SaveSettings(); ShowToast(pv + "분 삭제됨", false); } };
            tip.SetToolTip(c, "클릭: 적용 · 우클릭: 삭제");
            panelPresets.Controls.Add(c);
        }

        int ax = rowOff[shown / perRow] + (shown % perRow) * stride, ay = (shown / perRow) * 30;
        AppButton add = MakeGhost("+", ax, ay, addW, 26); add.Radius = 7; add.Font = Fonts.Semi(11F);
        add.Click += delegate { AddPresetFromStepper(); };
        tip.SetToolTip(add, "현재 분값을 자주 쓰는 목록에 저장 (최대 " + presetCap + "개)");
        panelPresets.Controls.Add(add);

        int rows = (shown / perRow) + 1;
        panelPresets.Height = rows * 30 - 4;
    }

    // + 버튼: 스텝퍼가 입력 중이어도 지금 타이핑한 값을 그대로 저장한다
    private void AddPresetFromStepper()
    {
        if (numMinutes == null) return;
        numMinutes.CommitEdit();                 // 커서/편집 상태 해제 후 값 확정
        int v = (int)numMinutes.Value;
        if (v < 1 || v > 1440) { ShowToast("1~1440분만 저장할 수 있어요", false); return; }
        if (presets.Contains(v)) { ShowToast(v + "분은 이미 저장돼 있어요", false); return; }
        if (presets.Count >= presetCap)
        {
            ShowToast("최대 " + presetCap + "개까지예요 · 칩 우클릭으로 삭제", false);
            return;   // 저장 안 됐는데 "저장됨"이라고 하지 않는다
        }
        presets.Add(v); presets.Sort();
        RebuildPresets(); SaveSettings();
        ShowToast(v + "분 저장됨", true);
    }

    private bool merging = false;
    private void SaveSettings()
    {
        try
        {
            // 아직 저장소를 못 읽은 상태에서 저장하면 기존 데이터가 날아가므로, 먼저 병합 시도
            if (loadedFrom == null && !merging)
            {
                merging = true;
                try { LoadSettings(); RebuildPresets(); RelayoutAlarms(); ArmLoadedAlarms(); } catch { }
                merging = false;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            System.Text.StringBuilder ps = new System.Text.StringBuilder();
            foreach (int p in presets) { if (ps.Length > 0) ps.Append(','); ps.Append(p); }
            sb.AppendLine("presets=" + ps);
            sb.AppendLine("min=" + numMinutes.Value);
            sb.AppendLine("amin=" + numAlarmMin.Value);
            sb.AppendLine("gap=" + numGap.Value);
            sb.AppendLine("msg=" + txtMsg.Text.Replace("\r", " ").Replace("\n", " "));
            sb.AppendLine("gaming=" + (chkGaming.Checked ? "1" : "0"));
            sb.AppendLine("noti=" + notifyKind);
            sb.AppendLine("dark=" + (Theme.Dark ? "1" : "0"));
            sb.AppendLine("seq=" + (chkSeq.Checked ? "1" : "0"));
            sb.AppendLine("fb=" + (chkFallback.Checked ? "1" : "0"));
            foreach (object o in lstTargets.Items) sb.AppendLine("target=" + o);
            for (int i = 0; i < defMin.Count && i < defMsg.Count; i++)
                sb.AppendLine("alarm=" + defMin[i] + "|" + ((i < defOn.Count && defOn[i]) ? "1" : "0") + "|" + (i < defKind.Count ? defKind[i] : 0) + "|" + defMsg[i].Replace("\r", " ").Replace("\n", " ").Replace("|", " ") + "|" + ShowAt(i) + "|" + DaysAt(i) + "|" + EncNote(NoteAt(i)));
            string data = sb.ToString();
            bool okReg = false, okApp = false; string err = "";
            // 1순위: 레지스트리 (파일 없이 exe 하나로 동작)
            try
            {
                using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\ShutdownTimer"))
                { rk.SetValue("settings", data); }
                okReg = true;
            }
            catch (Exception e0) { err += "[레지스트리] " + e0.Message + "  "; }
            // 2순위 백업: AppData 파일
            try
            {
                System.IO.File.WriteAllText(SettingsPath, data);
                okApp = System.IO.File.Exists(SettingsPath);
            }
            catch (Exception e2) { err += "[AppData] " + e2.Message; }
            lastSaveInfo = DateTime.Now.ToString("HH:mm:ss") + " 레지스트리=" + (okReg ? "OK" : "실패") + " AppData=" + (okApp ? "OK" : "실패") + (err.Length > 0 ? " · " + err : "");
            Log("설정 저장: " + lastSaveInfo);
            if (!okReg && !okApp)
            {
                if (lblStatus != null) { lblStatus.Text = "설정 저장 실패"; lblStatus.ForeColor = DANGER; }
                ShowToast("설정 저장 실패", false);
                if (!saveErrShown)
                {
                    saveErrShown = true;   // 같은 오류 반복 팝업 방지
                    MessageBox.Show("설정을 저장할 수 없습니다.\r\n\r\n" + err +
                        "\r\n\r\n이 메시지를 개발자에게 알려주세요.", "저장 오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch { }
    }
    private void RefreshSchedBox()
    {
        if (schedBox == null) return;
        try
        {
            scheds.Sort(delegate(Sched a, Sched b) { return (a.H * 60 + a.M).CompareTo(b.H * 60 + b.M); });
            schedBox.Items.Clear();
            foreach (Sched sc in scheds) schedBox.Items.Add(sc);
            schedBox.Invalidate();
        }
        catch { }
        UpdateSchedTimer();
    }
    // 켜진 예약이 있을 때만 감시 타이머 동작
    private void UpdateSchedTimer()
    {
        if (schedTimer == null) return;
        try
        {
            bool any = false;
            foreach (Sched sc in scheds) if (sc.On) { any = true; break; }
            if (any && !schedTimer.Enabled) schedTimer.Start();
            if (!any && schedTimer.Enabled) schedTimer.Stop();
        }
        catch { }
    }
    private void SchedTick(object sender, EventArgs e)
    {
        try { CheckScheds(); } catch { }
    }
    private void AddSchedClick(object sender, EventArgs e)
    {
        int h = (int)sHour.Value, m = (int)sMin.Value;
        foreach (Sched sc in scheds) if (sc.H == h && sc.M == m) return;
        Sched n = new Sched(); n.H = h; n.M = m; n.On = true; scheds.Add(n);
        RefreshSchedBox(); SaveScheds();
    }
    private void DelSched()
    {
        int i = schedBox.SelectedIndex;
        if (i >= 0 && i < scheds.Count) { scheds.RemoveAt(i); RefreshSchedBox(); SaveScheds(); }
    }
    private void SchedMouse(object sender, MouseEventArgs e)
    {
        int i = schedBox.IndexFromPoint(e.Location);
        if (i < 0 || i >= scheds.Count) return;
        schedBox.SelectedIndex = i;
        if (e.X > schedBox.Width - 64) { scheds[i].On = !scheds[i].On; schedBox.Invalidate(); SaveScheds(); UpdateSchedTimer(); }
    }
    private void SchedDraw(object sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= scheds.Count) return;
        Sched sc = scheds[e.Index];
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = e.Bounds;
        TextRenderer.DrawText(g, sc.Key, Fonts.Semi(14F), new Rectangle(r.X + 14, r.Y, 120, r.Height), sc.On ? INK : Color.FromArgb(170, 176, 186), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        // 토글
        int tw = 44, th = 24, tx = r.Right - tw - 12, tyv = r.Y + (r.Height - th) / 2;
        using (GraphicsPath tp = AppButton.Round(new Rectangle(tx, tyv, tw, th), th / 2))
        using (SolidBrush b = new SolidBrush(sc.On ? Color.FromArgb(52, 199, 89) : Color.FromArgb(206, 210, 218))) g.FillPath(b, tp);
        int kd = th - 6, kx = sc.On ? tx + tw - kd - 3 : tx + 3;
        using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, kx, tyv + 3, kd, kd);
    }
    private void CheckScheds()
    {
        string mnow = DateTime.Now.ToString("HHmm");
        if (mnow == lastMin) return;
        lastMin = mnow;
        int hh = DateTime.Now.Hour, mm = DateTime.Now.Minute;
        foreach (Sched sc in scheds)
            if (sc.On && sc.H == hh && sc.M == mm)
            {
                Log("예약 시각 전원 끄기 " + sc.Key);
                lblStatus.Text = "예약 시각 · 전원을 끕니다"; lblStatus.ForeColor = DANGER;
                RunHidden("shutdown", "/s /f /t 0");
                break;
            }
    }

    // ───── 관리자 ─────
    public static bool IsAdmin()
    {
        try
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent())
                   .IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    // ───── 프로세스 목록 ─────
    private void ComboDraw(object sender, DrawItemEventArgs e)
    {
        ListBox lb = sender as ListBox;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (SolidBrush bg2 = new SolidBrush(sel ? Theme.RowSelSoft : Theme.Card))
            e.Graphics.FillRectangle(bg2, e.Bounds);
        if (lb == null || e.Index < 0 || e.Index >= lb.Items.Count) return;
        object o = lb.Items[e.Index];
        ProcItem it = o as ProcItem;
        int x = e.Bounds.X + 10;
        if (it != null && it.Ico != null)
        {
            try { e.Graphics.DrawImage(it.Ico, x, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 16, 16); } catch { }
        }
        x += 24;
        string txt = it != null ? it.Display : o.ToString();
        TextRenderer.DrawText(e.Graphics, txt, Fonts.Regular(9.5F),
            new Rectangle(x, e.Bounds.Y, Math.Max(20, (lb.Parent != null ? lb.Parent.ClientSize.Width - 6 : e.Bounds.Width) - x - 22), e.Bounds.Height),
            INK, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    private static System.Drawing.Image IconFor(Process p)
    {
        try
        {
            string path = null;
            try { path = p.MainModule.FileName; } catch { }
            if (string.IsNullOrEmpty(path)) return null;
            using (Icon ic = Icon.ExtractAssociatedIcon(path))
            {
                if (ic == null) return null;
                Bitmap bmp = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(ic.ToBitmap(), new Rectangle(0, 0, 16, 16));
                }
                return bmp;
            }
        }
        catch { return null; }
    }

    // 실행 중인 앱 목록 팝업 (윈도우 콤보박스 대신 iOS 카드 스타일)
    private void ShowProcPicker(List<ProcItem> items)
    {
        try { if (procPop != null) { procPop.Close(); procPop = null; } } catch { }
        Form pop = new Form();
        pop.FormBorderStyle = FormBorderStyle.None; pop.ShowInTaskbar = false; pop.StartPosition = FormStartPosition.Manual;
        pop.BackColor = Theme.Card; pop.TopMost = true;
        int pw = pillProg.Width + 84 + 8, ph = Math.Min(7, Math.Max(1, items.Count)) * 30 + 12;
        pop.ClientSize = new Size(pw, ph);
        try { pop.Region = new Region(AppButton.Round(new Rectangle(0, 0, pw, ph), 10)); } catch { }
        pop.Paint += delegate(object s2, PaintEventArgs pe)
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; pe.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            using (GraphicsPath bp = AppButton.Round(new Rectangle(0, 0, pw - 1, ph - 1), 10))
            using (Pen pen = new Pen(Theme.PillBorder, 1F)) pe.Graphics.DrawPath(pen, bp);
        };
        DbListBox lb = new DbListBox(); lb.BackColor = Theme.Card; lb.ForeColor = Theme.Ink;
        lb.BorderStyle = BorderStyle.None; lb.DrawMode = DrawMode.OwnerDrawFixed; lb.ItemHeight = 30;
        lb.IntegralHeight = false; lb.Location = new Point(6, 6); lb.Size = new Size(pw - 12 + 20, ph - 12);   // 스크롤바는 둥근 영역 밖으로 밀어 숨김
        lb.EmptyHint = "실행 중인 창이 없습니다";
        lb.DrawItem += ComboDraw;
        foreach (ProcItem it in items) lb.Items.Add(it);
        lb.Click += delegate
        {
            ProcItem it = lb.SelectedItem as ProcItem;
            if (it != null) { txtProg.Text = it.Name; }
            try { pop.Close(); } catch { }
        };
        lb.KeyDown += delegate(object s2, KeyEventArgs k)
        {
            if (k.KeyCode == Keys.Enter) { ProcItem it = lb.SelectedItem as ProcItem; if (it != null) txtProg.Text = it.Name; pop.Close(); }
            else if (k.KeyCode == Keys.Escape) pop.Close();
        };
        pop.Controls.Add(lb);
        pop.Deactivate += delegate { try { pop.Close(); } catch { } };
        pop.FormClosed += delegate { procPop = null; };
        Point scr = pillProg.PointToScreen(new Point(0, pillProg.Height + 4));
        pop.Location = scr;
        procPop = pop;
        pop.Show(this);
        lb.Focus();
    }

    private void RefreshClick(object sender, EventArgs e)
    {
        List<ProcItem> items = new List<ProcItem>();
        HashSet<string> seen = new HashSet<string>();
        bool all = chkAll.Checked;
        try
        {
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    string t = "";
                    try { t = p.MainWindowTitle; } catch { }
                    if (t == null || t == "null") t = "";
                    if (!all && t.Length == 0) continue;
                    string img = p.ProcessName + ".exe";
                    if (!seen.Add(img.ToLower())) continue;
                    items.Add(new ProcItem {
                        Name = img,
                        Display = string.IsNullOrEmpty(t) ? img : t + "  —  " + img,
                        Ico = IconFor(p)
                    });
                }
                catch { }
            }
        }
        catch { }

        items.Sort(delegate(ProcItem a, ProcItem b) {
            return string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase);
        });

        ShowProcPicker(items);
    }

    private void BrowseClick(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*";
        ofd.Title = "종료할 프로그램 선택";
        if (ofd.ShowDialog() == DialogResult.OK)
            txtProg.Text = Path.GetFileName(ofd.FileName);
    }

    private string Normalize(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return "";
        if (!name.ToLower().EndsWith(".exe")) name += ".exe";
        return name;
    }

    private void AddClick(object sender, EventArgs e)
    {
        string name = Normalize(txtProg.Text);
        if (name.Length == 0) return;
        foreach (object o in lstTargets.Items)
            if (string.Equals(o.ToString(), name, StringComparison.OrdinalIgnoreCase)) return;
        lstTargets.Items.Add(name);
        txtProg.Text = "";
        try { SaveSettings(); } catch { }
    }

    private void RemoveClick(object sender, EventArgs e)
    {
        if (lstTargets.SelectedIndex >= 0)
        {
            lstTargets.Items.RemoveAt(lstTargets.SelectedIndex);
            try { SaveSettings(); } catch { }
        }
    }

    private List<string> CollectTargets()
    {
        List<string> list = new List<string>();
        foreach (object o in lstTargets.Items) list.Add(o.ToString());
        string typed = Normalize(txtProg.Text);
        if (typed.Length > 0)
        {
            bool dup = false;
            foreach (string s in list)
                if (string.Equals(s, typed, StringComparison.OrdinalIgnoreCase)) dup = true;
            if (!dup) list.Add(typed);
        }
        return list;
    }

    // ───── 로그(내부 보관, 평소 숨김) ─────
    private void Log(string msg)
    {
        logBuf.AppendLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg);
    }

    private void ShowLogClick(object sender, EventArgs e)
    {
        Form f = new Form();
        f.Text = "실행 기록";
        f.ClientSize = new Size(560, 380);
        f.StartPosition = FormStartPosition.CenterParent;
        f.BackColor = Color.White;
        TextBox t = new TextBox();
        t.Multiline = true;
        t.ReadOnly = true;
        t.ScrollBars = ScrollBars.Both;
        t.WordWrap = false;
        t.Dock = DockStyle.Fill;
        t.BorderStyle = BorderStyle.None;
        t.Font = Fonts.System(9F);
        t.Text = logBuf.ToString();
        f.Controls.Add(t);
        f.ShowDialog(this);
    }

    private void SaveLogClick(object sender, EventArgs e)
    {
        SaveFileDialog sfd = new SaveFileDialog();
        sfd.Filter = "텍스트 파일 (*.txt)|*.txt";
        sfd.FileName = "ShutdownTimer_log_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt";
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                File.WriteAllText(sfd.FileName, logBuf.ToString(), Encoding.UTF8);
                MessageBox.Show("저장했습니다.", "완료",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 실패: " + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void HideFail()
    {
        panelFail.Visible = false;
        lblCountdown.Visible = false;
        lblStatus.Visible = true;
    }

    private void ShowFail(string text)
    {
        lblFail.Text = text;
        lblCountdown.Visible = false;
        lblStatus.Visible = false;
        panelFail.Visible = true;
        panelFail.BringToFront();
    }

    // ───── 시작 / 취소 ─────
    private void StartClick(object sender, EventArgs e)
    {
        CommitAllSteppers();
        HideFail();
        if (mode == 1 && CollectTargets().Count == 0)
        {
            MessageBox.Show("종료할 프로그램을 먼저 추가하세요.", "안내",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (mode == 2)
        {
            // 입력창에 문구가 있는데 목록에 안 넣었으면 자동 추가
            if ((txtMsg.Text ?? "").Trim().Length > 0 && defMin.Count == lstAlarms.Items.Count)
                AddAlarmClick(null, null);
            if (defMin.Count == 0)
            {
                MessageBox.Show("알림을 하나 이상 추가하세요.", "안내",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            while (defOn.Count < defMin.Count) defOn.Add(true);
            while (defKind.Count < defMin.Count) defKind.Add(0);
            int onCount = 0;
            foreach (bool b in defOn) if (b) onCount++;
            if (onCount == 0)
            {
                MessageBox.Show("켜진 알림이 없어요. 우측 토글을 켜주세요.", "안내",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            runRemain = new int[onCount];
            runMsg = new string[onCount];
            int ri = 0;
            DateTime now = DateTime.Now;
            for (int i = 0; i < defMin.Count; i++)
                if (defOn[i])
                {
                    int secs;
                    if (defKind[i] == 1)   // 특정 시각 → 오늘 그 시각까지, 지났으면 내일
                    {
                        DateTime target = now.Date.AddMinutes(defMin[i]);
                        if (target <= now) target = target.AddDays(1);
                        secs = (int)(target - now).TotalSeconds;
                        if (secs < 1) secs = 1;
                    }
                    else secs = defMin[i] * 60;   // 분 뒤
                    runRemain[ri] = secs; runMsg[ri] = defMsg[i]; ri++;
                }
            alarmRunning = true;
            UpdateButtons();
            lstAlarms.Invalidate();   // 실행 중 표시 갱신
            lblStatus.Text = "알림 " + onCount + "개 예약됨";
            lblStatus.ForeColor = ACCENT;
            ShowToast("알림 " + onCount + "개 실행 중", true);
            Log("알림 예약: " + onCount + "개");
            TickAlarm2Init();
            if (!timer.Enabled) timer.Start();
            return;
        }
        remainingSeconds = (int)numMinutes.Value * 60;
        warned = false;
        powerRunning = true; powerKind = mode;
        if (mode == 1) pendingTargets = CollectTargets();
        UpdateButtons();
        RefreshStatus(); UpdateEta();
        Log("예약: 모드=" + mode + " / " + numMinutes.Value + "분 후");
        UpdateCountdown(); SetIslandPower(lblCountdown.Text);
        if (!timer.Enabled) timer.Start();
    }

    private void CancelClick(object sender, EventArgs e)
    {
        // 알림은 토글이 곧 예약 상태 → 여기서 건드리면 토글은 켜져 있는데 안 울리는 상태가 됨. 무시.
        if (mode == 2 || !powerRunning) return;
        powerRunning = false; killStepTimer.Stop();
        if (!alarmRunning) { SetIsland(""); timer.Stop(); }
        else UpdateAlarmIsland();
        UpdateButtons();
        RefreshStatus(); UpdateEta();
        ShowToast("중지됨", false);
        Log("중지 (모드 " + mode + ")");
    }

    // 하단 상태줄은 "지금 보고 있는 탭"의 상태만 보여준다 (알림 개수는 아일랜드 배지가 이미 표시)
    private void RefreshStatus()
    {
        if (lblStatus == null) return;
        if (mode == 3) { lblStatus.Text = "설정"; lblStatus.ForeColor = MUTED; return; }
        if (mode == 2)
        {
            int cnt = 0;
            for (int i = 0; i < defMin.Count; i++)
                if (i < defOn.Count && defOn[i] && i < almTarget.Count && almTarget[i] != DateTime.MaxValue) cnt++;
            if (cnt > 0) { lblStatus.Text = "알림 " + cnt + "개 예약됨"; lblStatus.ForeColor = ACCENT; }
            else { lblStatus.Text = "대기 중"; lblStatus.ForeColor = MUTED; }
            return;
        }
        if (powerRunning)
        {
            if (powerKind == 0) lblStatus.Text = "전원 끄기 예약됨";
            else lblStatus.Text = "프로그램 " + (pendingTargets != null ? pendingTargets.Count : 0) + "개 종료 예약됨";
            lblStatus.ForeColor = ACCENT;
        }
        else { lblStatus.Text = "대기 중"; lblStatus.ForeColor = MUTED; }
    }

    private void UpdateButtons()
    {
        bool cur = (mode == 2) ? alarmRunning : powerRunning;
        btnStart.Enabled = !cur;
        btnCancel.Enabled = cur;
    }
    private void LockUI(bool running) { UpdateButtons(); }

    private void TimerTick(object sender, EventArgs e)
    {
        if (alarmRunning) AlarmTick();
        if (powerRunning)
        {
            remainingSeconds--;
            if (remainingSeconds <= 60 && !warned)
            {
                warned = true;
                lblStatus.Text = "1분 후 실행됩니다";
                lblStatus.ForeColor = DANGER;
                try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
            }
            if (remainingSeconds <= 0) { powerRunning = false; FirePower(); }
            else { UpdateCountdown(); SetIslandPower(lblCountdown.Text); }
        }
        if (!powerRunning && !alarmRunning) timer.Stop();
        UpdateButtons();
    }

    private void UpdateCountdown()
    {
        int m = remainingSeconds / 60, s = remainingSeconds % 60;
        lblCountdown.Text = m.ToString("00") + ":" + s.ToString("00");
    }

    // ───── 알림: 토글=예약 (시작 버튼 불필요) ─────
    private void ArmLoadedAlarms()
    {
        EnsureAlmSize();
        for (int i = 0; i < defMin.Count; i++)
            if (i < defOn.Count && defOn[i] && almTarget[i] == DateTime.MaxValue) ArmAlarm(i);
        SyncAlarmTimer();
    }
    private void EnsureAlmSize()
    {
        while (almTarget.Count < defMin.Count) almTarget.Add(DateTime.MaxValue);
        while (almTarget.Count > defMin.Count) almTarget.RemoveAt(almTarget.Count - 1);
        while (defNote.Count < defMin.Count) defNote.Add("");
        while (defNote.Count > defMin.Count) defNote.RemoveAt(defNote.Count - 1);
        while (defShow.Count < defMin.Count) defShow.Add(-1);
        while (defShow.Count > defMin.Count) defShow.RemoveAt(defShow.Count - 1);
        while (defDays.Count < defMin.Count) defDays.Add(0x7F);
        while (defDays.Count > defMin.Count) defDays.RemoveAt(defDays.Count - 1);
    }
    private int DaysAt(int i) { return (i >= 0 && i < defDays.Count) ? defDays[i] : 0x7F; }
    // .NET DayOfWeek(일=0..토=6) → 우리 비트(월=0..일=6)
    private static int DowBit(DateTime d) { int w = (int)d.DayOfWeek; return (w + 6) % 7; }
    // 지정 시각(하루 중 분)·요일마스크로 다음 발동 시각 (mask 0 또는 0x7F = 매일)
    private DateTime NextDailyTarget(int minutesOfDay, int mask)
    {
        DateTime now = DateTime.Now;
        DateTime t = now.Date.AddMinutes(minutesOfDay);
        for (int add = 0; add <= 8; add++)
        {
            DateTime cand = t.AddDays(add);
            if (cand <= now) continue;
            if (mask == 0 || (mask & 0x7F) == 0x7F || (mask & (1 << DowBit(cand))) != 0) return cand;
        }
        return t.AddDays(1);
    }
    private string NoteAt(int i) { return (i >= 0 && i < defNote.Count && defNote[i] != null) ? defNote[i] : ""; }
    // 행 우측 팝업/종 아이콘 중심 x (그리기·클릭 판정 공용)
    private void AlarmIconCenters(out int popupCx, out int bellCx)
    {
        int visR = (alarmCard != null ? alarmCard.Width - 10 : lstAlarms.ClientSize.Width);
        int tw = 32, tx = visR - tw - 20;
        bellCx = (tx - 8) - 8;            // 종: 토글 왼쪽
        popupCx = bellCx - 15 - 4;        // 팝업: 종 왼쪽
    }
    // 행에서 팝업/종 아이콘을 눌러 채널 토글 (최소 하나 유지)
    private void ToggleAlarmChannel(int i, bool popup)
    {
        EnsureAlmSize();
        int sv = ShowAt(i); if (sv < 0 || sv > 2) sv = notifyKind;
        bool pu = (sv == 0 || sv == 2), be = (sv == 1 || sv == 2);
        if (popup) { pu = !pu; if (!pu && !be) be = true; } else { be = !be; if (!be && !pu) pu = true; }
        if (i < defShow.Count) defShow[i] = pu && be ? 2 : (pu ? 0 : 1);
        try { SaveSettings(); } catch { }
        lstAlarms.Invalidate();
    }
    private int ShowAt(int i) { return (i >= 0 && i < defShow.Count) ? defShow[i] : -1; }   // -1=기본
    // 실제 발동 시 사용할 방식: 개별값(-1이면 전역 notifyKind)
    private int EffShow(int i) { int v = ShowAt(i); return (v < 0 || v > 2) ? notifyKind : v; }

    // 저장용: 여러 줄 메모를 한 줄로 인코딩(줄바꿈 \n, 역슬래시 이스케이프). |는 마지막 필드라 그대로 둬도 됨
    private static string EncNote(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");
    }
    private static string DecNote(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char n = s[i + 1];
                if (n == 'n') { b.Append('\n'); i++; continue; }
                if (n == '\\') { b.Append('\\'); i++; continue; }
            }
            b.Append(s[i]);
        }
        return b.ToString();
    }
    private void ArmAlarm(int i)
    {
        EnsureAlmSize();
        if (i < 0 || i >= defMin.Count) return;
        int kind = i < defKind.Count ? defKind[i] : 0;
        if (kind == 1) almTarget[i] = NextDailyTarget(defMin[i], DaysAt(i));   // 시각: 요일 반영
        else almTarget[i] = DateTime.Now.AddSeconds(defMin[i] * 60);           // 분 뒤
    }
    private void DisarmAlarm(int i)
    {
        EnsureAlmSize();
        if (i >= 0 && i < almTarget.Count) almTarget[i] = DateTime.MaxValue;
    }
    // 켜진(예약된) 알림이 하나라도 있으면 감시 타이머 켜고 아일랜드 갱신
    private void SyncAlarmTimer()
    {
        EnsureAlmSize();
        bool any = false;
        for (int i = 0; i < defMin.Count; i++)
            if (i < defOn.Count && defOn[i] && almTarget[i] != DateTime.MaxValue) { any = true; break; }
        alarmRunning = any;
        if (any) { if (!timer.Enabled) timer.Start(); UpdateAlarmIsland(); }
        else if (!powerRunning) { SetIsland(""); timer.Stop(); if (mode == 2) RefreshStatus(); }
        if (btnCancel != null) UpdateButtons();
        InvalidateAlarmListIfChanged();
    }
    // 알림 목록은 "켜짐/예약 상태가 실제로 바뀌었을 때"만 다시 그린다.
    // (예전엔 1초마다 무조건 Invalidate 해서 목록 전체가 깜빡였음)
    private void InvalidateAlarmListIfChanged()
    {
        if (lstAlarms == null) return;
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < defMin.Count; i++)
        {
            sb.Append(defMin[i]).Append(',');
            sb.Append(i < defKind.Count ? defKind[i] : 0).Append(',');
            sb.Append((i < defOn.Count && defOn[i]) ? '1' : '0').Append(',');
            sb.Append((i < almTarget.Count && almTarget[i] != DateTime.MaxValue) ? '1' : '0').Append(';');
        }
        string sig = sb.ToString();
        if (sig == alarmListSig) return;
        alarmListSig = sig;
        lstAlarms.Invalidate();
    }
    private void UpdateAlarmIsland()
    {
        DateTime now = DateTime.Now; TimeSpan soonest = TimeSpan.MaxValue; int cnt = 0;
        for (int i = 0; i < defMin.Count; i++)
            if (i < defOn.Count && defOn[i] && almTarget[i] != DateTime.MaxValue)
            { cnt++; TimeSpan t = almTarget[i] - now; if (t < soonest) soonest = t; }
        if (cnt == 0) { islandCount = 0; if (!powerRunning) SetIsland(""); return; }
        if (soonest < TimeSpan.Zero) soonest = TimeSpan.Zero;
        string txt = soonest.TotalHours >= 1
            ? ((int)soonest.TotalHours) + ":" + soonest.Minutes.ToString("00") + ":" + soonest.Seconds.ToString("00")
            : soonest.Minutes.ToString("00") + ":" + soonest.Seconds.ToString("00");
        islandCount = cnt;
        SetIsland(txt);
        if (mode == 2) RefreshStatus();
    }
    // 매초 호출: 예약 시각 도달한 알림 울리기
    private void AlarmTick()
    {
        EnsureAlmSize();
        DateTime now = DateTime.Now; bool changed = false;
        for (int i = 0; i < defMin.Count; i++)
        {
            if (i >= defOn.Count || !defOn[i]) continue;
            if (almTarget[i] == DateTime.MaxValue) continue;
            if (now >= almTarget[i])
            {
                FireAlarm(defMsg[i], NoteAt(i), EffShow(i));
                int kind = i < defKind.Count ? defKind[i] : 0;
                if (kind == 1 && DaysAt(i) != 0)   // 특정 시각·요일 반복 → 다음 해당 요일로
                { almTarget[i] = NextDailyTarget(defMin[i], DaysAt(i)); }
                else             // 분 뒤 · 또는 요일 전부 해제(1회성) → 끄기
                { defOn[i] = false; almTarget[i] = DateTime.MaxValue; changed = true; }
            }
        }
        if (changed) { try { SaveSettings(); } catch { } }
        SyncAlarmTimer();
    }

    // ───── 알림 목록 ─────
    private void AddAlarmClick(object sender, EventArgs e)
    {
        CommitAllSteppers();
        string msg = (txtMsg.Text ?? "").Trim();
        if (msg.Length == 0) msg = "시간이 되었습니다";
        int val, kind;
        if (alarmMode == 1)   // 특정 시각
        { kind = 1; val = (int)numAlarmH.Value * 60 + (int)numAlarmM.Value; }
        else                   // 분 뒤
        { kind = 0; val = (int)numAlarmMin.Value; }

        if (editIndex >= 0 && editIndex < defMin.Count)   // ── 기존 항목 수정 ──
        {
            int i = editIndex;
            defMin[i] = val; defMsg[i] = msg; defKind[i] = kind;
            lstAlarms.Items[i] = AlarmLabel(val, kind, msg);
            if (i < defOn.Count && defOn[i]) ArmAlarm(i);   // 켜져 있으면 새 값으로 다시 예약
            txtMsg.Text = "";
            ExitEditMode();
            lstAlarms.Invalidate();
            try { SaveSettings(); } catch { }
            SyncAlarmTimer();
            ShowToast("수정됨", true);
            return;
        }

        // ── 새 항목 추가 (켜진 상태 = 바로 예약) ──
        defMin.Add(val); defMsg.Add(msg); defNote.Add(""); defShow.Add(-1); defDays.Add(0x7F); defOn.Add(true); defKind.Add(kind);
        lstAlarms.Items.Add(AlarmLabel(val, kind, msg));
        ArmAlarm(defMin.Count - 1);
        txtMsg.Text = "";
        try { SaveSettings(); } catch { }
        RelayoutAlarms();
        SyncAlarmTimer();
        ShowToast(kind == 1 ? (val / 60).ToString("00") + ":" + (val % 60).ToString("00") + " 예약됨" : val + "분 뒤 예약됨", true);
    }

    private int SecondsForAlarm(int val, int kind)
    {
        if (kind == 1)
        {
            DateTime now = DateTime.Now;
            DateTime target = now.Date.AddMinutes(val);
            if (target <= now) target = target.AddDays(1);
            int s = (int)(target - now).TotalSeconds;
            return s < 1 ? 1 : s;
        }
        return val * 60;
    }

    private string AlarmLabel(int val, int kind, string msg)
    {
        if (kind == 1) return (val / 60).ToString("00") + ":" + (val % 60).ToString("00") + "  ·  " + msg;
        return val + "분 뒤  ·  " + msg;
    }

    private void DelAlarmClick(object sender, EventArgs e)
    {
        int i = lstAlarms.SelectedIndex;
        if (i >= 0 && i < lstAlarms.Items.Count)
        {
            lstAlarms.Items.RemoveAt(i);
            defMin.RemoveAt(i);
            defMsg.RemoveAt(i);
            if (i < defNote.Count) defNote.RemoveAt(i);
            if (i < defShow.Count) defShow.RemoveAt(i);
            if (i < defDays.Count) defDays.RemoveAt(i);
            if (i < defOn.Count) defOn.RemoveAt(i);
            if (i < defKind.Count) defKind.RemoveAt(i);
            if (i < almTarget.Count) almTarget.RemoveAt(i);
            if (editIndex == i) { txtMsg.Text = ""; ExitEditMode(); }
            else if (editIndex > i) editIndex--;
            try { SaveSettings(); } catch { }
            RelayoutAlarms();
            SyncAlarmTimer();
            ShowToast("알림 삭제됨", false);
        }
    }

    // 아이폰 알람식 행 그리기: 분 · 문구 · 우측 토글
    private void AlarmDraw(object sender, DrawItemEventArgs e)
    {
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (SolidBrush bg2 = new SolidBrush(sel ? Theme.RowSel : Theme.Card))
            e.Graphics.FillRectangle(bg2, e.Bounds);
        if (e.Index < 0 || e.Index >= defMin.Count) return;
        bool on = e.Index < defOn.Count && defOn[e.Index];
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = e.Bounds;
        Color cMain = on ? INK : Theme.OffText;
        Color cSub = on ? MUTED : Theme.OffSub;
        int kind = e.Index < defKind.Count ? defKind[e.Index] : 0;
        int vv = defMin[e.Index];
        string head = kind == 1 ? (vv / 60).ToString("00") + ":" + (vv % 60).ToString("00") : vv + "분";
        bool armed = on && e.Index < almTarget.Count && almTarget[e.Index] != DateTime.MaxValue;
        // 켜진 알림은 초록 배지, 꺼진 건 밋밋하게 (배지·폰트 슬림하게)
        int bw = 52, bh = 22, bx = r.X + 8, by = r.Y + (r.Height - bh) / 2;
        using (GraphicsPath bp = AppButton.Round(new Rectangle(bx, by, bw, bh), 5))
        {
            using (SolidBrush bb = new SolidBrush(armed ? (Theme.Dark ? Color.FromArgb(34, 74, 52) : Color.FromArgb(223, 245, 231)) : Theme.BadgeOffFill)) g.FillPath(bb, bp);
            if (armed) using (Pen pn = new Pen(Color.FromArgb(52, 199, 89), 1.3F)) g.DrawPath(pn, bp);
        }
        TextRenderer.DrawText(g, head, Fonts.Semi(9.5F),
            new Rectangle(bx, by, bw, bh), armed ? (Theme.Dark ? Color.FromArgb(96, 220, 140) : Color.FromArgb(24, 138, 72)) : cMain,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        // 토글 (슬림) — 스크롤바 숨김으로 리스트가 카드보다 넓으므로 '보이는 우측' 기준으로 배치
        int visR = (alarmCard != null ? alarmCard.Width - 10 : r.Right);
        int tw = 32, th = 18, tx = visR - tw - 20, tyv = r.Y + (r.Height - th) / 2;
        bool hasNote = NoteAt(e.Index).Length > 0;
        // 우측 클러스터: [메모점] [팝업][종 아이콘 항상 표시·색상 on/off] [토글]
        int sv = ShowAt(e.Index); if (sv < 0 || sv > 2) sv = notifyKind;   // 기본이면 전역 채널로 표시
        bool puOnR = (sv == 0 || sv == 2), beOnR = (sv == 1 || sv == 2);
        int cym = r.Y + r.Height / 2;
        int bellCx, popupCx; AlarmIconCenters(out popupCx, out bellCx);
        Color onCol = on ? ACCENT : Theme.OffSub;
        Color offCol = Theme.Dark ? Color.FromArgb(74, 79, 92) : Color.FromArgb(200, 206, 215);
        using (Pen pp = new Pen(puOnR ? onCol : offCol, 1.5F) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        { AppButton.DrawPopupIcon(g, pp, popupCx, cym); }
        using (Pen bpn = new Pen(beOnR ? onCol : offCol, 1.5F) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        { AppButton.DrawBellIcon(g, bpn, bellCx, cym); }
        int cx2 = popupCx - 9 - 6;   // 아이콘 왼쪽부터 메모점/제목
        if (hasNote)   // 메모 있으면 작은 점
        { using (SolidBrush nb = new SolidBrush(on ? ACCENT : Theme.OffSub)) g.FillEllipse(nb, cx2 - 6, cym - 3, 6, 6); cx2 -= 6 + 6; }
        int msgLeft = bx + bw + 10, msgRight = cx2;
        TextRenderer.DrawText(g, defMsg[e.Index], Fonts.Regular(9F),
            new Rectangle(msgLeft, r.Y, Math.Max(10, msgRight - msgLeft), r.Height), cSub,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        using (GraphicsPath tp = AppButton.Round(new Rectangle(tx, tyv, tw, th), th / 2))
        using (SolidBrush b = new SolidBrush(on ? Color.FromArgb(52, 199, 89) : Theme.ToggleOff)) g.FillPath(b, tp);
        int kd = th - 6, kx = on ? tx + tw - kd - 3 : tx + 3;
        using (SolidBrush b = new SolidBrush(Theme.Knob)) g.FillEllipse(b, kx, tyv + 3, kd, kd);

        // 스크롤 표시(작은 썸) — 항목이 보이는 줄 수보다 많을 때 우측 끝에
        int visRows = lstAlarms.ItemHeight > 0 ? lstAlarms.Height / lstAlarms.ItemHeight : 3;
        int total = defMin.Count;
        if (total > visRows && visRows > 0)
        {
            int trackX = visR - 16;                        // 토글 오른쪽·카드 안쪽 여백에
            int trackH = lstAlarms.Height;
            float thumbH = Math.Max(20f, trackH * visRows / (float)total);
            int maxTop = total - visRows;
            float thumbY = maxTop > 0 ? (trackH - thumbH) * (lstAlarms.TopIndex / (float)maxTop) : 0;
            int segTop = Math.Max(r.Y, (int)thumbY), segBot = Math.Min(r.Bottom, (int)(thumbY + thumbH));
            if (segBot > segTop)
                using (SolidBrush sb = new SolidBrush(Theme.Dark ? Color.FromArgb(96, 102, 116) : Color.FromArgb(198, 204, 214)))
                using (GraphicsPath tp2 = AppButton.Round(new Rectangle(trackX, segTop + 1, 3, segBot - segTop - 2), 1))
                    g.FillPath(sb, tp2);
        }
    }
    private void AlarmMouse(object sender, MouseEventArgs e)
    {
        int i = lstAlarms.IndexFromPoint(e.Location);
        if (i < 0 || i >= defMin.Count)   // 공란 클릭: 선택·수정 해제
        {
            if (almClickT != null) almClickT.Stop();
            if (editIndex >= 0) { txtMsg.Text = ""; ExitEditMode(); }
            lstAlarms.SelectedIndex = -1; lstAlarms.Invalidate();
            return;
        }
        lstAlarms.SelectedIndex = i;
        int visRight = (alarmCard != null ? alarmCard.Width - 10 : lstAlarms.ClientSize.Width);
        if (e.X > visRight - 52)   // 우측 토글 영역: 켜기/끄기 = 예약/해제 (즉시)
        {
            if (almClickT != null) almClickT.Stop();
            while (defOn.Count < defMin.Count) defOn.Add(true);
            defOn[i] = !defOn[i];
            if (defOn[i]) ArmAlarm(i); else DisarmAlarm(i);
            lstAlarms.Invalidate();
            try { SaveSettings(); } catch { }
            SyncAlarmTimer();
            ShowToast(defOn[i] ? "예약됨" : "해제됨", defOn[i]);
            return;
        }
        // 팝업/종 아이콘 클릭 → 해당 채널 토글 (즉시)
        int pcx, bcx; AlarmIconCenters(out pcx, out bcx);
        if (e.X >= pcx - 9 && e.X <= pcx + 9) { if (almClickT != null) almClickT.Stop(); ToggleAlarmChannel(i, true); return; }
        if (e.X >= bcx - 9 && e.X <= bcx + 9) { if (almClickT != null) almClickT.Stop(); ToggleAlarmChannel(i, false); return; }
        // 행 본문: 단일클릭=수정 / 더블클릭=메모. 더블클릭 오인 방지 위해 지연 판별.
        almDbl = false; almPendIdx = i;
        if (almClickT == null) { almClickT = new Timer(); almClickT.Interval = 260; almClickT.Tick += AlmSingleClick; }
        almClickT.Stop(); almClickT.Start();
    }
    private void AlmSingleClick(object sender, EventArgs e)
    {
        almClickT.Stop();
        if (almDbl) { almDbl = false; return; }   // 더블클릭이었으면 무시(메모가 열림)
        int i = almPendIdx;
        if (i < 0 || i >= defMin.Count) return;
        if (editIndex == i)   // 같은 행 다시 → 해제(추가 모드)
        { txtMsg.Text = ""; lstAlarms.SelectedIndex = -1; ExitEditMode(); lstAlarms.Invalidate(); }
        else LoadAlarmForEdit(i);
    }

    // 알림 행을 눌러 입력창으로 불러오기 → "수정" 모드
    private void LoadAlarmForEdit(int i)
    {
        if (i < 0 || i >= defMin.Count) return;
        editIndex = i;
        int kind = i < defKind.Count ? defKind[i] : 0;
        int v = defMin[i];
        SetAlarmMode(kind);
        if (kind == 1) { numAlarmH.Value = v / 60; numAlarmM.Value = v % 60; }
        else numAlarmMin.Value = Math.Max(1, Math.Min(1440, v));
        txtMsg.Text = defMsg[i];
        btnAddAlarm.Text = "수정";
        if (lahHelp != null) { lahHelp.Text = "수정 중 · 행 다시 눌러 취소"; lahHelp.ForeColor = ACCENT; }
        // 목록에 포커스를 둔다 → Delete 키가 "문구 글자 지우기"가 아니라 "알림 삭제"로 동작
        try { lstAlarms.Focus(); } catch { }
    }
    private void ExitEditMode()
    {
        editIndex = -1;
        if (btnAddAlarm != null) btnAddAlarm.Text = "추가";
        if (lahHelp != null) { lahHelp.Text = "더블클릭·Enter: 메모 추가"; lahHelp.ForeColor = MUTED; }
    }

    // 리스트 더블클릭 → 본문 메모(여러 줄) 편집
    private void OpenNoteEditor(int i)
    {
        if (i < 0 || i >= defMin.Count) return;
        EnsureAlmSize();
        int kind = i < defKind.Count ? defKind[i] : 0;
        string head = AlarmLabel(defMin[i], kind, defMsg[i]);
        int[] selDays = new int[] { DaysAt(i) };   // 시각 알림 요일 비트마스크

        Form dlg = new Form();
        dlg.FormBorderStyle = FormBorderStyle.None;
        dlg.StartPosition = FormStartPosition.CenterParent;
        dlg.ShowInTaskbar = false; dlg.BackColor = Theme.Card;
        int DW = 300, DH = 272;
        dlg.ClientSize = new Size(DW, DH);
        try { dlg.Region = new Region(AppButton.Round(new Rectangle(0, 0, DW, DH), 12)); } catch { }
        dlg.Paint += delegate(object s2, PaintEventArgs pe)
        {
            Graphics g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.Half;
            TextRenderer.DrawText(g, kind == 1 ? "요일 · 전부 해제 시 1회성" : "분 뒤 알림 · 요일 없음", Fonts.Regular(8.5F),
                new Rectangle(18, 166, DW - 36, 16), MUTED, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            using (GraphicsPath bp = AppButton.Round(new Rectangle(0, 0, DW - 1, DH - 1), 12))
            using (Pen pen = new Pen(Theme.Border, 1F)) g.DrawPath(pen, bp);
            TextRenderer.DrawText(g, "메모", Fonts.Semi(11F), new Rectangle(18, 14, DW - 36, 20), INK,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Enter 저장 · Shift+Enter 줄바꿈", Fonts.Regular(8F), new Rectangle(18, 14, DW - 36, 20), Theme.Hint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, head, Fonts.Regular(9F), new Rectangle(18, 34, DW - 36, 18), MUTED,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        };

        // 멀티라인 메모 입력칸 (둥근 흰 박스에 채워넣기)
        ScreenPanel pill = new ScreenPanel();
        pill.Location = new Point(18, 58); pill.Size = new Size(DW - 36, 100); pill.BackColor = Theme.Card;
        try { pill.Region = new Region(AppButton.Round(new Rectangle(0, 0, pill.Width, pill.Height), 8)); } catch { }
        pill.Paint += delegate(object s2, PaintEventArgs pe) {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; pe.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            using (Pen p = new Pen(Theme.PillBorder, 1F))
            using (GraphicsPath gp = AppButton.Round(new Rectangle(0, 0, pill.Width - 1, pill.Height - 1), 8))
                pe.Graphics.DrawPath(p, gp);
        };
        TextBox tb = new TextBox();
        tb.Multiline = true; tb.ScrollBars = ScrollBars.None; tb.WordWrap = true;   // 스크롤바 숨김(짧은 메모 · 캐럿 따라 자동 스크롤)
        tb.Font = new Font("맑은 고딕", 10F * Fonts.PX * Fonts.SCALE, FontStyle.Regular, GraphicsUnit.Pixel);
        tb.BorderStyle = BorderStyle.None; tb.BackColor = Theme.Card; tb.ForeColor = Theme.Ink;
        tb.Location = new Point(12, 9); tb.Size = new Size(pill.Width - 24, pill.Height - 18);   // 테두리 안쪽에 여백 두고 배치 (잘림 방지)
        tb.Text = NoteAt(i);
        tb.AcceptsReturn = true;
        EnableTextActions(tb);   // Enter 입력을 폼 기본버튼이 아니라 여기서 처리
        // Enter=저장 · Shift+Enter=줄바꿈
        tb.KeyDown += delegate(object s2, KeyEventArgs k)
        {
            if (k.KeyCode == Keys.Enter && !k.Shift)
            {
                k.SuppressKeyPress = true;
                EnsureAlmSize();
                if (i < defNote.Count) defNote[i] = (tb.Text ?? "");
                if (kind == 1 && i < defDays.Count) { defDays[i] = selDays[0] & 0x7F; if (i < defOn.Count && defOn[i]) ArmAlarm(i); }
                try { SaveSettings(); } catch { }
                lstAlarms.Invalidate(); SyncAlarmTimer();
                try { dlg.Close(); } catch { }
            }
            // Shift+Enter 는 기본 동작(줄바꿈) 그대로
        };
        pill.Controls.Add(tb);
        dlg.Controls.Add(pill);

        // 요일 선택 (시각 알림만). 월화수목금토일 · 탭 토글 · 전부 해제=1회성
        if (kind == 1)
        {
            string[] dn = new string[] { "월", "화", "수", "목", "금", "토", "일" };
            int cw = 34, cgap = 4, cx0 = 18, cy0 = 186, ch = 30;
            for (int d = 0; d < 7; d++)
            {
                ScreenPanel c = new ScreenPanel();
                c.Location = new Point(cx0 + d * (cw + cgap), cy0); c.Size = new Size(cw, ch); c.BackColor = Theme.Card;
                int bit = 1 << d; string nm = dn[d];
                c.Paint += delegate(object s2, PaintEventArgs pe)
                {
                    Graphics g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.Half;
                    bool sel = (selDays[0] & bit) != 0;
                    Color fill = sel ? ACCENT : (Theme.Dark ? Color.FromArgb(52, 56, 66) : Color.FromArgb(236, 238, 243));
                    Color fg = sel ? Color.White : Theme.OffText;
                    using (GraphicsPath bp = AppButton.Round(new Rectangle(0, 0, cw - 1, ch - 1), 8))
                    { using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, bp);
                      if (!sel) using (Pen pn = new Pen(Theme.PillBorder, 1F)) g.DrawPath(pn, bp); }
                    TextRenderer.DrawText(g, nm, Fonts.Semi(9.5F), new Rectangle(0, 0, cw, ch), fg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                };
                c.Click += delegate { selDays[0] ^= bit; ((Control)c).Invalidate(); };
                dlg.Controls.Add(c);
            }
        }

        AppButton save = new AppButton();
        save.Text = "저장"; save.Location = new Point(18, DH - 44); save.Size = new Size((DW - 36 - 8) / 2, 34);
        save.Radius = 9; save.Font = Fonts.Semi(11F);
        save.Click += delegate
        {
            EnsureAlmSize();
            if (i < defNote.Count) defNote[i] = (tb.Text ?? "");
            if (kind == 1 && i < defDays.Count) { defDays[i] = selDays[0] & 0x7F; if (i < defOn.Count && defOn[i]) ArmAlarm(i); }
            try { SaveSettings(); } catch { }
            lstAlarms.Invalidate(); SyncAlarmTimer();
            try { dlg.Close(); } catch { }
        };
        dlg.Controls.Add(save);

        AppButton cancel = new AppButton();
        cancel.Text = "취소"; cancel.Location = new Point(18 + (DW - 36 - 8) / 2 + 8, DH - 44);
        cancel.Size = new Size((DW - 36 - 8) / 2, 34); cancel.Radius = 9; cancel.Font = Fonts.Semi(11F);
        cancel.Fill = Color.FromArgb(238, 240, 245); cancel.TextColor = Color.FromArgb(74, 82, 96); cancel.BorderColor = Theme.CancelBorder;
        cancel.Click += delegate { try { dlg.Close(); } catch { } };
        dlg.Controls.Add(cancel);

        dlg.KeyPreview = true;
        dlg.KeyDown += delegate(object s2, KeyEventArgs k) { if (k.KeyCode == Keys.Escape) dlg.Close(); };
        try { dlg.ShowDialog(this); tb.Focus(); } catch { }
        try { dlg.Dispose(); } catch { }
    }

    private void FireAlarm(string msg) { FireAlarm(msg, "", notifyKind); }
    private void FireAlarm(string msg, string note0) { FireAlarm(msg, note0, notifyKind); }
    private void FireAlarm(string msg, string note0, int showKind)
    {
        bool quiet = SilentMode;   // 무음 모드: 소리 X, 포커스도 안 뺏음
        if (!quiet) { try { System.Media.SystemSounds.Exclamation.Play(); } catch { } }
        Log("알림: " + msg + (quiet ? " (무음)" : "") + " · 방식=" + showKind);

        string note = (note0 ?? "").Trim();
        // 윈도우 알림(트레이 풍선): 제목 + 본문 앞부분
        bool winShown = false;
        if (showKind != 0)
        {
            string body = note.Length > 0 ? note.Replace("\r", " ").Replace("\n", " ") : "";
            winShown = TrayNotify(msg, body.Length > 0 ? body : "알림");
        }
        // 앱 팝업 — "윈도우만" 인데 트레이가 없어 알림을 못 띄웠으면 팝업으로 대체(알림을 놓치지 않게)
        if (showKind == 1 && winShown && WindowsToastEnabled()) return;   // OS 알림이 꺼져 있으면 팝업으로 대체

        AlertForm pop = new AlertForm();
        pop.NoActivate = quiet;
        pop.FormBorderStyle = FormBorderStyle.None;
        pop.StartPosition = FormStartPosition.CenterScreen;
        pop.ShowInTaskbar = false;
        pop.BackColor = Theme.Card;
        pop.TopMost = !quiet;
        pop.KeyPreview = true;

        int PW = 320;
        int titleY = 90, titleH = 26;
        // 본문 높이 측정 (없으면 0)
        int noteH = 0;
        if (note.Length > 0)
        {
            using (Bitmap bmp = new Bitmap(1, 1))
            using (Graphics mg = Graphics.FromImage(bmp))
                noteH = TextRenderer.MeasureText(mg, note, Fonts.Regular(10F),
                    new Size(PW - 44, 400), TextFormatFlags.WordBreak).Height;
            if (noteH > 150) noteH = 150;   // 너무 길면 잘라서(스크롤 없이) 상한
        }
        int noteY = titleY + titleH + (note.Length > 0 ? 8 : 0);
        int PH = noteY + noteH + 18 + 46 + 16;   // 본문 + 여백 + 버튼 + 하단여백
        if (PH < 190) PH = 190;
        pop.ClientSize = new Size(PW, PH);
        try { pop.Region = new Region(AppButton.Round(new Rectangle(0, 0, PW, PH), 12)); } catch { }

        string m2 = msg, note2 = note;
        int noteYc = noteY, noteHc = noteH;
        AppButton ok = new AppButton();
        pop.Paint += delegate(object ps, PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (GraphicsPath bp = AppButton.Round(new Rectangle(0, 0, PW - 1, PH - 1), 12))
            {
                using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillPath(b, bp);
                using (Pen pen = new Pen(Theme.Border, 1F)) g.DrawPath(pen, bp);
            }
            // 아이콘 원
            int cxp = PW / 2;
            using (SolidBrush b = new SolidBrush(ACCENT))
            using (GraphicsPath ic = AppButton.Round(new Rectangle(cxp - 24, 24, 48, 48), 13)) g.FillPath(b, ic);
            using (Pen pen = new Pen(Color.White, 3F) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawEllipse(pen, cxp - 11, 35, 22, 22);
                g.DrawLine(pen, cxp, 46, cxp, 40);
                g.DrawLine(pen, cxp, 46, cxp + 6, 49);
            }
            // 제목 (한 줄, 굵게)
            TextRenderer.DrawText(g, m2, Fonts.Semi(13F), new Rectangle(16, titleY, PW - 32, titleH), INK,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            // 본문 메모 (여러 줄)
            if (note2.Length > 0)
                TextRenderer.DrawText(g, note2, Fonts.Regular(10F), new Rectangle(22, noteYc, PW - 44, noteHc), MUTED,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        };

        ok.Text = "확인";
        ok.Size = new Size(PW - 40, 46);
        ok.Location = new Point(20, PH - 62);
        ok.Radius = 13;
        ok.Font = Fonts.Semi(12F);
        EventHandler close = delegate { try { pop.Close(); } catch { } };
        ok.Click += close;
        pop.Controls.Add(ok);
        pop.KeyDown += delegate(object s2, KeyEventArgs k) { if (k.KeyCode == Keys.Escape || k.KeyCode == Keys.Enter) close(null, null); };
        pop.Show(this);
        if (!quiet) { try { ok.Focus(); } catch { } }
    }

    private void TickAlarm2Init()
    {
        int soonest = int.MaxValue;
        foreach (int r in runRemain) if (r != int.MinValue && r < soonest) soonest = r;
        if (soonest != int.MaxValue)
        {
            int m = soonest / 60, sc = soonest % 60;
            lblCountdown.Text = m.ToString("00") + ":" + sc.ToString("00");
            SetIsland(lblCountdown.Text);
        }
    }

    private void TickAlarm()
    {
        int soonest = int.MaxValue;
        for (int i = 0; i < runRemain.Length; i++)
        {
            if (runRemain[i] == int.MinValue) continue;   // 이미 울림
            runRemain[i]--;
            if (runRemain[i] <= 0)
            {
                FireAlarm(runMsg[i]);
                runRemain[i] = int.MinValue;
            }
            else if (runRemain[i] < soonest) soonest = runRemain[i];
        }
        if (soonest == int.MaxValue)
        {
            alarmRunning = false;
            lblStatus.Text = "모든 알림 완료";
            lblStatus.ForeColor = OKC;
            if (!powerRunning) SetIsland("");
            UpdateButtons();
        }
        else
        {
            int m = soonest / 60, sc = soonest % 60;
            lblCountdown.Text = m.ToString("00") + ":" + sc.ToString("00");
            int left = 0;
            foreach (int r in runRemain) if (r != int.MinValue) left++;
            lblStatus.Text = "다음 알림까지 · 남은 알림 " + left + "개";
            lblStatus.ForeColor = ACCENT;
            SetIsland(lblCountdown.Text + " · " + left);
        }
    }

    // ───── 실행 ─────
    private void TestClick(object sender, EventArgs e)
    {
        HideFail();
        if (mode == 2) { string mm=(txtMsg.Text??"").Trim(); if(mm.Length==0) mm="시간이 되었습니다"; Log("알림 테스트"); FireAlarm(mm); return; }
        if (CollectTargets().Count == 0)
        {
            MessageBox.Show("종료할 프로그램을 먼저 추가하세요.", "안내",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Log("바로 종료");
        isTestRun = true;
        DoKill(null);
    }

    private void FirePower()
    {
        if (powerKind == 0)
        {
            lblStatus.Text = "전원을 끕니다"; lblStatus.ForeColor = DANGER;
            Log("전원 끄기 실행: shutdown /s /f /t 0");
            RunHidden("shutdown", "/s /f /t 0");
        }
        else
        {
            isTestRun = false;
            DoKill(pendingTargets);
            UpdateButtons();
        }
    }

    private void DoKill(System.Collections.Generic.List<string> src)
    {
        anyFail = false;
        killQueue = (src != null) ? src : CollectTargets();
        killIndex = 0;
        if (chkSeq.Checked && killQueue.Count > 1)
        {
            KillOne(killQueue[0]);
            killIndex = 1;
            killStepTimer.Interval = (int)numGap.Value * 1000;
            killStepTimer.Start();
            lblStatus.Text = "하나씩 종료 중";
            lblStatus.ForeColor = ACCENT;
        }
        else
        {
            foreach (string n in killQueue) KillOne(n);
            FinishKill();
        }
    }

    private void KillStep(object sender, EventArgs e)
    {
        if (killIndex >= killQueue.Count) { killStepTimer.Stop(); FinishKill(); return; }
        KillOne(killQueue[killIndex]);
        killIndex++;
        if (killIndex >= killQueue.Count) { killStepTimer.Stop(); FinishKill(); }
    }

    private void FinishKill()
    {
        if (anyFail)
        {
            string why = IsAdmin()
                ? "일부 종료 실패 · 보호된 프로그램"
                : "일부 종료 실패 · 권한 부족";
            Log(IsAdmin()
                ? "관리자 권한인데도 차단됨 → 안티치트 보호 가능성"
                : "일반 권한으로 실행 중 → 관리자 권한 필요");

            if (chkFallback.Checked && !isTestRun)
            {
                Log("대체 실행: 전원 끄기");
                lblStatus.Text = "종료 실패 · 전원을 끕니다";
                lblStatus.ForeColor = DANGER;
                RunHidden("shutdown", "/s /f /t 0");
                return;
            }
            ShowFail(why);
        }
        else
        {
            HideFail();
            lblCountdown.Text = "--:--";
            lblCountdown.ForeColor = INK;
            lblStatus.Text = "모두 종료 완료";
            lblStatus.ForeColor = OKC;
        }
    }

    private void KillOne(string name)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("taskkill", "/F /IM \"" + name + "\" /T");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            try
            {
                Encoding enc = Encoding.GetEncoding(949);
                psi.StandardOutputEncoding = enc;
                psi.StandardErrorEncoding = enc;
            }
            catch { }
            Process p = Process.Start(psi);
            string o = p.StandardOutput.ReadToEnd();
            string er = p.StandardError.ReadToEnd();
            p.WaitForExit();
            string detail = (o + " " + er).Replace("\r", " ").Replace("\n", " ").Trim();
            if (p.ExitCode == 0) Log("성공 · " + name + " · " + detail);
            else { anyFail = true; Log("실패(" + p.ExitCode + ") · " + name + " · " + detail); }
        }
        catch (Exception ex)
        {
            anyFail = true;
            Log("오류 · " + name + " · " + ex.Message);
        }
    }

    private void RunHidden(string file, string args)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, args);
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show("실행 실패: " + ex.Message, "오류",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ───── 트레이 (닫기·최소화 시 트레이로, 백그라운드 계속 동작) ─────
    private NotifyIcon tray; private ContextMenuStrip trayMenu;
    private Icon trayIconRef;            // GC 방지용 보관 (예전 아이콘 사라짐 버그 원인)
    private bool reallyExit = false;
    internal static bool startInTray = false;   // --tray 로 실행되면 창을 띄우지 않고 트레이에서 시작
    private bool allowVisible = false;
    protected override void SetVisibleCore(bool value)
    {
        // 자동 실행(--tray) 시 창이 잠깐도 깜빡이지 않게: 사용자가 열기 전까지 Visible=false 유지
        if (startInTray && !allowVisible)
        {
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }     // 트레이 메뉴 '완전 종료'에서만 true
    private bool trayTipShown = false;

    private void SetupTray()
    {
        try
        {
            trayMenu = new ContextMenuStrip();
            ToolStripMenuItem miOpen = new ToolStripMenuItem("열기");
            miOpen.Click += delegate { RestoreFromTray(); };
            ToolStripMenuItem miExit = new ToolStripMenuItem("완전 종료");
            miExit.Click += delegate { reallyExit = true; this.Close(); };
            trayMenu.Items.Add(miOpen);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(miExit);

            trayIconRef = this.Icon;                       // 창 아이콘 재사용
            if (trayIconRef == null) trayIconRef = SystemIcons.Application;

            tray = new NotifyIcon();
            tray.Icon = trayIconRef;
            tray.Text = "자동 종료 타이머";
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += delegate { RestoreFromTray(); };
            tray.Visible = true;
            Log("트레이 준비 완료");
        }
        catch (Exception ex) { tray = null; Log("트레이 준비 실패: " + ex.Message); }
    }

    // 새 버전을 찾았을 때 — 팝업으로 막아서지 않고 버전 줄에 표시만 남긴다.
    // 사용자가 알아서 누르면 되고, 하루 한 번만 트레이 풍선으로 가볍게 귀띔한다.
    // 설정 맨 아래 버전 줄을 현재 상태에 맞게 다시 그린다. 상태는 셋뿐이다.
    //
    // ★ "자동 종료 타이머 v" + VERSION 이라는 연결식을 바꾸지 말 것.
    //   VERSION 이 const 라 컴파일 때 한 덩어리 리터럴로 접히고,
    //   릴리스 검증이 exe 안에서 바로 그 문자열을 찾아 버전 일치를 확인한다.
    //   string.Format 이나 문자열 분해로 바꾸면 컴파일은 되고 배포만 조용히 막힌다.
    private void RefreshVerLabel()
    {
        try
        {
            if (lblVerSet == null || lblVerSet.IsDisposed) return;
            string baseText = "자동 종료 타이머 v" + VERSION;
            if (pendingTag != null)
            {
                lblVerSet.Text = baseText + "   ·   새 버전 " + pendingTag + " 있음 ▸";
                lblVerSet.ForeColor = ACCENT;
                lblVerSet.Font = Fonts.Semi(8.5F);
                tip.SetToolTip(lblVerSet, "클릭하면 " + pendingTag + " 로 업데이트합니다");
                return;
            }
            if (verChecking)
            {
                lblVerSet.Text = baseText + "   ·   확인 중…";
                lblVerSet.ForeColor = MUTED;
                lblVerSet.Font = Fonts.Regular(8.5F);
                tip.SetToolTip(lblVerSet, "새 버전이 있는지 확인하고 있습니다");
                return;
            }
            lblVerSet.Text = baseText;
            lblVerSet.ForeColor = MUTED;
            lblVerSet.Font = Fonts.Regular(8.5F);
            tip.SetToolTip(lblVerSet, "클릭하면 새 버전이 있는지 확인합니다");
        }
        catch { }
    }

    // 헤더의 시계 아이콘을 누르면 뜨는 사용법. 동봉 사용설명서보다 훨씬 짧게, 말투는 친근하게.
    private void ShowGuide()
    {
        try
        {
            AppSheet g = new AppSheet("사용법", "v" + VERSION, null);
            g.Body.AddHead("기본");
            g.Body.AddBullet("위쪽 세 칸에서 뭘 할지 고르세요. 전원 끄기 · 프로그램 · 알림", 0);
            g.Body.AddBullet("시간을 정하고 [시작] 을 누르면 끝이에요", 0);
            g.Body.AddBullet("자주 쓰는 시간은 + 로 저장해두면 한 번에 고를 수 있어요", 0);
            g.Body.AddBullet("취소하려면 [취소] 나 ESC 를 눌러주세요", 0);

            g.Body.AddHead("전원 끄기");
            g.Body.AddBullet("정해둔 시간이 되면 PC가 꺼져요", 0);
            g.Body.AddBullet("쓰던 프로그램이 열려 있어도 꺼지니 저장은 미리 해두세요", 0);

            g.Body.AddHead("프로그램");
            g.Body.AddBullet("고른 프로그램만 닫아요. 여러 개도 돼요", 0);
            g.Body.AddBullet("순서대로 닫거나 한꺼번에 닫는 것 중에 고를 수 있어요", 0);

            g.Body.AddHead("알림");
            g.Body.AddBullet("시간이 되면 알려줘요. 여러 개 저장해두고 켜고 끌 수 있어요", 0);
            g.Body.AddBullet("무음 모드를 켜면 소리 없이 조용히 알려줘요", 0);

            g.Body.AddHead("알아두면 좋아요");
            g.Body.AddBullet("창을 닫아도 꺼지지 않고 작업표시줄 오른쪽에 숨어요", 0);
            g.Body.AddBullet("완전히 끄려면 거기 아이콘에서 종료를 눌러주세요", 0);
            g.Body.AddBullet("설정 맨 아래 버전 줄을 누르면 새 버전이 있는지 봐줘요", 0);
            g.Tell(this, "닫기");
        }
        catch { }
    }

    // 인터넷에서 받은 파일에는 윈도우가 차단 표시를 붙인다. 그것 때문에 실행이 막히거나
    // 경고가 뜨는데, 이 앱은 관리자 권한으로 뜨므로 **스스로 풀 수 있다.**
    // 동봉한 보안패치.bat 을 사람이 실행하지 않아도 되게 하려는 것. 버전이 바뀔 때마다 한 번만 한다.
    private void SelfUnblock()
    {
        try
        {
            string done = null;
            using (Microsoft.Win32.RegistryKey rk =
                       Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ShutdownTimer"))
            { if (rk != null) { object v = rk.GetValue("unblocked"); if (v != null) done = v.ToString(); } }
            if (done == VERSION) return;

            string exe = Application.ExecutablePath;
            string dir = Path.GetDirectoryName(exe);
            try { DeleteFileW(exe + ":Zone.Identifier"); } catch { }
            try
            {
                foreach (string f in Directory.GetFiles(dir))
                { try { DeleteFileW(f + ":Zone.Identifier"); } catch { } }
            }
            catch { }

            // 백신 예외 등록은 몇 초 걸릴 수 있어 화면을 붙잡지 않게 뒤로 보낸다.
            string safeDir = dir.Replace("'", "''");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("powershell",
                        "-NoProfile -ExecutionPolicy Bypass -Command \"Add-MpPreference -ExclusionPath '" + safeDir + "'\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process pr = Process.Start(psi);
                    if (pr != null) pr.WaitForExit(20000);
                }
                catch { }
            });

            using (Microsoft.Win32.RegistryKey rk =
                       Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\ShutdownTimer"))
            { if (rk != null) rk.SetValue("unblocked", VERSION); }
            Log("보안 설정 자동 처리 완료");
        }
        catch { }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeleteFileW(string lpFileName);

    private void ShowUpdateBadge(string tag)
    {
        try
        {
            pendingTag = tag;
            RefreshVerLabel();
        }
        catch { }

        // 하루 한 번만. 날짜는 설정과 같은 레지스트리 자리에 따로 저장한다.
        try
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            string seen = null;
            using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ShutdownTimer"))
            { if (rk != null) { object v = rk.GetValue("updNotice"); if (v != null) seen = v.ToString(); } }
            if (seen == today) return;
            using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\ShutdownTimer"))
            { if (rk != null) rk.SetValue("updNotice", today); }

            // 어디를 눌러야 하는지까지 한 문장에 담는다. 받을지 말지는 사용자가 정한다.
            TrayNotify("새 버전 " + tag + " 이 나왔어요",
                       "설정(톱니) → 맨 아래 버전 줄을 누르면 받을 수 있어요. 지금 안 받아도 괜찮아요.");
        }
        catch { }
    }

    // 윈도우 기본 알림(트레이 풍선). 트레이 아이콘이 있을 때만 동작.
    private bool TrayNotify(string title, string body)
    {
        try
        {
            if (tray == null || !tray.Visible) return false;
            tray.BalloonTipTitle = title;
            tray.BalloonTipText = body;
            tray.BalloonTipIcon = ToolTipIcon.Info;
            tray.ShowBalloonTip(5000);
            return true;
        }
        catch { }
        return false;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    private static void SetPlaceholder(TextBox tb, string text)
    {   // EM_SETCUEBANNER — 텍스트가 비어 있을 때만 연하게 표시 (Windows 전용, 실패해도 무해)
        try { if (tb != null && tb.IsHandleCreated) SendMessage(tb.Handle, 0x1501, (IntPtr)1, text); } catch { }
    }

    private void HideToTray()
    {
        try
        {
            // ShowInTaskbar를 건드리면 핸들이 재생성되며 오류가 나므로 Hide()만 사용
            this.Hide();
            if (tray != null && !trayTipShown)
            {
                trayTipShown = true;
                tray.BalloonTipTitle = "자동 종료 타이머";
                tray.BalloonTipText = "트레이에서 계속 실행 중입니다. 아이콘을 더블클릭하면 다시 열립니다.";
                try { tray.ShowBalloonTip(3000); } catch { }
            }
        }
        catch { }
    }

    private void RestoreFromTray()
    {
        allowVisible = true;
        try
        {
            this.Show();
            if (this.WindowState == FormWindowState.Minimized) this.WindowState = FormWindowState.Normal;
            this.Activate();
            this.BringToFront();
        }
        catch { }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // 최소화하면 작업표시줄 대신 트레이로
        if (this.WindowState == FormWindowState.Minimized && tray != null) HideToTray();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try { SaveSettings(); } catch { }

        // X(닫기)는 종료가 아니라 트레이로 (트레이 '완전 종료'로만 실제 종료)
        if (!reallyExit && tray != null && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        // 업데이트로 인한 종료일 때는 묻지 않는다. 여기서 모달이 뜨면 종료가 멈추는데
        // 교체 배치는 기다리다 지쳐 진행해버려서, 살아 있는 앱을 덮어쓰려다 실패하고
        // 사용자 눈에는 "업데이트를 눌렀더니 앱이 사라졌다"로 보인다.
        if (!Updater.Updating && (powerRunning || alarmRunning))
        {
            if (MessageBox.Show("예약이 진행 중입니다. 종료할까요?", "확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            { e.Cancel = true; reallyExit = false; return; }
        }
        try { if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; } } catch { }
        base.OnFormClosing(e);
    }

    // ── 오류를 조용히 삼키지 않고 바탕화면 로그 + 메시지로 노출 ──
    private static void ReportCrash(string where, Exception ex)
    {
        string body = "[" + where + "]\r\n" + (ex == null ? "(알 수 없음)" : ex.ToString());
        try
        {
            string dir;
            try { dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); }
            catch { dir = null; }
            if (string.IsNullOrEmpty(dir)) dir = System.IO.Path.GetTempPath();
            string path = System.IO.Path.Combine(dir, "ShutdownTimer_오류.txt");
            System.IO.File.WriteAllText(path, DateTime.Now + "\r\n" + body, Encoding.UTF8);
        }
        catch { }
        try
        {
            MessageBox.Show(
                "앱 시작 중 오류가 발생했습니다.\r\n바탕화면의 'ShutdownTimer_오류.txt' 내용을 개발자에게 보내주세요.\r\n\r\n" + body,
                "자동 종료 타이머 · 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { }
    }

    [STAThread]
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int RegisterWindowMessage(string message);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr w, IntPtr l);
    internal static int WM_SHOWME = 0;
    static MainForm()
    {   // user32가 없는 환경(개발/테스트)에서 정적 초기화가 터지지 않게 감싼다
        try { WM_SHOWME = RegisterWindowMessage("ShutdownTimer600g_ShowMe"); }
        catch { WM_SHOWME = 0; }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_SHOWME && WM_SHOWME != 0)
        { try { RestoreFromTray(); } catch { } }
        base.WndProc(ref m);
    }

    public static void Main(string[] args)
    {
        // 디스플레이 배율(125%/150%)에서 Windows가 화면을 통째로 확대(=뿌옇게)하는 걸 막고
        // 네이티브 픽셀로 선명하게 그리도록 DPI 인식을 켠다. (폰트는 픽셀 단위라 크기 일정)
        try { if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware(); } catch { }

        // 처리되지 않은 모든 예외를 잡아 표시(조용한 종료 방지)
        try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); } catch { }
        Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) {
            ReportCrash("UI 스레드", e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) {
            ReportCrash("백그라운드", e.ExceptionObject as Exception);
        };

        bool retried = false;
        foreach (string a in args) { if (a == "--noelevate") retried = true; if (a == "--tray") MainForm.startInTray = true; }
        bool admin = false;
        try { admin = IsAdmin(); } catch { }
        if (!admin && !retried)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "--noelevate" + (MainForm.startInTray ? " --tray" : ""));
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                return;
            }
            catch { }   // 권한 상승 취소/실패 시 일반 권한으로라도 실행
        }

        // 트레이 상주 앱이라 이미 켜져 있는데 또 실행하면 창이 두 개가 된다.
        // 이미 실행 중이면 그 창을 꺼내주고 이번 프로세스는 조용히 끝낸다.
        bool createdNew = true;
        System.Threading.Mutex mtx = null;
        try { mtx = new System.Threading.Mutex(true, "Global\\ShutdownTimer600g_SingleInstance", out createdNew); }
        catch { createdNew = true; }
        if (!createdNew)
        {
            bool sent = false;
            try { if (WM_SHOWME != 0) sent = PostMessage((IntPtr)0xffff, WM_SHOWME, IntPtr.Zero, IntPtr.Zero); }
            catch { }
            if (!sent)
            {
                try { MessageBox.Show("이미 실행 중입니다.\n작업표시줄 오른쪽 트레이 아이콘을 확인하세요.",
                                      "자동 종료 타이머", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                catch { }
            }
            return;
        }

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            ReportCrash("시작", ex);
        }
        finally
        {
            try { if (mtx != null) mtx.ReleaseMutex(); } catch { }
        }
    }
}


// ─────────────────────────────────────────────────────────────────────────────
//  자동 업데이트
//
//  GitHub 릴리스에서 최신 build 번호를 읽어 지금 버전과 비교하고, 사용자가 동의하면
//  임시 배치를 띄운 뒤 앱을 종료한다. 실행 중인 exe 는 자기 자신을 덮어쓸 수 없어서
//  실제 교체는 그 배치가 대신한다 (앱 종료 대기 → 내려받기 → 압축 해제 → 교체 → 재실행).
//
//  원칙: 어떤 단계가 실패해도 앱 동작에는 영향이 없어야 한다. 전부 try/catch 로 감싸고,
//  조용한 확인(silent)에서는 실패를 사용자에게 알리지 않는다.
// ─────────────────────────────────────────────────────────────────────────────
public static class Updater
{
    const string ApiUrl  = "https://api.github.com/repos/600-g/shutdown-timer/releases/latest";
    const string ZipUrl  = "https://github.com/600-g/shutdown-timer/releases/latest/download/AutoShutdownTimer.zip";
    const string SiteUrl = "https://600g.net";
    const string Title   = "자동 종료 타이머";

    static bool busy = false;

    /// 업데이트 때문에 종료하는 중. MainForm 의 종료 확인 모달을 건너뛰게 한다.
    /// 그 모달이 종료를 붙잡으면 교체 배치가 먼저 진행해버려 앱을 잃는다.
    public static bool Updating = false;

    /// 확인이 끝났다(성공·실패 무관). MainForm 이 버전 줄의 "확인 중…" 을 푼다.
    public static Action Done;

    /// 전원 끄기 예약이 진행 중인가. MainForm 의 private 필드를 대신 읽어온다.
    /// 이름을 Busy 로 하면 위의 busy 플래그와 헷갈린다.
    public static Func<bool> PowerBusy;

    /// "1.0.0" · "v1.2.3" 에서 비교 가능한 숫자를 만든다. 실패하면 -1.
    ///
    /// ★ 끝 숫자만 비교하면 안 된다 — 1.1.0 의 끝은 0 이라 1.0.9 보다 작아진다.
    ///   반드시 major·minor·patch 를 자리별로 비교해야 한다.
    public static long ParseVer(string s)
    {
        if (string.IsNullOrEmpty(s)) return -1;
        System.Text.RegularExpressions.Match m =
            System.Text.RegularExpressions.Regex.Match(s, @"(\d+)\.(\d+)\.(\d+)");
        if (!m.Success) return -1;
        try
        {
            long a = long.Parse(m.Groups[1].Value);
            long b = long.Parse(m.Groups[2].Value);
            long c = long.Parse(m.Groups[3].Value);
            if (a > 9999 || b > 999 || c > 999) return -1;
            return a * 1000000L + b * 1000L + c;
        }
        catch { return -1; }
    }

    /// JSON 에서 문자열 값 하나만 꺼낸다 (\n \" \uXXXX 정도만 푼다).
    /// 릴리스 본문을 읽으려고 JSON 라이브러리를 끌어오고 싶지 않아서 최소한으로 쓴다.
    static string JsonStr(string json, string key)
    {
        try
        {
            int i = json.IndexOf("\"" + key + "\"");
            if (i < 0) return null;
            i = json.IndexOf(':', i);
            if (i < 0) return null;
            while (i < json.Length && json[i] != '"') i++;
            if (i >= json.Length) return null;
            i++;
            StringBuilder sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char n = json[i + 1];
                    if (n == 'n') sb.Append('\n');
                    else if (n == 'r') { }
                    else if (n == 't') sb.Append(' ');
                    else if (n == 'u' && i + 5 < json.Length)
                    {
                        try { sb.Append((char)Convert.ToInt32(json.Substring(i + 2, 4), 16)); } catch { }
                        i += 4;
                    }
                    else sb.Append(n);
                    i += 2;
                    continue;
                }
                if (c == '"') break;
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
        catch { return null; }
    }

    /// 최신 릴리스의 태그와 변경 내역. 실패하면 둘 다 null.
    static void FetchLatest(out string tag, out string notes)
    {
        tag = null; notes = null;
        string body = FetchLatestJson();
        if (body == null) return;
        tag = JsonStr(body, "tag_name");
        notes = JsonStr(body, "body");
    }

    /// 최신 릴리스 JSON 원문. 실패하면 null — 네트워크 없음·차단·방화벽 전부 여기로.
    static string FetchLatestJson()
    {
        // .NET 4.5 기본값은 TLS 1.0 이라 GitHub 에 연결되지 않는다. 1.2 를 명시해야 한다.
        try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(ApiUrl);
            req.UserAgent = "AutoShutdownTimer";          // GitHub 은 User-Agent 없으면 403
            req.Accept = "application/vnd.github+json";
            req.Timeout = 8000;
            req.ReadWriteTimeout = 8000;
            using (WebResponse res = req.GetResponse())
            using (Stream st = res.GetResponseStream())
            using (StreamReader sr = new StreamReader(st, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }
        catch { return null; }
    }

    /// 새 버전 확인. 네트워크는 백그라운드에서 하고 결과만 UI 스레드로 돌려준다.
    /// silent = true 면 최신이거나 확인에 실패해도 아무것도 띄우지 않는다(시작 시 자동 확인).
    public static void Check(Form owner, string currentVersion, bool silent, Action<string> onNewer)
    {
        if (busy) return;
        busy = true;
        long cur = ParseVer(currentVersion);
        try
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string tag = null, notes = null;
                try { FetchLatest(out tag, out notes); } catch { }
                long latest = ParseVer(tag);
                try
                {
                    if (owner != null && !owner.IsDisposed && owner.IsHandleCreated)
                    {
                        owner.BeginInvoke((MethodInvoker)delegate
                        {
                            try { Decide(owner, tag, notes, cur, latest, silent, onNewer); }
                            catch { }
                            finally { busy = false; }
                        });
                        return;
                    }
                }
                catch { }
                busy = false;
            });
        }
        catch { busy = false; }
    }

    static void Decide(Form owner, string tag, string notes, long cur, long latest, bool silent, Action<string> onNewer)
    {
        // 네트워크 단계가 끝났다. 버전 줄의 "확인 중…" 을 먼저 풀어준다.
        if (Done != null) { try { Done(); } catch { } }

        if (cur < 0 || latest < 0)
        {
            if (!silent) Sheet(owner, 0, tag, notes, cur);
            return;
        }
        if (latest <= cur)
        {
            if (!silent) Sheet(owner, 1, tag, notes, cur);
            return;
        }

        // 새 버전이 있다 — 어느 경로로 왔든 버전 줄에 표시부터 남긴다.
        if (onNewer != null) { try { onNewer(tag); } catch { } }

        // 시작 시 자동 확인이면 여기서 끝. 하던 일을 막지 않는다.
        if (silent) return;

        Sheet(owner, 2, tag, notes, cur);
    }

    /// 업데이트 안내 화면. 윈도우 기본 대화상자 대신 앱 디자인으로 띄운다.
    /// state 0 연결실패 · 1 최신 · 2 새 버전 · 3 설치 시작 실패
    static void Sheet(Form owner, int state, string tag, string notes, long cur)
    {
        try
        {
            // 말투 원칙: 팩트는 짧게, 설명은 친근하게. 링크로 내보내지 않고 이 화면에서 끝낸다.
            string myVer = VerText(cur);
            if (state == 0)
            {
                AppSheet s = new AppSheet("확인하지 못했어요", myVer, null);
                s.SetStatus("인터넷 연결을 확인해 주세요", Theme.Muted);
                s.Body.AddText("잠시 뒤에 버전 줄을 다시 눌러보세요.");
                s.Tell(owner, "닫기");
                return;
            }
            if (state == 1)
            {
                // 맨 위는 번호만, 상태줄이 "최신 버전" 을 말한다.
                // 제목까지 "최신 버전입니다" 로 두면 같은 말이 두 번 나온다.
                AppSheet s = new AppSheet(myVer, null, null);
                s.SetStatus("최신 버전", Theme.Ok);
                s.Body.AddHead("이번 버전에 담긴 것");
                AppSheet.AddMarkdown(s.Body, notes);
                s.Tell(owner, "닫기");
                return;
            }
            if (state == 3)
            {
                AppSheet s = new AppSheet("지금은 받을 수 없어요", myVer, null);
                s.SetStatus("잠시 뒤에 다시 해주세요", Theme.Danger);
                s.Body.AddText("계속 안 되면 600g.net 에서 직접 받으실 수 있어요.");
                s.Tell(owner, "닫기");
                return;
            }

            AppSheet up = new AppSheet("새 버전이 나왔어요", myVer, "지금 " + myVer + " → " + tag);
            up.SetStatus("바로 받을 수 있어요", Theme.Accent);
            up.Body.AddHead("새 버전에 담긴 것");
            AppSheet.AddMarkdown(up.Body, notes);
            up.Body.AddGap(8);
            up.Body.AddSub("앱이 잠깐 닫혔다가 다시 열려요.");
            bool busyPower = false;
            if (PowerBusy != null) { try { busyPower = PowerBusy(); } catch { } }
            if (busyPower) up.Body.AddWarn("전원 끄기 예약이 걸려 있어요. 지금 받으면 예약은 취소돼요.");

            if (!up.Ask(owner, "지금 받기", "나중에")) return;
            if (!Install(owner)) Sheet(owner, 3, tag, notes, cur);
        }
        catch { }
    }

    /// 내부 비교용 숫자를 다시 사람이 읽는 버전 문자열로.
    static string VerText(long v)
    {
        if (v < 0) return "";
        return "v" + (v / 1000000) + "." + (v / 1000 % 1000) + "." + (v % 1000);
    }

    /// 교체 배치를 만들고, **앱이 실제로 닫힌 뒤에만** 실행한다.
    /// 종료가 취소되면 만든 배치를 지우고 false 를 돌려준다.
    static bool Install(Form owner)
    {
        string bat = null, cancel = null;
        ProcessStartInfo psi = null;
        try
        {
            string exePath = Application.ExecutablePath;
            string dir     = Path.GetDirectoryName(exePath);
            int    pid     = Process.GetCurrentProcess().Id;
            string stamp = Guid.NewGuid().ToString("N").Substring(0, 8);
            bat    = Path.Combine(Path.GetTempPath(), "ast_update_" + stamp + ".bat");
            cancel = Path.Combine(Path.GetTempPath(), "ast_cancel_" + stamp + ".txt");

            // 배치 본문은 **순수 ASCII** 로만 만든다. 경로는 환경 변수로 넘긴다 —
            // 환경 블록은 유니코드로 전달되므로 코드페이지 문제가 아예 없다.
            // 경로를 배치에 글자로 박으면 비한국어 윈도우나 % 가 든 경로에서 깨진다.
            StringBuilder b = new StringBuilder();
            b.AppendLine("@echo off");
            b.AppendLine("setlocal enableextensions");
            b.AppendLine("set \"PID=%AST_PID%\"");
            b.AppendLine("set \"EXE=%AST_EXE%\"");
            b.AppendLine("set \"DIR=%AST_DIR%\"");
            b.AppendLine("set \"CANCEL=%AST_CANCEL%\"");
            b.AppendLine("set \"BAK=%AST_EXE%.bak\"");
            b.AppendLine("set \"TMPD=%TEMP%\\ast_up_%RANDOM%%RANDOM%\"");
            // 1) 앱이 완전히 끝날 때까지 최대 60초 대기.
            //    ★ 타임아웃이면 절대 교체하지 않는다 — 살아 있는 exe 를 덮어쓰면 앱을 잃는다.
            //    ★ 앱이 종료를 취소하면 취소 파일을 만든다. 그러면 조용히 물러난다.
            b.AppendLine("for /L %%i in (1,1,60) do (");
            b.AppendLine("  if exist \"%CANCEL%\" goto :quit");
            b.AppendLine("  tasklist /FI \"PID eq %PID%\" 2>nul | find \"%PID%\" >nul || goto :gone");
            b.AppendLine("  ping -n 2 127.0.0.1 >nul");
            b.AppendLine(")");
            b.AppendLine("goto :fail");
            b.AppendLine(":gone");
            b.AppendLine("if exist \"%CANCEL%\" goto :quit");
            b.AppendLine("md \"%TMPD%\" 2>nul");
            // 2) 내려받기 + 압축 풀기 (TLS 1.2 명시 — 옛 파워셸 기본값으로는 GitHub 연결 실패)
            b.AppendLine("powershell -NoProfile -ExecutionPolicy Bypass -Command " +
                         "\"[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; " +
                         "Invoke-WebRequest -Uri '" + ZipUrl + "' -OutFile (Join-Path $env:TMPD 'u.zip') -UseBasicParsing; " +
                         "Expand-Archive -LiteralPath (Join-Path $env:TMPD 'u.zip') -DestinationPath $env:TMPD -Force; " +
                         "Get-ChildItem -LiteralPath $env:TMPD -Recurse -File | Unblock-File\" || goto :fail");
            b.AppendLine("if not exist \"%TMPD%\\AutoShutdownTimer.exe\" goto :fail");
            // ★ 인터넷에서 받은 파일에는 차단 표시가 붙는다. 압축을 풀면 그 표시가 exe 로 옮겨가고
            //   윈도우가 실행을 막는다(보안패치.bat 이 하는 일과 같다). 위 Unblock-File 이 1차,
            //   아래 삭제가 2차 방어다. 이걸 빼면 업데이트 후 앱이 다시 켜지지 않는다.
            b.AppendLine("del \"%TMPD%\\AutoShutdownTimer.exe:Zone.Identifier\" 2>nul");
            // 3) ★ 백업 → 교체 → 검증. 한 단계라도 어긋나면 원래 exe 를 되돌린다.
            //    제자리 덮어쓰기만 하면 복사가 중간에 끊겼을 때 되돌릴 방법이 없다.
            b.AppendLine("copy /Y \"%EXE%\" \"%BAK%\" >nul");
            b.AppendLine("if errorlevel 1 goto :fail");
            b.AppendLine("copy /Y \"%TMPD%\\AutoShutdownTimer.exe\" \"%EXE%\" >nul");
            b.AppendLine("if errorlevel 1 goto :rollback");
            b.AppendLine("if not exist \"%EXE%\" goto :rollback");
            b.AppendLine("for %%z in (\"%EXE%\") do if %%~zz LSS 500000 goto :rollback");
            // 부속 파일(설명서·보안패치)은 실패해도 앱 실행에 지장이 없으므로 결과를 따지지 않는다
            b.AppendLine("for %%f in (\"%TMPD%\\*\") do (");
            b.AppendLine("  if /I not \"%%~nxf\"==\"u.zip\" if /I not \"%%~nxf\"==\"AutoShutdownTimer.exe\" copy /Y \"%%f\" \"%DIR%\\\" >nul");
            b.AppendLine(")");
            // 4) 새 앱을 띄운다.
            //    ★ "떴는지 확인하고 안 떴으면 되돌리기" 는 넣었다가 뺐다(1.0.2 에서 실제로 사고).
            //      백신이 새 exe 를 검사하느라 5초 안에 안 뜨는 일이 흔한데, 그때 되돌리려고
            //      막 시작한 exe 를 덮어쓰려다 실패해서 앱이 아예 안 켜졌다.
            //      파일 무결성(크기·존재)은 이미 확인했으니 여기서는 그냥 띄운다.
            b.AppendLine("start \"\" \"%EXE%\"");
            b.AppendLine("del \"%BAK%\" 2>nul");
            b.AppendLine("goto :done");
            // 5) 교체가 깨졌으면 백업으로 되돌린다.
            b.AppendLine(":rollback");
            b.AppendLine("copy /Y \"%BAK%\" \"%EXE%\" >nul");
            b.AppendLine("if errorlevel 1 goto :lost");
            b.AppendLine("del \"%BAK%\" 2>nul");
            // 6) 실패했지만 앱 파일은 멀쩡하다 — 되살리고 사이트로 안내한다.
            b.AppendLine(":fail");
            b.AppendLine("start \"\" \"%EXE%\"");
            b.AppendLine("start \"\" \"" + SiteUrl + "\"");
            b.AppendLine("goto :done");
            // 7) 복원까지 실패 — 백업을 남기고, 그래도 앱은 띄워본다.
            //    폴더도 열어 사용자가 .bak 을 직접 되돌릴 수 있게 한다.
            b.AppendLine(":lost");
            // 배치는 순수 ASCII 여야 하므로 안내문도 영문으로 쓴다(한글은 깨진다).
            b.AppendLine("echo Update failed. Rename AutoShutdownTimer.exe.bak to AutoShutdownTimer.exe to restore. > \"%DIR%\\UPDATE-FAILED.txt\"");
            b.AppendLine("start \"\" \"%EXE%\"");
            b.AppendLine("start \"\" \"%DIR%\"");
            b.AppendLine("start \"\" \"" + SiteUrl + "\"");
            b.AppendLine("goto :done");
            b.AppendLine(":quit");
            b.AppendLine(":done");
            b.AppendLine("del \"%CANCEL%\" 2>nul");
            b.AppendLine("rd /S /Q \"%TMPD%\" 2>nul");
            b.AppendLine("(goto) 2>nul & del \"%~f0\"");

            File.WriteAllText(bat, b.ToString(), Encoding.ASCII);

            // ★ .bat 은 CreateProcess 로 직접 실행할 수 없다(오류 193 — 유효한 Win32 응용 프로그램이 아님).
            //   반드시 cmd 로 감싸야 한다. 환경 변수를 넘기려면 UseShellExecute 는 false 여야 하므로
            //   ShellExecute 로 바꾸는 것은 해법이 아니다.
            psi = new ProcessStartInfo(
                      Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                      "/d /c \"\"" + bat + "\"\"");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Path.GetTempPath();
            psi.EnvironmentVariables["AST_EXE"] = exePath;
            psi.EnvironmentVariables["AST_DIR"] = dir;
            psi.EnvironmentVariables["AST_PID"] = pid.ToString();
            psi.EnvironmentVariables["AST_CANCEL"] = cancel;
        }
        catch { return false; }

        // ★ 배치를 **먼저** 띄운다. 못 띄우면 앱이 살아 있는 동안 사용자에게 알릴 수 있다.
        //   배치는 앱이 죽기를 기다리고, 60초 안에 안 죽으면 교체를 포기하므로 먼저 띄워도 안전하다.
        try { Process.Start(psi); }
        catch
        {
            try { File.Delete(bat); } catch { }
            return false;
        }

        try { Updating = true; Application.Exit(); }
        catch { }
        finally { Updating = false; }

        // 종료가 취소됐다면 배치에 취소를 알린다 (배치는 이 파일을 보고 조용히 물러난다).
        if (owner != null && !owner.IsDisposed)
        {
            try { File.WriteAllText(cancel, "1"); } catch { }
            return false;
        }
        return true;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  앱 스타일 시트(모달) — 윈도우 기본 대화상자 대신 쓰는 화면
//
//  이 앱은 아이폰 느낌의 커스텀 UI 인데 안내창만 윈도우 기본이라 확 튀었다.
//  OpenNoteEditor·FireAlarm 이 쓰던 관습(무테 폼 · 반경 12 · Theme.Card · 1px 테두리)을
//  그대로 일반화한 것이라 새 색·새 폰트·새 반경을 만들지 않는다.
//
//  ★ 색 리터럴을 하나라도 박으면 다크 모드에서 그대로 남는다. 반드시 Theme.* 를 쓸 것.
//  ★ Fonts.Regular/Semi 는 호출마다 new Font 이고 Dispose 되지 않는다.
//    그래서 폰트는 생성자에서 만들어 필드로 들고, Paint 안에서는 절대 만들지 않는다.
// ─────────────────────────────────────────────────────────────────────────────

/// 시트 본문 — 블록을 쌓아 문서처럼 그리고, 넘치면 스크롤한다.
public class SheetBody : ScreenPanel
{
    private class Blk
    {
        public int Kind;
        public string A, B;
        public EventHandler Go;
        public int Indent, Y, H;
    }

    private const int K_TEXT = 0, K_SUB = 1, K_HEAD = 2, K_BULLET = 3, K_KV = 4;
    private const int K_PATH = 5, K_LOG = 6, K_LINK = 7, K_GAP = 8, K_RULE = 9, K_WARN = 10;

    public const int CW = 264;
    private const int KEYW = 84, VALX = 88, VALW = 176;

    private readonly List<Blk> blocks = new List<Blk>();
    private readonly Font fBody, fSub, fHead, fLog;
    private int contentH = 0, scroll = 0;
    private bool dragging = false, moved = false;
    private int dragY = 0, dragScroll = 0;

    public SheetBody()
    {
        fBody = Fonts.Regular(9.5F);
        fSub  = Fonts.Regular(9F);
        fHead = Fonts.Semi(9.5F);
        fLog  = Fonts.Regular(8F);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Theme.Card;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { if (fBody != null) fBody.Dispose(); } catch { }
            try { if (fSub  != null) fSub.Dispose();  } catch { }
            try { if (fHead != null) fHead.Dispose(); } catch { }
            try { if (fLog  != null) fLog.Dispose();  } catch { }
        }
        base.Dispose(disposing);
    }

    private void Add(int kind, string a, string b, int indent, EventHandler go)
    {
        Blk k = new Blk();
        k.Kind = kind; k.A = a; k.B = b; k.Indent = indent; k.Go = go;
        blocks.Add(k);
    }

    public void AddText(string t)            { Add(K_TEXT, t, null, 0, null); }
    public void AddSub(string t)             { Add(K_SUB, t, null, 0, null); }
    public void AddHead(string t)            { Add(K_HEAD, t, null, 0, null); }
    public void AddWarn(string t)            { Add(K_WARN, t, null, 0, null); }
    public void AddBullet(string t, int d)   { Add(K_BULLET, t, null, d, null); }
    public void AddKV(string k, string v)    { Add(K_KV, k, v, 0, null); }
    public void AddPath(string k, string p)  { Add(K_PATH, k, p, 0, null); }
    public void AddLog(string line)          { Add(K_LOG, line, null, 0, null); }
    public void AddLink(string t, EventHandler go) { Add(K_LINK, t, null, 0, go); }
    public void AddGap(int px)               { Add(K_GAP, null, null, px, null); }
    public void AddRule()                    { Add(K_RULE, null, null, 0, null); }

    public bool IsEmpty { get { return blocks.Count == 0; } }

    /// 오프스크린 측정. FireAlarm 이 쓰는 방식과 같다. 4000 은 GDI 안전값(int.MaxValue 금지).
    private static int Measure(string t, Font f, int w)
    {
        if (string.IsNullOrEmpty(t)) return 0;
        try
        {
            using (Bitmap bmp = new Bitmap(1, 1))
            using (Graphics mg = Graphics.FromImage(bmp))
                return TextRenderer.MeasureText(mg, t, f, new Size(w, 4000), TextFormatFlags.WordBreak).Height;
        }
        catch { return 16; }
    }

    /// 경로는 GDI 가 공백에서만 끊으므로 역슬래시 단위로 직접 접는다.
    private static string WrapPath(string p, Font f, int w)
    {
        if (string.IsNullOrEmpty(p)) return "";
        try
        {
            string[] parts = p.Replace("/", "\\").Split('\\');
            StringBuilder outp = new StringBuilder(), line = new StringBuilder();
            using (Bitmap bmp = new Bitmap(1, 1))
            using (Graphics mg = Graphics.FromImage(bmp))
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    string seg = parts[i] + (i < parts.Length - 1 ? "\\" : "");
                    string cand = line.ToString() + seg;
                    int cw = TextRenderer.MeasureText(mg, cand, f, new Size(4000, 100), TextFormatFlags.NoPadding).Width;
                    if (cw > w && line.Length > 0)
                    {
                        if (outp.Length > 0) outp.Append("\n");
                        outp.Append(line.ToString());
                        line.Length = 0;
                    }
                    line.Append(seg);
                }
            }
            if (line.Length > 0) { if (outp.Length > 0) outp.Append("\n"); outp.Append(line.ToString()); }
            return outp.ToString();
        }
        catch { return p; }
    }

    /// 블록 높이를 재고 필요한 본문 높이를 돌려준다(40~maxH 로 클램프).
    public int Layout(int maxH)
    {
        int y = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            Blk b = blocks[i];
            b.Y = y;
            switch (b.Kind)
            {
                case K_HEAD:
                    b.H = (y == 0 ? 0 : 10) + 18;
                    break;
                case K_TEXT:
                    b.H = Measure(b.A, fBody, CW) + 4;
                    break;
                case K_SUB:
                    b.H = Measure(b.A, fSub, CW) + 3;
                    break;
                case K_WARN:
                    b.H = Measure(b.A, fSub, CW - 9) + 6;
                    break;
                case K_BULLET:
                    b.H = Measure(b.A, fBody, CW - 14 - b.Indent * 12) + 4;
                    break;
                case K_KV:
                    {
                        int hk = Measure(b.A, fSub, KEYW), hv = Measure(b.B, fSub, VALW);
                        b.H = (hk > hv ? hk : hv) + 5;
                    }
                    break;
                case K_PATH:
                    {
                        b.B = WrapPath(b.B, fLog, VALW);
                        int hk = Measure(b.A, fSub, KEYW), hv = Measure(b.B, fLog, VALW);
                        b.H = (hk > hv ? hk : hv) + 5;
                    }
                    break;
                case K_LOG:  b.H = 14; break;
                case K_LINK: b.H = 24; break;
                case K_GAP:  b.H = b.Indent; break;
                case K_RULE: b.H = 9; break;
                default:     b.H = 16; break;
            }
            y += b.H;
        }
        contentH = y;
        int h = contentH;
        if (h > maxH) h = maxH;
        if (h < 40) h = 40;
        return h;
    }

    public bool Scrollable { get { return contentH > Height; } }

    private void ScrollBy(int dy)
    {
        int max = contentH - Height;
        if (max < 0) max = 0;
        int n = scroll + dy;
        if (n < 0) n = 0;
        if (n > max) n = max;
        if (n != scroll) { scroll = n; Invalidate(); }
    }

    public bool HandleKey(Keys k)
    {
        if (!Scrollable) return false;
        if (k == Keys.Down)      { ScrollBy(36);  return true; }
        if (k == Keys.Up)        { ScrollBy(-36); return true; }
        if (k == Keys.PageDown)  { ScrollBy(Height - 24);  return true; }
        if (k == Keys.PageUp)    { ScrollBy(-(Height - 24)); return true; }
        if (k == Keys.Home)      { ScrollBy(-contentH); return true; }
        if (k == Keys.End)       { ScrollBy(contentH);  return true; }
        return false;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        try { Focus(); } catch { }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollBy(-e.Delta * 40 / 120);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        dragging = true; moved = false; dragY = e.Y; dragScroll = scroll;
        try { Focus(); } catch { }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging)
        {
            int dy = dragY - e.Y;
            if (dy > 3 || dy < -3) moved = true;
            int max = contentH - Height;
            if (max < 0) max = 0;
            int n = dragScroll + dy;
            if (n < 0) n = 0;
            if (n > max) n = max;
            if (n != scroll) { scroll = n; Invalidate(); }
            return;
        }
        Cursor = HitLink(e.Y) != null ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        bool wasDrag = moved;
        dragging = false; moved = false;
        if (wasDrag) return;
        EventHandler go = HitLink(e.Y);
        if (go != null) { try { go(this, EventArgs.Empty); } catch { } }
    }

    private EventHandler HitLink(int mouseY)
    {
        int y = mouseY + scroll;
        for (int i = 0; i < blocks.Count; i++)
        {
            Blk b = blocks[i];
            if (b.Kind == K_LINK && b.Go != null && y >= b.Y && y < b.Y + b.H) return b.Go;
        }
        return null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Theme.Card);

        int top = scroll, bot = scroll + Height;
        for (int i = 0; i < blocks.Count; i++)
        {
            Blk b = blocks[i];
            if (b.Y + b.H < top || b.Y > bot) continue;
            int y = b.Y - scroll;
            switch (b.Kind)
            {
                case K_HEAD:
                    TextRenderer.DrawText(g, b.A, fHead, new Rectangle(0, y + (b.Y == 0 ? 0 : 10), CW, 18),
                        Theme.Muted, TextFormatFlags.Left | TextFormatFlags.NoPadding);
                    break;
                case K_TEXT:
                    TextRenderer.DrawText(g, b.A, fBody, new Rectangle(0, y, CW, b.H),
                        Theme.Ink, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    break;
                case K_SUB:
                    TextRenderer.DrawText(g, b.A, fSub, new Rectangle(0, y, CW, b.H),
                        Theme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    break;
                case K_WARN:
                    using (GraphicsPath bar = AppButton.Round(new Rectangle(0, y + 1, 2, b.H - 6), 1))
                    using (SolidBrush sb = new SolidBrush(Theme.Danger)) g.FillPath(sb, bar);
                    TextRenderer.DrawText(g, b.A, fSub, new Rectangle(9, y, CW - 9, b.H),
                        Theme.Danger, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    break;
                case K_BULLET:
                    {
                        int ix = 3 + b.Indent * 12;
                        using (SolidBrush sb = new SolidBrush(Theme.Muted)) g.FillEllipse(sb, ix, y + 7, 3, 3);
                        TextRenderer.DrawText(g, b.A, fBody, new Rectangle(ix + 11, y, CW - ix - 11, b.H),
                            Theme.Ink, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    }
                    break;
                case K_KV:
                    TextRenderer.DrawText(g, b.A, fSub, new Rectangle(0, y, KEYW, b.H),
                        Theme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, b.B, fSub, new Rectangle(VALX, y, VALW, b.H),
                        Theme.Ink, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    break;
                case K_PATH:
                    TextRenderer.DrawText(g, b.A, fSub, new Rectangle(0, y, KEYW, b.H),
                        Theme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, b.B, fLog, new Rectangle(VALX, y, VALW, b.H),
                        Theme.Hint, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    break;
                case K_LOG:
                    TextRenderer.DrawText(g, b.A, fLog, new Rectangle(0, y, CW, 14),
                        Theme.Hint, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                    break;
                case K_LINK:
                    TextRenderer.DrawText(g, b.A + "  ▸", fHead, new Rectangle(0, y, CW, 24),
                        Theme.Accent, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    break;
                case K_RULE:
                    using (Pen p = new Pen(Theme.Border, 1F)) g.DrawLine(p, 0, y + 4, CW, y + 4);
                    break;
            }
        }

        if (contentH > Height)
        {
            int trackH = Height, th = trackH * trackH / contentH;
            if (th < 24) th = 24;
            int max = contentH - Height;
            int ty = max > 0 ? (trackH - th) * scroll / max : 0;
            using (GraphicsPath tp = AppButton.Round(new Rectangle(Width - 4, ty, 3, th), 2))
            using (SolidBrush sb = new SolidBrush(Theme.ToggleOff)) g.FillPath(sb, tp);
        }
    }
}

/// 앱 스타일 모달 시트. 제목 · (부제) · (상태줄) · 본문 · 버튼.
public class AppSheet : Form
{
    public SheetBody Body;

    private const int SW = 300, PAD = 18;
    private readonly string title, hint, sub;
    private string statusText = null;
    private Color statusColor = Color.Empty;
    private readonly Font fTitle, fHint, fSub, fStatus;
    private int sy, by, bodyH;
    private bool ok = false;
    private bool moving = false;
    private Point downPt;

    public AppSheet(string titleText, string rightHint, string subText)
    {
        title = titleText; hint = rightHint; sub = subText;
        fTitle  = Fonts.Semi(11F);
        fHint   = Fonts.Regular(8F);
        fSub    = Fonts.Regular(8.5F);
        fStatus = Fonts.Semi(9F);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Card;
        KeyPreview = true;
        MaximizeBox = false; MinimizeBox = false;

        Body = new SheetBody();
        Controls.Add(Body);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { fTitle.Dispose(); }  catch { }
            try { fHint.Dispose(); }   catch { }
            try { fSub.Dispose(); }    catch { }
            try { fStatus.Dispose(); } catch { }
        }
        base.Dispose(disposing);
    }

    public void SetStatus(string text, Color c) { statusText = text; statusColor = c; }

    private void Build()
    {
        sy = sub != null ? 54 : 36;
        by = statusText != null ? sy + 24 : (sub != null ? 56 : 40);
        bodyH = Body.Layout(400);
        int sh = by + bodyH + 12 + 46;
        ClientSize = new Size(SW, sh);
        Body.SetBounds(PAD, by, SheetBody.CW, bodyH);
        try { Region = new Region(AppButton.Round(new Rectangle(0, 0, SW, sh), 12)); } catch { }
    }

    private AppButton MakeBtn(string text, bool primary)
    {
        AppButton b = new AppButton();
        b.Text = text;
        b.Font = Fonts.Semi(11F);
        b.Radius = 9;
        if (!primary)
        {
            b.Fill = Theme.CancelFill; b.FillHover = Theme.CancelHover; b.FillDown = Theme.CancelDown;
            b.TextColor = Theme.CancelText; b.BorderColor = Theme.CancelBorder;
        }
        Controls.Add(b);
        return b;
    }

    private void Place(Form owner)
    {
        try
        {
            Rectangle wa = Screen.FromControl(owner != null ? (Control)owner : this).WorkingArea;
            int x, y;
            if (owner != null)
            {
                x = owner.Left + (owner.Width - Width) / 2;
                y = owner.Top + (owner.Height - Height) / 2;
            }
            else { x = wa.Left + (wa.Width - Width) / 2; y = wa.Top + (wa.Height - Height) / 2; }
            if (x < wa.Left) x = wa.Left;
            if (y < wa.Top) y = wa.Top;
            if (x + Width > wa.Right) x = wa.Right - Width;
            if (y + Height > wa.Bottom) y = wa.Bottom - Height;
            Location = new Point(x, y);
        }
        catch { }
    }

    /// 두 버튼(주/보조). 주 버튼을 눌렀으면 true.
    public bool Ask(Form owner, string yes, string no)
    {
        Build();
        int bw = (SheetBody.CW - 8) / 2, byy = ClientSize.Height - 46;
        AppButton a = MakeBtn(yes, true);  a.SetBounds(PAD, byy, bw, 34);
        AppButton c = MakeBtn(no, false);  c.SetBounds(PAD + bw + 8, byy, bw, 34);
        a.Click += delegate { ok = true; try { Close(); } catch { } };
        c.Click += delegate { ok = false; try { Close(); } catch { } };
        Show(owner, false);
        return ok;
    }

    /// 버튼 하나(풀폭).
    public void Tell(Form owner, string close)
    {
        Build();
        AppButton c = MakeBtn(close, true);
        c.SetBounds(PAD, ClientSize.Height - 46, SheetBody.CW, 34);
        c.Click += delegate { try { Close(); } catch { } };
        Show(owner, false);
    }

    /// 주 버튼이 닫기가 아닌 동작인 1버튼 + 보조. extra 가 null 이면 Tell 과 같다.
    private void Show(Form owner, bool dummy)
    {
        Place(owner);
        try { ShowDialog(owner); } catch { }
        try { Dispose(); } catch { }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) { ok = false; try { Close(); } catch { } return; }
        if (Body != null && Body.HandleKey(e.KeyCode)) { e.Handled = true; }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Y < 34) { moving = true; downPt = new Point(e.X, e.Y); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (moving) Location = new Point(Left + e.X - downPt.X, Top + e.Y - downPt.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); moving = false; }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        int w = ClientSize.Width, h = ClientSize.Height;
        using (GraphicsPath bp = AppButton.Round(new Rectangle(0, 0, w - 1, h - 1), 12))
        {
            using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillPath(b, bp);
            using (Pen p = new Pen(Theme.Border, 1F)) g.DrawPath(p, bp);
        }

        TextRenderer.DrawText(g, title, fTitle, new Rectangle(PAD, 14, SheetBody.CW, 20),
            Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (!string.IsNullOrEmpty(hint))
            TextRenderer.DrawText(g, hint, fHint, new Rectangle(PAD, 14, SheetBody.CW, 20),
                Theme.Hint, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (sub != null)
            TextRenderer.DrawText(g, sub, fSub, new Rectangle(PAD, 34, SheetBody.CW, 16),
                Theme.Muted, TextFormatFlags.Left | TextFormatFlags.NoPadding);
        if (statusText != null)
        {
            using (SolidBrush b = new SolidBrush(statusColor)) g.FillEllipse(b, PAD + 1, sy + 7, 7, 7);
            TextRenderer.DrawText(g, statusText, fStatus, new Rectangle(PAD + 14, sy, SheetBody.CW - 14, 20),
                statusColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        using (Pen p = new Pen(Theme.Border, 1F)) g.DrawLine(p, 0, h - 56, w, h - 56);
    }

    /// 브라우저 열기. 이 앱은 관리자로 뜨므로 explorer 를 거쳐야 브라우저가 일반 권한으로 열린다.
    public static void OpenUrl(string url)
    {
        try { Process.Start("explorer.exe", url); return; }
        catch { }
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(url);
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        catch { }
    }

    /// 릴리스 본문(마크다운)을 블록으로 옮긴다. CHANGELOG 의 한 절이 그대로 들어온다.
    public static void AddMarkdown(SheetBody body, string md)
    {
        if (body == null) return;
        if (string.IsNullOrEmpty(md)) { body.AddSub("변경 내역을 불러오지 못했습니다."); return; }
        try
        {
            string[] lines = md.Replace("\r", "").Split('\n');
            bool lastGap = true;
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].TrimEnd();
                string t = raw.TrimStart();
                if (t.Length == 0)
                {
                    if (!lastGap) { body.AddGap(6); lastGap = true; }
                    continue;
                }
                lastGap = false;
                int lead = raw.Length - t.Length;
                if (t.StartsWith("#"))
                {
                    int n = 0;
                    while (n < t.Length && t[n] == '#') n++;
                    body.AddHead(StripInline(t.Substring(n).Trim()));
                }
                else if (t.StartsWith("- ") || t.StartsWith("* ") || t.StartsWith("+ "))
                    body.AddBullet(StripInline(t.Substring(2).Trim()), lead >= 2 ? 1 : 0);
                else
                {
                    int dot = t.IndexOf(". ");
                    bool numbered = dot > 0 && dot <= 3;
                    if (numbered)
                        for (int k = 0; k < dot; k++) if (t[k] < '0' || t[k] > '9') { numbered = false; break; }
                    if (numbered) body.AddBullet(StripInline(t), lead >= 2 ? 1 : 0);
                    else body.AddText(StripInline(t));
                }
            }
        }
        catch { body.AddSub("변경 내역을 읽지 못했습니다."); }
    }

    /// 굵게·코드·링크 마크업만 걷어낸다.
    private static string StripInline(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        try
        {
            s = s.Replace("**", "").Replace("__", "").Replace("`", "");
            int guard = 0;
            while (guard++ < 20)
            {
                int a = s.IndexOf('[');
                if (a < 0) break;
                int b = s.IndexOf("](", a);
                if (b < 0) break;
                int c = s.IndexOf(')', b);
                if (c < 0) break;
                s = s.Substring(0, a) + s.Substring(a + 1, b - a - 1) + s.Substring(c + 1);
            }
            return s;
        }
        catch { return s; }
    }
}
