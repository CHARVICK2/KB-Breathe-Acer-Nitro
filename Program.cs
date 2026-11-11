using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace KbBreathe;

static class Program
{
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main(string[] args)
    {
        var settings = Settings.Load();
        bool hidden = args.Contains("--tray") || settings.StartMin;

        using var mutex = new Mutex(true, @"Global\KbBreatheSingleInstance", out bool created);
        if (!created)
        {
            if (!hidden)
                MessageBox.Show("KB Breathe is already running (check the tray).", "KB Breathe",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(settings, hidden));
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}

// ============================================================ helpers (net48 has no Math.Clamp etc.)
static class M
{
    public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    public static bool Has(this string s, string part) => s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
}

// ============================================================ global shortcuts
static class Hk
{
    public const int Alt = 1, Ctrl = 2, Shift = 4;
    public const int Count = 2;   // cycle mode, start/stop

    public static readonly string[] Names = { "Cycle light mode", "Start / stop" };
    public static readonly int[] DefMod = { Ctrl | Alt, Ctrl | Alt };
    public static readonly int[] DefKey = { (int)Keys.M, (int)Keys.B };

    public static string KeyName(int vk)
    {
        switch ((Keys)vk)
        {
            case Keys.Left: return "←";
            case Keys.Right: return "→";
            case Keys.Up: return "↑";
            case Keys.Down: return "↓";
            case Keys.Return: return "Enter";
            case Keys.Space: return "Space";
            case Keys.Oemtilde: return "`";
            case Keys.OemMinus: return "-";
            case Keys.Oemplus: return "=";
            case Keys.Oemcomma: return ",";
            case Keys.OemPeriod: return ".";
            case Keys.OemOpenBrackets: return "[";
            case Keys.OemCloseBrackets: return "]";
            case Keys.OemSemicolon: return ";";
            case Keys.Oem7: return "'";
            case Keys.OemPipe: return "\\";
            case Keys.OemQuestion: return "/";
            case Keys.PageUp: return "PgUp";
            case Keys.Next: return "PgDn";
        }
        if (vk >= (int)Keys.D0 && vk <= (int)Keys.D9) return ((char)('0' + (vk - (int)Keys.D0))).ToString();
        return ((Keys)vk).ToString();
    }

    public static string ModText(int mod)
    {
        var p = new List<string>();
        if ((mod & Ctrl) != 0) p.Add("Ctrl");
        if ((mod & Alt) != 0) p.Add("Alt");
        if ((mod & Shift) != 0) p.Add("Shift");
        return string.Join("+", p.ToArray());
    }

    public static string Text(int mod, int key)
    {
        if (key == 0) return "Not set";
        string m = ModText(mod);
        return m.Length > 0 ? m + "+" + KeyName(key) : KeyName(key);
    }
}

// ============================================================ settings
sealed class Settings
{
    private const string Key = @"Software\KbBreathe";
    public bool Tray = true, StartMin, Auto = true, ReactPower, ReactPlug, ReactUnplug;
    public int Speed = 550, SpeedTab = 2, Pattern = 0, Sens = 5;
    public string Custom = "0 1 2 3 4 3 2 1";
    public string Color = "#FFFFFF";
    public int Effect;
    public int SoundColor;   // 0 single, 1 duo, 2 warm, 3 cool, 4 rainbow
    public int[] HkMod = (int[])Hk.DefMod.Clone();
    public int[] HkKey = (int[])Hk.DefKey.Clone();

    public static Settings Load()
    {
        var s = new Settings();
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key);
            if (k != null)
            {
                s.Tray = Get(k, "Tray", 1) != 0;
                s.StartMin = Get(k, "StartMin", 0) != 0;
                s.Auto = Get(k, "Auto", 1) != 0;
                s.ReactPower = Get(k, "ReactPower", 0) != 0;
                s.ReactPlug = Get(k, "ReactPlug", 0) != 0;
                s.ReactUnplug = Get(k, "ReactUnplug", 0) != 0;
                s.Speed = M.Clamp(Get(k, "Speed", 550), 120, 2000);
                s.SpeedTab = M.Clamp(Get(k, "SpeedTab", 2), 0, 4);
                int raw = Math.Max(0, Get(k, "Pattern", 0));
                // PatternVer marks a value written by this five-mode list. Only saves from the
                // old eleven-mode list get remapped; otherwise the last chosen mode survives.
                s.Pattern = Get(k, "PatternVer", 0) >= 1 ? M.Clamp(raw, 0, 4) : MapPattern(raw);
                s.Sens = M.Clamp(Get(k, "Sens", 5), 1, 10);
                s.Custom = k.GetValue("Custom") as string ?? s.Custom;
                s.Color = k.GetValue("Color") as string ?? s.Color;
                s.Effect = M.Clamp(Get(k, "Effect", 0), 0, ColorFx.Count - 1);
                s.SoundColor = M.Clamp(Get(k, "SoundColor", 0), 0, SoundPalette.Count - 1);
                for (int i = 0; i < Hk.Count; i++)
                {
                    s.HkMod[i] = Get(k, "Bind" + i + "Mod", Hk.DefMod[i]);
                    s.HkKey[i] = Get(k, "Bind" + i + "Key", Hk.DefKey[i]);
                }
            }
        }
        catch { }
        return s;
    }

    private static int Get(RegistryKey k, string name, int def) => k.GetValue(name) is int v ? v : def;

    // The old eleven-preset list is now five. Saved numbers from that list land on the
    // closest surviving mode instead of on an index that no longer exists.
    private static int MapPattern(int old) => old switch
    {
        5 => 3,              // Fade out
        6 => 0,              // Pulse
        7 => 4,              // Custom
        8 => 2,              // Sound Sync #2 (Beat)
        9 => 1,              // Sound Sync (Overall) -> #1 Vibe
        10 => 1,             // Sound Sync-Vibe -> #1 Vibe
        _ => 0               // waves -> Pulse
    };

    public void Save()
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(Key);
            k.SetValue("Tray", Tray ? 1 : 0, RegistryValueKind.DWord);
            k.SetValue("StartMin", StartMin ? 1 : 0, RegistryValueKind.DWord);
            k.SetValue("Auto", Auto ? 1 : 0, RegistryValueKind.DWord);
            k.SetValue("ReactPower", ReactPower ? 1 : 0, RegistryValueKind.DWord);
            k.SetValue("ReactPlug", ReactPlug ? 1 : 0, RegistryValueKind.DWord);
            k.SetValue("ReactUnplug", ReactUnplug ? 1 : 0, RegistryValueKind.DWord);
            k.SetValue("Speed", Speed, RegistryValueKind.DWord);
            k.SetValue("SpeedTab", SpeedTab, RegistryValueKind.DWord);
            k.SetValue("Pattern", Pattern, RegistryValueKind.DWord);
            k.SetValue("PatternVer", 1, RegistryValueKind.DWord);
            k.SetValue("Sens", Sens, RegistryValueKind.DWord);
            k.SetValue("Custom", Custom, RegistryValueKind.String);
            k.SetValue("Color", Color, RegistryValueKind.String);
            k.SetValue("Effect", Effect, RegistryValueKind.DWord);
            k.SetValue("SoundColor", SoundColor, RegistryValueKind.DWord);
            for (int i = 0; i < Hk.Count; i++)
            {
                k.SetValue("Bind" + i + "Mod", HkMod[i], RegistryValueKind.DWord);
                k.SetValue("Bind" + i + "Key", HkKey[i], RegistryValueKind.DWord);
            }
        }
        catch { }
    }
}

// ============================================================ design tokens (Linear-style: charcoal, hairlines, monochrome)
static class Th
{
    public static readonly Color Bg = Color.FromArgb(13, 14, 17);        // #0D0E11
    public static readonly Color Card = Color.FromArgb(17, 18, 22);
    public static readonly Color Input = Color.FromArgb(22, 23, 28);
    public static readonly Color Border = Color.FromArgb(36, 38, 46);    // #24262E
    public static readonly Color Hover = Color.FromArgb(26, 28, 34);
    public static readonly Color Selected = Color.FromArgb(32, 34, 42);
    public static readonly Color Text = Color.White;
    public static readonly Color Muted = Color.FromArgb(161, 161, 170);  // #A1A1AA
    public static readonly Color Dim = Color.FromArgb(104, 104, 114);
    public static readonly Color Accent = Color.FromArgb(99, 102, 241);  // indigo - call-to-action only
    public static readonly Color Violet = Color.FromArgb(139, 92, 246);
    public static readonly Color Ok = Color.FromArgb(74, 222, 128);
    public static readonly Color Bad = Color.FromArgb(239, 68, 68);
    public const float R = 2f;   // corner radius: barely there
}

static class Fonts
{
    private static readonly bool HasInter = Probe();
    private static bool Probe() { using var f = new Font("Inter", 10f); return f.Name == "Inter"; }

    private static Font Make(float size, bool strong)
    {
        if (HasInter) return new Font("Inter", size, strong ? FontStyle.Bold : FontStyle.Regular);
        return new Font(strong ? "Segoe UI Semibold" : "Segoe UI", size);
    }

    public static readonly Font Body = Make(9.5f, false);
    public static readonly Font Strong = Make(9.5f, true);
    public static readonly Font Small = Make(8.5f, false);
    public static readonly Font Cap = Make(8f, true);
    public static readonly Font Title = Make(10.5f, true);
    public static readonly Font Button = Make(10f, true);
}

// Segoe Fluent Icons (Win11) / Segoe MDL2 Assets (Win10): built into Windows, zero bytes added to the exe.
static class Ico
{
    public const string Keyboard = "\uE765", Play = "\uE768", Stop = "\uE71A", Chevron = "\uE70D",
                        Power = "\uE7E8", Minimize = "\uE921", Close = "\uE8BB", Tray = "\uE73F";

    private static readonly string Family = Pick();

    private static string Pick()
    {
        foreach (var name in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            using var f = new Font(name, 10f);
            if (f.Name == name) return name;
        }
        return "Segoe UI Symbol";
    }

    public static Font Make(float size) => new Font(Family, size, FontStyle.Regular, GraphicsUnit.Point);

    public static void Draw(Graphics g, string glyph, Font f, Color c, Rectangle r) =>
        TextRenderer.DrawText(g, glyph, f, r, c,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
}

static class IconFonts
{
    public static readonly Font Large = Ico.Make(14f);
    public static readonly Font Medium = Ico.Make(11f);
    public static readonly Font Small = Ico.Make(8.5f);
}

static class Gfx
{
    public static GraphicsPath Round(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
        var p = new GraphicsPath();
        if (d <= 0f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Box(Graphics g, RectangleF r, float radius, Color fill, Color border, float width = 1f)
    {
        using var path = Round(r, radius);
        if (fill.A > 0)
        {
            using var b = new SolidBrush(fill);
            g.FillPath(b, path);
        }
        if (border.A > 0)
        {
            using var pen = new Pen(border, width);
            g.DrawPath(pen, path);
        }
    }

    public static void Txt(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags extra = 0) =>
        TextRenderer.DrawText(g, s, f, r, c,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | extra);

    public static int TextW(string s, Font f) =>
        TextRenderer.MeasureText(s, f, new Size(2000, 100),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;

    // ---- caps labels: drawn as one piece of text (per-letter drawing mis-measured single glyphs and left gaps)
    public static int CapsWidth(string s, Font f, float trackPx) => s.Length == 0 ? 0 : TextW(s, f);

    public static int Caps(Graphics g, string s, Font f, Color c, int x, int y, int h, float trackPx)
    {
        int w = CapsWidth(s, f, trackPx);
        TextRenderer.DrawText(g, s, f, new Rectangle(x, y, w + 8, h), c,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine | TextFormatFlags.Left);
        return x + w;
    }
}

static class AppIcon
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

    private const string IconData =
        "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";

    public static Icon Create()
    {
        // Icon is stored inside the program itself (no separate file needed).
        try
        {
            using var ms = new System.IO.MemoryStream(Convert.FromBase64String(IconData));
            return new Icon(ms);
        }
        catch { }

        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var back = Gfx.Round(new RectangleF(1, 1, 30, 30), 5))
            using (var bb = new SolidBrush(Color.FromArgb(24, 24, 27)))
                g.FillPath(bb, back);
            using var white = new SolidBrush(Color.White);
            for (int i = 0; i < 4; i++)
            {
                float bar = 6f + i * 4.5f;
                using var bp = Gfx.Round(new RectangleF(6f + i * 5.5f, 25f - bar, 3.5f, bar), 1f);
                g.FillPath(white, bp);
            }
        }
        IntPtr hIcon = bmp.GetHicon();
        try { using var tmp = Icon.FromHandle(hIcon); return (Icon)tmp.Clone(); }
        finally { DestroyIcon(hIcon); }
    }
}

// ============================================================ lightweight custom controls
// Shapes are drawn in logical (96-dpi) units via ScaleTransform; text/icons use R() to get device-pixel rectangles.
abstract class Ui : Control
{
    protected bool Hot, Down;
    public bool OnCard { get; set; }
    protected Color Back => OnCard ? Th.Card : Th.Bg;
    protected float S => DeviceDpi / 96f;
    protected float LW => Width / S;
    protected float LH => Height / S;

    protected Rectangle R(float x, float y, float w, float h)
    {
        float s = S;
        return Rectangle.Round(new RectangleF(x * s, y * s, w * s, h * s));
    }

    // tracked-out caps label; positions in logical units, returns the logical right edge
    protected float CapsAt(Graphics g, string t, Color c, float lx, float ly, float lh, float track = 0.4f)
    {
        float s = S;
        int end = Gfx.Caps(g, t, Fonts.Cap, c, (int)Math.Round(lx * s), (int)Math.Round(ly * s),
                           (int)Math.Round(lh * s), track * s);
        return end / s;
    }

    protected float CapsW(string t, float track = 0.4f) => Gfx.CapsWidth(t, Fonts.Cap, track * S) / S;

    protected Ui()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }

    protected abstract void Draw(Graphics g);

