using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// ── 테마 정의 ────────────────────────────────────────────────
// 배경/텍스트는 모든 테마 공통 뉴트럴, 테마색은 포인트(accent) 전용
struct ColorTheme {
    public string Key, Label;
    public Color Sample, cBg, cAc, cTx, cSub, cDv, cDi;
}

static class Themes {
    // 다크 뉴트럴 팔레트
    static readonly Color DBg  = Color.FromArgb(26, 27, 31);
    static readonly Color DTx  = Color.FromArgb(230, 232, 237);
    static readonly Color DSub = Color.FromArgb(140, 144, 154);
    static readonly Color DDv  = Color.FromArgb(50, 52, 60);
    static readonly Color DDi  = Color.FromArgb(38, 40, 46);

    // 라이트 뉴트럴 팔레트
    static readonly Color LBg  = Color.FromArgb(246, 247, 249);
    static readonly Color LTx  = Color.FromArgb(32, 34, 40);
    static readonly Color LSub = Color.FromArgb(112, 116, 126);
    static readonly Color LDv  = Color.FromArgb(222, 224, 230);
    static readonly Color LDi  = Color.FromArgb(232, 234, 238);

    // 라이트 모드 accent 최소 명도대비 (WCAG 큰 글씨 기준 3:1 + 여유)
    const float LIGHT_MIN_CONTRAST = 3.4f;

    static ColorTheme Mk(string key, string label, Color accent) {
        return new ColorTheme {
            Key=key, Label=label, Sample=accent,
            cBg=DBg, cAc=accent, cTx=DTx, cSub=DSub, cDv=DDv, cDi=DDi
        };
    }

    public static readonly ColorTheme[] All = new ColorTheme[] {
        Mk("red",    "빨강", Color.FromArgb(255, 107, 107)),
        Mk("orange", "주황", Color.FromArgb(255, 159,  67)),
        Mk("yellow", "노랑", Color.FromArgb(250, 215,  70)),
        Mk("green",  "초록", Color.FromArgb( 72, 219, 140)),
        Mk("blue",   "파랑", Color.FromArgb( 86, 182, 255)),
        Mk("indigo", "남색", Color.FromArgb(133, 133, 255)),
        Mk("violet", "보라", Color.FromArgb(196, 120, 255)),
    };

    public static bool IsValidKey(string key) {
        if (string.IsNullOrEmpty(key)) return false;
        foreach (var c in All) if (c.Key == key) return true;
        return false;
    }

    public static string LabelOf(string key) {
        foreach (var c in All) if (c.Key == key) return c.Label;
        return "기본";
    }

    public static ColorTheme Get(string key) { return Get(key, false); }

    public static ColorTheme Get(string key, bool light) {
        ColorTheme t = All[4];
        foreach (var c in All) if (c.Key == key) { t = c; break; }
        if (light) {
            // 라이트 모드: 뉴트럴 교체 + accent는 명도대비 기준으로 필요한 만큼만 어둡게
            // (고정 20%로는 노랑·초록이 흰 배경에서 대비 부족)
            t.cBg = LBg;  t.cTx = LTx;  t.cSub = LSub;
            t.cDv = LDv;  t.cDi = LDi;
            t.cAc = Ui.EnsureContrast(t.cAc, LBg, LIGHT_MIN_CONTRAST);
        }
        return t;
    }
}

// ── 공용 그리기 유틸 ─────────────────────────────────────────
static class Ui {
    public static GraphicsPath Rounded(Rectangle r, int rad) {
        if (rad < 1) rad = 1;
        var gp = new GraphicsPath();
        gp.AddArc(r.X, r.Y, rad*2, rad*2, 180, 90);
        gp.AddArc(r.Right-rad*2, r.Y, rad*2, rad*2, 270, 90);
        gp.AddArc(r.Right-rad*2, r.Bottom-rad*2, rad*2, rad*2, 0, 90);
        gp.AddArc(r.X, r.Bottom-rad*2, rad*2, rad*2, 90, 90);
        gp.CloseFigure();
        return gp;
    }

    public static Color Blend(Color a, Color b, float t) {
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    static double Lin(int c) {
        double v = c / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    // WCAG 상대 휘도
    public static double Luminance(Color c) {
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    public static double Contrast(Color a, Color b) {
        double la = Luminance(a), lb = Luminance(b);
        if (la < lb) { double t = la; la = lb; lb = t; }
        return (la + 0.05) / (lb + 0.05);
    }

    // 배경 대비가 target 이상이 될 때까지 배경 반대쪽(검정/흰색)으로 조금씩 섞는다
    public static Color EnsureContrast(Color c, Color bg, float target) {
        Color toward = Luminance(bg) > 0.5 ? Color.Black : Color.White;
        Color r = c;
        for (float t = 0.02f; t <= 1.0f && Contrast(r, bg) < target; t += 0.02f)
            r = Blend(c, toward, t);
        return r;
    }

    // 주어진 배경 위에 올릴 글자색 (어두운 글자 / 흰 글자)
    public static Color ReadableOn(Color bg) {
        Color dark = Color.FromArgb(22, 23, 27);
        return Contrast(dark, bg) >= Contrast(Color.White, bg) ? dark : Color.White;
    }
}

// ── 이벤트 데이터 ────────────────────────────────────────────
class DEvent {
    public string   Name;
    public DateTime Date;
    public string   ColorKey = "";   // "" = 테마 기본색
}

static class EventStore {
    const string Header =
        "# D-Day 이벤트 설정\r\n" +
        "# 형식: 이름=YYYY-MM-DD  또는  이름=YYYY-MM-DD|색상\r\n" +
        "# 색상: red, orange, yellow, green, blue, indigo, violet (생략 시 테마 색)\r\n" +
        "# 위젯의 ✎ 버튼으로도 편집할 수 있습니다\r\n\r\n";

    public static List<DEvent> Load(string path) {
        var r = new List<DEvent>();
        if (!File.Exists(path)) return r;
        try {
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8)) {
                string l = line.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                // 날짜/색상에는 '='가 없으므로 마지막 '=' 기준 분리 → 이름에 '=' 허용
                int i = l.LastIndexOf('=');
                if (i <= 0) continue;
                string nm  = l.Substring(0, i).Trim();
                string val = l.Substring(i + 1).Trim();
                string key = "";
                int p = val.IndexOf('|');
                if (p >= 0) {
                    key = val.Substring(p + 1).Trim().ToLowerInvariant();
                    val = val.Substring(0, p).Trim();
                    if (!Themes.IsValidKey(key)) key = "";
                }
                DateTime dt;
                if (nm.Length > 0 &&
                    DateTime.TryParseExact(val, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                           DateTimeStyles.None, out dt))
                    r.Add(new DEvent { Name=nm, Date=dt.Date, ColorKey=key });
            }
        } catch {}
        return r;
    }

    static string Sanitize(string name) {
        string s = (name ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        if (s.StartsWith("#")) s = "＃" + s.Substring(1);   // 주석으로 오인 방지
        return s;
    }

    // 임시 파일에 먼저 쓴 뒤 교체 → 쓰기 도중 실패해도 원본 보존
    public static void Save(string path, List<DEvent> list) {
        var sb = new StringBuilder(Header);
        foreach (var ev in list) {
            string nm = Sanitize(ev.Name);
            if (nm.Length == 0) continue;
            sb.Append(nm).Append('=')
              .Append(ev.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (Themes.IsValidKey(ev.ColorKey)) sb.Append('|').Append(ev.ColorKey);
            sb.Append("\r\n");
        }
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(true));
        File.Copy(tmp, path, true);
        File.Delete(tmp);
    }
}

// ── 자동 시작 관리 ───────────────────────────────────────────
static class AutoStart {
    const string RegPath    = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RegName    = "D-Day_App";
    const string LegacyName = "DDayWidget";   // v6.3 이전 이름

    static string Quote(string path) { return "\"" + path + "\""; }

    public static bool IsEnabled() {
        try {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegPath, false)) {
                if (key == null) return false;
                return key.GetValue(RegName) != null;
            }
        } catch { return false; }
    }

