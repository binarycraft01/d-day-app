using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// ── 테마 정의 ────────────────────────────────────────────────
// v5.9: 배경/텍스트는 모든 테마 공통 뉴트럴, 테마색은 포인트(accent) 전용
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

    public static ColorTheme Get(string key) { return Get(key, false); }

    public static ColorTheme Get(string key, bool light) {
        ColorTheme t = All[4];
        foreach (var c in All) if (c.Key == key) { t = c; break; }
        if (light) {
            // 라이트 모드: 뉴트럴 교체 + 밝은 배경 대비용 accent 어둡게
            t.cBg  = LBg;  t.cTx = LTx;  t.cSub = LSub;
            t.cDv  = LDv;  t.cDi = LDi;
            t.cAc  = Ui.Blend(t.cAc, Color.Black, 0.20f);
        }
        return t;
    }
}

// ── 공용 그리기 유틸 ─────────────────────────────────────────
static class Ui {
    public static GraphicsPath Rounded(Rectangle r, int rad) {
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
}

// ── 자동 시작 관리 ───────────────────────────────────────────
static class AutoStart {
    const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RegName = "DDayWidget";

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
                key.SetValue(RegName, "\"" + exePath + "\"");
                string written = (string)key.GetValue(RegName);
                return written != null;
            }
        } catch { return false; }
    }

    public static void Disable() {
        try {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegPath, true)) {
                if (key != null) key.DeleteValue(RegName, false);
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
    public Color AccentOn { set { _accentOn = value; Invalidate(); } }
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
    public Color GlyphColor  = Color.Gray;
    public Color GlyphHover  = Color.White;
    public Color HoverBack   = Color.FromArgb(50, 52, 60);

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
            using (var gp = Ui.Rounded(new Rectangle(1, 1, Width-3, Height-3), 8))
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
        int ph = ts.Height + 8;
        int pw = ts.Width + 22;
        if (pw > Width) pw = Width;
        int px = Width - pw;
        int py = (Height - ph) / 2;
        var rect = new Rectangle(px, py, pw - 1, ph - 1);
        using (var gp = Ui.Rounded(rect, ph / 2))
        using (var br = new SolidBrush(Accent)) g.FillPath(br, gp);

        TextRenderer.DrawText(g, Text, Font, rect,
            Color.FromArgb(22, 23, 27),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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
    public const string VERSION_TEXT = "D-day widget v6.1(20260706)";
    public const string CREDIT_TEXT  = "Developed by 김민수 with Claude";
}

// ── 메인 위젯 창 ─────────────────────────────────────────────
class DDayForm : Form {
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int msg, int w, int l);

    string baseDir, eventsFile, posFile, settingsFile;
    ColorTheme theme;
    int  opacityPct;
    bool topMostEnabled;
    bool sortByDate;
    bool lightMode;

    // 폰트 멤버
    Font fT, fN, fD, fS, fDate, fBtn, fClose;

    // 시계/자동 갱신
    Timer    clockTimer;
    Label    clockLbl;
    DateTime builtDate;

    public DDayForm() {
        baseDir      = AppDomain.CurrentDomain.BaseDirectory;
        eventsFile   = Path.Combine(baseDir, "events.txt");
        posFile      = Path.Combine(baseDir, "position.txt");
        settingsFile = Path.Combine(baseDir, "settings.txt");

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = true;
        StartPosition   = FormStartPosition.Manual;
        Text            = "D-Day Widget";

        string iconPath = Path.Combine(baseDir, "icon.ico");
        if (File.Exists(iconPath)) Icon = new Icon(iconPath);

        LoadSettings();
        Location = LoadPos();
        Build();
        LocationChanged += delegate(object s, EventArgs e) { SavePos(); };
        FormClosing     += delegate(object s, FormClosingEventArgs e) { SavePos(); };

        // 1초 주기: 시:분 갱신 + 날짜가 바뀌면(자정) D-Day 전체 재계산
        clockTimer = new Timer();
        clockTimer.Interval = 1000;
        clockTimer.Tick += delegate(object s, EventArgs e) {
            if (DateTime.Today != builtDate) { Build(); return; }
            if (clockLbl != null && !clockLbl.IsDisposed) {
                string t = FooterNow();
                if (clockLbl.Text != t) clockLbl.Text = t;
            }
        };
        clockTimer.Start();
    }

    string FooterNow() {
        DateTime n = DateTime.Now;
        return n.ToString("yyyy.MM.dd") + ".(" + DayOfWeekKr(n.DayOfWeek) + ") "
             + n.ToString("HH:mm");
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (clockTimer != null) { clockTimer.Stop(); clockTimer.Dispose(); clockTimer = null; }
            DisposeFonts();
        }
        base.Dispose(disposing);
    }

    void LoadSettings() {
        string themeKey = "blue";
        opacityPct     = 93;
        topMostEnabled = false;
        sortByDate     = true;
        lightMode      = false;

        if (File.Exists(settingsFile)) {
            try {
                foreach (var line in File.ReadAllLines(settingsFile, System.Text.Encoding.UTF8)) {
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
                "mode="    + (lightMode ? "light" : "dark"),
                System.Text.Encoding.UTF8);
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

    List<Tuple<string, DateTime>> LoadEvents() {
        var r = new List<Tuple<string, DateTime>>();
        if (!File.Exists(eventsFile)) return r;
        try {
            foreach (var line in File.ReadAllLines(eventsFile, System.Text.Encoding.UTF8)) {
                string l = line.Trim();
                if (string.IsNullOrEmpty(l) || l.StartsWith("#") || !l.Contains("=")) continue;
                int i = l.IndexOf('=');
                string nm = l.Substring(0, i).Trim(), ds = l.Substring(i+1).Trim();
                DateTime dt;
                if (DateTime.TryParseExact(ds, "yyyy-MM-dd", null, DateTimeStyles.None, out dt))
                    r.Add(Tuple.Create(nm, dt));
            }
        } catch {}

        if (sortByDate) {
            r.Sort(delegate(Tuple<string, DateTime> a, Tuple<string, DateTime> b) {
                int da = (a.Item2.Date - DateTime.Today).Days;
                int db = (b.Item2.Date - DateTime.Today).Days;
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
        return d > 0 ? "D-" + d : d == 0 ? "D-Day!" : "D+" + Math.Abs(d);
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

    Point LoadPos() {
        var wa = Screen.PrimaryScreen.WorkingArea;
        if (File.Exists(posFile)) {
            try {
                var p = File.ReadAllText(posFile).Trim().Split(',');
                int x = int.Parse(p[0]), y = int.Parse(p[1]);
                if (x >= 0 && y >= 0 && x < wa.Width - 50 && y < wa.Height - 50)
                    return new Point(x, y);
            } catch {}
        }
        return new Point((wa.Width - 360) / 2, (wa.Height - 220) / 2);
    }

    void SavePos() {
        try { File.WriteAllText(posFile, Left + "," + Top); } catch {}
    }

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

    Panel MkDiv(int y, Color c, int px, int cw) {
        return new Panel { BackColor=c, Location=new Point(px,y), Size=new Size(cw,1) };
    }

    void Build() {
        if (ContextMenuStrip != null) {
            ContextMenuStrip.Dispose();
            ContextMenuStrip = null;
        }
        // foreach 순회 중 Dispose()가 컬렉션을 수정해 일부 컨트롤이
        // dispose되지 않고 누수되는 문제 → 선-제거 후-해제 방식으로 수정
        while (Controls.Count > 0) {
            Control c = Controls[0];
            Controls.Remove(c);
            c.Dispose();
        }

        DisposeFonts();
        fT     = new Font("Malgun Gothic", 10, FontStyle.Bold);
        fN     = new Font("Malgun Gothic", 10);
        fD     = new Font("Segoe UI", 14, FontStyle.Bold);   // 숫자 전용
        fS     = new Font("Malgun Gothic", 8);
        fDate  = new Font("Malgun Gothic", 9);
        fBtn   = new Font("Segoe UI Symbol", 12);
        fClose = new Font("Arial", 14, FontStyle.Bold);

        BackColor = theme.cBg;
        builtDate = DateTime.Today;
        var events = LoadEvents();

        // ── 너비 계산 ──────────────────────────────────────
        int dDayFixedW = Math.Max(
            TextRenderer.MeasureText("D-999", fD).Width,
            TextRenderer.MeasureText("D-DAY", fD).Width + 22) + 10;
        int maxNameW = TextRenderer.MeasureText("events.txt not found", fN).Width;
        foreach (var ev in events) {
            int w = TextRenderer.MeasureText(ev.Item1, fN).Width;
            if (w > maxNameW) maxNameW = w;
            int dw = TextRenderer.MeasureText(EventDateStr(ev.Item2), fS).Width;
            if (dw > maxNameW) maxNameW = dw;
        }

        string today = FooterNow();

        int btnSz = 30, btnGap = 6;
        // 헤더 우측: [↺][✎][⚙][×] = 4×30 + 3×6 + 4 = 142
        int headerRightW = btnSz * 4 + btnGap * 3 + 4;
        int titleW       = TextRenderer.MeasureText("D-DAY", fT).Width;
        int headerW      = titleW + headerRightW + 10;

        // 푸터: 시계(좌) + 크레딧(우) 한 줄
        int dateW   = TextRenderer.MeasureText(today, fDate).Width + 12;
        int creditW = TextRenderer.MeasureText(AppInfo.CREDIT_TEXT, fS).Width + 8;
        int footerW = dateW + creditW + 20;

        int PX = 28;
        int CW = Math.Max(Math.Max(Math.Max(
            maxNameW + dDayFixedW + 16, headerW), footerW), 290);

        int cy = 18;

        // ── 타이틀 (accent — 테마 변경이 눈에 보이도록) ────
        Controls.Add(MkLbl("D-DAY", PX, cy, CW - headerRightW, 30,
            fT, theme.cAc, ContentAlignment.MiddleLeft));

        // ── 헤더 버튼 4개: ↺ ✎ ⚙ × ──────────────────────
        int bx = PX + CW - btnSz * 4 - btnGap * 3;

        // ↺ 새로고침
        var refreshBtn = new IconButton {
            Text="\u21BA", Font=fBtn, BackColor=theme.cBg,
            GlyphColor=theme.cSub, GlyphHover=theme.cTx, HoverBack=theme.cDv,
            Size=new Size(btnSz,btnSz), Location=new Point(bx,cy), Tag="btn"
        };
        refreshBtn.Click += delegate(object s, EventArgs e) { Build(); };
        Controls.Add(refreshBtn);
        bx += btnSz + btnGap;

        // ✎ 이벤트 편집
        string ef = eventsFile;
        var editBtn = new IconButton {
            Text="\u270E", Font=fBtn, BackColor=theme.cBg,
            GlyphColor=theme.cSub, GlyphHover=theme.cTx, HoverBack=theme.cDv,
            Size=new Size(btnSz,btnSz), Location=new Point(bx,cy), Tag="btn"
        };
        editBtn.Click += delegate(object s, EventArgs e) {
            try {
                if (File.Exists(ef)) System.Diagnostics.Process.Start(ef);
                else MessageBox.Show("events.txt 파일이 없습니다.\n경로: " + ef,
                    "파일 없음", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            } catch {}
        };
        Controls.Add(editBtn);
        bx += btnSz + btnGap;

        // ⚙ 설정
        var settingsBtn = new IconButton {
            Text="\u2699", Font=fBtn, BackColor=theme.cBg,
            GlyphColor=theme.cSub, GlyphHover=theme.cTx, HoverBack=theme.cDv,
            Size=new Size(btnSz,btnSz), Location=new Point(bx,cy), Tag="btn"
        };
        settingsBtn.Click += delegate(object s, EventArgs e) { OpenSettings(); };
        Controls.Add(settingsBtn);
        bx += btnSz + btnGap;

        // × 닫기
        var closeBtn = new IconButton {
            Text="\u00D7", Font=fClose, BackColor=theme.cBg,
            GlyphColor=theme.cSub, GlyphHover=Color.FromArgb(255,107,107), HoverBack=theme.cDv,
            Size=new Size(btnSz,btnSz), Location=new Point(bx,cy), Tag="close"
        };
        closeBtn.Click += delegate(object s, EventArgs e) { SavePos(); Close(); };
        Controls.Add(closeBtn);

        cy += 40;   // 구분선 없이 여백으로 분리

        // ── 이벤트 목록 (구분선 없음, 긴급도별 색상 위계) ──
        if (events.Count == 0) {
            Controls.Add(MkLbl("events.txt not found", PX, cy, CW, 24,
                fN, theme.cTx, ContentAlignment.MiddleLeft));
            cy += 30;
        } else {
            for (int idx = 0; idx < events.Count; idx++) {
                var ev    = events[idx];
                int nameW = CW - dDayFixedW - 16;
                int d     = (ev.Item2.Date - DateTime.Today).Days;

                Color nameColor = theme.cTx;
                Color dateColor = theme.cSub;
                Color numColor  = theme.cAc;
                if (d < 0) {
                    // 지난 이벤트: 전체 흐리게
                    nameColor = theme.cSub;
                    dateColor = Ui.Blend(theme.cSub, theme.cBg, 0.35f);
                    numColor  = theme.cSub;
                } else if (d >= 1 && d <= 7) {
                    // 임박(D-7 이내): 포인트색을 더 밝게
                    numColor = Ui.Blend(theme.cAc, Color.White, 0.30f);
                }

                Controls.Add(MkLbl(ev.Item1, PX, cy+2, nameW, 22,
                    fN, nameColor, ContentAlignment.MiddleLeft));
                Controls.Add(MkLbl(EventDateStr(ev.Item2), PX, cy+24, nameW, 16,
                    fS, dateColor, ContentAlignment.MiddleLeft));

                if (d == 0) {
                    // D-Day 당일: accent pill로 반전 강조
                    var pill = new DDayPill {
                        Text="D-DAY", Font=fD, Accent=theme.cAc, BackColor=theme.cBg,
                        Size=new Size(dDayFixedW, 42), Location=new Point(PX+nameW, cy)
                    };
                    Controls.Add(pill);
                } else {
                    Controls.Add(MkLbl(CalcDDay(ev.Item2), PX+nameW, cy, dDayFixedW, 42,
                        fD, numColor, ContentAlignment.MiddleRight));
                }
                cy += 50;
            }
        }

        cy += 6;

        // ── 푸터: 시계(좌) | 크레딧(우) 한 줄 ──────────────
        Color dimColor = Ui.Blend(theme.cSub, theme.cBg, 0.45f);
        clockLbl = MkLbl(today, PX, cy, dateW + 4, 18,
            fDate, theme.cSub, ContentAlignment.MiddleLeft);
        Controls.Add(clockLbl);
        Controls.Add(MkLbl(AppInfo.CREDIT_TEXT, PX, cy, CW, 18,
            fS, dimColor, ContentAlignment.MiddleRight));
        cy += 22;

        ClientSize = new Size(CW + PX * 2, cy + 14);

        // ── 라운드 코너 (ra=18) ────────────────────────────
        var oldRgn = Region;
        var gp = Ui.Rounded(new Rectangle(0, 0, Width, Height), 18);
        Region = new Region(gp);
        if (oldRgn != null) oldRgn.Dispose();
        gp.Dispose();

        // ── 우클릭 메뉴 ────────────────────────────────────
        var ctx = new ContextMenuStrip();
        ctx.Items.Add("Refresh").Click     += delegate(object s, EventArgs e) { Build(); };
        ctx.Items.Add("이벤트 편집").Click += delegate(object s, EventArgs e) {
            try { if (File.Exists(eventsFile)) System.Diagnostics.Process.Start(eventsFile); } catch {}
        };
        ctx.Items.Add("설정").Click        += delegate(object s, EventArgs e) { OpenSettings(); };
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add("Exit").Click        += delegate(object s, EventArgs e) { SavePos(); Close(); };
        ContextMenuStrip = ctx;
        foreach (Control c in Controls) c.ContextMenuStrip = ctx;

        // ── 드래그 ─────────────────────────────────────────
        MouseEventHandler drag = delegate(object s, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); }
        };
        MouseDown += drag;
        foreach (Control c in Controls) {
            string tag = (string)c.Tag;
            if (tag != "close" && tag != "btn") c.MouseDown += drag;
        }
    }

    void OpenSettings() {
        string exePath = Path.Combine(baseDir, "DDayWidget.exe");
        var sf = new SettingsForm(this, theme.Key, opacityPct, topMostEnabled, sortByDate, lightMode, exePath);
        sf.ShowDialog(this);
        sf.Dispose();
    }
}

// ── 설정 창 ──────────────────────────────────────────────────
class SettingsForm : Form {
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int msg, int w, int l);

    DDayForm     parent;
    string       selTheme;
    int          selOpacity;
    bool         selTopMost;
    bool         selSort;
    bool         selLight;
    string       exePath;

    Button[]     themeBtns;
    TrackBar     opacityBar;
    Label        opacityValLbl;
    ToggleSwitch autoStartToggle;
    ToggleSwitch topMostToggle;
    ToggleSwitch sortToggle;
    ToggleSwitch lightToggle;

    Font   sfTitle, sfSection, sfNormal, sfClose;
    Font[] sfThemeFonts;

    const int FW = 360, FH = 410;

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
        Text            = "D-Day 설정";
        ClientSize      = new Size(FW, FH);
        TopMost         = true;
        Opacity         = 1.0;

        ApplyRoundCorner();
        BuildUI();
    }

    void DisposeUIFonts() {
        if (sfTitle   != null) { sfTitle.Dispose();   sfTitle   = null; }
        if (sfSection != null) { sfSection.Dispose(); sfSection = null; }
        if (sfNormal  != null) { sfNormal.Dispose();  sfNormal  = null; }
        if (sfClose   != null) { sfClose.Dispose();   sfClose   = null; }
        if (sfThemeFonts != null) {
            foreach (var f in sfThemeFonts) if (f != null) f.Dispose();
            sfThemeFonts = null;
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) DisposeUIFonts();
        base.Dispose(disposing);
    }

    void ApplyRoundCorner() {
        var gp = new GraphicsPath();
        int r = 18;
        gp.AddArc(0,0,r*2,r*2,180,90);
        gp.AddArc(FW-r*2,0,r*2,r*2,270,90);
        gp.AddArc(FW-r*2,FH-r*2,r*2,r*2,0,90);
        gp.AddArc(0,FH-r*2,r*2,r*2,90,90);
        gp.CloseFigure();
        var oldRgn = Region;
        Region = new Region(gp);
        if (oldRgn != null) oldRgn.Dispose();
        gp.Dispose();
    }

    Color ThemeBg()  { return Darken(Themes.Get(selTheme, selLight).cBg, 6); }
    Color ThemeAc()  { return Themes.Get(selTheme, selLight).cAc; }
    Color ThemeSub() { return Themes.Get(selTheme, selLight).cSub; }
    Color ThemeDi()  { return Themes.Get(selTheme, selLight).cDi; }

    Color Darken(Color c, int amt) {
        return Color.FromArgb(
            Math.Max(0, c.R - amt),
            Math.Max(0, c.G - amt),
            Math.Max(0, c.B - amt));
    }

    void BuildUI() {
        DisposeUIFonts();
        while (Controls.Count > 0) {
            Control c = Controls[0];
            Controls.Remove(c);
            c.Dispose();
        }

        Color sBg  = ThemeBg();
        Color sAc  = ThemeAc();
        Color sSub = ThemeSub();
        Color sDi  = ThemeDi();

        BackColor = sBg;

        sfTitle      = new Font("Malgun Gothic", 13, FontStyle.Bold);
        sfSection    = new Font("Malgun Gothic", 11, FontStyle.Bold);
        sfNormal     = new Font("Malgun Gothic", 10);
        sfClose      = new Font("Arial", 15, FontStyle.Bold);
        sfThemeFonts = new Font[Themes.All.Length];
        for (int i = 0; i < Themes.All.Length; i++)
            sfThemeFonts[i] = new Font("Malgun Gothic", 8, FontStyle.Bold);

        int PX = 20, cy = 16;

        // 타이틀 + X
        Controls.Add(new Label {
            Text="설정", Font=sfTitle, ForeColor=sAc, BackColor=sBg,
            Location=new Point(PX, cy), Size=new Size(200, 30),
            TextAlign=ContentAlignment.MiddleLeft
        });
        var xBtn = new IconButton {
            Text="\u00D7", Font=sfClose, BackColor=sBg,
            GlyphColor=sSub, GlyphHover=Color.FromArgb(255,107,107), HoverBack=sDi,
            Size=new Size(30,30), Location=new Point(FW-40, cy), Tag="close"
        };
        xBtn.Click += delegate(object s, EventArgs e) { Close(); };
        Controls.Add(xBtn);
        cy += 38;

        Controls.Add(new Panel { BackColor=sDi, Location=new Point(PX,cy), Size=new Size(FW-PX*2,1) });
        cy += 14;

        // 테마 색상
        Controls.Add(new Label {
            Text="테마 색상", Font=sfSection, ForeColor=sAc, BackColor=sBg,
            Location=new Point(PX, cy), Size=new Size(200, 24)
        });
        cy += 30;

        int gap = 5;
        int bw  = (FW - PX*2 - gap * (Themes.All.Length - 1)) / Themes.All.Length;
        int bh  = 32;

        themeBtns = new Button[Themes.All.Length];
        for (int i = 0; i < Themes.All.Length; i++) {
            var t   = Themes.All[i];
            var btn = new Button {
                Text      = t.Label,
                Font      = sfThemeFonts[i],
                BackColor = t.Sample,
                ForeColor = Color.FromArgb(20,20,30),
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(bw, bh),
                Location  = new Point(PX + i * (bw + gap), cy),
                Cursor    = Cursors.Hand,
                Tag       = t.Key
            };
            btn.FlatAppearance.BorderSize         = 2;
            btn.FlatAppearance.BorderColor        = t.Key == selTheme ? Color.White : t.Sample;
            btn.FlatAppearance.MouseOverBackColor = t.Sample;
            btn.FlatAppearance.MouseDownBackColor = t.Sample;
            var captured = t;
            btn.Click += delegate(object s, EventArgs e) { SelectTheme(captured.Key); };
            themeBtns[i] = btn;
            Controls.Add(btn);
        }
        cy += bh + 18;

        Controls.Add(new Panel { BackColor=sDi, Location=new Point(PX,cy), Size=new Size(FW-PX*2,1) });
        cy += 14;

        // 투명도
        opacityValLbl = new Label {
            Text="투명도: " + selOpacity + "%",
            Font=sfSection, ForeColor=sAc, BackColor=sBg,
            Location=new Point(PX, cy), Size=new Size(280, 24)
        };
        Controls.Add(opacityValLbl);
        cy += 30;

        opacityBar = new TrackBar {
            Minimum=40, Maximum=100, Value=selOpacity,
            TickFrequency=10, SmallChange=5, LargeChange=10,
            Location=new Point(PX-5, cy),
            Size=new Size(FW - PX*2 + 10, 36),
            BackColor=sBg
        };
        opacityBar.ValueChanged += delegate(object s, EventArgs e) {
            selOpacity         = opacityBar.Value;
            opacityValLbl.Text = "투명도: " + selOpacity + "%";
        };
        Controls.Add(opacityBar);
        cy += 44;

        Controls.Add(new Panel { BackColor=sDi, Location=new Point(PX,cy), Size=new Size(FW-PX*2,1) });
        cy += 14;

        // 자동 실행 (좌) + 항상 위 (우)
        Controls.Add(new Label {
            Text="자동 시작", Font=sfSection, ForeColor=sAc, BackColor=sBg,
            Location=new Point(PX, cy+4), Size=new Size(112, 24)
        });
        autoStartToggle = new ToggleSwitch {
            On=AutoStart.IsEnabled(), Location=new Point(PX+114, cy+4), BackColor=sBg
        };
        autoStartToggle.AccentOn = sAc;
        string ep = exePath;
        autoStartToggle.Toggled += delegate(object s, EventArgs e) {
            if (autoStartToggle.On) {
                bool ok = AutoStart.Enable(ep);
                if (!ok) {
                    MessageBox.Show(
                        "등록 실패\nexePath: " + ep +
                        "\n파일 존재: " + System.IO.File.Exists(ep),
                        "자동 시작 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    autoStartToggle.On = false;
                }
            } else { AutoStart.Disable(); }
        };
        Controls.Add(autoStartToggle);

        int rightX = PX + 160;
        Controls.Add(new Label {
            Text="항상 위 표시", Font=sfSection, ForeColor=sAc, BackColor=sBg,
            Location=new Point(rightX, cy+4), Size=new Size(100, 24)
        });
        topMostToggle = new ToggleSwitch {
            On=selTopMost, Location=new Point(rightX+102, cy+4), BackColor=sBg
        };
        topMostToggle.AccentOn = sAc;
        topMostToggle.Toggled += delegate(object s, EventArgs e) { selTopMost = topMostToggle.On; };
        Controls.Add(topMostToggle);
        cy += 40;

        Controls.Add(new Panel { BackColor=sDi, Location=new Point(PX,cy), Size=new Size(FW-PX*2,1) });
        cy += 14;

        // 날짜순 정렬
        Controls.Add(new Label {
            Text="날짜순 정렬", Font=sfSection, ForeColor=sAc, BackColor=sBg,
            Location=new Point(PX, cy+4), Size=new Size(112, 24)
        });
        sortToggle = new ToggleSwitch {
            On=selSort, Location=new Point(PX+114, cy+4), BackColor=sBg
        };
        sortToggle.AccentOn = sAc;
        sortToggle.Toggled += delegate(object s, EventArgs e) { selSort = sortToggle.On; };
        Controls.Add(sortToggle);

        Controls.Add(new Label {
            Text="라이트 모드", Font=sfSection, ForeColor=sAc, BackColor=sBg,
            Location=new Point(rightX, cy+4), Size=new Size(100, 24)
        });
        lightToggle = new ToggleSwitch {
            On=selLight, Location=new Point(rightX+102, cy+4), BackColor=sBg
        };
        lightToggle.AccentOn = sAc;
        lightToggle.Toggled += delegate(object s, EventArgs e) {
            selLight = lightToggle.On;
            BuildUI();   // 설정 창 자체도 즉시 모드 반영 (미리보기)
        };
        Controls.Add(lightToggle);
        cy += 40;

        Controls.Add(new Panel { BackColor=sDi, Location=new Point(PX,cy), Size=new Size(FW-PX*2,1) });
        cy += 14;

        // 적용 버튼
        var applyBtn = new Button {
            Text="적용", Font=sfNormal,
            BackColor=sDi, ForeColor=sAc,
            FlatStyle=FlatStyle.Flat,
            Size=new Size(88, 34),
            Location=new Point(FW - PX - 88, cy),
            Cursor=Cursors.Hand
        };
        applyBtn.FlatAppearance.BorderColor = sAc;
        applyBtn.Click += delegate(object s, EventArgs e) {
            parent.ApplySettings(selTheme, selOpacity, selTopMost, selSort, selLight);
            Close();
        };
        Controls.Add(applyBtn);

        // 버전 표기 (v5.9: 위젯 푸터에서 설정 창으로 이동)
        Controls.Add(new Label {
            Text=AppInfo.VERSION_TEXT, Font=sfThemeFonts[0],
            ForeColor=Ui.Blend(sSub, sBg, 0.4f), BackColor=sBg,
            Location=new Point(PX, cy+8), Size=new Size(FW - PX*2 - 100, 18),
            TextAlign=ContentAlignment.MiddleLeft
        });

        // 드래그
        MouseEventHandler drag = delegate(object s, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); }
        };
        MouseDown += drag;
        foreach (Control c in Controls) {
            if (!(c is Button) && !(c is TrackBar) && !(c is ToggleSwitch) && !(c is IconButton))
                c.MouseDown += drag;
        }
    }

    void SelectTheme(string key) {
        selTheme = key;
        BuildUI();
    }
}