    protected sealed override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = S;
        g.ScaleTransform(s, s);
        Draw(g);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnMouseEnter(EventArgs e) { Hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { Hot = false; Down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { Down = false; Invalidate(); base.OnMouseUp(e); }
}

// header: title on the left, ON / IDLE status on the right
sealed class HeaderBar : Ui
{
    private bool _on;
    public void SetState(bool on) { _on = on; Invalidate(); }

    protected override void Draw(Graphics g)
    {
        // logo: the same four bars as the tray icon, then the wordmark
        using (var b = new SolidBrush(Color.White))
            for (int i = 0; i < 4; i++)
            {
                float bar = 4f + i * 2.6f;
                using var bp = Gfx.Round(new RectangleF(20f + i * 4f, (LH - 1) / 2f + 5.5f - bar, 2.6f, bar), 1f);
                g.FillPath(b, bp);
            }
        CapsAt(g, "KB BREATHE", Th.Text, 42, 0, LH - 1);

        string text = _on ? "ON" : "IDLE";
        Color c = _on ? Th.Ok : Th.Dim;
        float tw = CapsW(text);
        float tx = LW - 20 - 28 - 8 - tw;
        using (var dot = new SolidBrush(c)) g.FillEllipse(dot, tx - 14, (LH - 1) / 2f - 2.5f, 5, 5);
        CapsAt(g, text, _on ? Th.Text : Th.Muted, tx, 0, LH - 1);

        using var pen = new Pen(Th.Border, 1f);
        g.DrawLine(pen, 0, LH - 1, LW, LH - 1);
    }
}

sealed class IconButton : Ui
{
    public string Glyph { get; set; } = "";

    public IconButton() { Cursor = Cursors.Hand; }

    protected override void Draw(Graphics g)
    {
        if (Hot) Gfx.Box(g, new RectangleF(0.5f, 0.5f, LW - 1, LH - 1), Th.R, Th.Hover, Th.Border);
        Ico.Draw(g, Glyph, IconFonts.Small, Hot ? Th.Text : Th.Muted, ClientRectangle);
    }
}

// page tabs under the header: tracked caps, thin white underline on the active page
sealed class NavBar : Ui
{
    private string[] _items = { "DASH", "KEYS", "REACT", "SETTINGS" };
    private int _sel;
    public event EventHandler? SelectedChanged;

    public string[] Items
    {
        get => _items;
        set
        {
            _items = value;
            if (_sel >= _items.Length) _sel = Math.Max(0, _items.Length - 1);
            Invalidate();
        }
    }

    public int Selected
    {
        get => _sel;
        set
        {
            if (value < 0 || value >= _items.Length || _sel == value) return;
            _sel = value;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public NavBar() { Cursor = Cursors.Hand; }

    protected override void Draw(Graphics g)
    {
        float x = 20;
        for (int i = 0; i < _items.Length; i++)
        {
            bool on = i == _sel;
            float w = CapsW(_items[i]);
            CapsAt(g, _items[i], on ? Th.Text : Th.Muted, x, 0, LH - 1);
            if (on)
                using (var b = new SolidBrush(Th.Text))
                    g.FillRectangle(b, x - 2, LH - 2, w + 4, 1.5f);
            x += w + 28;
        }
        using var pen = new Pen(Th.Border, 1f);
        g.DrawLine(pen, 0, LH - 0.5f, LW, LH - 0.5f);
    }

    private float[] Starts()
    {
        var s = new float[_items.Length + 1];
        float x = 20;
        for (int i = 0; i < _items.Length; i++) { s[i] = x - 12; x += CapsW(_items[i]) + 28; }
        s[_items.Length] = x;
        return s;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        float lx = e.X / S;
        var st = Starts();
        for (int i = _items.Length - 1; i >= 0; i--)
            if (lx >= st[i]) { Selected = i; return; }
    }
}

sealed class CapLabel : Ui
{
    private string _t = "";
    public string Caption { get => _t; set { _t = value; Invalidate(); } }
    protected override void Draw(Graphics g) => CapsAt(g, _t, Th.Muted, 1, 0, LH);
}

sealed class LinkButton : Ui
{
    public string Caption { get; set; } = "";
    public LinkButton() { Cursor = Cursors.Hand; }

    protected override void Draw(Graphics g)
    {
        float w = CapsW(Caption);
        CapsAt(g, Caption, Hot ? Th.Text : Th.Dim, LW - w - 1, 0, LH);
    }
}

sealed class AboutCard : Ui
{
    protected override void Draw(Graphics g)
    {
        Gfx.Box(g, new RectangleF(0.5f, 0.5f, LW - 1, LH - 1), Th.R, Th.Card, Th.Border);
        Gfx.Box(g, new RectangleF(16, 16, 40, 40), Th.R, Color.White, Color.White);
        Ico.Draw(g, Ico.Keyboard, IconFonts.Large, Color.Black, R(16, 16, 40, 40));
        Gfx.Txt(g, "KB Breathe 6.2", Fonts.Title, Th.Text, R(70, 15, LW - 86, 22));
        Gfx.Txt(g, "Light control for Acer laptops.", Fonts.Small, Th.Muted, R(70, 37, LW - 86, 20));
    }
}

// status list with hairline dividers
sealed class StatList : Ui
{
    private bool _running, _error, _backendOk;
    private int _level = -1, _pct;
    private string _backend = "—", _mode = "";

    public StatList() { OnCard = true; }

    public void Set(bool running, int level, int pct, bool error)
    {
        _running = running; _level = level; _pct = pct; _error = error;
        Invalidate();
    }

    public void SetBackend(string shortName, bool ok) { _backend = shortName; _backendOk = ok; Invalidate(); }
    public void SetMode(string m) { _mode = m; Invalidate(); }
    public string ModeText() => _mode;

    private void Row(Graphics g, int i, float rh, string cap, string val, Color valColor, bool dot, Color dotColor)
    {
        float y = 1 + i * rh;
        if (i > 0)
            using (var pen = new Pen(Th.Border, 1f)) g.DrawLine(pen, 14, y, LW - 14, y);
        CapsAt(g, cap, Th.Muted, 16, y, rh);
        float vw = Gfx.TextW(val, Fonts.Strong) / S;
        float vx = LW - 16 - vw;
        if (dot)
            using (var b = new SolidBrush(dotColor)) g.FillEllipse(b, vx - 13, y + rh / 2f - 2.5f, 5, 5);
        Gfx.Txt(g, val, Fonts.Strong, valColor, R(vx - 2, y, vw + 6, rh));
    }

    protected override void Draw(Graphics g)
    {
        Gfx.Box(g, new RectangleF(0.5f, 0.5f, LW - 1, LH - 1), Th.R, Th.Card, Th.Border);
        float rh = (LH - 2) / 3f;
        string lv = !_running ? "—" : _error ? "No response" : (_level >= 0 ? _level + "  ·  " + _pct + "%" : "…");
        Row(g, 0, rh, "LEVEL", lv, _error ? Th.Bad : (_running ? Th.Text : Th.Muted), false, Th.Muted);
        Row(g, 1, rh, "BACKEND", _backendOk ? _backend : "Not found", Th.Text, true, _backendOk ? Th.Ok : Th.Bad);
        Row(g, 2, rh, "MODE", _mode, Th.Text, false, Th.Muted);
    }
}

sealed class SelectButton : Ui
{
    private string _caption = "";
    public string Caption { get => _caption; set { _caption = value; Invalidate(); } }

    public SelectButton() { OnCard = true; Cursor = Cursors.Hand; }

    protected override void Draw(Graphics g)
    {
        Gfx.Box(g, new RectangleF(0.5f, 0.5f, LW - 1, LH - 1), Th.R, Hot ? Th.Hover : Th.Input, Th.Border);
        Gfx.Txt(g, _caption, Fonts.Body, Th.Text, R(12, 0, LW - 40, LH));
        Ico.Draw(g, Ico.Chevron, IconFonts.Small, Th.Muted, R(LW - 28, 0, 18, LH));
    }
}

sealed class Tabs : Ui
{
    private string[] _items = new string[0];
    private int _sel;
    public event EventHandler? SelectedChanged;

    public string[] Items { get => _items; set { _items = value; Invalidate(); } }

    public int Selected
    {
        get => _sel;
        set
        {
            if (_sel == value) return;
            _sel = value;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Tabs() { OnCard = true; Cursor = Cursors.Hand; }

    protected override void Draw(Graphics g)
    {
        Gfx.Box(g, new RectangleF(0.5f, 0.5f, LW - 1, LH - 1), Th.R, Th.Card, Th.Border);
        int n = _items.Length;
        if (n == 0) return;
        float segW = (LW - 2f) / n;
        using (var pen = new Pen(Th.Border, 1f))
            for (int i = 1; i < n; i++)
                if (i != _sel && i - 1 != _sel)
                    g.DrawLine(pen, 1f + i * segW, 10, 1f + i * segW, LH - 10);
        for (int i = 0; i < n; i++)
        {
            float x = 1f + i * segW;
            if (i == _sel)
                Gfx.Box(g, new RectangleF(x + 2, 3, segW - 4, LH - 6), Th.R, Th.Selected, Color.FromArgb(70, 72, 84));
            Gfx.Txt(g, _items[i], Fonts.Strong, i == _sel ? Th.Text : Th.Muted,
                R(x, 0, segW, LH), TextFormatFlags.HorizontalCenter);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _items.Length == 0) return;
        float segW = (LW - 2f) / _items.Length;
        Selected = M.Clamp((int)((e.X / S - 1f) / segW), 0, _items.Length - 1);
    }
}

sealed class SettingRow : Ui
{
    private bool _on;
    public string Glyph { get; set; } = "";
    public string Caption { get; set; } = "";
    public bool Divider { get; set; }
    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _on;
        set
        {
            if (_on == value) return;
            _on = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SettingRow() { OnCard = true; Cursor = Cursors.Hand; }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

    protected override void Draw(Graphics g)
    {
        if (Hot) Gfx.Box(g, new RectangleF(0, 0, LW, LH), 0, Th.Hover, Color.Transparent);
        if (Divider)
            using (var pen = new Pen(Th.Border, 1f)) g.DrawLine(pen, 0, 0.5f, LW, 0.5f);
        Ico.Draw(g, Glyph, IconFonts.Medium, Th.Muted, R(10, 0, 24, LH));
        Gfx.Txt(g, Caption, Fonts.Body, Th.Text, R(46, 0, LW - 110, LH));

        var track = new RectangleF(LW - 14 - 34, (LH - 18) / 2f, 34, 18);
        Gfx.Box(g, track, 3, _on ? Color.FromArgb(235, 235, 240) : Th.Border, _on ? Color.FromArgb(235, 235, 240) : Color.FromArgb(58, 60, 70));
        float kx = _on ? track.Right - 3 - 12 : track.X + 3;
        Gfx.Box(g, new RectangleF(kx, track.Y + 3, 12, 12), 2, _on ? Th.Bg : Th.Muted, Color.Transparent);
    }
}

sealed class Slider : Ui
{
    private int _v;
    public int Minimum { get; set; }
    public int Maximum { get; set; } = 100;
    public int Step { get; set; } = 1;
    public event EventHandler? ValueChanged;

    public int Value
    {
        get => _v;
        set
        {
            value = M.Clamp(value, Minimum, Maximum);
            if (value == _v) return;
            _v = value;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Slider() { OnCard = true; Cursor = Cursors.Hand; }

    private float Frac => Maximum > Minimum ? (_v - Minimum) / (float)(Maximum - Minimum) : 0f;

    protected override void Draw(Graphics g)
    {
        float x0 = 8, w = LW - 16, cy = LH / 2f;
        Gfx.Box(g, new RectangleF(x0, cy - 1, w, 2), 1, Th.Border, Color.Transparent);
        Gfx.Box(g, new RectangleF(x0, cy - 1, Math.Max(2f, w * Frac), 2), 1, Color.FromArgb(220, 220, 228), Color.Transparent);

        float tx = x0 + w * Frac;
        float r = Down ? 6f : 5f;
        Gfx.Box(g, new RectangleF(tx - r, cy - r, r * 2, r * 2), 2, Color.White, Th.Bg, 1.5f);
    }

    private void SetFromX(int x)
    {
        float lx = x / S;
        float f = M.Clamp((lx - 8f) / Math.Max(1f, LW - 16f), 0f, 1f);
        int steps = (int)Math.Round(f * (Maximum - Minimum) / Step);
        Value = Minimum + steps * Step;
    }

    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) SetFromX(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Down) SetFromX(e.X); }
}

// the only accent in the UI: an ultra-subtle indigo -> violet gradient on the call-to-action
sealed class ActionButton : Ui
{
    public bool Active { get; set; }
    public string Caption { get; set; } = "";
    public string Glyph { get; set; } = "";
    public string Hint { get; set; } = "";

    public ActionButton() { Cursor = Cursors.Hand; }

    protected override void Draw(Graphics g)
    {
        var r = new RectangleF(0.5f, 0.5f, LW - 1, LH - 1);
        using (var path = Gfx.Round(r, Th.R))
        {
            if (Active)
            {
                int a = 30 + (Hot ? 14 : 0);
                using (var br = new SolidBrush(Color.FromArgb(a, Th.Bad))) g.FillPath(br, path);
                using (var pen = new Pen(Color.FromArgb(Hot ? 200 : 140, Th.Bad), 1f)) g.DrawPath(pen, path);
            }
            else
            {
                int a = 46 + (Hot ? 22 : 0) - (Down ? 10 : 0);
                using (var br = new LinearGradientBrush(r, Color.FromArgb(a, Th.Accent), Color.FromArgb(a, Th.Violet), 0f))
                    g.FillPath(br, path);
                using (var pen = new Pen(Color.FromArgb(Hot ? 190 : 120, Th.Accent), 1f)) g.DrawPath(pen, path);
            }
        }

        var fg = Active ? Color.FromArgb(255, 160, 160) : Color.White;
        float s = S;
        int tw = Gfx.TextW(Caption, Fonts.Button);
        int iconW = (int)(18 * s), gap = (int)(8 * s);
        int x = (Width - (iconW + gap + tw)) / 2;
        Ico.Draw(g, Glyph, IconFonts.Medium, fg, new Rectangle(x, 0, iconW, Height));
        Gfx.Txt(g, Caption, Fonts.Button, fg, new Rectangle(x + iconW + gap, 0, tw + (int)(6 * s), Height));

        if (Hint.Length > 0)
        {
            int hw = Gfx.TextW(Hint, Fonts.Small);
            Gfx.Txt(g, Hint, Fonts.Small, Active ? Color.FromArgb(200, 120, 120) : Th.Muted,
                new Rectangle(Width - hw - (int)(14 * s), 0, hw + (int)(6 * s), Height));
        }
    }
}

// Shows the selected preset as bars; the active step lights up while running.
sealed class Preview : Ui
{
    private int[] _levels = new int[0];
    private int _step = -1;

    public Preview() { OnCard = true; }

    public void SetPattern(int[] levels) { _levels = levels; _step = -1; Invalidate(); }

    public int Step
    {
        get => _step;
        set { if (_step == value) return; _step = value; Invalidate(); }
    }

    protected override void Draw(Graphics g)
    {
        Gfx.Box(g, new RectangleF(0.5f, 0.5f, LW - 1, LH - 1), Th.R, Th.Card, Th.Border);
        int n = _levels.Length;
        if (n == 0) return;
        float gap = 6f, avail = LW - 40f;
        float bw = Math.Min(26f, (avail - gap * (n - 1)) / n);
        float x0 = (LW - (bw * n + gap * (n - 1))) / 2f;
        float maxH = LH - 26f;
        for (int i = 0; i < n; i++)
        {
            float h = 4f + _levels[i] / 4f * (maxH - 4f);
            Gfx.Box(g, new RectangleF(x0 + i * (bw + gap), LH - 13f - h, bw, h), 1f,
                i == _step ? Color.White : Color.FromArgb(46, 48, 58), Color.Transparent);
        }
    }
}

// one global shortcut: click it, press the new combination (Esc cancels, Backspace clears)
sealed class KeyRow : Ui
{
    public static bool AnyRecording;

    public string Caption { get; set; } = "";
    public bool Divider { get; set; }
    public bool Fail { get; set; }
    public int Mod { get; set; }
    public int Key { get; set; }
    public event EventHandler? Changed;
    public event EventHandler? RecordingChanged;

    private bool _rec;
    private int _liveMod;

    public KeyRow()
    {
        OnCard = true;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.Selectable, true);
    }

    public bool Recording => _rec;

    private void Begin()
    {
        if (_rec) return;
        _rec = true; AnyRecording = true; _liveMod = 0;
        Focus();
        Invalidate();
        RecordingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void End(bool commit)
    {
        if (!_rec) return;
        _rec = false; AnyRecording = false;
        Invalidate();
        RecordingChanged?.Invoke(this, EventArgs.Empty);
        if (commit) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void CancelRecording() => End(false);

    protected override void OnClick(EventArgs e) { base.OnClick(e); if (!_rec) Begin(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); End(false); }
    protected override bool IsInputKey(Keys keyData) => _rec || base.IsInputKey(keyData);

    private static int Mods(KeyEventArgs e) => (e.Control ? Hk.Ctrl : 0) | (e.Alt ? Hk.Alt : 0) | (e.Shift ? Hk.Shift : 0);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_rec) { base.OnKeyDown(e); return; }
        e.Handled = true;
        e.SuppressKeyPress = true;
        var kc = e.KeyCode;
        if (kc == Keys.Escape) { End(false); return; }
        if (kc == Keys.Back || kc == Keys.Delete)
        {
            Mod = 0; Key = 0;
            End(true);
            return;
        }
        if (kc == Keys.ControlKey || kc == Keys.ShiftKey || kc == Keys.Menu ||
            kc == Keys.LMenu || kc == Keys.RMenu || kc == Keys.LControlKey || kc == Keys.RControlKey ||
            kc == Keys.LShiftKey || kc == Keys.RShiftKey || kc == Keys.LWin || kc == Keys.RWin)
        {
            _liveMod = Mods(e);
            Invalidate();
            return;
        }
        int m = Mods(e);
        bool fkey = kc >= Keys.F1 && kc <= Keys.F24;
        if (m == 0 && !fkey) return;   // plain letters would break typing everywhere
        Mod = m; Key = (int)kc;
        End(true);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (_rec) { _liveMod = Mods(e); Invalidate(); e.Handled = true; }
        base.OnKeyUp(e);
    }

    protected override void Draw(Graphics g)
    {
        if (Divider)
            using (var pen = new Pen(Th.Border, 1f)) g.DrawLine(pen, 0, 0.5f, LW, 0.5f);
        Gfx.Txt(g, Caption, Fonts.Body, Th.Text, R(14, 0, LW - 200, LH));

        string t = _rec ? (_liveMod != 0 ? Hk.ModText(_liveMod) + "+…" : "Press keys…") : Hk.Text(Mod, Key);
        Color fg = _rec ? Th.Text : (Fail ? Th.Bad : (Key == 0 ? Th.Dim : Th.Muted));
        Color bd = _rec ? Color.FromArgb(200, 200, 210) : (Fail ? Color.FromArgb(150, Th.Bad) : (Hot ? Color.FromArgb(70, 72, 84) : Th.Border));
        float w = Gfx.TextW(t, Fonts.Small) / S + 20;
        var box = new RectangleF(LW - 12 - w, (LH - 24) / 2f, w, 24);
        Gfx.Box(g, new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width, box.Height), Th.R, _rec ? Th.Input : Th.Card, bd);
        Gfx.Txt(g, t, Fonts.Small, fg, R(box.X, box.Y, box.Width + 1, box.Height + 1), TextFormatFlags.HorizontalCenter);
    }
}

// live 0-4 level meter used by Sound sync
sealed class LevelMeter : Ui
{
    private int _lvl = -1;
    public LevelMeter() { OnCard = true; }
    public int Level { get => _lvl; set { if (_lvl == value) return; _lvl = value; Invalidate(); } }

    protected override void Draw(Graphics g)
    {
        const int n = 5;
        float gap = 6f, bw = (LW - gap * (n - 1)) / n;
        for (int i = 0; i < n; i++)
        {
            float h = 6f + i / 4f * (LH - 8f);
            bool lit = _lvl >= 0 && i <= _lvl;
            Gfx.Box(g, new RectangleF(i * (bw + gap), LH - 1f - h, bw, h), 1f,
                lit ? Color.White : Color.FromArgb(46, 48, 58), Color.Transparent);
        }
    }
}

// live view of the four zone colours
sealed class ZonePreview : Ui
{
    private readonly RgbColor[] _colors = { RgbColor.White, RgbColor.White, RgbColor.White, RgbColor.White };

    public ZonePreview() { OnCard = true; }

    public void Set(IReadOnlyList<RgbColor> colors)
    {
        for (int i = 0; i < _colors.Length; i++)
            _colors[i] = i < colors.Count ? colors[i] : RgbColor.White;
        Invalidate();
    }

    protected override void Draw(Graphics g)
    {
        Gfx.Box(g, new RectangleF(0.5f, 0.5f, LW - 1, LH - 1), Th.R, Th.Card, Th.Border);

        float gap = 6f, w = (LW - 24f - gap * 3) / 4f;
        for (int i = 0; i < 4; i++)
        {
            var c = _colors[i];
            var r = new RectangleF(12 + i * (w + gap), 10, w, LH - 20);
            using var path = Gfx.Round(r, Th.R);
            using (var brush = new SolidBrush(Color.FromArgb(c.R, c.G, c.B)))
                g.FillPath(brush, path);
            using var pen = new Pen(Th.Border, 1f);
            g.DrawPath(pen, path);
        }
    }
}

// colour swatches, drawn only on models whose firmware takes RGB values
sealed class ColorRow : Ui
{
    public static readonly RgbColor[] Swatches =
    {
        RgbColor.White,
        new RgbColor(255, 64, 64),     // red
        new RgbColor(255, 146, 48),    // orange
        new RgbColor(255, 214, 64),    // amber
        new RgbColor(84, 214, 120),    // green
        new RgbColor(64, 220, 232),    // cyan
        new RgbColor(72, 132, 255),    // blue
        new RgbColor(154, 102, 255),   // violet
        new RgbColor(255, 96, 208)     // pink
    };

    private int _sel;
    public event EventHandler? SelectedChanged;

    public ColorRow() { OnCard = true; Cursor = Cursors.Hand; }

    public RgbColor Selection => Swatches[_sel];

    public int Selected
    {
        get => _sel;
        set
        {
            value = M.Clamp(value, 0, Swatches.Length - 1);
            if (value == _sel) return;
            _sel = value;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Select(RgbColor color)
    {
        int found = Array.IndexOf(Swatches, color);
        if (found >= 0) Selected = found;
    }

    private float SwatchWidth => (LW - 8f * (Swatches.Length - 1)) / Swatches.Length;

    protected override void Draw(Graphics g)
    {
        float w = SwatchWidth, size = Math.Min(w, LH), y = (LH - size) / 2f;
        for (int i = 0; i < Swatches.Length; i++)
        {
            var c = Swatches[i];
            var r = new RectangleF(i * (w + 8f) + (w - size) / 2f, y, size, size);
            using (var path = Gfx.Round(r, 2f))
            using (var brush = new SolidBrush(System.Drawing.Color.FromArgb(c.R, c.G, c.B)))
                g.FillPath(brush, path);

            if (i != _sel) continue;
            using var ring = Gfx.Round(RectangleF.Inflate(r, 2.5f, 2.5f), 3f);
            using var pen = new Pen(Th.Text, 1.5f);
            g.DrawPath(pen, ring);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Selected = M.Clamp((int)(e.X / S / (SwatchWidth + 8f)), 0, Swatches.Length - 1);
    }
}

sealed class RoundPanel : Panel
{
    public RoundPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = DeviceDpi / 96f;
        g.ScaleTransform(s, s);
        Gfx.Box(g, new RectangleF(0.5f, 0.5f, Width / s - 1, Height / s - 1), Th.R, Th.Card, Th.Border);
    }
}

sealed class MenuColors : ProfessionalColorTable
{
    public override Color MenuItemSelected => Th.Hover;
    public override Color MenuItemSelectedGradientBegin => Th.Hover;
    public override Color MenuItemSelectedGradientEnd => Th.Hover;
    public override Color MenuItemBorder => Th.Border;
    public override Color MenuItemPressedGradientBegin => Th.Hover;
    public override Color MenuItemPressedGradientMiddle => Th.Hover;
    public override Color MenuItemPressedGradientEnd => Th.Hover;
    public override Color ToolStripDropDownBackground => Th.Card;
    public override Color ImageMarginGradientBegin => Th.Card;
    public override Color ImageMarginGradientMiddle => Th.Card;
    public override Color ImageMarginGradientEnd => Th.Card;
    public override Color MenuBorder => Th.Border;
    public override Color SeparatorDark => Th.Border;
    public override Color SeparatorLight => Th.Card;
    public override Color CheckBackground => Th.Hover;
    public override Color CheckSelectedBackground => Th.Hover;
    public override Color CheckPressedBackground => Th.Hover;
}

// ============================================================ main window (pops up bottom-right, above the tray)
sealed class MainForm : Form
{
    private enum Backend { None, Rgb, Ec, WmiAction, Wmi }

    private static readonly int[] LevelBrightness = { 0, 25, 50, 75, 100 };

    private static readonly string[] SpeedNames = { "Glacial", "Calm", "Steady", "Swift", "Turbo" };
    private static readonly int[] SpeedMs = { 1400, 900, 550, 320, 160 };

    // Five modes only: two breathing/visual patterns, the two sound engines, and Custom.
    private static readonly (string Name, int[] Levels)[] Presets =
    {
        ("Pulse",                new[] { 0, 4, 0, 4 }),
        ("Sound Sync #1 (Vibe)", new int[0]),   // beat = 4, highs = 3, vocals/mids = 1-2, silence = 0
        ("Sound Sync #2 (Beat)", new int[0]),   // kick / snare attacks only
        ("Fade out",             new[] { 4, 3, 2, 1, 0 }),
        ("Custom",               new int[0]),
    };

    // The SOUND page is for the two audio engines only. Pulse, Fade out and Custom stay on DASH.
    private static readonly string[] SoundModeNames = { "VIBE", "BEAT" };

    private readonly Settings _s;
    private bool _startHidden;
    private readonly HeaderBar _header;
    private readonly IconButton _closeBtn;
    private readonly NavBar _nav;
    // dash page
    private readonly StatList _stats;
    private readonly CapLabel _patCap, _speedCap;
    // colour page
    private readonly CapLabel _colorCap, _syncCap;
    private readonly ColorRow _swatches;
    private readonly ZonePreview _zones;
    private readonly SettingRow _syncRow;
    private readonly Label _syncHint;
    private readonly CapLabel _effectCap;
    private readonly SelectButton _effectSelect;
    private readonly RoundPanel _effectCard;
    private ContextMenuStrip? _effectMenu;
    private int _effect;
    private CancellationTokenSource? _fxCts;
    private bool _fxRunning, _closing;
    private readonly RoundPanel _colorCard, _syncCard;
    private readonly Label _patLabel, _speedLbl, _speedVal;
    private readonly RoundPanel _patCard, _inputBox, _speedCard;
    private readonly SelectButton _select;
    private readonly TextBox _custom;
    private readonly Tabs _tabs;
    private readonly Slider _slider, _sens;
    private readonly LevelMeter _meter;
    private readonly Preview _preview;
    private readonly ActionButton _go;
    // sound page
    private readonly CapLabel _soundCap, _soundCtlCap, _soundStyleCap;
    private readonly Tabs _soundModes, _soundStyles;
    private readonly Label _soundDesc, _soundLbl, _soundVal, _soundStyleHint;
    private readonly RoundPanel _soundCard;
    private readonly Slider _soundSens;
    private readonly LevelMeter _soundMeter;
    private volatile int _soundStyle;
    // keys page
    private readonly CapLabel _keysCap;
    private readonly LinkButton _resetKeys;
    private readonly RoundPanel _keysCard;
    private readonly KeyRow[] _keyRows = new KeyRow[Hk.Count];
    private readonly Label _keysNote;
    // settings page
    private readonly AboutCard _about;
    private readonly CapLabel _startCap;
    private readonly RoundPanel _card;
    private readonly SettingRow _startupRow, _minRow, _trayRow, _autoRow;
    // reactions page
    private readonly CapLabel _reactCap;
    private readonly RoundPanel _reactCard;
    private readonly SettingRow _reactPowerRow, _reactPlugRow, _reactUnplugRow;
    private readonly Label _reactNote;
    private readonly System.Windows.Forms.Timer _reactTimer;

    private readonly NotifyIcon _notify;
    private ContextMenuStrip? _presetMenu;
    private ToolStripMenuItem? _trayToggle;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _syncCts;
    private bool _running, _quit, _syncRunning;
    private volatile bool _uiVisible;
    private volatile int _delay = 550;
    private volatile bool _sound;
    private volatile int _soundMode;                   // 0 = beat, 1 = overall, 2 = vibe
    private volatile int _sensVal = 5;
    private volatile int[] _levels = { 1, 2, 3, 4 };   // read by the worker every step -> changes apply live
    private volatile int _ver;                          // bumped whenever the pattern changes
    private Backend _backend = Backend.None;
    private bool _rgb;                       // the ENEK colour controller is present
    private int _pattern, _page;
    private string _customText;
    private bool _reacting;          // a reaction pulse owns the keys right now
    private bool? _onAc;             // last charger state seen
    private string? _powerSig;       // last performance mode seen (Quiet/Balanced/Performance/Turbo)

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WmHotkey = 0x0312, ModNoRepeat = 0x4000;

    // Preset order: Pulse, Sound Sync #1 (Vibe), Sound Sync #2 (Beat), Fade out, Custom.
    private bool IsCustom => _pattern == 4;
    private bool IsSound => _pattern == 1 || _pattern == 2;
    private int SoundModeIndex => _pattern == 1 ? 2 : 0;   // SoundSync: 0 beat, 2 vibe

    private static Label Lbl(string text, Font f, Color c, Color back, ContentAlignment align) => new Label
    {
        Text = text, Font = f, ForeColor = c, BackColor = back, AutoSize = false, TextAlign = align
    };

    public MainForm(Settings s, bool startHidden)
    {
        _s = s;
        _startHidden = startHidden;
        _uiVisible = !startHidden;
        _pattern = M.Clamp(s.Pattern, 0, Presets.Length - 1);
        _customText = s.Custom;

        Text = "KB Breathe";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        Font = Fonts.Body;
        BackColor = Th.Bg;
        ForeColor = Th.Text;
        Icon = AppIcon.Create();
        DoubleBuffered = true;

        _header = new HeaderBar();
        _closeBtn = new IconButton { Glyph = Ico.Close };
        _closeBtn.Click += (_, _) => HideOrExit();
        _nav = new NavBar();
        _nav.SelectedChanged += (_, _) => SetPage(NavPage(_nav.Selected));

        // ---- dash page
        _stats = new StatList();
        _patCap = new CapLabel { Caption = "PATTERN" };
        _speedCap = new CapLabel { Caption = "SPEED" };

        // The colour page only exists on models whose keyboard has the ENEK controller. Like
        // Nitrous, the tab is dropped entirely when the firmware has none.
        _colorCap = new CapLabel { Caption = "KEYBOARD COLOUR" };
        _colorCard = new RoundPanel { BackColor = Th.Bg };
        _swatches = new ColorRow();
        _swatches.Select(RgbColor.FromHex(_s.Color));
        RgbKeyboard.Color = _swatches.Selection;
        _swatches.SelectedChanged += (_, _) =>
        {
            _s.Color = _swatches.Selection.ToHex();
            _s.Save();
            RgbKeyboard.Color = _swatches.Selection;
            if (_syncRunning || _running) return;   // an engine already owns the LEDs
            ApplyEffect();              // show the choice right away, in whichever effect is selected
        };
        _colorCard.Controls.Add(_swatches);

        _syncCap = new CapLabel { Caption = "SCREEN COLOUR SYNC" };
        _syncCard = new RoundPanel { BackColor = Th.Bg };
        _syncRow = new SettingRow { Glyph = Ico.Play, Caption = "Follow the screen colours" };
        _syncRow.CheckedChanged += (_, _) => { if (_syncRow.Checked) StartSync(); else StopSync(); };
        _syncCard.Controls.Add(_syncRow);
        _zones = new ZonePreview();
        _syncHint = Lbl("Each zone takes the colour of one quarter of the screen and follows it eight times a second. Start hands the keys back to the patterns.",
            Fonts.Small, Th.Dim, Th.Bg, ContentAlignment.MiddleLeft);

        _effectCap = new CapLabel { Caption = "KEYBOARD EFFECT" };
        _effectCard = new RoundPanel { BackColor = Th.Bg };
        _effect = M.Clamp(s.Effect, 0, ColorFx.Count - 1);
        _effectSelect = new SelectButton { Caption = ColorFx.Names[_effect] };
        _effectSelect.Click += (_, _) => ShowEffectMenu();
        _effectCard.Controls.Add(_effectSelect);
        ShowZones(_swatches.Selection);

        _patCard = new RoundPanel { BackColor = Th.Bg };
        _patLabel = Lbl("Pattern", Fonts.Body, Th.Text, Th.Card, ContentAlignment.MiddleLeft);
        _select = new SelectButton();
        _select.Click += (_, _) => ShowPresetMenu();
        _inputBox = new RoundPanel { BackColor = Th.Card };
        _custom = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = Fonts.Body,
            BackColor = Th.Card,
            ForeColor = Th.Text,
            MaxLength = 64,
            Text = _customText
        };
        _custom.TextChanged += (_, _) =>
        {
            _customText = _custom.Text;
            if (IsCustom) RefreshLevels();   // live: edits take effect while running
        };
        _inputBox.Controls.Add(_custom);
        _patCard.Controls.Add(_patLabel);
        _patCard.Controls.Add(_select);
        _patCard.Controls.Add(_inputBox);

        _tabs = new Tabs { Items = SpeedNames, Selected = M.Clamp(s.SpeedTab, 0, 4) };
        _tabs.SelectedChanged += (_, _) => UpdateDelay();
        _preview = new Preview();

        _speedCard = new RoundPanel { BackColor = Th.Bg };
        _speedLbl = Lbl("Step delay", Fonts.Body, Th.Text, Th.Card, ContentAlignment.MiddleLeft);
        _speedVal = Lbl("", Fonts.Strong, Th.Text, Th.Card, ContentAlignment.MiddleRight);
        _slider = new Slider { Minimum = 120, Maximum = 2000, Step = 10 };
        _slider.Value = s.Speed;
        _slider.ValueChanged += (_, _) => UpdateDelay();
        _speedCard.Controls.Add(_speedLbl);
        _speedCard.Controls.Add(_speedVal);
        _speedCard.Controls.Add(_slider);
        _sensVal = s.Sens;
        _sens = new Slider { Minimum = 1, Maximum = 10, Step = 1 };
        _sens.Value = s.Sens;
        _sens.ValueChanged += (_, _) => { _sensVal = _sens.Value; UpdateDelay(); };
        _meter = new LevelMeter();
        _speedCard.Controls.Add(_sens);
        _speedCard.Controls.Add(_meter);

        _go = new ActionButton { Caption = "Start", Glyph = Ico.Play };
        _go.Click += (_, _) => ToggleRun();

        // ---- sound page: the two audio engines and their colour styles (RGB four-zone models only)
        _soundCap = new CapLabel { Caption = "SOUND SYNC" };
        _soundModes = new Tabs { Items = SoundModeNames };
        _soundDesc = Lbl("", Fonts.Small, Th.Muted, Th.Bg, ContentAlignment.MiddleLeft);
        _soundCtlCap = new CapLabel { Caption = "SENSITIVITY" };
        _soundCard = new RoundPanel { BackColor = Th.Bg };
        _soundLbl = Lbl("Sensitivity", Fonts.Body, Th.Text, Th.Card, ContentAlignment.MiddleLeft);
        _soundVal = Lbl("", Fonts.Strong, Th.Text, Th.Card, ContentAlignment.MiddleRight);
        _soundSens = new Slider { Minimum = 1, Maximum = 10, Step = 1, Value = s.Sens };
        _soundSens.ValueChanged += (_, _) =>
        {
            _sensVal = _soundSens.Value;
            _sens.Value = _soundSens.Value;
            UpdateDelay();
        };
        _soundMeter = new LevelMeter();
        _soundCard.Controls.Add(_soundLbl);
        _soundCard.Controls.Add(_soundVal);
        _soundCard.Controls.Add(_soundSens);
        _soundCard.Controls.Add(_soundMeter);

        _soundStyle = M.Clamp(s.SoundColor, 0, SoundPalette.Count - 1);
        _soundStyleCap = new CapLabel { Caption = "COLOUR WHILE SYNCING" };
        _soundStyles = new Tabs { Items = SoundPalette.Caps, Selected = _soundStyle };
        _soundStyles.SelectedChanged += (_, _) =>
        {
            _soundStyle = _soundStyles.Selected;
            _s.SoundColor = _soundStyle;
            _s.Save();
        };
        _soundStyleHint = Lbl("Single uses the COLOR swatch. Duo, Warm, Cool and Rainbow split the four zones while Sound Sync runs.",
            Fonts.Small, Th.Dim, Th.Bg, ContentAlignment.MiddleLeft);

        // Keep the two copies of sensitivity in step; the DASH and SOUND pages share one value.
        _sens.ValueChanged += (_, _) =>
        {
            if (_soundSens.Value != _sens.Value) _soundSens.Value = _sens.Value;
        };

        _soundModes.SelectedChanged += (_, _) =>
        {
            if (_soundModes.Selected >= 0) SetPattern(_soundModes.Selected == 1 ? 2 : 1);
        };

        // ---- keys page
        _keysCap = new CapLabel { Caption = "GLOBAL SHORTCUTS" };
        _resetKeys = new LinkButton { Caption = "RESET DEFAULTS" };
        _resetKeys.Click += (_, _) => ResetKeys();
        _keysCard = new RoundPanel { BackColor = Th.Bg };
        for (int i = 0; i < Hk.Count; i++)
        {
            int idx = i;
            var row = new KeyRow { Caption = Hk.Names[i], Divider = i > 0, Mod = _s.HkMod[i], Key = _s.HkKey[i] };
            row.Changed += (_, _) => OnKeyChanged(idx);
            row.RecordingChanged += (_, _) => { if (KeyRow.AnyRecording) UnregisterHotkeys(); else RegisterHotkeys(); };
            _keyRows[i] = row;
            _keysCard.Controls.Add(row);
        }
        _keysNote = Lbl("Click a shortcut, then press the new keys.  Esc cancels · Backspace clears.", Fonts.Small, Th.Dim, Th.Bg, ContentAlignment.MiddleLeft);

        // ---- settings page
        _about = new AboutCard();
        _startCap = new CapLabel { Caption = "STARTUP" };
        _card = new RoundPanel { BackColor = Th.Bg };
        _startupRow = new SettingRow { Glyph = Ico.Power, Caption = "Launch at Windows startup", Checked = IsStartupEnabled() };
        _startupRow.CheckedChanged += (_, _) => SetStartup(_startupRow.Checked);
        _minRow = new SettingRow { Glyph = Ico.Minimize, Caption = "Start minimized", Divider = true, Checked = s.StartMin };
        _trayRow = new SettingRow { Glyph = Ico.Tray, Caption = "Keep running in tray on close", Divider = true, Checked = s.Tray };
        _autoRow = new SettingRow { Glyph = Ico.Play, Caption = "Apply selected mode on startup", Divider = true, Checked = s.Auto };
        _autoRow.CheckedChanged += (_, _) => SaveSettings();
        _card.Controls.Add(_startupRow);
        _card.Controls.Add(_minRow);
        _card.Controls.Add(_trayRow);
        _card.Controls.Add(_autoRow);

        // ---- reactions page
        _reactCap = new CapLabel { Caption = "KEYBOARD REACTIONS" };
        _reactCard = new RoundPanel { BackColor = Th.Bg };
        _reactPowerRow = new SettingRow { Glyph = Ico.Power, Caption = "Pulse on power mode change", Checked = s.ReactPower };
        _reactPowerRow.CheckedChanged += (_, _) => { _s.ReactPower = _reactPowerRow.Checked; _s.Save(); };
        _reactPlugRow = new SettingRow { Glyph = Ico.Play, Caption = "Pulse on charger plug-in", Divider = true, Checked = s.ReactPlug };
        _reactPlugRow.CheckedChanged += (_, _) => { _s.ReactPlug = _reactPlugRow.Checked; _s.Save(); };
        _reactUnplugRow = new SettingRow { Glyph = Ico.Stop, Caption = "Pulse on charger unplug", Divider = true, Checked = s.ReactUnplug };
        _reactUnplugRow.CheckedChanged += (_, _) => { _s.ReactUnplug = _reactUnplugRow.Checked; _s.Save(); };
        _reactCard.Controls.Add(_reactPowerRow);
        _reactCard.Controls.Add(_reactPlugRow);
        _reactCard.Controls.Add(_reactUnplugRow);
        _reactNote = Lbl("Each reaction flashes the keys 0-4-0, then the pattern carries on.", Fonts.Small, Th.Dim, Th.Bg, ContentAlignment.MiddleLeft);

        _reactTimer = new System.Windows.Forms.Timer { Interval = 1000 };   // 1 s poll: no system hooks installed
        _reactTimer.Tick += (_, _) => CheckReactions();
        _reactTimer.Start();

        // earlier in the list = higher in z-order (close button must sit above the header)
        Controls.AddRange(new Control[] { _closeBtn, _header, _nav,
            _stats, _patCap, _patCard, _speedCap, _tabs, _preview, _speedCard, _go,
            _soundCap, _soundModes, _soundDesc, _soundCtlCap, _soundCard,
            _soundStyleCap, _soundStyles, _soundStyleHint,
            _colorCap, _colorCard, _syncCap, _syncCard, _zones, _syncHint,
            _keysCap, _resetKeys, _keysCard, _keysNote,
            _about, _startCap, _card,
            _reactCap, _reactCard, _reactNote });

        _notify = new NotifyIcon { Icon = Icon, Text = "KB Breathe – Off", Visible = true };
        _notify.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (Visible && WindowState != FormWindowState.Minimized) Hide(); else ShowWindow();
        };

        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };
        _header.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape && !KeyRow.AnyRecording) Hide(); };
        VisibleChanged += (_, _) => UpdateVisibility();
        FormClosing += OnClosing;

        SetupNav();
        RebuildMenus();
        SetPattern(_pattern);
        UpdateHint();
        if (IsHandleCreated) RegisterHotkeys();
    }

    // ---- window plumbing
    protected override void SetVisibleCore(bool value)
    {
        if (_startHidden)
        {
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        else if (value && !Visible)
        {
            PositionNearTray();
        }
        base.SetVisibleCore(value);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int round = 1; DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int)); } catch { }   // square corners, even on Win11
        if (_keyRows[Hk.Count - 1] != null) RegisterHotkeys();
        DetectAsync();
        if (_s.Auto) BeginInvoke((MethodInvoker)AutoApply);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterHotkeys();
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey)
        {
            int id = m.WParam.ToInt32();
            if (id >= 1 && id <= Hk.Count) DoHotkey(id - 1);
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Th.Border, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    private void UpdateVisibility()
    {
        _uiVisible = Visible;
        if (!Visible) foreach (var r in _keyRows) r.CancelRecording();
    }

    private void DragWindow()
    {
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 2, 0);   // WM_NCLBUTTONDOWN, HTCAPTION
    }