    public static bool Enable(string exePath) {
        try {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegPath, true)) {
                if (key == null) return false;
                key.SetValue(RegName, Quote(exePath));
                return key.GetValue(RegName) != null;
            }
        } catch { return false; }
    }

    public static void Disable() {
        try {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegPath, true)) {
                if (key == null) return;
                key.DeleteValue(RegName, false);
                key.DeleteValue(LegacyName, false);
            }
        } catch {}
    }

    // 시작 시 1회: 예전 이름 등록을 새 이름으로 옮기고,
    // 폴더를 옮겨 등록 경로의 exe가 없어졌으면 현재 위치로 고친다
    public static void Migrate(string exePath) {
        try {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegPath, true)) {
                if (key == null) return;
                bool hadLegacy = key.GetValue(LegacyName) != null;
                if (hadLegacy) key.DeleteValue(LegacyName, false);

                string cur  = key.GetValue(RegName) as string;
                bool broken = cur != null && !File.Exists(cur.Trim('"'));
                if (hadLegacy || broken) key.SetValue(RegName, Quote(exePath));
            }
        } catch {}
    }
}

// ── 토글 스위치 ──────────────────────────────────────────────
class ToggleSwitch : Control {
    bool _on;
    Color _accentOn  = Color.FromArgb(86, 182, 255);
    Color _accentOff = Color.FromArgb(58, 61, 70);

    public bool On {
        get { return _on; }
        set { _on = value; Invalidate(); }
    }
    public Color AccentOn  { set { _accentOn  = value; Invalidate(); } }
    public Color AccentOff { set { _accentOff = value; Invalidate(); } }
    public event EventHandler Toggled;

    public ToggleSwitch() {
        Size   = new Size(48, 24);
        Cursor = Cursors.Hand;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnClick(EventArgs e) {
        _on = !_on;
        Invalidate();
        if (Toggled != null) Toggled(this, EventArgs.Empty);
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        using (var br = new SolidBrush(BackColor)) g.FillRectangle(br, ClientRectangle);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color bg = _on ? _accentOn : _accentOff;
        using (var gp = new GraphicsPath()) {
            int h = Height, w = Width;
            gp.AddArc(0, 0, h, h, 90, 180);
            gp.AddArc(w - h, 0, h, h, 270, 180);
            gp.CloseFigure();
            using (var br = new SolidBrush(bg)) g.FillPath(br, gp);
        }
        int pad = 3, kd = Height - pad * 2;
        int kx  = _on ? Width - Height + pad : pad;
        using (var br = new SolidBrush(Color.White))
            g.FillEllipse(br, kx, pad, kd, kd);
    }
}

// ── 아이콘 버튼 (hover 시 둥근 배경 하이라이트) ──────────────
class IconButton : Control {
    bool hover;
    public Color GlyphColor = Color.Gray;
    public Color GlyphHover = Color.White;
    public Color HoverBack  = Color.FromArgb(50, 52, 60);
    public int   Radius     = 8;

    public IconButton() {
        Cursor = Cursors.Hand;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        using (var br = new SolidBrush(BackColor)) g.FillRectangle(br, ClientRectangle);
        if (hover) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var gp = Ui.Rounded(new Rectangle(1, 1, Width-3, Height-3), Radius))
            using (var br = new SolidBrush(HoverBack)) g.FillPath(br, gp);
        }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle,
            hover ? GlyphHover : GlyphColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// ── D-Day 당일 강조 pill ─────────────────────────────────────
class DDayPill : Control {
    public Color Accent = Color.White;