    private void PositionNearTray()
    {
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        int gap = (int)Math.Round(12 * DeviceDpi / 96f);
        Location = new Point(wa.Right - Width - gap, wa.Bottom - Height - gap);
    }

    private void ShowWindow()
    {
        _startHidden = false;
        if (!Visible) Show();
        Activate();
    }

    private void HideOrExit()
    {
        if (_trayRow.Checked) Hide();
        else { _quit = true; Close(); }
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        SaveSettings();
        if (!_quit && _trayRow.Checked && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        _closing = true;
        StopBreathing();
        StopSync();
        StopFx();
        EcHid.Release();
        RgbKeyboard.Release();
        UnregisterHotkeys();
        _notify.Visible = false;
        _notify.Dispose();
        _reactTimer.Stop();
        _reactTimer.Dispose();
    }

    private void SaveSettings()
    {
        _s.Speed = _slider.Value;
        _s.Sens = _sens.Value;
        _s.SpeedTab = _tabs.Selected;
        _s.Pattern = _pattern;
        _s.Custom = _customText;
        _s.SoundColor = _soundStyle;
        _s.Tray = _trayRow.Checked;
        _s.StartMin = _minRow.Checked;
        _s.Auto = _autoRow.Checked;
        _s.Save();
    }

    // ---- global hotkeys
    private void RegisterHotkeys()
    {
        if (!IsHandleCreated || KeyRow.AnyRecording) return;
        for (int i = 0; i < Hk.Count; i++)
        {
            UnregisterHotKey(Handle, i + 1);
            bool fail = false;
            if (_s.HkKey[i] != 0)
                fail = !RegisterHotKey(Handle, i + 1, (uint)(_s.HkMod[i] | ModNoRepeat), (uint)_s.HkKey[i]);
            _keyRows[i].Fail = fail;
            _keyRows[i].Invalidate();
        }
    }

    private void UnregisterHotkeys()
    {
        if (!IsHandleCreated) return;
        for (int i = 0; i < Hk.Count; i++) UnregisterHotKey(Handle, i + 1);
    }

    private void OnKeyChanged(int i)
    {
        _s.HkMod[i] = _keyRows[i].Mod;
        _s.HkKey[i] = _keyRows[i].Key;
        // the same combination can only belong to one action: the older owner loses it
        if (_s.HkKey[i] != 0)
            for (int j = 0; j < Hk.Count; j++)
                if (j != i && _s.HkKey[j] == _s.HkKey[i] && _s.HkMod[j] == _s.HkMod[i])
                {
                    _s.HkMod[j] = 0; _s.HkKey[j] = 0;
                    _keyRows[j].Mod = 0; _keyRows[j].Key = 0;
                    _keyRows[j].Invalidate();
                }
        RegisterHotkeys();
        UpdateHint();
        _s.Save();
    }

    private void ResetKeys()
    {
        for (int i = 0; i < Hk.Count; i++)
        {
            _s.HkMod[i] = Hk.DefMod[i]; _s.HkKey[i] = Hk.DefKey[i];
            _keyRows[i].Mod = Hk.DefMod[i]; _keyRows[i].Key = Hk.DefKey[i];
            _keyRows[i].Invalidate();
        }
        RegisterHotkeys();
        UpdateHint();
        _s.Save();
    }

    private void DoHotkey(int action)
    {
        int n = Presets.Length;
        switch (action)
        {
            case 0: SetPattern((_pattern + 1) % n); break;
            case 1: ToggleRun(); break;
        }
    }

    private void UpdateHint()
    {
        _go.Hint = _s.HkKey[1] != 0 ? Hk.Text(_s.HkMod[1], _s.HkKey[1]) : "";
        _go.Invalidate();
    }

    // ---- pages + layout (all numbers are 96-dpi units, scaled once here; window size is fixed)
    private const int PageDash = 0, PageSound = 1, PageColor = 2, PageKeys = 3, PageReact = 4, PageSettings = 5;

    private int[] _navPages = { PageDash, PageKeys, PageReact, PageSettings };

    private int NavPage(int navIndex) =>
        navIndex >= 0 && navIndex < _navPages.Length ? _navPages[navIndex] : PageDash;

    // SOUND and COLOR exist only when the keyboard has the four-zone colour controller. A model
    // without it keeps the original DASH | KEYS | REACT | SETTINGS strip, with sound modes still
    // available from the DASH pattern selector.
    private void SetupNav()
    {
        if (_rgb)
        {
            _nav.Items = new[] { "DASH", "SOUND", "COLOR", "KEYS", "REACT", "SETTINGS" };
            _navPages = new[] { PageDash, PageSound, PageColor, PageKeys, PageReact, PageSettings };
        }
        else
        {
            _nav.Items = new[] { "DASH", "KEYS", "REACT", "SETTINGS" };
            _navPages = new[] { PageDash, PageKeys, PageReact, PageSettings };
        }

        for (int i = 0; i < _navPages.Length; i++)
            if (_navPages[i] == _page) { _nav.Selected = i; _nav.Invalidate(); return; }

        // The page disappeared with the controller: land on the dashboard rather than on a tab
        // that no longer describes what is showing.
        _nav.Selected = 0;
        SetPage(PageDash);
    }

    private void SetPage(int p)
    {
        // SOUND is built only for the four-zone colour controller. Opening it while a non-audio
        // pattern is selected picks the first audio engine so the page always has a valid subject.
        if (p == PageSound)
        {
            if (!_rgb) p = PageDash;
            else if (!IsSound) SetPattern(1);
        }

        _page = p;
        LayoutUi();
    }

    private void LayoutUi()
    {
        float k = DeviceDpi / 96f;
        int D(float v) => (int)Math.Round(v * k);

        const int W = 420, H = 510, M0 = 20, CW = 380, HeadH = 48, NavH = 36, Top = HeadH + NavH + 16;
        bool custom = IsCustom, sound = IsSound, dash = _page == PageDash, soundPage = _page == PageSound,
             soundUi = soundPage && _rgb && sound,
             color = _page == PageColor, keys = _page == PageKeys, react = _page == PageReact, sett = _page == PageSettings;

        _header.SetBounds(0, 0, D(W), D(HeadH));
        _closeBtn.SetBounds(D(W - 20 - 28), D(10), D(28), D(28));
        _nav.SetBounds(0, D(HeadH), D(W), D(NavH));

        // ---- dash
        _about.Visible = _patCap.Visible = _patCard.Visible = _speedCap.Visible = dash;
        _go.Visible = dash || soundUi;
        _tabs.Visible = _preview.Visible = dash && !custom && !sound;
        _speedCard.Visible = dash && (custom || sound);
        _slider.Visible = custom;
        _sens.Visible = _meter.Visible = sound;
        _speedCap.Caption = sound ? "SOUND" : "SPEED";
        _speedLbl.Text = sound ? "Sensitivity" : "Step delay";

        int y = Top;
        _about.SetBounds(D(M0), D(y), D(CW), D(72));
        y += 72 + 18;

        _patCap.SetBounds(D(M0), D(y), D(200), D(16));
        y += 20;
        int pcH = custom ? 104 : 52;
        _patCard.SetBounds(D(M0), D(y), D(CW), D(pcH));
        _patLabel.SetBounds(D(14), D(14), D(110), D(24));
        _select.SetBounds(D(CW - 12 - 190), D(8), D(190), D(36));
        _inputBox.Visible = custom;
        if (custom)
        {
            _inputBox.SetBounds(D(8), D(52), D(CW - 16), D(40));
            _custom.SetBounds(D(12), (D(40) - _custom.Height) / 2, D(CW - 40), _custom.Height);
        }
        y += pcH + 18;

        _speedCap.SetBounds(D(M0), D(y), D(200), D(16));
        y += 20;
        if (custom)
        {
            _speedCard.SetBounds(D(M0), D(y), D(CW), D(78));
            _speedLbl.SetBounds(D(14), D(8), D(200), D(24));
            _speedVal.SetBounds(D(CW - 14 - 110), D(8), D(110), D(24));
            _slider.SetBounds(D(10), D(38), D(CW - 20), D(32));
        }
        else if (sound)
        {
            _speedCard.SetBounds(D(M0), D(y), D(CW), D(130));
            _speedLbl.SetBounds(D(14), D(8), D(200), D(24));
            _speedVal.SetBounds(D(CW - 14 - 110), D(8), D(110), D(24));
            _sens.SetBounds(D(10), D(36), D(CW - 20), D(32));
            _meter.SetBounds(D(14), D(80), D(CW - 28), D(38));
        }
        else
        {
            _tabs.SetBounds(D(M0), D(y), D(CW), D(42));
            y += 42 + 12;
            _preview.SetBounds(D(M0), D(y), D(CW), D(76));
        }
        _go.SetBounds(D(M0), D(H - 20 - 44), D(CW), D(44));

        // ---- sound (RGB four-zone models only; this page only ever shows an audio engine)
        _soundCap.Visible = _soundModes.Visible = _soundDesc.Visible =
            _soundCtlCap.Visible = _soundCard.Visible = soundUi;
        _soundStyleCap.Visible = _soundStyles.Visible = _soundStyleHint.Visible = soundUi;
        y = Top;
        _soundCap.SetBounds(D(M0), D(y), D(200), D(16));
        y += 20;
        _soundModes.SetBounds(D(M0), D(y), D(CW), D(42));
        y += 42 + 8;
        _soundDesc.SetBounds(D(M0), D(y), D(CW), D(18));
        y += 26;
        _soundCtlCap.SetBounds(D(M0), D(y), D(240), D(16));
        y += 20;
        _soundCard.SetBounds(D(M0), D(y), D(CW), D(116));
        _soundLbl.SetBounds(D(14), D(8), D(200), D(24));
        _soundVal.SetBounds(D(CW - 14 - 110), D(8), D(110), D(24));
        _soundSens.SetBounds(D(10), D(36), D(CW - 20), D(32));
        _soundMeter.SetBounds(D(14), D(82), D(CW - 28), D(30));
        _soundStyleCap.SetBounds(D(M0), D(344), D(240), D(16));
        _soundStyles.SetBounds(D(M0), D(364), D(CW), D(42));
        _soundStyleHint.SetBounds(D(M0), D(410), D(CW), D(28));

        // ---- colour
        _colorCap.Visible = _colorCard.Visible = _effectCap.Visible = _effectCard.Visible =
            _syncCap.Visible = _syncCard.Visible = _zones.Visible = _syncHint.Visible = color;
        y = Top;
        if (color)
        {
            _colorCap.SetBounds(D(M0), D(y), D(240), D(16));
            y += 20;
            _colorCard.SetBounds(D(M0), D(y), D(CW), D(52));
            _swatches.SetBounds(D(8), D(8), D(CW - 16), D(36));
            y += 52 + 18;

            _effectCap.SetBounds(D(M0), D(y), D(240), D(16));
            y += 20;
            _effectCard.SetBounds(D(M0), D(y), D(CW), D(48));
            _effectSelect.SetBounds(D(8), D(6), D(CW - 16), D(36));
            y += 48 + 18;

            _syncCap.SetBounds(D(M0), D(y), D(240), D(16));
            y += 20;
            _syncCard.SetBounds(D(M0), D(y), D(CW), D(48));
            _syncRow.SetBounds(D(1), D(1), D(CW - 2), D(46));
            y += 48 + 16;

            _zones.SetBounds(D(M0), D(y), D(CW), D(56));
            y += 56 + 8;
            _syncHint.SetBounds(D(M0), D(y), D(CW), D(38));
        }

        // ---- keys
        _keysCap.Visible = _resetKeys.Visible = _keysCard.Visible = _keysNote.Visible = keys;
        y = Top;
        _keysCap.SetBounds(D(M0), D(y), D(200), D(16));
        _resetKeys.SetBounds(D(M0 + CW - 160), D(y), D(160), D(16));
        y += 20;
        int rowH = 42;
        _keysCard.SetBounds(D(M0), D(y), D(CW), D(rowH * Hk.Count + 2));
        for (int i = 0; i < Hk.Count; i++) _keyRows[i].SetBounds(D(1), D(1 + i * rowH), D(CW - 2), D(rowH));
        y += rowH * Hk.Count + 2 + 8;
        _keysNote.SetBounds(D(M0), D(y), D(CW), D(18));

        // ---- reactions
        _reactCap.Visible = _reactCard.Visible = _reactNote.Visible = react;
        y = Top;
        _reactCap.SetBounds(D(M0), D(y), D(240), D(16));
        y += 20;
        _reactCard.SetBounds(D(M0), D(y), D(CW), D(3 * 48 + 2));
        _reactPowerRow.SetBounds(D(1), D(1), D(CW - 2), D(48));
        _reactPlugRow.SetBounds(D(1), D(49), D(CW - 2), D(48));
        _reactUnplugRow.SetBounds(D(1), D(97), D(CW - 2), D(48));
        y += 3 * 48 + 2 + 8;
        _reactNote.SetBounds(D(M0), D(y), D(CW), D(18));

        // ---- settings
        _stats.Visible = _startCap.Visible = _card.Visible = sett;
        y = Top;
        _stats.SetBounds(D(M0), D(y), D(CW), D(92));
        y += 92 + 18;
        _startCap.SetBounds(D(M0), D(y), D(200), D(16));
        y += 20;
        _card.SetBounds(D(M0), D(y), D(CW), D(4 * 48 + 2));
        _startupRow.SetBounds(D(1), D(1), D(CW - 2), D(48));
        _minRow.SetBounds(D(1), D(49), D(CW - 2), D(48));
        _trayRow.SetBounds(D(1), D(97), D(CW - 2), D(48));
        _autoRow.SetBounds(D(1), D(145), D(CW - 2), D(48));

        ClientSize = new Size(D(W), D(H));
        Invalidate(true);
    }

    // ---- menus
    private static ContextMenuStrip MakeMenu() => new ContextMenuStrip
    {
        Renderer = new ToolStripProfessionalRenderer(new MenuColors()) { RoundedEdges = false },
        BackColor = Th.Card,
        ForeColor = Th.Text,
        Font = Fonts.Body,
        ShowImageMargin = false,
        ShowCheckMargin = true
    };

    private void RebuildMenus()
    {
        _presetMenu = MakeMenu();
        for (int i = 0; i < Presets.Length; i++)
        {
            int idx = i;
            var item = new ToolStripMenuItem(Presets[i].Name) { Checked = i == _pattern, Padding = new Padding(2, 5, 2, 5) };
            item.Click += (_, _) => SetPattern(idx);
            _presetMenu.Items.Add(item);
        }

        var tray = MakeMenu();
        tray.Items.Add("Show", null, (_, _) => ShowWindow());
        _trayToggle = new ToolStripMenuItem("Start");
        _trayToggle.Click += (_, _) => ToggleRun();
        tray.Items.Add(_trayToggle);
        tray.Items.Add(new ToolStripSeparator());
        tray.Items.Add("Exit", null, (_, _) => { _quit = true; Close(); });
        _notify.ContextMenuStrip = tray;
    }

    private void ShowPresetMenu()
    {
        if (_presetMenu == null) return;
        _presetMenu.MinimumSize = new Size(_select.Width, 0);
        _presetMenu.Show(_select, new Point(0, _select.Height + 4));
    }

    // ---- pattern / speed (all of this applies instantly, also while breathing)
    private string SoundDescription() => _pattern switch
    {
        0 => "A slow on-off pulse. No audio needed.",
        1 => "Beat, highs and voice each get their own brightness.",
        2 => "Only kick and snare hits light the keys.",
        3 => "Steps down 4-3-2-1-0 and repeats. No audio needed.",
        _ => "Your own 0-4 sequence, played at the chosen speed."
    };

    private void SetPattern(int idx)
    {
        _pattern = idx;
        if (_s.Pattern != idx) { _s.Pattern = idx; _s.Save(); }   // remember it for the next launch
        _select.Caption = Presets[idx].Name;
        _soundModes.Selected = IsSound ? (_pattern == 2 ? 1 : 0) : -1;
        _soundDesc.Text = SoundDescription();
        _preview.SetPattern(IsCustom ? new int[0] : Presets[idx].Levels);
        if (_presetMenu != null)
            for (int i = 0; i < _presetMenu.Items.Count; i++)
                if (_presetMenu.Items[i] is ToolStripMenuItem mi) mi.Checked = i == idx;
        // The SOUND page only describes the two audio engines. If a shortcut changes the pattern
        // to Pulse, Fade out or Custom while it is open, go back to DASH instead of showing a
        // mismatched page.
        if (_page == PageSound && !IsSound)
        {
            _page = PageDash;
            if (_nav.Selected != 0) _nav.Selected = 0;
        }

        RefreshLevels();
        UpdateDelay();
        LayoutUi();
    }

    private void UpdateDelay()
    {
        _delay = IsCustom ? _slider.Value : SpeedMs[_tabs.Selected];
        _speedVal.Text = IsSound ? _sens.Value + " / 10" : _slider.Value + " ms";
        if (_soundVal.Visible)
            _soundVal.Text = _sens.Value + " / 10";
        UpdateModeText();
    }

    private void UpdateModeText()
    {
        string mode = IsSound
            ? Presets[_pattern].Name + " · " + _sens.Value + "/10"
            : IsCustom
                ? "Custom · " + _slider.Value + " ms"
                : Presets[_pattern].Name + " · " + SpeedNames[_tabs.Selected];
        _stats.SetMode(mode);
        UpdateTip();
    }

    private void UpdateTip()
    {
        string t = "KB Breathe – " + (_running ? "On" : "Off") + " · " + _stats.ModeText();
        _notify.Text = t.Length > 62 ? t.Substring(0, 62) : t;
    }

    // Worker threads pick the new pattern up on their next step (and abandon the current one right away).
    private void RefreshLevels()
    {
        _sound = IsSound;
        _soundMode = IsSound ? SoundModeIndex : 0;
        var p = ParsePattern();
        if (p == null) return;      // half-typed custom text: keep the last good pattern
        _levels = p;
        _ver++;
    }

    private int[]? ParsePattern()
    {
        if (!IsCustom) return Presets[_pattern].Levels;
        var list = new List<int>();
        foreach (var part in _customText.Split(new[] { ' ', ',', ';', '-', '→', '>', '↔' }, StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(part, out var n) && n >= 0 && n <= 4) list.Add(n);
        return list.Count > 0 ? list.ToArray() : null;
    }

    // ---- backend
    // The order Nitrous probes in: the colour controller first (it drives brightness too), then the
    // Acer EC HID, then the two WMI backlight paths the WMI-only models use.
    private static Backend Probe()
    {
        if (RgbKeyboard.IsAvailable()) return Backend.Rgb;
        if (EcHid.IsAvailable()) return Backend.Ec;
        if (AcerWmiKeyboard.IsAvailable()) return Backend.WmiAction;
        return WmiBacklight.IsAvailable() ? Backend.Wmi : Backend.None;
    }

    // Every write in the app goes through here, so a backend that can carry colour and one that can
    // only dim are interchangeable to the pattern and sound workers.
    private static bool Apply(Backend backend, int pct)
    {
        switch (backend)
        {
            case Backend.Rgb: return RgbKeyboard.SetBrightness(pct);
            case Backend.Ec: return EcHid.SetBrightness(pct);
            case Backend.WmiAction: return AcerWmiKeyboard.SetBrightness(pct);
            default: return WmiBacklight.SetBrightness(pct);
        }
    }

    private void ShowBackend()
    {
        switch (_backend)
        {
            case Backend.Rgb: _stats.SetBackend("RGB", true); break;
            case Backend.Ec: _stats.SetBackend("EC", true); break;
            case Backend.WmiAction: _stats.SetBackend("WMI KB", true); break;
            case Backend.Wmi: _stats.SetBackend("WMI", true); break;
            default: _stats.SetBackend("—", false); break;
        }
    }

    private void Post(Action a)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try { BeginInvoke(a); }
        catch (InvalidOperationException) { }   // includes ObjectDisposedException
    }

    private void DetectAsync()
    {
        Task.Run(() =>
        {
            bool rgb = RgbKeyboard.IsAvailable();
            var b = Probe();
            Post(() =>
            {
                bool changed = rgb != _rgb;
                _rgb = rgb;
                if (_rgb && _effect != ColorFx.Static) ApplyEffect();   // bring the saved effect back
                if (!_running) { _backend = b; ShowBackend(); }
                if (!_rgb) { StopFx(); if (_syncRunning) StopSync(); }
                if (changed)
                {
                    SetupNav();
                    LayoutUi();
                }
            });
        });
    }

    // ---- keyboard reactions: power plan change / charger plug / charger unplug -> 0-4-0 pulse
    // Polled once a second (cheap, and nothing system-wide gets hooked), so a plan switch or a cable shows up
    // within a second. The first tick only records state, so nothing fires at startup.
    private void CheckReactions()
    {
        if (IsDisposed) return;

        bool ac = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;

        // charger first: a cable change can also switch which mode is active, and one flash is enough
        bool acChanged = _onAc != null && ac != _onAc;
        if (acChanged)
        {
            if (ac && _s.ReactPlug) React();        // charger connected
            if (!ac && _s.ReactUnplug) React();     // charger removed
            _powerSig = PowerSignature();            // re-arm without also firing the mode reaction
        }
        _onAc = ac;

        if (_s.ReactPower)
        {
            var sig = PowerSignature();
            if (sig != null)
            {
                if (!acChanged && _powerSig != null && sig != _powerSig) React(TopForMode(PerfModeName()));
                _powerSig = sig;
            }
        }
        else _powerSig = null;
    }

    // Nitrous writes the mode where it applies it: "EC HID: power mode set to Turbo (read back Turbo)". That is
    // exact and immediate, unlike the LastAcPowerMode/LastDcPowerMode numbers, which turned out to sit a step
    // behind (quiet climbed to 4, balanced stopped at 1). So the log tail is the source of truth.
    private static string? PerfModeName()
    {
        try
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Nitrous", "logs", "nitrous.log");
            using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
            long start = Math.Max(0, fs.Length - 8192);
            fs.Seek(start, System.IO.SeekOrigin.Begin);
            var buf = new byte[(int)(fs.Length - start)];
            int n = fs.Read(buf, 0, buf.Length);
            string text = System.Text.Encoding.UTF8.GetString(buf, 0, n);
            const string mark = "power mode set to ";
            int at = text.LastIndexOf(mark, StringComparison.Ordinal);
            if (at < 0) return null;
            int i = at + mark.Length, from = i;
            while (i < text.Length && char.IsLetter(text[i])) i++;
            return i > from ? text.Substring(from, i - from) : null;
        }
        catch { }
        return null;
    }