    public DDayPill() {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        using (var br = new SolidBrush(BackColor)) g.FillRectangle(br, ClientRectangle);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Size ts = TextRenderer.MeasureText(Text, Font);
        int padV = Math.Max(4, ts.Height / 4);
        int padH = Math.Max(10, ts.Height * 2 / 3);
        int ph = Math.Min(Height, ts.Height + padV);
        int pw = Math.Min(Width,  ts.Width  + padH);
        var rect = new Rectangle(Width - pw, (Height - ph) / 2, pw - 1, ph - 1);
        using (var gp = Ui.Rounded(rect, ph / 2))
        using (var br = new SolidBrush(Accent)) g.FillPath(br, gp);

        // 라이트 모드에선 accent가 진해지므로 글자색을 대비 기준으로 선택
        TextRenderer.DrawText(g, Text, Font, rect, Ui.ReadableOn(Accent),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// ── 색상 선택 점 (클릭: 다음 색 / 우클릭: 이전 색) ───────────
class ColorDot : Control {
    static readonly string[] Cycle = { "", "red", "orange", "yellow", "green", "blue", "indigo", "violet" };

    string key = "";
    bool   hover;
    public bool  Light;
    public Color RingColor  = Color.Gray;
    public Color HoverBack  = Color.FromArgb(50, 52, 60);

    public string Key {
        get { return key; }
        set { key = Themes.IsValidKey(value) ? value : ""; Invalidate(); }
    }

    public ColorDot() {
        Cursor = Cursors.Hand;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseUp(MouseEventArgs e) {
        int i = Array.IndexOf(Cycle, key);
        if (i < 0) i = 0;
        if (e.Button == MouseButtons.Left)  i = (i + 1) % Cycle.Length;
        if (e.Button == MouseButtons.Right) i = (i + Cycle.Length - 1) % Cycle.Length;
        key = Cycle[i];
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        using (var br = new SolidBrush(BackColor)) g.FillRectangle(br, ClientRectangle);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (hover) {
            using (var gp = Ui.Rounded(new Rectangle(0, 0, Width-1, Height-1), 6))
            using (var br = new SolidBrush(HoverBack)) g.FillPath(br, gp);
        }
        int d = Math.Min(Width, Height) - 10;
        var r = new Rectangle((Width - d) / 2, (Height - d) / 2, d, d);
        if (key.Length == 0) {
            // 기본 = 테마 색을 따름 → 빈 원
            using (var pen = new Pen(RingColor, 1.6f)) g.DrawEllipse(pen, r);
        } else {
            using (var br = new SolidBrush(Themes.Get(key, Light).cAc)) g.FillEllipse(br, r);
        }
    }
}

// ── 메인 진입점 ──────────────────────────────────────────────
static class Program {
    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DDayForm());
    }
}

// ── 앱 정보 ──────────────────────────────────────────────────
static class AppInfo {
    public const string NAME         = "D-Day App";
    public const string VERSION      = "v6.4";
    public const string RELEASE_DATE = "2026.10.27";
    public const string PURPOSE      = "PC 바탕화면에 중요한 디데이를 표시하는 앱";
    public const string COPYRIGHT    = "© 2026 김민수";
    public const string BUILT_WITH   = "Built with Claude";
    public const string FOOTER_TEXT  = NAME + " · " + BUILT_WITH;
    public const string REPO_URL     = "https://github.com/binarycraft01/d-day-app";
    public const string REPO_LABEL   = "github.com/binarycraft01/d-day-app";
}

// ── 공용 Win32 ───────────────────────────────────────────────
static class Native {
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern int  SendMessage(IntPtr h, int msg, int w, int l);

    // 창 이동 모달 루프 시작. SendMessage는 드래그가 끝나야 반환된다.
    public static void BeginMove(Form f) {
        ReleaseCapture();
        SendMessage(f.Handle, 0xA1, 2, 0);
    }

    // 폼을 기준 폼이 있는 모니터의 작업 영역 중앙에 배치
    public static void CenterOnScreenOf(Form f, Control anchor) {
        var wa = Screen.FromControl(anchor).WorkingArea;
        f.StartPosition = FormStartPosition.Manual;
        f.Location = new Point(wa.X + (wa.Width  - f.Width)  / 2,
                               wa.Y + (wa.Height - f.Height) / 2);
    }
}

// ── 메인 위젯 창 ─────────────────────────────────────────────
class DDayForm : Form {
    const float SCALE_MIN = 0.70f, SCALE_MAX = 2.00f;

    string baseDir, eventsFile, posFile, settingsFile;
    ColorTheme theme;
    int   opacityPct;
    bool  topMostEnabled;
    bool  sortByDate;
    bool  lightMode;
    float scale = 1.0f;

    // 폰트 멤버
    Font fT, fN, fD, fS, fDate, fBtn, fClose;

    // 시계/자동 갱신
    Timer    clockTimer;
    Label    clockLbl;
    DateTime builtDate;

    // 배율 조절 드래그 상태
    bool  resizing;
    Point resizeStart;
    Size  resizeStartSize;
    float resizeStartScale;

    // 모니터 구성 변경 감지
    EventHandler displayHandler;

    public DDayForm() {
        baseDir      = AppDomain.CurrentDomain.BaseDirectory;
        eventsFile   = Path.Combine(baseDir, "events.txt");
        posFile      = Path.Combine(baseDir, "position.txt");
        settingsFile = Path.Combine(baseDir, "settings.txt");

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = true;
        StartPosition   = FormStartPosition.Manual;
        Text            = AppInfo.NAME;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        string iconPath = Path.Combine(baseDir, "icon.ico");
        if (File.Exists(iconPath)) Icon = new Icon(iconPath);

        AutoStart.Migrate(Application.ExecutablePath);

        LoadSettings();
        Build();                 // 크기를 먼저 확정해야 위치 검증이 정확함
        Location = LoadPos();

        // 폼 본체 마우스 처리는 생성자에서 한 번만 등록 (Build마다 누적되던 문제 수정)
        MouseDown  += OnFormMouseDown;
        MouseMove  += OnFormMouseMove;
        MouseUp    += OnFormMouseUp;
        MouseLeave += delegate { if (!resizing) Cursor = Cursors.Default; };
        FormClosing += delegate(object s, FormClosingEventArgs e) { SavePos(); };

        displayHandler = delegate {
            if (IsHandleCreated && !IsDisposed) BeginInvoke((MethodInvoker)EnsureOnScreen);
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += displayHandler;

        // 1초 주기: 시:분 갱신 + 날짜가 바뀌면(자정) D-Day 전체 재계산
        clockTimer = new Timer();
        clockTimer.Interval = 1000;
        clockTimer.Tick += delegate(object s, EventArgs e) {
            if (resizing) return;
            if (DateTime.Today != builtDate) { Build(); return; }
            if (clockLbl != null && !clockLbl.IsDisposed) {
                string t = FooterNow();
                if (clockLbl.Text != t) clockLbl.Text = t;
            }
        };
        clockTimer.Start();
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (clockTimer != null) { clockTimer.Stop(); clockTimer.Dispose(); clockTimer = null; }
            if (displayHandler != null) {
                // 정적 이벤트라 해제하지 않으면 폼이 GC되지 않음
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= displayHandler;
                displayHandler = null;
            }
            DisposeFonts();
        }
        base.Dispose(disposing);
    }

    int S(int v) { return (int)Math.Round(v * scale); }

    string FooterNow() {
        DateTime n = DateTime.Now;
        return n.ToString("yyyy.MM.dd") + ".(" + DayOfWeekKr(n.DayOfWeek) + ") "
             + n.ToString("HH:mm");
    }

    // ── 설정 ──────────────────────────────────────────────
    void LoadSettings() {
        string themeKey = "blue";
        opacityPct     = 93;
        topMostEnabled = false;
        sortByDate     = true;
        lightMode      = false;
        scale          = 1.0f;

        if (File.Exists(settingsFile)) {
            try {
                foreach (var line in File.ReadAllLines(settingsFile, Encoding.UTF8)) {
                    string l = line.Trim();
                    if (l.StartsWith("theme="))   themeKey = l.Substring(6).Trim();
                    if (l.StartsWith("opacity=")) {
                        int v;
                        if (int.TryParse(l.Substring(8).Trim(), out v) && v >= 40 && v <= 100)
                            opacityPct = v;
                    }
                    if (l.StartsWith("topmost=")) {
                        bool b;
                        if (bool.TryParse(l.Substring(8).Trim(), out b)) topMostEnabled = b;
                    }
                    if (l.StartsWith("sort=")) {
                        bool b;
                        if (bool.TryParse(l.Substring(5).Trim(), out b)) sortByDate = b;
                    }
                    if (l.StartsWith("mode=")) {
                        lightMode = l.Substring(5).Trim().ToLower() == "light";
                    }
                    if (l.StartsWith("scale=")) {
                        int v;
                        if (int.TryParse(l.Substring(6).Trim(), out v))
                            scale = ClampScale(v / 100f);
                    }
                }
            } catch {}
        }
        theme     = Themes.Get(themeKey, lightMode);
        Opacity   = opacityPct / 100.0;
        TopMost   = topMostEnabled;
        BackColor = theme.cBg;
    }

    void SaveSettings() {
        try {
            File.WriteAllText(settingsFile,
                "theme="   + theme.Key   + "\r\n" +
                "opacity=" + opacityPct  + "\r\n" +
                "topmost=" + topMostEnabled.ToString().ToLower() + "\r\n" +
                "sort="    + sortByDate.ToString().ToLower() + "\r\n" +
                "mode="    + (lightMode ? "light" : "dark") + "\r\n" +
                "scale="   + (int)Math.Round(scale * 100),
                Encoding.UTF8);
        } catch {}
    }

    public void ApplySettings(string themeKey, int opacityVal, bool topMost, bool sort, bool light) {
        lightMode      = light;
        theme          = Themes.Get(themeKey, lightMode);
        opacityPct     = opacityVal;
        topMostEnabled = topMost;
        sortByDate     = sort;
        Opacity        = opacityPct / 100.0;
        TopMost        = topMostEnabled;
        BackColor      = theme.cBg;
        SaveSettings();
        Build();
    }

    public void RefreshApp() { Build(); }

    static float ClampScale(float s) {
        if (s < SCALE_MIN) return SCALE_MIN;
        if (s > SCALE_MAX) return SCALE_MAX;
        return s;
    }

    // ── 이벤트 ────────────────────────────────────────────
    List<DEvent> LoadEvents() {
        var r = EventStore.Load(eventsFile);
        if (sortByDate) {
            DateTime today = DateTime.Today;
            r.Sort(delegate(DEvent a, DEvent b) {
                int da = (a.Date - today).Days;
                int db = (b.Date - today).Days;
                bool af = da >= 0, bf = db >= 0;
                if (af && bf)   return da.CompareTo(db);
                if (!af && !bf) return db.CompareTo(da);
                return af ? -1 : 1;
            });
        }
        return r;
    }

    string EventDateStr(DateTime t) {
        return t.ToString("yyyy.MM.dd") + ".(" + DayOfWeekKr(t.DayOfWeek) + ")";
    }

    string CalcDDay(DateTime t) {
        int d = (t.Date - DateTime.Today).Days;
        return d > 0 ? "D-" + d : d == 0 ? "D-DAY" : "D+" + Math.Abs(d);
    }

    string DayOfWeekKr(DayOfWeek d) {
        switch (d) {
            case DayOfWeek.Monday:    return "월";
            case DayOfWeek.Tuesday:   return "화";
            case DayOfWeek.Wednesday: return "수";
            case DayOfWeek.Thursday:  return "목";
            case DayOfWeek.Friday:    return "금";
            case DayOfWeek.Saturday:  return "토";
            default:                  return "일";
        }
    }

    Color AccentFor(DEvent ev) {
        return ev.ColorKey.Length == 0 ? theme.cAc : Themes.Get(ev.ColorKey, lightMode).cAc;
    }

    // ── 위치 (멀티모니터) ─────────────────────────────────
    // 헤더(드래그 가능 영역)가 어느 모니터 작업 영역에든 충분히 보이면 유효
    bool IsReachable(Rectangle r) {
        var header = new Rectangle(r.X, r.Y, r.Width, Math.Max(S(40), 30));
        foreach (var scr in Screen.AllScreens) {
            var inter = Rectangle.Intersect(scr.WorkingArea, header);
            if (inter.Width >= 80 && inter.Height >= 20) return true;
        }
        return false;
    }

    Point LoadPos() {
        if (File.Exists(posFile)) {
            try {
                var p = File.ReadAllText(posFile).Trim().Split(',');
                int x = int.Parse(p[0].Trim()), y = int.Parse(p[1].Trim());
                if (IsReachable(new Rectangle(x, y, Width, Height)))
                    return new Point(x, y);
            } catch {}
        }
        var wa = Screen.PrimaryScreen.WorkingArea;
        return new Point(wa.X + (wa.Width - Width) / 2, wa.Y + (wa.Height - Height) / 2);
    }

    void SavePos() {
        try { File.WriteAllText(posFile, Left + "," + Top); } catch {}
    }

    // 모니터 분리·해상도 변경으로 화면 밖에 남으면 가장 가까운 모니터 안으로 이동
    void EnsureOnScreen() {
        if (IsDisposed || IsReachable(Bounds)) return;
        var wa = Screen.FromRectangle(Bounds).WorkingArea;
        int x = Math.Max(wa.Left, Math.Min(Left, wa.Right  - Width));
        int y = Math.Max(wa.Top,  Math.Min(Top,  wa.Bottom - Height));
        Location = new Point(x, y);
        SavePos();
    }

    // ── 마우스: 드래그 이동 / 우하단 배율 조절 ────────────
    Rectangle GripRect() {
        int g = S(22);
        return new Rectangle(ClientSize.Width - g, ClientSize.Height - g, g, g);
    }

    void BeginMove() {
        Native.BeginMove(this);
        SavePos();   // 드래그 종료 후 1회만 저장 (기존: 이동 중 매 픽셀마다 파일 쓰기)
    }

    void ChildMouseDown(object s, MouseEventArgs e) {
        if (e.Button == MouseButtons.Left) BeginMove();
    }

    void OnFormMouseDown(object s, MouseEventArgs e) {
        if (e.Button != MouseButtons.Left) return;
        if (GripRect().Contains(e.Location)) {
            resizing         = true;
            resizeStart      = Cursor.Position;
            resizeStartSize  = ClientSize;
            resizeStartScale = scale;
            Capture          = true;
        } else {
            BeginMove();
        }
    }

    void OnFormMouseMove(object s, MouseEventArgs e) {
        if (!resizing) {
            Cursor = GripRect().Contains(e.Location) ? Cursors.SizeNWSE : Cursors.Default;
            return;
        }
        Point cp = Cursor.Position;
        float rx = (resizeStartSize.Width  + (cp.X - resizeStart.X)) / (float)resizeStartSize.Width;
        float ry = (resizeStartSize.Height + (cp.Y - resizeStart.Y)) / (float)resizeStartSize.Height;
        float ns = resizeStartScale * (rx + ry) / 2f;
        ns = ClampScale((float)Math.Round(ns * 20f) / 20f);   // 5% 단위 스냅 → 재구성 횟수 최소화
        if (Math.Abs(ns - scale) > 0.001f) {
            scale = ns;
            Build();
        }
    }

    void OnFormMouseUp(object s, MouseEventArgs e) {
        if (!resizing) return;
        resizing = false;
        Capture  = false;
        Cursor   = Cursors.Default;
        SaveSettings();
        EnsureOnScreen();
    }

    void SetScale(float s) {
        scale = ClampScale(s);
        SaveSettings();
        Build();
        EnsureOnScreen();
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        // 우하단 크기 조절 그립 (점 6개, 흐리게)
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int W = ClientSize.Width, H = ClientSize.Height;
        int d = Math.Max(2, S(2));
        int[] ox = { 7, 12, 7, 17, 12, 7 };
        int[] oy = { 7, 7, 12, 7, 12, 17 };
        using (var br = new SolidBrush(Ui.Blend(theme.cSub, theme.cBg, 0.45f))) {
            for (int i = 0; i < ox.Length; i++)
                g.FillEllipse(br, W - S(ox[i]) - d / 2, H - S(oy[i]) - d / 2, d, d);
        }
    }

    // ── 하위 창 ───────────────────────────────────────────
    void OpenSettings() {
        string exePath = Application.ExecutablePath;
        var sf = new SettingsForm(this, theme.Key, opacityPct, topMostEnabled, sortByDate, lightMode, exePath);
        Native.CenterOnScreenOf(sf, this);
        sf.ShowDialog(this);
        sf.Dispose();
    }

    void OpenEditor() {
        var ed = new EventEditorForm(this, eventsFile, theme, lightMode);
        Native.CenterOnScreenOf(ed, this);
        ed.ShowDialog(this);
        ed.Dispose();
    }

    void OpenEventsFile() {
        try {
            if (!File.Exists(eventsFile)) EventStore.Save(eventsFile, new List<DEvent>());
            System.Diagnostics.Process.Start(eventsFile);
        } catch {}
    }

    // ── 화면 구성 ─────────────────────────────────────────
    void DisposeFonts() {
        if (fT     != null) { fT.Dispose();     fT     = null; }
        if (fN     != null) { fN.Dispose();     fN     = null; }
        if (fD     != null) { fD.Dispose();     fD     = null; }
        if (fS     != null) { fS.Dispose();     fS     = null; }
        if (fDate  != null) { fDate.Dispose();  fDate  = null; }
        if (fBtn   != null) { fBtn.Dispose();   fBtn   = null; }
        if (fClose != null) { fClose.Dispose(); fClose = null; }
    }

    Label MkLbl(string txt, int x, int y, int w, int h, Font f, Color fc, ContentAlignment a) {
        return new Label {
            Text=txt, Font=f, ForeColor=fc, BackColor=theme.cBg,
            Location=new Point(x,y), Size=new Size(w,h), TextAlign=a
        };
    }

    IconButton MkIconBtn(string glyph, Font f, int x, int y, int sz, Color hoverGlyph) {
        return new IconButton {
            Text=glyph, Font=f, BackColor=theme.cBg,
            GlyphColor=theme.cSub, GlyphHover=hoverGlyph, HoverBack=theme.cDv,
            Radius=S(8), Size=new Size(sz,sz), Location=new Point(x,y), Tag="btn"
        };
    }

    void Build() {
        SuspendLayout();
        if (ContextMenuStrip != null) {
            ContextMenuStrip.Dispose();
            ContextMenuStrip = null;
        }
        // foreach 순회 중 Dispose()가 컬렉션을 수정하는 문제 → 선-제거 후-해제
        while (Controls.Count > 0) {
            Control c = Controls[0];
            Controls.Remove(c);
            c.Dispose();
        }

        DisposeFonts();
        fT     = new Font("Malgun Gothic",   10f * scale, FontStyle.Bold);
        fN     = new Font("Malgun Gothic",   10f * scale);
        fD     = new Font("Segoe UI",        14f * scale, FontStyle.Bold);   // 숫자 전용
        fS     = new Font("Malgun Gothic",    8f * scale);
        fDate  = new Font("Malgun Gothic",    9f * scale);
        fBtn   = new Font("Segoe UI Symbol", 12f * scale);
        fClose = new Font("Arial",           14f * scale, FontStyle.Bold);

        BackColor = theme.cBg;
        builtDate = DateTime.Today;
        var events = LoadEvents();

        const string EMPTY_MSG = "✎ 버튼으로 일정을 추가하세요";

        // ── 너비 계산 ──────────────────────────────────────
        int dDayFixedW = Math.Max(
            TextRenderer.MeasureText("D-999", fD).Width,
            TextRenderer.MeasureText("D-DAY", fD).Width + S(22)) + S(10);
        int maxNameW = events.Count == 0 ? TextRenderer.MeasureText(EMPTY_MSG, fN).Width : 0;
        foreach (var ev in events) {
            int w = TextRenderer.MeasureText(ev.Name, fN).Width;
            if (w > maxNameW) maxNameW = w;
            int dw = TextRenderer.MeasureText(EventDateStr(ev.Date), fS).Width;
            if (dw > maxNameW) maxNameW = dw;
        }

        string today = FooterNow();

        int btnSz = S(30), btnGap = S(6);
        int headerRightW = btnSz * 4 + btnGap * 3 + S(4);
        int titleW       = TextRenderer.MeasureText("D-DAY", fT).Width;
        int headerW      = titleW + headerRightW + S(10);

        // 푸터: 시계(좌) + 크레딧(우) 한 줄
        int dateW   = TextRenderer.MeasureText(today, fDate).Width + S(12);
        int creditW = TextRenderer.MeasureText(AppInfo.FOOTER_TEXT, fS).Width + S(8);
        int footerW = dateW + creditW + S(20);

        int PX = S(28);
        int CW = Math.Max(Math.Max(Math.Max(
            maxNameW + dDayFixedW + S(16), headerW), footerW), S(290));

        int cy = S(18);

        // ── 타이틀 ─────────────────────────────────────────
        Controls.Add(MkLbl("D-DAY", PX, cy, CW - headerRightW, S(30),
            fT, theme.cAc, ContentAlignment.MiddleLeft));

        // ── 헤더 버튼 4개: ↺ ✎ ⚙ × ──────────────────────
        int bx = PX + CW - btnSz * 4 - btnGap * 3;

        var refreshBtn = MkIconBtn("↺", fBtn, bx, cy, btnSz, theme.cTx);
        refreshBtn.Click += delegate(object s, EventArgs e) { Build(); };
        Controls.Add(refreshBtn);
        bx += btnSz + btnGap;

        var editBtn = MkIconBtn("✎", fBtn, bx, cy, btnSz, theme.cTx);
        editBtn.Click += delegate(object s, EventArgs e) { OpenEditor(); };
        Controls.Add(editBtn);
        bx += btnSz + btnGap;

        var settingsBtn = MkIconBtn("⚙", fBtn, bx, cy, btnSz, theme.cTx);
        settingsBtn.Click += delegate(object s, EventArgs e) { OpenSettings(); };
        Controls.Add(settingsBtn);
        bx += btnSz + btnGap;

        var closeBtn = MkIconBtn("×", fClose, bx, cy, btnSz, Color.FromArgb(255,107,107));
        closeBtn.Click += delegate(object s, EventArgs e) { SavePos(); Close(); };
        Controls.Add(closeBtn);

        cy += S(40);

        // ── 이벤트 목록 (긴급도·이벤트별 색상) ─────────────
        if (events.Count == 0) {
            Controls.Add(MkLbl(EMPTY_MSG, PX, cy, CW, S(24),
                fN, theme.cSub, ContentAlignment.MiddleLeft));
            cy += S(30);
        } else {
            int nameW = CW - dDayFixedW - S(16);
            DateTime todayDate = DateTime.Today;
            foreach (var ev in events) {
                int   d     = (ev.Date - todayDate).Days;
                Color evAc  = AccentFor(ev);

                Color nameColor = theme.cTx;
                Color dateColor = theme.cSub;
                Color numColor  = evAc;
                if (d < 0) {
                    // 지난 이벤트: 전체 흐리게
                    nameColor = theme.cSub;
                    dateColor = Ui.Blend(theme.cSub, theme.cBg, 0.35f);
                    numColor  = theme.cSub;
                } else if (d >= 1 && d <= 7) {
                    // 임박(D-7 이내): 배경과 반대 방향으로 강조
                    // (라이트 모드에서 흰색 쪽으로 섞으면 오히려 흐려지던 문제 수정)
                    numColor = lightMode ? Ui.Blend(evAc, Color.Black, 0.22f)
                                         : Ui.Blend(evAc, Color.White, 0.30f);
                }

                Controls.Add(MkLbl(ev.Name, PX, cy + S(2), nameW, S(22),
                    fN, nameColor, ContentAlignment.MiddleLeft));
                Controls.Add(MkLbl(EventDateStr(ev.Date), PX, cy + S(24), nameW, S(16),
                    fS, dateColor, ContentAlignment.MiddleLeft));

                if (d == 0) {
                    Controls.Add(new DDayPill {
                        Text="D-DAY", Font=fD, Accent=evAc, BackColor=theme.cBg,
                        Size=new Size(dDayFixedW, S(42)), Location=new Point(PX + nameW, cy)
                    });
                } else {
                    Controls.Add(MkLbl(CalcDDay(ev.Date), PX + nameW, cy, dDayFixedW, S(42),
                        fD, numColor, ContentAlignment.MiddleRight));
                }
                cy += S(50);
            }
        }

        cy += S(6);

        // ── 푸터: 시계(좌) | 크레딧(우) ────────────────────
        Color dimColor = Ui.Blend(theme.cSub, theme.cBg, 0.45f);
        clockLbl = MkLbl(today, PX, cy, dateW + S(4), S(18),
            fDate, theme.cSub, ContentAlignment.MiddleLeft);
        Controls.Add(clockLbl);
        Controls.Add(MkLbl(AppInfo.FOOTER_TEXT, PX + dateW + S(4), cy, CW - dateW - S(4), S(18),
            fS, dimColor, ContentAlignment.MiddleRight));
        cy += S(22);

        ClientSize = new Size(CW + PX * 2, cy + S(14));

        // ── 라운드 코너 ────────────────────────────────────
        var oldRgn = Region;
        using (var gp = Ui.Rounded(new Rectangle(0, 0, Width, Height), S(18)))
            Region = new Region(gp);
        if (oldRgn != null) oldRgn.Dispose();

        // ── 우클릭 메뉴 ────────────────────────────────────
        var ctx = new ContextMenuStrip();
        ctx.Items.Add("새로고침").Click            += delegate(object s, EventArgs e) { Build(); };
        ctx.Items.Add("이벤트 편집").Click         += delegate(object s, EventArgs e) { OpenEditor(); };
        ctx.Items.Add("events.txt 직접 열기").Click += delegate(object s, EventArgs e) { OpenEventsFile(); };
        ctx.Items.Add("설정").Click                += delegate(object s, EventArgs e) { OpenSettings(); };
        ctx.Items.Add(new ToolStripSeparator());
        var sizeItem = new ToolStripMenuItem("크기 " + (int)Math.Round(scale * 100) + "% → 100%로 초기화");
        sizeItem.Enabled = Math.Abs(scale - 1f) > 0.001f;
        sizeItem.Click  += delegate(object s, EventArgs e) { SetScale(1f); };
        ctx.Items.Add(sizeItem);
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add("종료").Click += delegate(object s, EventArgs e) { SavePos(); Close(); };
        ContextMenuStrip = ctx;

        // ── 하위 컨트롤 드래그 (버튼 제외) ─────────────────
        foreach (Control c in Controls) {
            c.ContextMenuStrip = ctx;
            if ((c.Tag as string) != "btn") c.MouseDown += ChildMouseDown;
        }

        ResumeLayout(false);
        Invalidate();   // 그립 다시 그리기
    }
}

// ── 이벤트 편집 창 ───────────────────────────────────────────
class EventEditorForm : Form {
    class Row {
        public Panel          Panel;
        public TextBox        Name;
        public DateTimePicker Date;
        public ColorDot       Dot;
    }

    const int FW = 460, PX = 20, ROW_H = 36, MAX_VISIBLE = 8, LIST_TOP = 86;

    DDayForm   parent;
    string     path;
    ColorTheme th;
    bool       light;
    Color      bg, fieldBg;

    Font efTitle, efNormal, efSmall, efClose;
    FlowLayoutPanel list;
    List<Row> rows = new List<Row>();
    ToolTip   tip  = new ToolTip();
    Label     hint;
    Button    addBtn, saveBtn, cancelBtn;

    public EventEditorForm(DDayForm parent, string path, ColorTheme theme, bool light) {
        this.parent = parent;
        this.path   = path;
        this.th     = theme;
        this.light  = light;
        bg      = theme.cBg;
        fieldBg = light ? Color.White : Ui.Blend(theme.cBg, Color.White, 0.07f);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.CenterScreen;
        Text            = AppInfo.NAME + " 이벤트 편집";
        TopMost         = true;
        BackColor       = bg;
        KeyPreview      = true;

        efTitle  = new Font("Malgun Gothic", 13, FontStyle.Bold);
        efNormal = new Font("Malgun Gothic", 10);
        efSmall  = new Font("Malgun Gothic", 8.5f);
        efClose  = new Font("Arial", 13, FontStyle.Bold);

        BuildStatic();

        var events = EventStore.Load(path);
        if (events.Count == 0)
            events.Add(new DEvent { Name="", Date=DateTime.Today.AddDays(30) });
        foreach (var ev in events) AddRow(ev, false);
        Relayout();

        MouseDown += DragMouseDown;
        KeyDown   += delegate(object s, KeyEventArgs e) {
            if (e.KeyCode == Keys.Escape) Close();
        };
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (tip      != null) { tip.Dispose();      tip      = null; }
            if (efTitle  != null) { efTitle.Dispose();  efTitle  = null; }
            if (efNormal != null) { efNormal.Dispose(); efNormal = null; }
            if (efSmall  != null) { efSmall.Dispose();  efSmall  = null; }
            if (efClose  != null) { efClose.Dispose();  efClose  = null; }
        }
        base.Dispose(disposing);
    }

    void DragMouseDown(object s, MouseEventArgs e) {
        if (e.Button == MouseButtons.Left) Native.BeginMove(this);
    }

    Label MkLabel(string txt, Font f, Color fc, int x, int y, int w, int h) {
        var l = new Label {
            Text=txt, Font=f, ForeColor=fc, BackColor=bg,
            Location=new Point(x, y), Size=new Size(w, h),
            TextAlign=ContentAlignment.MiddleLeft
        };
        l.MouseDown += DragMouseDown;
        return l;
    }

    Button MkButton(string txt, Color back, Color fore, Color border, int w) {
        var b = new Button {
            Text=txt, Font=efNormal, BackColor=back, ForeColor=fore,
            FlatStyle=FlatStyle.Flat, Size=new Size(w, 34), Cursor=Cursors.Hand
        };
        b.FlatAppearance.BorderColor        = border;
        b.FlatAppearance.MouseOverBackColor = Ui.Blend(back, light ? Color.Black : Color.White, 0.08f);
        b.FlatAppearance.MouseDownBackColor = Ui.Blend(back, light ? Color.Black : Color.White, 0.15f);
        return b;
    }

    void BuildStatic() {
        Controls.Add(MkLabel("이벤트 편집", efTitle, th.cAc, PX, 16, 220, 30));

        var xBtn = new IconButton {
            Text="×", Font=efClose, BackColor=bg,
            GlyphColor=th.cSub, GlyphHover=Color.FromArgb(255,107,107), HoverBack=th.cDv,
            Size=new Size(30, 30), Location=new Point(FW - 46, 16)
        };
        xBtn.Click += delegate(object s, EventArgs e) { Close(); };
        Controls.Add(xBtn);

        // 컬럼 헤더
        Controls.Add(MkLabel("이름", efSmall, th.cSub, PX + 2,   60, 100, 20));
        Controls.Add(MkLabel("날짜", efSmall, th.cSub, PX + 212, 60, 80,  20));
        Controls.Add(MkLabel("색",   efSmall, th.cSub, PX + 344, 60, 30,  20));

        list = new FlowLayoutPanel {
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoScroll    = true,
            BackColor     = bg,
            Location      = new Point(PX, LIST_TOP),
            Size          = new Size(FW - PX * 2, ROW_H),
            Margin        = new Padding(0),
            Padding       = new Padding(0)
        };
        Controls.Add(list);

        hint = MkLabel("색: 클릭 = 다음 색, 우클릭 = 이전 색, 빈 원 = 테마 색",
                       efSmall, Ui.Blend(th.cSub, bg, 0.25f), PX, 0, FW - PX * 2, 18);
        Controls.Add(hint);

        addBtn = MkButton("+ 일정 추가", bg, th.cAc, th.cDv, 110);
        addBtn.Click += delegate(object s, EventArgs e) {
            AddRow(new DEvent { Name="", Date=DateTime.Today.AddDays(30) }, true);
            Relayout();
        };
        Controls.Add(addBtn);

        cancelBtn = MkButton("취소", bg, th.cSub, th.cDv, 80);
        cancelBtn.Click += delegate(object s, EventArgs e) { Close(); };
        Controls.Add(cancelBtn);

        saveBtn = MkButton("저장", th.cAc, Ui.ReadableOn(th.cAc), th.cAc, 80);
        saveBtn.Click += delegate(object s, EventArgs e) { Save(); };
        Controls.Add(saveBtn);
    }

    void AddRow(DEvent ev, bool focus) {
        var r = new Row();
        r.Panel = new Panel {
            Size=new Size(400, ROW_H), Margin=new Padding(0), BackColor=bg
        };

        r.Name = new TextBox {
            Text=ev.Name, Font=efNormal, BorderStyle=BorderStyle.FixedSingle,
            BackColor=fieldBg, ForeColor=th.cTx,
            Location=new Point(0, 5), Width=204
        };
        r.Date = new DateTimePicker {
            Format=DateTimePickerFormat.Custom, CustomFormat="yyyy-MM-dd",
            Font=efNormal, Location=new Point(212, 5), Width=120
        };
        DateTime dv = ev.Date;
        if (dv < r.Date.MinDate) dv = r.Date.MinDate;
        if (dv > r.Date.MaxDate) dv = r.Date.MaxDate;
        r.Date.Value = dv;

        // 이름 칸 Enter → 경고음 대신 날짜 칸으로 이동
        DateTimePicker dtp = r.Date;
        r.Name.KeyDown += delegate(object s, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; dtp.Focus(); }
        };

        r.Dot = new ColorDot {
            Light=light, RingColor=th.cSub, HoverBack=th.cDv, BackColor=bg,
            Size=new Size(28, 28), Location=new Point(340, 4)
        };
        r.Dot.Key = ev.ColorKey;
        tip.SetToolTip(r.Dot, "클릭: 다음 색 / 우클릭: 이전 색");

        var del = new IconButton {
            Text="×", Font=efClose, BackColor=bg,
            GlyphColor=th.cSub, GlyphHover=Color.FromArgb(255,107,107), HoverBack=th.cDv,
            Size=new Size(28, 28), Location=new Point(372, 4)
        };
        tip.SetToolTip(del, "삭제");
        del.Click += delegate(object s, EventArgs e) { RemoveRow(r); };

        r.Panel.Controls.Add(r.Name);
        r.Panel.Controls.Add(r.Date);
        r.Panel.Controls.Add(r.Dot);
        r.Panel.Controls.Add(del);

        rows.Add(r);
        list.Controls.Add(r.Panel);

        if (focus) {
            list.ScrollControlIntoView(r.Panel);
            r.Name.Focus();
        }
    }