    // the height the flash climbs to: quiet 0-1, balanced 0-1-2, performance 0-1-2-3, turbo 0-1-2-3-4
    private static int TopForMode(string? name)
    {
        switch ((name ?? "").ToLowerInvariant())
        {
            case "quiet": return 1;
            case "balanced": return 2;
            case "performance": return 3;
            case "turbo": return 4;
        }
        return 4;   // unknown mode: the full climb
    }

    // Nitrous first; without it the Windows power-mode slider (ActiveOverlay*) is the fallback so those machines
    // still get a reaction. The Acer WMI profile API needs admin, so it is not used.
    private static string? PowerSignature()
    {
        var name = PerfModeName();
        if (name != null) return "nitrous|" + name;
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes");
            if (k != null)
            {
                var v = k.GetValue("ActiveOverlayAcPowerScheme") ?? k.GetValue("ActiveOverlayDcPowerScheme");
                if (v != null) return "win|" + v;
            }
        }
        catch { }
        return null;
    }

    // Steps ON one level at a time and cuts straight to off at the mode's height (no ending fade):
    // quiet 0-1, balanced 0-1-2, performance 0-1-2-3, turbo 0-1-2-3-4 (0 = off, 1-4 = brightness).
    // Charger reactions climb all four.
    private const int RiseMs = 180, HoldMs = 600;

    private async void React(int top = 4)
    {
        if (_reacting) return;
        if (_backend == Backend.None) { _backend = Probe(); ShowBackend(); }
        var backend = _backend;
        if (backend == Backend.None) return;      // no backlight interface: nothing to flash

        top = M.Clamp(top, 1, 4);
        _reacting = true;
        try
        {
            for (int i = 0; i <= top; i++) { Flash(backend, i); await Task.Delay(RiseMs); }
            await Task.Delay(HoldMs);                             // sit at the mode's level so it can be read
            Flash(backend, 0);                                    // straight off: no ending fade
        }
        finally { _reacting = false; }
    }

    private static void Flash(Backend backend, int lvl)
    {
        Apply(backend, LevelBrightness[M.Clamp(lvl, 0, 4)]);
    }

    // "Apply selected mode on startup": turn the saved pattern on as soon as the app is up
    private void AutoApply()
    {
        if (_running || IsDisposed || Probe() == Backend.None) return;   // no interface: stay idle, no dialog at boot
        StartBreathing();
    }

    // ---- run / stop
    private void ToggleRun()
    {
        if (_running) StopBreathing(); else StartBreathing();
    }

    private void StartBreathing()
    {
        if (_syncRunning) StopSync();   // one owner of the LEDs at a time
        _backend = Probe();
        ShowBackend();
        if (_backend == Backend.None)
        {
            MessageBox.Show("No backlight interface found.\nClose NitroSense / OpenSense first.",
                "Device not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RefreshLevels();
        var backend = _backend;
        _running = true;
        SetRunningUi(true);

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int ver = _ver;   // read the version first: a pattern change in between is then noticed
                    if (_sound) { await RunSound(backend, token, ver).ConfigureAwait(false); continue; }
                    var levels = _levels;
                    if (levels.Length == 0) { await Task.Delay(30, token).ConfigureAwait(false); continue; }
                    for (int i = 0; i < levels.Length; i++)
                    {
                        if (token.IsCancellationRequested || ver != _ver) break;   // pattern changed -> restart on the new one
                        int step = i;
                        int lvl = levels[i];
                        while (_reacting && !token.IsCancellationRequested)   // a reaction pulse owns the keys
                            await Task.Delay(10, token).ConfigureAwait(false);
                        int pct = LevelBrightness[M.Clamp(lvl, 0, 4)];
                        bool ok = Apply(backend, pct);
                        if (_uiVisible)
                            Post(() =>
                            {
                                if (!_running) return;
                                _stats.Set(true, lvl, pct, !ok);
                                _preview.Step = step;
                            });

                        // wait in short slices so speed / pattern changes bite immediately
                        var sw = Stopwatch.StartNew();
                        while (ver == _ver && !token.IsCancellationRequested)
                        {
                            long left = _delay - sw.ElapsedMilliseconds;
                            if (left <= 0) break;
                            await Task.Delay((int)Math.Min(left, 20), token).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Post(() =>
                {
                    StopBreathing();
                    MessageBox.Show("Error:\n" + ex.Message, "KB Breathe", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                if (backend == Backend.Ec) EcHid.Release();
                if (backend == Backend.Rgb) RgbKeyboard.Release();
            }
        });
    }

    // Sound sync: follow the music until the pattern changes or breathing stops
    private async Task RunSound(Backend backend, CancellationToken token, int ver)
    {
        using var snd = new SoundSync();   // throws a readable error if no audio device is available
        var zones = new RgbColor[4];
        int last = -1, lastStyle = -1;
        while (ver == _ver && !token.IsCancellationRequested)
        {
            snd.Sensitivity = _sensVal;
            snd.Mode = _soundMode;
            snd.Process();
            int lvl = snd.Level;
            if (_reacting) { last = -1; await Task.Delay(8, token).ConfigureAwait(false); continue; }
            if (lvl != last || _soundStyle != lastStyle)
            {
                last = lvl;
                lastStyle = _soundStyle;
                int pct = LevelBrightness[M.Clamp(lvl, 0, 4)];
                bool ok;
                // Colour styles split the four zones, so they go out as zone packets. Single
                // colour (0) and every non-RGB backend keep the original one-packet path.
                if (backend == Backend.Rgb && _soundStyle > 0 && lvl > 0)
                {
                    SoundPalette.Zones(_soundStyle, RgbKeyboard.Color, zones);
                    ok = RgbKeyboard.SetZones(zones, pct);
                }
                else ok = Apply(backend, pct);
                if (_uiVisible)
                    Post(() =>
                    {
                        if (!_running) return;
                        _stats.Set(true, lvl, pct, !ok);
                        _meter.Level = lvl;
                    });
            }
            await Task.Delay(8, token).ConfigureAwait(false);
        }
    }

    private void StopBreathing()
    {
        if (_cts != null) { _cts.Cancel(); _cts.Dispose(); _cts = null; }
        _running = false;
        if (!IsDisposed) SetRunningUi(false);
        if (_rgb && !_syncRunning) ApplyEffect();   // hand the keys back to the chosen effect
    }

    // ---- screen colour sync (the colour page)
    private void ShowZones(RgbColor c) => _zones.Set(new[] { c, c, c, c });

    // ---- keyboard effects (the colour page)
    private void ShowEffectMenu()
    {
        if (_effectMenu == null)
        {
            _effectMenu = MakeMenu();
            for (int i = 0; i < ColorFx.Count; i++)
            {
                int idx = i;
                var item = new ToolStripMenuItem(ColorFx.Names[i]) { Padding = new Padding(2, 5, 2, 5) };
                item.Click += (_, _) => SelectEffect(idx);
                _effectMenu.Items.Add(item);
            }
        }

        for (int i = 0; i < _effectMenu.Items.Count; i++)
            if (_effectMenu.Items[i] is ToolStripMenuItem mi) mi.Checked = i == _effect;
        _effectMenu.MinimumSize = new Size(_effectSelect.Width, 0);
        _effectMenu.Show(_effectSelect, new Point(0, _effectSelect.Height + 4));
    }

    private void SelectEffect(int index)
    {
        _effect = M.Clamp(index, 0, ColorFx.Count - 1);
        _effectSelect.Caption = ColorFx.Names[_effect];
        _s.Effect = _effect;
        _s.Save();

        // The effect and the patterns both write the same LEDs, so one has to stop.
        if (_running) { StopBreathing(); return; }   // StopBreathing applies the new effect
        // Screen sync owns the LEDs while it runs; picking an effect takes them back.
        if (_syncRunning) { StopSync(); return; }
        ApplyEffect();
    }

    private void ApplyEffect()
    {
        if (!_rgb || _closing || IsDisposed) return;
        StopFx();

        if (_effect == ColorFx.Static)
        {
            RgbKeyboard.SetBrightness(LevelBrightness[4]);
            ShowZones(_swatches.Selection);
            return;
        }

        if (ColorFx.IsFlow(_effect)) { StartFx(); return; }

        // The firmware animates these itself, so one packet keeps running after we let go.
        RgbKeyboard.SetEffect(ColorFx.EffectByte(_effect), _swatches.Selection, 4);
        ShowZones(_swatches.Selection);
    }

    private void StartFx()
    {
        if (!_rgb || _closing || _fxRunning) return;
        _fxRunning = true;
        _fxCts = new CancellationTokenSource();
        var token = _fxCts.Token;
        var clock = Stopwatch.StartNew();
        var zones = new RgbColor[4];

        Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // patterns, reactions and screen sync own the LEDs while they run
                    if (_running || _reacting || _syncRunning)
                    {
                        await Task.Delay(50, token).ConfigureAwait(false);
                        continue;
                    }

                    ColorFx.Flow(_effect, clock.Elapsed.TotalSeconds, zones);
                    RgbKeyboard.SetZones(zones, LevelBrightness[4]);

                    if (_uiVisible)
                    {
                        var shown = (RgbColor[])zones.Clone();
                        Post(() => { if (_fxRunning) _zones.Set(shown); });
                    }

                    // full rate while the page is on screen, a third of it in the tray
                    await Task.Delay(_uiVisible ? 40 : 120, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }   // the token can go away mid-wait
        });
    }

    private void StopFx()
    {
        if (_fxCts != null) { _fxCts.Cancel(); _fxCts.Dispose(); _fxCts = null; }
        _fxRunning = false;
    }

    private void StartSync()
    {
        if (_syncRunning) return;

        _backend = Probe();
        ShowBackend();
        if (_backend != Backend.Rgb)
        {
            _syncRow.Checked = false;
            MessageBox.Show("Screen colour sync needs the keyboard's RGB controller (close NitroSense first).",
                "Not supported", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        StopBreathing();
        StopFx();
        _syncRunning = true;
        _syncCts = new CancellationTokenSource();
        var token = _syncCts.Token;

        Task.Run(async () =>
        {
            using var screen = new ScreenZones();
            var zones = new RgbColor[4];

            while (!token.IsCancellationRequested)
            {
                if (!_reacting && screen.TryCapture(out var pixels))
                {
                    ScreenZones.ReadZones(pixels, zones);
                    RgbKeyboard.SetZones(zones, LevelBrightness[4]);

                    if (_uiVisible)
                    {
                        var shown = (RgbColor[])zones.Clone();
                        Post(() => { if (_syncRunning) _zones.Set(shown); });
                    }
                }

                try { await Task.Delay(125, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        });
    }

    private void StopSync()
    {
        if (_syncCts == null && !_syncRunning) return;
        if (_syncCts != null) { _syncCts.Cancel(); _syncCts.Dispose(); _syncCts = null; }
        _syncRunning = false;
        _syncRow.Checked = false;
        ApplyEffect();   // back to the chosen colour and effect
    }

    private void SetRunningUi(bool on)
    {
        _go.Active = on;
        _go.Caption = on ? "Stop" : "Start";
        _go.Glyph = on ? Ico.Stop : Ico.Play;
        _go.Invalidate();
        _stats.Set(on, -1, 0, false);
        _preview.Step = -1;
        if (!on) _meter.Level = -1;
        _header.SetState(on);
        UpdateTip();
        if (_trayToggle != null) _trayToggle.Text = on ? "Stop" : "Start";
    }

    // ---- startup
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "KbBreathe";

    private static bool IsStartupEnabled()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(RunKey, false); return k?.GetValue(AppName) != null; }
        catch { return false; }
    }

    private static void SetStartup(bool enable)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (k == null) return;
            if (enable) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
            else k.DeleteValue(AppName, false);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Startup setting failed:\n" + ex.Message, "KB Breathe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

// ============================================================ system audio (WASAPI loopback) -> brightness level 0-4
// Captures whatever the PC is playing, follows the bass/beat envelope and turns it into the same 0-4 levels.
sealed class SoundSync : IDisposable
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity,
            IntPtr pFormat, IntPtr audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    private const uint LoopbackFlag = 0x00020000;
    private const uint SilentFlag = 2;

    private IAudioClient? _client;
    private IAudioCaptureClient? _cap;
    private readonly int _ch, _bits;
    private readonly bool _float;
    private readonly int _hop;
    private readonly float _a;
    private float[] _fbuf = new float[0];
    private short[] _sbuf = new short[0];
    private int[] _ibuf = new int[0];
    private byte[] _bbuf = new byte[0];

    // analysis state
    private float _lp1, _lp2, _m1;                                  // kick band (2-pole 150 Hz), body band (150 Hz-2 kHz)
    private float _sumB, _sumM, _sumF;
    // vibe-mode front end: 5 kHz high-pass (2 stages), voice split 300-1000 Hz / 1-3.4 kHz (12 dB/oct high-passes)
    private float _lpH1, _lpH2, _fa1, _fa2, _fb1, _fb2, _fc1, _fc2, _fd1;
    private float _sumH, _sumVL, _sumVH;
    private readonly float _hc, _c300, _c1000, _c3400;
    // vibe-mode state
    private float _vHs, _vHavg, _vE, _vFE, _vPk = 0.004f, _vPE = 0.4f, _vPC = 0.4f;
    private int _vBLvl, _vBHold, _vHLast = -1000, _vHHold, _vAbove, _vBelow, _vAge;
    private bool _vOn, _vHigh;
    // vibe-mode tuning (10 ms steps)
    private const int BeatHold = 11;         // a hit stays lit ~110 ms, then drops straight to the base level (no 4-3-2-1 staircase)
    private const int HiHold = 12, HiRefract = 6;   // a high burst (hat / cymbal / "s") holds level 3 for ~120 ms
    private const int VOnHops = 4, VOffHops = 18;   // vocal must persist 40 ms to count, and holds 180 ms so words do not blink
    private const int MinDwell = 6;          // a level is never dropped sooner than 60 ms after it was set (rises are instant)
    private const float SoftHit = 0.55f;     // hit strength below this = soft / ghost hit -> level 3, above -> level 4
    private const float VOn = 0.16f, VOff = 0.08f, VFrac = 0.22f;   // vocal presence vs. its own recent peak; voice band >= 22 % of total
    private float _bassSlow, _midSlow, _onsetAvg, _onsetPeak, _flash, _gain = 1f;
    private float _hi = 0.004f, _lo, _env;                            // overall mode: adaptive range + envelope
    private int _cnt, _hold, _hopIdx, _lastBeat = -1000;
    private volatile int _mode;                                     // 0 = beat, 1 = overall, 2 = vibe
    private int _level;
    private readonly float _b;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private long _lastData, _silentAt;
    private const float FallPerHop = 0.05f;   // after the hold: one level every ~45 ms, 4->0 in ~0.2 s

    public int Level => _level;
    public int Sensitivity { set { _gain = 0.5f + M.Clamp(value, 1, 10) * 0.1f; } }
    public int Mode { set { _mode = value; } }          // 0 = beat only, 1 = follow the whole tune, 2 = vibe (beat 4 / highs 3 / vocals 1-2)

    public SoundSync()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        IMMDevice? device;
        int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out device);   // eRender, eMultimedia
        if (hr != 0 || device == null) throw new InvalidOperationException("No audio output device found.");

        var iid = typeof(IAudioClient).GUID;
        object o;
        hr = device.Activate(ref iid, 23, IntPtr.Zero, out o);
        if (hr != 0) throw new InvalidOperationException("Could not open the audio device (0x" + hr.ToString("X8") + ").");
        _client = (IAudioClient)o;

        IntPtr pf;
        hr = _client.GetMixFormat(out pf);
        if (hr != 0 || pf == IntPtr.Zero) throw new InvalidOperationException("Could not read the audio format.");
        int rate;
        try
        {
            ushort tag = (ushort)Marshal.ReadInt16(pf, 0);
            _ch = Math.Max(1, (int)Marshal.ReadInt16(pf, 2));
            rate = Marshal.ReadInt32(pf, 4);
            _bits = Marshal.ReadInt16(pf, 14);
            if (tag == 0xFFFE) tag = (ushort)Marshal.ReadInt16(pf, 24);   // extensible: sub-format id
            _float = tag == 3;

            // A short buffer keeps the light close to what you hear; fall back to the safe 200 ms if refused.
            hr = _client.Initialize(0, LoopbackFlag, 600000, 0, pf, IntPtr.Zero);    // shared mode, loopback, 60 ms buffer
            if (hr != 0) hr = _client.Initialize(0, LoopbackFlag, 2000000, 0, pf, IntPtr.Zero);
        }
        finally { Marshal.FreeCoTaskMem(pf); }
        if (hr != 0) throw new InvalidOperationException("Could not capture system audio (0x" + hr.ToString("X8") + ").");
        if (!(_float && _bits == 32) && _bits != 16 && _bits != 24 && _bits != 32)
            throw new InvalidOperationException("Unsupported audio format (" + _bits + " bit).");

        var capIid = typeof(IAudioCaptureClient).GUID;
        object svc;
        hr = _client.GetService(ref capIid, out svc);
        if (hr != 0) throw new InvalidOperationException("Could not start audio capture (0x" + hr.ToString("X8") + ").");
        _cap = (IAudioCaptureClient)svc;

        _hop = Math.Max(1, rate / 100);                                         // 10 ms analysis hop
        _a = 1f - (float)Math.Exp(-2.0 * Math.PI * 150.0 / rate);               // ~150 Hz low-pass = the kick/bass band
        _b = 1f - (float)Math.Exp(-2.0 * Math.PI * 2000.0 / rate);              // 2 kHz low-pass on the rest = snare/clap body
        _hc = 1f - (float)Math.Exp(-2.0 * Math.PI * Math.Min(5000.0, rate * 0.3) / rate);   // 5 kHz: start of the "highs" (hats, cymbals, air)
        _c300 = 1f - (float)Math.Exp(-2.0 * Math.PI * 300.0 / rate);
        _c1000 = 1f - (float)Math.Exp(-2.0 * Math.PI * 1000.0 / rate);
        _c3400 = 1f - (float)Math.Exp(-2.0 * Math.PI * Math.Min(3400.0, rate * 0.3) / rate);
        _client.Start();
        _lastData = _silentAt = _sw.ElapsedMilliseconds;
    }

    // call every few ms: drains captured audio and updates Level
    public void Process()
    {
        if (_cap == null) return;
        bool got = false;
        for (int guard = 0; guard < 64; guard++)
        {
            uint n;
            if (_cap.GetNextPacketSize(out n) != 0 || n == 0) break;
            IntPtr data; uint frames, flags; ulong dp, qp;
            if (_cap.GetBuffer(out data, out frames, out flags, out dp, out qp) != 0) break;
            got = true;
            if ((flags & SilentFlag) != 0 || data == IntPtr.Zero)
                for (uint i = 0; i < frames; i++) Feed(0f);
            else
                ReadFrames(data, (int)frames);
            _cap.ReleaseBuffer(frames);
        }

        long now = _sw.ElapsedMilliseconds;
        if (got) { _lastData = now; _silentAt = now; }
        else if (now - _lastData > 50)
        {
            // loopback delivers nothing while the PC is silent: let the envelope fall on the wall clock
            while (_silentAt + 10 <= now) { _silentAt += 10; Hop(0f, 0f, 0f, 0f, 0f, 0f); }
        }
    }

    private void ReadFrames(IntPtr data, int frames)
    {
        int total = frames * _ch;
        float inv = 1f / _ch;
        if (_float)
        {
            if (_fbuf.Length < total) _fbuf = new float[total];
            Marshal.Copy(data, _fbuf, 0, total);
            for (int f = 0; f < frames; f++)
            {
                float m = 0; int b = f * _ch;
                for (int c = 0; c < _ch; c++) m += _fbuf[b + c];
                Feed(m * inv);
            }
        }
        else if (_bits == 16)
        {
            if (_sbuf.Length < total) _sbuf = new short[total];
            Marshal.Copy(data, _sbuf, 0, total);
            for (int f = 0; f < frames; f++)
            {
                float m = 0; int b = f * _ch;
                for (int c = 0; c < _ch; c++) m += _sbuf[b + c];
                Feed(m * inv / 32768f);
            }
        }
        else if (_bits == 32)
        {
            if (_ibuf.Length < total) _ibuf = new int[total];
            Marshal.Copy(data, _ibuf, 0, total);
            for (int f = 0; f < frames; f++)
            {
                float m = 0; int b = f * _ch;
                for (int c = 0; c < _ch; c++) m += _ibuf[b + c];
                Feed(m * inv / 2147483648f);
            }
        }
        else   // 24-bit packed
        {
            int bytes = total * 3;
            if (_bbuf.Length < bytes) _bbuf = new byte[bytes];
            Marshal.Copy(data, _bbuf, 0, bytes);
            for (int f = 0; f < frames; f++)
            {
                float m = 0;
                for (int c = 0; c < _ch; c++)
                {
                    int o = (f * _ch + c) * 3;
                    int v = (_bbuf[o] << 8) | (_bbuf[o + 1] << 16) | (_bbuf[o + 2] << 24);
                    m += v;
                }
                Feed(m * inv / 2147483648f);
            }
        }
    }

    private void Feed(float m)
    {
        _lp1 += _a * (m - _lp1);
        _lp2 += _a * (_lp1 - _lp2);            // bass / kick
        _m1 += _b * ((m - _lp2) - _m1);        // snare / clap / tom body

        // highs: two cascaded one-pole high-passes at 5 kHz (12 dB/oct) so mids and vocals do not leak in
        _lpH1 += _hc * (m - _lpH1);
        float h1 = m - _lpH1;
        _lpH2 += _hc * (h1 - _lpH2);
        float hi = h1 - _lpH2;

        // voice, low half 300-1000 Hz (fundamentals): 12 dB/oct high-pass at 300, then 2-pole low-pass at 1 kHz
        _fa1 += _c300 * (m - _fa1);
        float y1 = m - _fa1;
        _fa2 += _c300 * (y1 - _fa2);
        float y2 = y1 - _fa2;
        _fb1 += _c1000 * (y2 - _fb1);
        _fb2 += _c1000 * (_fb1 - _fb2);
        float vl = _fb2;

        // voice, high half 1-3.4 kHz (upper harmonics, brighter / higher voices): high-pass at 1 kHz, low-pass at 3.4 kHz
        _fc1 += _c1000 * (m - _fc1);
        float z1 = m - _fc1;
        _fc2 += _c1000 * (z1 - _fc2);
        float z2 = z1 - _fc2;
        _fd1 += _c3400 * (z2 - _fd1);
        float vh = _fd1;

        _sumB += _lp2 * _lp2;
        _sumM += _m1 * _m1;
        _sumF += m * m;
        _sumH += hi * hi;
        _sumVL += vl * vl;
        _sumVH += vh * vh;
        if (++_cnt >= _hop)
        {
            float inv = 1f / _cnt;
            float b = (float)Math.Sqrt(_sumB * inv), md = (float)Math.Sqrt(_sumM * inv), f = (float)Math.Sqrt(_sumF * inv);
            float h = (float)Math.Sqrt(_sumH * inv), vlr = (float)Math.Sqrt(_sumVL * inv), vhr = (float)Math.Sqrt(_sumVH * inv);
            _sumB = _sumM = _sumF = _sumH = _sumVL = _sumVH = 0f; _cnt = 0;
            Hop(b, md, f, h, vlr, vhr);
        }
    }

    // One 10 ms step. Both sound modes share the same 0-4 output:
    //   Beat    - only ATTACKS drive the light (a band jumping above its own ~250 ms baseline). Pads, vocals
    //             and sustained bass produce no attack, so the light stays dark; a kick or snare fires a flash
    //             that holds ~80 ms (the glow) and then walks back down 4-3-2-1.
    //   Overall - the light follows the tune's loudness envelope instead, smoothed so it flows, not twitches.
    //   Vibe    - three layers stacked, loudest wins: beat -> 4 (soft hit 3), highs burst (hat / cymbal) -> 3,
    //             sustained voice -> 1 (lower pitch) or 2 (higher pitch), nothing playing -> 0.
    private void Hop(float bass, float mid, float full, float high, float vl, float vh)
    {
        int mode = _mode;
        if (mode == 2)
        {
            Vibe(bass, mid, full, high, vl, vh);
            _hopIdx++;
            return;
        }
        if (mode == 1) FollowTune(bass, full);
        else Beat(bass, mid, full);
        _hopIdx++;

        // map the flash straight onto 0-4, one step at a time
        float q = _flash * 4.999f;
        if (q >= _level + 1f) _level = (int)Math.Floor(q);                // the hit lands on its own hop
        else if (q < _level - 0.25f) _level = (int)Math.Floor(q);         // margin keeps a level from flickering
    }

    // Attack detector shared by Beat and Vibe: true once per kick / snare hit, strength 0.4-1 = how hard it hit.
    private bool Onset(float bass, float mid, float full, out float strength, float bassK = 1.25f, float midK = 1.30f)
    {
        bool live = full >= 0.0006f;

        _bassSlow += (bass - _bassSlow) * 0.04f;                          // what the kick band has been doing
        _midSlow += (mid - _midSlow) * 0.04f;                             // what the snare band has been doing

        // attack strength = how far this step jumped above that baseline (bass weighted, snare ~half)
        float onset = live
            ? Math.Max(0f, bass - _bassSlow * bassK) + Math.Max(0f, mid - _midSlow * midK) * 0.55f
            : 0f;

        _onsetAvg += (onset - _onsetAvg) * 0.005f;                        // ~2 s of history for the threshold
        if (onset > _onsetPeak) _onsetPeak = onset; else _onsetPeak *= 0.9975f;   // ~3 s peak, for the flash height

        float thr = Math.Max(0.0012f, _onsetAvg * 1.8f) / _gain;          // sensitivity lowers the bar
        strength = 0f;
        if (onset > thr && _hopIdx - _lastBeat >= 10)                     // 100 ms refractory: one flash per hit
        {
            strength = M.Clamp(onset / Math.Max(_onsetPeak * 0.9f, thr), 0.4f, 1f);
            _lastBeat = _hopIdx;
            return true;
        }
        return false;
    }

    private void Beat(float bass, float mid, float full)
    {
        float strength;
        if (Onset(bass, mid, full, out strength))
        {
            if (strength > _flash) _flash = strength;                     // small hit = short glow, accent = full 4
            _hold = 8;                                                    // stay lit ~80 ms: this is the glow
        }
        else if (_hold > 0) _hold--;
        else
        {
            _flash -= FallPerHop;
            if (_flash < 0f) _flash = 0f;                                 // silence / steady tune = level 0
        }
    }

    // Vibe: three layers, the brightest wins.
    //   beat   -> 4 (soft / ghost hit -> 3), held ~110 ms, then straight back down to whatever else is playing
    //   highs  -> 3 for a burst above the high band's own baseline (hats, cymbals, "s")
    //   voice  -> 1 or 2 while something sustained sits in 300 Hz-3.4 kHz: lower pitch = 1, higher pitch = 2
    //   silence -> 0
    // Rises are instant so hits land on time; falls wait a minimum dwell so the light never chatters.
    private void Vibe(float bass, float mid, float full, float high, float vl, float vh)
    {
        bool live = full >= 0.0006f;

        // ---- beat layer (stricter attack test than Beat mode, so steady noise / pads do not fake a beat)
        float st;
        bool hit = Onset(bass, mid, full, out st, 1.7f, 1.9f);
        if (hit && high > 1.2f * mid && bass < 0.3f * mid) hit = false;   // hat-like attack: highs dominate, no body -> highs layer
        if (hit)
        {
            int lv = st >= SoftHit ? 4 : 3;
            if (lv >= _vBLvl || _vBHold == 0) _vBLvl = lv;
            _vBHold = BeatHold;
        }
        else if (_vBHold > 0 && --_vBHold == 0) _vBLvl = 0;

        // ---- highs layer
        _vHs += (high - _vHs) * 0.04f;
        float ho = live ? Math.Max(0f, high - _vHs * 1.4f) : 0f;
        _vHavg += (ho - _vHavg) * 0.005f;
        float hthr = Math.Max(0.0007f, _vHavg * 2f) / _gain;
        if (ho > hthr && _hopIdx - _vHLast >= HiRefract) { _vHLast = _hopIdx; _vHHold = HiHold; }
        else if (_vHHold > 0) _vHHold--;
        int hLvl = _vHHold > 0 ? 3 : 0;

        // ---- voice layer: presence (slow envelope, share of the whole sound) and pitch (high-half vs low-half balance)
        float v = (float)Math.Sqrt(vl * vl + vh * vh);
        _vE += (v - _vE) * (v > _vE ? 0.10f : 0.05f);                      // slow rise: short drum hits barely register
        _vFE += (full - _vFE) * (full > _vFE ? 0.10f : 0.05f);
        bool voiced = _vE >= VFrac * _vFE && _vE >= 0.003f;                // voice band must be a real share of the sound
        _vPk = _vE > _vPk ? _vE : Math.Max(0.004f, _vPk * 0.9996f);        // ~25 s memory: a held note does not fade out
        float ln = voiced ? _vE / _vPk * _gain : 0f;
        if (ln >= VOn) { _vAbove++; _vBelow = 0; }
        else if (ln < VOff) { _vBelow++; _vAbove = 0; }
        else { _vAbove = 0; _vBelow = 0; }
        if (!_vOn && _vAbove >= VOnHops) _vOn = true;
        else if (_vOn && _vBelow >= VOffHops) _vOn = false;
        if (!live) { _vOn = false; _vAbove = 0; }
        if (_vOn && v > 0.003f)
        {
            float p = vh / (vl + vh + 1e-9f);
            _vPE += (p - _vPE) * 0.05f;                                    // smoothed pitch brightness
            _vPC += (_vPE - _vPC) * 0.002f;                                // the song's own centre
        }
        float ctr = M.Clamp(_vPC, 0.36f, 0.44f);
        if (_vHigh && _vPE < ctr - 0.025f) _vHigh = false;
        else if (!_vHigh && _vPE > ctr + 0.025f) _vHigh = true;
        int vLvl = _vOn ? (_vHigh ? 2 : 1) : 0;

        // ---- combine
        int t = Math.Max(_vBLvl, Math.Max(hLvl, vLvl));
        if (!live && _vBHold == 0) t = 0;
        _vAge++;
        if (t > _level || (t < _level && _vAge >= MinDwell)) { _level = t; _vAge = 0; }
    }

    // overall sync: kick-weighted loudness, auto-ranged to the song, smoothed both ways
    private void FollowTune(float bass, float full)
    {
        float x = full < 0.0006f ? 0f : bass * 2.1f + full * 0.55f;

        if (x > _hi) _hi = x; else _hi = Math.Max(0.004f, _hi * 0.9990f);              // peak, ~3 s of memory
        if (x < _lo) _lo = x; else _lo += (x - _lo) * 0.0012f;                         // floor, ~8 s of memory
        float range = Math.Max(_hi - _lo, Math.Max(0.0006f, _hi * 0.25f));
        float n = M.Clamp((x - _lo) / range, 0f, 1f) * _gain;
        if (n > 1f) n = 1f;

        if (n > _env) _env += (n - _env) * 0.25f;                         // ~35 ms rise
        else { _env -= 0.02f; if (_env < 0f) _env = 0f; }                 // ~0.5 s fall: flows with the tune
        _flash = _env;
    }

    public void Dispose()
    {
        try { _client?.Stop(); } catch { }
        if (_cap != null) { try { Marshal.ReleaseComObject(_cap); } catch { } _cap = null; }
        if (_client != null) { try { Marshal.ReleaseComObject(_client); } catch { } _client = null; }
    }
}

// ============================================================ EC HID (same protocol; device is kept open while running)
static class EcHid
{
    private const ushort VendorId = 0x1025, ProductId = 0x174B, UsagePage = 0xFF05, Usage = 0x0001;
    private const byte ReportId = 0xA0;
    private const ushort RequestHeader = 0xA000, StatusDone = 0xE000, CommandDevice = 0x000A;
    private const byte KeyboardBacklight = 2, FunctionSet = 1;
    private const int ReportLen = 65, Attempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(15);
    private static readonly TimeSpan AnswerDelay = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMilliseconds(500);
    private const string LockName = @"Global\ECHIDFeatureVID_1025&PID_174B";

    private static readonly object Gate = new object();
    private static HidDevice? _dev;

    public static bool IsAvailable()
    {
        lock (Gate)
        {
            if (_dev != null) return true;
            using var d = Open();
            return d != null;
        }
    }

    public static bool SetBrightness(int brightness)
    {
        lock (Gate)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                if (_dev == null) _dev = Open();
                if (_dev == null) return false;
                if (Write(_dev, M.Clamp(brightness, 0, 100))) return true;
                _dev.Dispose();
                _dev = null;
            }
            return false;
        }
    }

    public static void Release()
    {
        lock (Gate)
        {
            if (_dev != null) { _dev.Dispose(); _dev = null; }
        }
    }

    private static bool Write(HidDevice device, int brightness)
    {
        var data = new byte[7];
        data[0] = KeyboardBacklight; data[1] = 1; data[2] = 0;
        data[3] = (byte)(brightness & 0xFF); data[4] = (byte)((brightness >> 8) & 0xFF);
        data[5] = 0; data[6] = 0;
        var reply = device.Exchange(CommandDevice, FunctionSet, data);
        return reply.HasValue && reply.Value.Status == StatusDone;
    }

    private static HidDevice? Open()
    {
        try
        {
            foreach (var path in HidInterop.Paths())
            {
                var handle = HidInterop.CreateFileW(path, 0xC0000000u, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (handle.IsInvalid) { handle.Dispose(); continue; }
                var info = HidInterop.Describe(handle);
                if (!info.HasValue ||
                    info.Value.VendorId != VendorId || info.Value.ProductId != ProductId ||
                    info.Value.UsagePage != UsagePage || info.Value.Usage != Usage)
                { handle.Dispose(); continue; }
                return new HidDevice(handle);
            }
        }
        catch { }
        return null;
    }

    private sealed class HidDevice : IDisposable
    {
        private readonly SafeFileHandle _handle;
        private readonly Mutex? _lock;
        private readonly byte[] _reply = new byte[ReportLen];

        public HidDevice(SafeFileHandle handle)
        {
            _handle = handle;
            try { _lock = new Mutex(false, LockName); } catch { _lock = null; }
        }

        public Reply? Exchange(ushort command, byte function, byte[] data)
        {
            var request = BuildRequest(command, function, data);
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                if (attempt > 0) Thread.Sleep(RetryDelay);
                var locked = false;
                try
                {
                    locked = _lock != null && _lock.WaitOne(LockTimeout);
                    if (!HidInterop.SetFeature(_handle, request)) continue;
                    Thread.Sleep(AnswerDelay);
                    Array.Clear(_reply, 0, _reply.Length); _reply[0] = ReportId;
                    if (!HidInterop.GetFeature(_handle, _reply)) continue;
                    var reply = Decode(_reply);
                    if (!reply.HasValue) continue;
                    if (reply.Value.Command != command) continue;
                    if (reply.Value.Status != StatusDone && reply.Value.Status != 0xE001) continue;
                    return reply;
                }
                catch (AbandonedMutexException) { locked = true; }
                finally { if (locked && _lock != null) _lock.ReleaseMutex(); }
            }
            return null;
        }

        private static byte[] BuildRequest(ushort command, byte function, byte[] data)
        {
            var report = new byte[ReportLen];
            report[0] = ReportId;
            report[1] = (byte)(RequestHeader & 0xFF); report[2] = (byte)(RequestHeader >> 8);
            report[3] = (byte)(command & 0xFF); report[4] = (byte)(command >> 8);
            report[5] = function;
            if (data.Length > 0) Buffer.BlockCopy(data, 0, report, 6, Math.Min(data.Length, report.Length - 6));
            return report;
        }

        private static Reply? Decode(byte[] report)
        {
            if (report.Length < 6 || report[0] != ReportId) return null;
            return new Reply((ushort)(report[1] | report[2] << 8), (ushort)(report[3] | report[4] << 8));
        }

        public void Dispose()
        {
            _handle.Dispose();
            if (_lock != null) _lock.Dispose();
        }
    }

    private readonly struct Reply
    {
        public readonly ushort Status, Command;
        public Reply(ushort status, ushort command) { Status = status; Command = command; }
    }

    internal readonly struct DeviceInfo
    {
        public readonly ushort VendorId, ProductId, UsagePage, Usage;
        public DeviceInfo(ushort v, ushort p, ushort up, ushort u) { VendorId = v; ProductId = p; UsagePage = up; Usage = u; }
    }

    internal static class HidInterop
    {
        public static IReadOnlyList<string> Paths()
        {
            HidD_GetHidGuid(out var guid);
            if (CM_Get_Device_Interface_List_SizeW(out var length, ref guid, null, 0) != 0 || length <= 0) return new string[0];
            var buffer = new char[length];
            if (CM_Get_Device_Interface_ListW(ref guid, null, buffer, length, 0) != 0) return new string[0];
            return new string(buffer).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static DeviceInfo? Describe(SafeFileHandle handle)
        {
            var attributes = new HidAttributes { Size = Marshal.SizeOf(typeof(HidAttributes)) };
            if (!HidD_GetAttributes(handle, ref attributes)) return null;
            if (!HidD_GetPreparsedData(handle, out var preparsed)) return null;
            try
            {
                var caps = new HidCaps();
                if (HidP_GetCaps(preparsed, ref caps) < 0) return null;
                return new DeviceInfo(attributes.VendorId, attributes.ProductId, caps.UsagePage, caps.Usage);
            }
            finally { HidD_FreePreparsedData(preparsed); }
        }

        public static bool SetFeature(SafeFileHandle h, byte[] b) => HidD_SetFeature(h, b, b.Length);
        public static bool GetFeature(SafeFileHandle h, byte[] b) => HidD_GetFeature(h, b, b.Length);

        [StructLayout(LayoutKind.Sequential)] private struct HidAttributes { public int Size; public ushort VendorId, ProductId, VersionNumber; }
        [StructLayout(LayoutKind.Sequential)]
        private struct HidCaps
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
            public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }
        [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern int CM_Get_Device_Interface_List_SizeW(out int length, ref Guid guid, string? deviceId, int flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern int CM_Get_Device_Interface_ListW(ref Guid guid, string? deviceId, char[] buffer, int length, int flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("hid.dll")] private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref HidAttributes attributes);
        [DllImport("hid.dll")] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
        [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, ref HidCaps caps);
        [DllImport("hid.dll")] private static extern bool HidD_SetFeature(SafeFileHandle handle, byte[] buffer, int length);
        [DllImport("hid.dll")] private static extern bool HidD_GetFeature(SafeFileHandle handle, byte[] buffer, int length);
    }
}

// ============================================================ ENEK colour controller (vid 0x0CF2 / pid 0x5130)
// Protocol ported from Nitrous' AcerKeyboard (which took it from OpenRGB's
// AcerNitroHidKeyboardController) and trimmed to what KB Breathe needs: one colour across all zones,
// stepped by the same 0-4 brightness levels the patterns and Sound Sync already produce.
readonly struct RgbColor
{
    public readonly byte R, G, B;

    public RgbColor(byte r, byte g, byte b) { R = r; G = g; B = b; }

    public static RgbColor White => new RgbColor(255, 255, 255);

    public string ToHex() => "#" + R.ToString("X2") + G.ToString("X2") + B.ToString("X2");

    public static RgbColor FromHex(string? hex)
    {
        var s = (hex ?? "").TrimStart('#');
        uint rgb;
        return s.Length == 6 && uint.TryParse(s, System.Globalization.NumberStyles.AllowHexSpecifier,
            System.Globalization.CultureInfo.InvariantCulture, out rgb)
            ? new RgbColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
            : White;
    }
}

// The colour page's effect list: the controller's own effects first, then two software flows.
// Hardware effects keep animating after a single packet; the flows are drawn here because the
// firmware has no full-spectrum mode of its own on every model.
static class ColorFx
{
    public const int Static = 0;

    public static readonly string[] Names =
    {
        "Static",
        "Breathing",
        "Neon",
        "Wave",
        "Ripple",
        "Zoom",
        "Snake",
        "Disco",
        "Rainbow flow",
        "Rainbow wave"
    };

    public static int Count => Names.Length;

    /// <summary>True for the modes this app animates zone by zone rather than the firmware.</summary>
    public static bool IsFlow(int mode) => mode >= 8;

    public static byte EffectByte(int mode) => mode switch
    {
        1 => RgbKeyboard.EffectBreathing,
        2 => RgbKeyboard.EffectNeon,
        3 => RgbKeyboard.EffectWave,
        4 => RgbKeyboard.EffectRipple,
        5 => RgbKeyboard.EffectZoom,
        6 => RgbKeyboard.EffectSnake,
        7 => RgbKeyboard.EffectDisco,
        _ => RgbKeyboard.EffectStatic
    };

    /// <summary>Four zone colours for a software flow; <paramref name="seconds"/> keeps it moving.</summary>
    public static void Flow(int mode, double seconds, RgbColor[] zones)
    {
        for (int i = 0; i < zones.Length; i++)
        {
            if (mode == 8)
            {
                // Rainbow flow: the spectrum scrolls left to right across the zones.
                zones[i] = Hsv(seconds * 55 + i * 42, 1, 1);
            }
            else
            {
                // Rainbow wave: the spectrum stands still and a brightness wave runs through it,
                // which reads as a vertical wipe on the four horizontal zones.
                double wave = 0.5 + 0.5 * Math.Sin(seconds * 3.4 - i * 1.4);
                zones[i] = Hsv(i * 68, 1, 0.6 + 0.4 * wave);
            }
        }
    }

    public static RgbColor Hsv(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        double c = value * saturation;
        double x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = value - c;
        double r = 0, g = 0, b = 0;

        if (hue < 60) { r = c; g = x; }
        else if (hue < 120) { r = x; g = c; }
        else if (hue < 180) { g = c; b = x; }
        else if (hue < 240) { g = x; b = c; }
        else if (hue < 300) { r = x; b = c; }
        else { r = c; b = x; }

        return new RgbColor(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}

// Sound Sync colour styles for the RGB controller. Five types, each one to four colours across
// the four zones. Single (0) is the normal one-colour path and does not go through here.
static class SoundPalette
{
    public static readonly string[] Names = { "Single", "Duo", "Warm", "Cool", "Rainbow" };
    public static readonly string[] Caps = { "SINGLE", "DUO", "WARM", "COOL", "RAINBOW" };
    public static int Count => Names.Length;

    public static void Zones(int style, RgbColor baseColor, RgbColor[] zones)
    {
        switch (style)
        {
            case 1:   // Duo: the selected colour and white, alternating
                zones[0] = zones[2] = baseColor;
                zones[1] = zones[3] = RgbColor.White;
                break;
            case 2:   // Warm
                zones[0] = new RgbColor(255, 90, 54);
                zones[1] = new RgbColor(255, 179, 71);
                zones[2] = new RgbColor(255, 46, 136);
                zones[3] = new RgbColor(255, 209, 102);
                break;
            case 3:   // Cool
                zones[0] = new RgbColor(0, 217, 255);
                zones[1] = new RgbColor(58, 134, 255);
                zones[2] = new RgbColor(123, 97, 255);
                zones[3] = new RgbColor(0, 245, 212);
                break;
            case 4:   // Rainbow: one spectrum quarter per zone
                for (int i = 0; i < zones.Length; i++) zones[i] = ColorFx.Hsv(i * 90, 1, 1);
                break;
            default:
                for (int i = 0; i < zones.Length; i++) zones[i] = baseColor;
                break;
        }
    }
}

static class RgbKeyboard
{
    private const ushort VendorId = 0x0CF2, ProductId = 0x5130, UsagePage = 0xFF5A, Usage = 0x0001;
    private const byte ReportId = 0xA4, KeyboardDevice = 0x21, AllZones = 0x0F;
    public const byte EffectStatic = 2;    // Static: one colour, brightness per packet
    // The firmware's own effects, in the controller's encoding (Nitrous / OpenRGB).
    public const byte EffectBreathing = 4, EffectNeon = 5, EffectWave = 7, EffectRipple = 8,
                        EffectZoom = 9, EffectSnake = 10, EffectDisco = 11;
    private const int ZoneCount = 4;
    private const int ReportLength = 11;

    private static readonly object Gate = new object();
    private static bool? _available;
    private static Device? _dev;
    private static volatile int _color = 0xFFFFFF;

    /// <summary>The colour the patterns light up in. Written while running, so edits apply live.</summary>
    public static RgbColor Color
    {
        get { int v = _color; return new RgbColor((byte)(v >> 16), (byte)(v >> 8), (byte)v); }
        set { _color = (value.R << 16) | (value.G << 8) | value.B; }
    }

    /// <summary>True when this machine exposes the keyboard's colour controller.</summary>
    public static bool IsAvailable()
    {
        lock (Gate)
        {
            if (!_available.HasValue)
            {
                using var probe = Open();
                _available = probe != null;
            }
            return _available.Value;
        }
    }

    public static bool SetBrightness(int percent)
    {
        lock (Gate)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                if (_dev == null) _dev = Open();
                if (_dev == null) return false;
                if (Write(_dev, M.Clamp(percent, 0, 100))) return true;
                _dev.Dispose();
                _dev = null;
            }
            return false;
        }
    }

    public static void Release()
    {
        lock (Gate)
        {
            if (_dev != null) { _dev.Dispose(); _dev = null; }
        }
    }

    /// <summary>One colour per zone, for screen sync.</summary>
    public static bool SetZones(IReadOnlyList<RgbColor> colors, int brightness)
    {
        lock (Gate)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                if (_dev == null) _dev = Open();
                if (_dev == null) return false;
                if (WriteZones(_dev, colors, brightness)) return true;
                _dev.Dispose();
                _dev = null;
            }
            return false;
        }
    }

    /// <summary>One of the firmware's effects, across all four zones; the controller animates it from here on.</summary>
    public static bool SetEffect(byte effect, RgbColor color, int speed)
    {
        lock (Gate)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                if (_dev == null) _dev = Open();
                if (_dev == null) return false;
                if (WriteEffect(_dev, effect, color, speed)) return true;
                _dev.Dispose();
                _dev = null;
            }
            return false;
        }
    }

    private static bool WriteEffect(Device device, byte effect, RgbColor color, int speed)
    {
        var packet = new byte[ReportLength];
        packet[0] = ReportId;
        packet[1] = KeyboardDevice;
        packet[2] = effect;
        packet[3] = 100;
        packet[4] = (byte)M.Clamp(speed, 1, 9);
        packet[5] = 1;              // direction: right
        packet[6] = color.R;
        packet[7] = color.G;
        packet[8] = color.B;
        packet[9] = AllZones;
        packet[10] = 0;
        return device.Set(packet);
    }

    private static bool WriteZones(Device device, IReadOnlyList<RgbColor> colors, int brightness)
    {
        var sent = false;
        for (int zone = 0; zone < ZoneCount; zone++)
        {
            var color = zone < colors.Count ? colors[zone] : RgbColor.White;
            var packet = new byte[ReportLength];
            packet[0] = ReportId;
            packet[1] = KeyboardDevice;
            packet[2] = EffectStatic;
            packet[3] = (byte)M.Clamp(brightness, 0, 100);
            packet[4] = 0;
            packet[5] = 1;
            packet[6] = color.R;
            packet[7] = color.G;
            packet[8] = color.B;
            packet[9] = (byte)(1 << zone);
            packet[10] = 0;
            sent |= device.Set(packet);
        }
        return sent;
    }

    private static bool Write(Device device, int percent)
    {
        var color = Color;
        var packet = new byte[ReportLength];
        packet[0] = ReportId;
        packet[1] = KeyboardDevice;
        packet[2] = EffectStatic;
        packet[3] = (byte)percent;
        packet[4] = 0;      // speed: unused by Static
        packet[5] = 1;      // direction: right
        packet[6] = color.R;
        packet[7] = color.G;
        packet[8] = color.B;
        packet[9] = AllZones;
        packet[10] = 0;
        return device.Set(packet);
    }

    private static Device? Open()
    {
        try
        {
            foreach (var path in EcHid.HidInterop.Paths())
            {
                var handle = EcHid.HidInterop.CreateFileW(path, 0xC0000000u, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (handle.IsInvalid) { handle.Dispose(); continue; }
                var info = EcHid.HidInterop.Describe(handle);
                if (!info.HasValue ||
                    info.Value.VendorId != VendorId || info.Value.ProductId != ProductId ||
                    info.Value.UsagePage != UsagePage || info.Value.Usage != Usage)
                { handle.Dispose(); continue; }
                return new Device(handle);
            }
        }
        catch { }
        return null;
    }

    private sealed class Device : IDisposable
    {
        private readonly SafeFileHandle _handle;
        public Device(SafeFileHandle handle) { _handle = handle; }
        public bool Set(byte[] packet) => EcHid.HidInterop.SetFeature(_handle, packet);
        public void Dispose() => _handle.Dispose();
    }
}