    void RemoveRow(Row r) {
        rows.Remove(r);
        list.Controls.Remove(r.Panel);
        r.Panel.Dispose();   // 자식(TextBox·DTP·Dot·버튼)까지 함께 해제
        Relayout();
    }

    void Relayout() {
        int vis = Math.Min(Math.Max(rows.Count, 1), MAX_VISIBLE);
        list.Height = vis * ROW_H;

        int cy = LIST_TOP + list.Height + 8;
        hint.Location = new Point(PX, cy);
        cy += 28;

        addBtn.Location    = new Point(PX, cy);
        saveBtn.Location   = new Point(FW - PX - saveBtn.Width, cy);
        cancelBtn.Location = new Point(saveBtn.Left - 8 - cancelBtn.Width, cy);
        cy += 34 + 18;

        ClientSize = new Size(FW, cy);

        var oldRgn = Region;
        using (var gp = Ui.Rounded(new Rectangle(0, 0, Width, Height), 18))
            Region = new Region(gp);
        if (oldRgn != null) oldRgn.Dispose();
    }

    void Save() {
        var outList = new List<DEvent>();
        foreach (var r in rows) {
            string nm = r.Name.Text.Trim();
            if (nm.Length == 0) continue;   // 이름 없는 행은 무시
            outList.Add(new DEvent { Name=nm, Date=r.Date.Value.Date, ColorKey=r.Dot.Key });
        }
        try {
            EventStore.Save(path, outList);
        } catch (Exception ex) {
            MessageBox.Show(this, "저장하지 못했습니다.\n" + ex.Message,
                "저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        parent.RefreshApp();
        Close();
    }
}

// ── 설정 창 ──────────────────────────────────────────────────
class SettingsForm : Form {
    const int FW = 360, PX = 22;      // 높이는 내용에 맞춰 BuildUI에서 결정
    const int KEY_W = 78;             // 정보 행 항목 이름 폭

    DDayForm parent;
    string   selTheme;
    int      selOpacity;
    bool     selTopMost;
    bool     selSort;
    bool     selLight;
    string   exePath;

    // 현재 미리보기 색
    Color sBg, sAc, sTx, sSub, sDi, sToggleOff;

    Font sfTitle, sfHead, sfLabel, sfValue, sfClose, sfThemeBtn;

    public SettingsForm(DDayForm parent, string curTheme, int curOpacity,
                        bool curTopMost, bool curSort, bool curLight, string exePath) {
        this.parent     = parent;
        this.selTheme   = curTheme;
        this.selOpacity = curOpacity;
        this.selTopMost = curTopMost;
        this.selSort    = curSort;
        this.selLight   = curLight;
        this.exePath    = exePath;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.CenterScreen;
        Text            = AppInfo.NAME + " 설정";
        TopMost         = true;
        Opacity         = 1.0;
        KeyPreview      = true;

        // 폼 본체 마우스·키 처리는 한 번만 등록 (BuildUI 재호출 시 누적 방지)
        MouseDown += DragMouseDown;
        KeyDown   += delegate(object s, KeyEventArgs e) {
            if (e.KeyCode == Keys.Escape) Close();
        };

        BuildUI();
    }

    void DragMouseDown(object s, MouseEventArgs e) {
        if (e.Button == MouseButtons.Left) Native.BeginMove(this);
    }

    void DisposeUIFonts() {
        if (sfTitle    != null) { sfTitle.Dispose();    sfTitle    = null; }
        if (sfHead     != null) { sfHead.Dispose();     sfHead     = null; }
        if (sfLabel    != null) { sfLabel.Dispose();    sfLabel    = null; }
        if (sfValue    != null) { sfValue.Dispose();    sfValue    = null; }
        if (sfClose    != null) { sfClose.Dispose();    sfClose    = null; }
        if (sfThemeBtn != null) { sfThemeBtn.Dispose(); sfThemeBtn = null; }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) DisposeUIFonts();
        base.Dispose(disposing);
    }

    void ApplyRoundCorner() {
        var oldRgn = Region;
        using (var gp = Ui.Rounded(new Rectangle(0, 0, Width, Height), 18))
            Region = new Region(gp);
        if (oldRgn != null) oldRgn.Dispose();
    }

    static Color Darken(Color c, int amt) {
        return Color.FromArgb(
            Math.Max(0, c.R - amt),
            Math.Max(0, c.G - amt),
            Math.Max(0, c.B - amt));
    }

    // ── 공용 생성 도우미 ──────────────────────────────────
    Label AddLabel(string txt, Font f, Color fc, int x, int y, int w, int h, ContentAlignment a) {
        var l = new Label {
            Text=txt, Font=f, ForeColor=fc, BackColor=sBg,
            Location=new Point(x, y), Size=new Size(w, h),
            TextAlign=a, AutoEllipsis=true
        };
        Controls.Add(l);
        return l;
    }

    // 섹션 제목: 작은 회색 굵은 글씨
    int AddHead(string txt, int cy) {
        AddLabel(txt, sfHead, sSub, PX, cy, FW - PX * 2, 20, ContentAlignment.MiddleLeft);
        return cy + 24;
    }

    void AddDivider(int cy) {
        Controls.Add(new Panel { BackColor=sDi, Location=new Point(PX, cy), Size=new Size(FW - PX * 2, 1) });
    }

    // 라벨(좌) + 토글(우) 한 줄
    int AddToggleRow(string label, bool on, EventHandler onToggle, out ToggleSwitch ts, int cy) {
        AddLabel(label, sfLabel, sTx, PX, cy + 3, FW - PX * 2 - 60, 26, ContentAlignment.MiddleLeft);
        ts = new ToggleSwitch {
            On=on, Location=new Point(FW - PX - 48, cy + 4), BackColor=sBg
        };
        ts.AccentOn  = sAc;
        ts.AccentOff = sToggleOff;
        ts.Toggled  += onToggle;
        Controls.Add(ts);
        return cy + 34;
    }

    // 항목 이름(회색) + 값 한 줄
    int AddInfoRow(string key, string value, int cy) {
        AddLabel(key,   sfValue, sSub, PX + 2,          cy, KEY_W,                   22, ContentAlignment.MiddleLeft);
        AddLabel(value, sfValue, sTx,  PX + 2 + KEY_W,  cy, FW - PX * 2 - 2 - KEY_W, 22, ContentAlignment.MiddleLeft);
        return cy + 22;
    }

    // ── 화면 구성 ─────────────────────────────────────────
    void BuildUI() {
        SuspendLayout();
        DisposeUIFonts();
        while (Controls.Count > 0) {
            Control c = Controls[0];
            Controls.Remove(c);
            c.Dispose();
        }

        var t = Themes.Get(selTheme, selLight);
        sBg  = Darken(t.cBg, 6);
        sAc  = t.cAc;
        sTx  = t.cTx;
        sSub = t.cSub;
        sDi  = t.cDi;
        sToggleOff = Ui.Blend(t.cSub, sBg, 0.5f);
        BackColor  = sBg;

        sfTitle    = new Font("Malgun Gothic", 13, FontStyle.Bold);
        sfHead     = new Font("Malgun Gothic", 9,  FontStyle.Bold);
        sfLabel    = new Font("Malgun Gothic", 10);
        sfValue    = new Font("Malgun Gothic", 9);
        sfClose    = new Font("Arial", 15, FontStyle.Bold);
        sfThemeBtn = new Font("Malgun Gothic", 8, FontStyle.Bold);

        int cy = 16;

        // ── 타이틀 + × ─────────────────────────────────────
        AddLabel("설정", sfTitle, sAc, PX, cy, 200, 30, ContentAlignment.MiddleLeft);
        var xBtn = new IconButton {
            Text="×", Font=sfClose, BackColor=sBg,
            GlyphColor=sSub, GlyphHover=Color.FromArgb(255,107,107), HoverBack=t.cDv,
            Size=new Size(30,30), Location=new Point(FW - PX - 26, cy)
        };
        xBtn.Click += delegate(object s, EventArgs e) { Close(); };
        Controls.Add(xBtn);
        cy += 40;

        // ── 테마 색상 ──────────────────────────────────────
        cy = AddHead("테마 색상", cy);
        int gap = 5;
        int bw  = (FW - PX * 2 - gap * (Themes.All.Length - 1)) / Themes.All.Length;
        int bh  = 32;
        for (int i = 0; i < Themes.All.Length; i++) {
            var th  = Themes.All[i];
            var btn = new Button {
                Text=th.Label, Font=sfThemeBtn,
                BackColor=th.Sample, ForeColor=Color.FromArgb(20,20,30),
                FlatStyle=FlatStyle.Flat,
                Size=new Size(bw, bh), Location=new Point(PX + i * (bw + gap), cy),
                Cursor=Cursors.Hand
            };
            btn.FlatAppearance.BorderSize         = 2;
            btn.FlatAppearance.BorderColor        = th.Key == selTheme
                ? (selLight ? Color.FromArgb(32,34,40) : Color.White) : th.Sample;
            btn.FlatAppearance.MouseOverBackColor = th.Sample;
            btn.FlatAppearance.MouseDownBackColor = th.Sample;
            string key = th.Key;
            btn.Click += delegate(object s, EventArgs e) { selTheme = key; BuildUI(); };
            Controls.Add(btn);
        }
        cy += bh + 20;

        // ── 투명도: 라벨(좌) + 값(우) + 슬라이더 ──────────
        AddLabel("투명도", sfLabel, sTx, PX, cy, 160, 24, ContentAlignment.MiddleLeft);
        var opacityVal = AddLabel(selOpacity + "%", sfLabel, sSub, FW - PX - 70, cy, 70, 24,
                                  ContentAlignment.MiddleRight);
        cy += 26;
        var opacityBar = new TrackBar {
            Minimum=40, Maximum=100, Value=selOpacity,
            TickFrequency=10, SmallChange=5, LargeChange=10,
            Location=new Point(PX - 6, cy), Size=new Size(FW - PX * 2 + 12, 36),
            BackColor=sBg
        };
        opacityBar.ValueChanged += delegate(object s, EventArgs e) {
            selOpacity      = opacityBar.Value;
            opacityVal.Text = selOpacity + "%";
        };
        Controls.Add(opacityBar);
        cy += 38;

        // ── 토글 4개 (한 줄에 하나) ─────────────────────────
        ToggleSwitch tsAuto, tsTop, tsSort, tsLight;
        string ep = exePath;
        cy = AddToggleRow("자동 시작", AutoStart.IsEnabled(), null, out tsAuto, cy);
        ToggleSwitch autoRef = tsAuto;
        tsAuto.Toggled += delegate(object s, EventArgs e) {
            if (autoRef.On) {
                if (!AutoStart.Enable(ep)) {
                    MessageBox.Show(this, "자동 시작 등록에 실패했습니다.\n경로: " + ep,
                        "자동 시작 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    autoRef.On = false;
                }
            } else {
                AutoStart.Disable();
            }
        };
        cy = AddToggleRow("항상 위 표시", selTopMost, null, out tsTop, cy);
        ToggleSwitch topRef = tsTop;
        tsTop.Toggled += delegate(object s, EventArgs e) { selTopMost = topRef.On; };

        cy = AddToggleRow("날짜순 정렬", selSort, null, out tsSort, cy);
        ToggleSwitch sortRef = tsSort;
        tsSort.Toggled += delegate(object s, EventArgs e) { selSort = sortRef.On; };

        cy = AddToggleRow("라이트 모드", selLight, null, out tsLight, cy);
        ToggleSwitch lightRef = tsLight;
        tsLight.Toggled += delegate(object s, EventArgs e) {
            selLight = lightRef.On;
            BuildUI();   // 설정 창도 즉시 모드 반영 (미리보기)
        };

        cy += 8;
        AddDivider(cy);
        cy += 14;

        // ── 버전 정보 ──────────────────────────────────────
        cy = AddHead("버전 정보", cy);
        cy = AddInfoRow("앱 이름",  AppInfo.NAME,         cy);
        cy = AddInfoRow("버전",     AppInfo.VERSION,      cy);
        cy = AddInfoRow("업데이트", AppInfo.RELEASE_DATE, cy);
        cy += 10;

        // ── 제작자 정보 ────────────────────────────────────
        cy = AddHead("제작자 정보", cy);
        AddLabel(AppInfo.PURPOSE, sfValue, sTx, PX + 2, cy, FW - PX * 2 - 2, 22, ContentAlignment.MiddleLeft);
        cy += 24;
        cy = AddInfoRow("제작", AppInfo.COPYRIGHT,  cy);
        cy = AddInfoRow("개발", AppInfo.BUILT_WITH, cy);

        AddLabel("소스 코드", sfValue, sSub, PX + 2, cy, KEY_W, 22, ContentAlignment.MiddleLeft);
        var repoLink = new LinkLabel {
            Text=AppInfo.REPO_LABEL, Font=sfValue, BackColor=sBg,
            LinkColor=sAc, ActiveLinkColor=sAc, VisitedLinkColor=sAc,
            LinkBehavior=LinkBehavior.HoverUnderline,
            Location=new Point(PX + 2 + KEY_W, cy), AutoSize=true,
            Padding=new Padding(0, 3, 0, 3)
        };
        repoLink.LinkClicked += delegate(object s, LinkLabelLinkClickedEventArgs e) {
            try { System.Diagnostics.Process.Start(AppInfo.REPO_URL); } catch {}
        };
        Controls.Add(repoLink);
        cy += 22;

        cy += 14;
        AddDivider(cy);
        cy += 16;

        // ── 적용 ───────────────────────────────────────────
        var applyBtn = new Button {
            Text="적용", Font=sfLabel,
            BackColor=sAc, ForeColor=Ui.ReadableOn(sAc),
            FlatStyle=FlatStyle.Flat,
            Size=new Size(88, 34), Location=new Point(FW - PX - 88, cy),
            Cursor=Cursors.Hand
        };
        applyBtn.FlatAppearance.BorderColor = sAc;
        applyBtn.Click += delegate(object s, EventArgs e) {
            parent.ApplySettings(selTheme, selOpacity, selTopMost, selSort, selLight);
            Close();
        };
        Controls.Add(applyBtn);
        cy += 34 + 18;

        ClientSize = new Size(FW, cy);
        ApplyRoundCorner();

        // 라벨·구분선만 창 드래그 (조작 컨트롤은 제외해야 클릭이 먹히지 않음)
        foreach (Control c in Controls) {
            if ((c is Label && !(c is LinkLabel)) || c is Panel)
                c.MouseDown += DragMouseDown;
        }
        ResumeLayout(false);
    }
}