// ============================================================ screen colours for the four zones
// Ported from Nitrous' ScreenCapture: StretchBlt scales the desktop straight into a 64x36 DIB, so a
// 1080p screen is not copied and then shrunk every frame.
sealed class ScreenZones : IDisposable
{
    public const int ZoneCount = 4;
    private const int Columns = 64, Rows = 36;
    private const uint Srccopy = 0x00CC0020, CaptureBlt = 0x40000000;

    private IntPtr _memoryDc, _bitmap, _previous, _bits;
    private readonly byte[] _pixels = new byte[Columns * Rows * 4];

    public ScreenZones()
    {
        var screen = GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) return;
        try
        {
            _memoryDc = CreateCompatibleDC(screen);
            if (_memoryDc == IntPtr.Zero) return;

            var info = new BitmapInfo();
            info.Header.Size = (uint)Marshal.SizeOf(typeof(BitmapInfoHeader));
            info.Header.Width = Columns;
            info.Header.Height = -Rows;   // top-down, so row 0 is the top of the screen
            info.Header.Planes = 1;
            info.Header.BitCount = 32;

            _bitmap = CreateDIBSection(screen, ref info, 0, out _bits, IntPtr.Zero, 0);
            if (_bitmap != IntPtr.Zero) _previous = SelectObject(_memoryDc, _bitmap);
        }
        finally { ReleaseDC(IntPtr.Zero, screen); }
    }

    public bool TryCapture(out byte[] pixels)
    {
        pixels = _pixels;
        if (_memoryDc == IntPtr.Zero || _bitmap == IntPtr.Zero) return false;

        var screen = GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) return false;
        try
        {
            var bounds = Screen.PrimaryScreen.Bounds;

            // CAPTUREBLT keeps overlay windows in the capture; a driver that refuses it gets the
            // plain copy instead of a sync that silently never moves.
            if (!StretchBlt(_memoryDc, 0, 0, Columns, Rows, screen,
                    bounds.X, bounds.Y, bounds.Width, bounds.Height, Srccopy | CaptureBlt) &&
                !StretchBlt(_memoryDc, 0, 0, Columns, Rows, screen,
                    bounds.X, bounds.Y, bounds.Width, bounds.Height, Srccopy))
                return false;
        }
        finally { ReleaseDC(IntPtr.Zero, screen); }

        Marshal.Copy(_bits, _pixels, 0, _pixels.Length);
        return true;
    }

    /// <summary>Four equal screen columns, left to right, as zone colours.</summary>
    public static void ReadZones(byte[] bgra, RgbColor[] zones)
    {
        int zoneWidth = Columns / ZoneCount;
        for (int zone = 0; zone < ZoneCount && zone < zones.Length; zone++)
        {
            long red = 0, green = 0, blue = 0;

            for (int y = 0; y < Rows; y++)
            {
                int row = y * Columns * 4;
                for (int x = zone * zoneWidth; x < (zone + 1) * zoneWidth; x++)
                {
                    int i = row + x * 4;
                    blue += bgra[i];
                    green += bgra[i + 1];
                    red += bgra[i + 2];
                }
            }

            int count = Rows * zoneWidth;
            zones[zone] = new RgbColor((byte)(red / count), (byte)(green / count), (byte)(blue / count));
        }
    }

    public void Dispose()
    {
        if (_memoryDc != IntPtr.Zero && _previous != IntPtr.Zero) SelectObject(_memoryDc, _previous);
        if (_bitmap != IntPtr.Zero) DeleteObject(_bitmap);
        if (_memoryDc != IntPtr.Zero) DeleteDC(_memoryDc);
        _memoryDc = _bitmap = _previous = _bits = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern bool StretchBlt(
        IntPtr dest, int x, int y, int width, int height,
        IntPtr source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, uint rop);
}

// ============================================================ WMI-only models (ANV15-41 and similar)
// Nitrous' second brightness path: some models expose no Acer EC HID interface and no
// Acer_Gaming backlight method, and keep their backlight in APGeAction. GetFunction/SetFunction are
// addressed with the keyboard hotkey Acer's own agent stores in the registry.
static class AcerWmiKeyboard
{
    private const string Scope = @"root\WMI", ActionClass = "APGeAction";
    private const ulong Flags = 0x00080002;
    private const uint Address = 0x80001;
    private static readonly int[] Levels = { 0, 25, 50, 75, 100 };
    private static bool? _available;

    public static bool IsAvailable()
    {
        if (_available.HasValue) return _available.Value;

        var read = Read();

        // A real answer either echoes the hotkey or carries one of the firmware's brightness steps.
        // An unimplemented function answers a constant (0xE1 on some BIOSes), which satisfies neither.
        _available = read.HasValue && (read.Value.Echo || read.Value.Brightness != 0);
        return _available.Value;
    }

    public static bool SetBrightness(int percent)
    {
        var level = Nearest(percent);
        var timeout = Read()?.TimeoutSeconds ?? 0;
        timeout = M.Clamp(timeout, 0, 0xFF);

        var value = ((ulong)level << 32) | ((ulong)Hotkey() << 8) | ((ulong)timeout << 40) | Flags;
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, "SELECT * FROM " + ActionClass);
            foreach (ManagementObject instance in searcher.Get())
            {
                using (instance)
                {
                    using var inParams = instance.GetMethodParameters("SetFunction");
                    inParams["uiInput"] = value;
                    using var outParams = instance.InvokeMethod("SetFunction", inParams, null);
                    return outParams != null;
                }
            }
        }
        catch { }
        return false;
    }

    private static int Nearest(int percent)
    {
        int best = 0;
        foreach (var level in Levels)
            if (Math.Abs(level - percent) < Math.Abs(best - percent)) best = level;
        return best;
    }

    private static Probed? Read()
    {
        var hotkey = Hotkey();
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, "SELECT * FROM " + ActionClass);
            foreach (ManagementObject instance in searcher.Get())
            {
                using (instance)
                {
                    using var inParams = instance.GetMethodParameters("GetFunction");
                    inParams["uiInput"] = (uint)((hotkey << 8) | Address);
                    using var outParams = instance.InvokeMethod("GetFunction", inParams, null);

                    var raw = outParams?.Properties["uiOutput"]?.Value;
                    if (raw == null) return null;

                    var value = Convert.ToUInt64(raw);
                    return new Probed(
                        (int)((value >> 32) & 0xFF), (int)((value >> 40) & 0xFF),
                        ((value >> 8) & 0xFF) == (ulong)hotkey);
                }
            }
        }
        catch { }
        return null;
    }

    private static int Hotkey()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\OEM\AcerAgentService");
            if (key?.GetValue("BK_Hotkey_Number") is int value && value > 0 && value <= 0xFF) return value;
        }
        catch { }
        return 132;   // what both machines Nitrous measured report
    }

    private readonly struct Probed
    {
        public readonly int Brightness, TimeoutSeconds;
        public readonly bool Echo;
        public Probed(int brightness, int timeoutSeconds, bool echo)
        {
            Brightness = brightness; TimeoutSeconds = timeoutSeconds; Echo = echo;
        }
    }
}

// ============================================================ WMI fallback (unchanged logic)
static class WmiBacklight
{
    private const string Scope = @"root\WMI";

    public static bool IsAvailable()
    {
        try
        {
            using var s = new ManagementObjectSearcher(Scope, "SELECT * FROM Acer_Gaming");
            foreach (ManagementObject _ in s.Get()) return true;
        }
        catch { }
        try
        {
            using var s = new ManagementObjectSearcher(Scope, "SELECT * FROM meta_class");
            foreach (ManagementClass mc in s.Get())
            {
                var name = mc["__CLASS"]?.ToString() ?? "";
                if (!name.Has("Gaming") && !name.Has("Acer")) continue;
                foreach (MethodData m in mc.Methods)
                    if (m.Name == "SetGamingKBBacklight" || m.Name == "GetGamingKBBacklight") return true;
            }
        }
        catch { }
        return false;
    }

    public static bool SetBrightness(int brightness)
    {
        brightness = M.Clamp(brightness, 0, 100);
        try
        {
            if (TrySetOnClass("Acer_Gaming", brightness)) return true;
            if (TrySetOnClass("AcerGamingMethods", brightness)) return true;
            using var s = new ManagementObjectSearcher(Scope, "SELECT * FROM meta_class");
            foreach (ManagementClass mc in s.Get())
            {
                var name = mc["__CLASS"]?.ToString() ?? "";
                if (name.Has("Gaming") && TrySetOnClass(name, brightness)) return true;
            }
        }
        catch { }
        return false;
    }

    private static bool TrySetOnClass(string className, int brightness)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, "SELECT * FROM " + className);
            foreach (ManagementObject obj in searcher.Get())
            {
                byte[]? record = null;
                try
                {
                    var getParams = obj.GetMethodParameters("GetGamingKBBacklight");
                    if (getParams != null)
                        foreach (PropertyData p in getParams.Properties)
                            if (p.Name.Has("Input"))
                                getParams[p.Name] = new byte[16];
                    using var outParams = obj.InvokeMethod("GetGamingKBBacklight", getParams, null);
                    if (outParams != null)
                        foreach (PropertyData p in outParams.Properties)
                            if (p.Value is byte[] bytes && bytes.Length >= 9) { record = bytes; break; }
                }
                catch { }

                var payload = new byte[16];
                if (record != null && record.Length >= 9) Array.Copy(record, payload, Math.Min(record.Length, 16));
                else { payload[0] = 2; payload[4] = 1; payload[5] = 255; payload[6] = 140; payload[7] = 0; }
                payload[2] = (byte)brightness;
                if (payload[3] == 0xFF) payload[3] = 0;
                payload[9] = 1;

                var inParams = obj.GetMethodParameters("SetGamingKBBacklight");
                if (inParams == null) continue;
                string? inputName = null;
                foreach (PropertyData p in inParams.Properties)
                    if (p.Name.Has("Input") || p.Name.Has("Data")) { inputName = p.Name; break; }
                if (inputName == null) foreach (PropertyData p in inParams.Properties) { inputName = p.Name; break; }
                if (inputName == null) continue;
                inParams[inputName] = payload;
                using var result = obj.InvokeMethod("SetGamingKBBacklight", inParams, null);
                Thread.Sleep(25);
                return true;
            }
        }
        catch { }
        return false;
    }
}
