// Tray icon that pops up a menu of the Desktop's contents (like the Windows 10 "Desktop" toolbar).
// Build: csc /target:winexe /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Microsoft.CSharp.dll /r:System.Core.dll DesktopMenu.cs
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static partial class Program
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO info, uint size, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    const uint SHGFI_ICON = 0x100, SHGFI_SMALLICON = 0x1, SHGFI_LARGEICON = 0x0;

    static Icon GetIcon(string path, bool small)
    {
        var info = new SHFILEINFO();
        SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), SHGFI_ICON | (small ? SHGFI_SMALLICON : SHGFI_LARGEICON));
        if (info.hIcon == IntPtr.Zero) return null;
        var icon = (Icon)Icon.FromHandle(info.hIcon).Clone();
        DestroyIcon(info.hIcon);
        return icon;
    }

    static Image GetImage(string path)
    {
        var icon = GetIcon(path, true);
        return icon == null ? null : icon.ToBitmap();
    }

    static readonly string UserDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    static readonly string PublicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

    static bool Visible(FileSystemInfo f)
    {
        return (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;
    }

    // Folders first, then files, alphabetical; merges several roots (user + public desktop).
    static void Fill(ToolStripItemCollection items, params string[] roots)
    {
        var dirs = roots.Where(Directory.Exists)
            .SelectMany(r => { try { return new DirectoryInfo(r).GetDirectories(); } catch { return new DirectoryInfo[0]; } })
            .Where(Visible).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase);
        var files = roots.Where(Directory.Exists)
            .SelectMany(r => { try { return new DirectoryInfo(r).GetFiles(); } catch { return new FileInfo[0]; } })
            .Where(Visible).OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase);

        foreach (var d in dirs) items.Add(FolderItem(d.FullName, d.Name));
        foreach (var f in files)
        {
            string name = f.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                          f.Extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(f.Name) : f.Name;
            string path = f.FullName;
            items.Add(new ToolStripMenuItem(name, GetImage(path), (s, e) => Open(path)));
        }
        if (items.Count == 0) items.Add(new ToolStripMenuItem(L("empty")) { Enabled = false });
    }

    // Subfolders load their contents only when hovered, so deep trees stay fast.
    static ToolStripMenuItem FolderItem(string path, string name)
    {
        var item = new ToolStripMenuItem(name, GetImage(path));
        item.DropDownItems.Add("...");
        item.DropDownOpening += (s, e) =>
        {
            item.DropDownItems.Clear();
            item.DropDownItems.Add(new ToolStripMenuItem(L("openFolder"), null, (s2, e2) => Open(path)));
            item.DropDownItems.Add(new ToolStripSeparator());
            Fill(item.DropDownItems, path);
        };
        return item;
    }

    // Windows theme: SystemUsesLightTheme = taskbar, AppsUseLightTheme = app windows/menus.
    static bool IsLight(string value)
    {
        try
        {
            object v = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", value, 0);
            return v is int && (int)v == 1;
        }
        catch { return false; }
    }

    const int IconCount = 12; // shown as a 4 x 3 grid in the right-click menu

    const string SettingsKey = @"HKEY_CURRENT_USER\Software\DesktopMenu";

    static int IconStyle()
    {
        try
        {
            object v = Microsoft.Win32.Registry.GetValue(SettingsKey, "IconStyle", 0);
            int i = v is int ? (int)v : 0;
            return i >= 0 && i < IconCount ? i : 0;
        }
        catch { return 0; }
    }

    static void SaveIconStyle(int i)
    {
        try { Microsoft.Win32.Registry.SetValue(SettingsKey, "IconStyle", i); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    // ---- Install location + user's own icons (kept in one folder per Windows user) ----
    static readonly string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopMenu");
    static readonly string InstalledExe = Path.Combine(InstallDir, "DesktopMenu.exe");
    static readonly string IconsDir = Path.Combine(InstallDir, "Icons");
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static readonly string[] ImageExts = { ".png", ".ico", ".jpg", ".jpeg", ".bmp", ".gif" };

    // Selected custom icon path, or null when a built-in icon is in use (or the file was deleted).
    static string CustomIcon()
    {
        try
        {
            var v = Microsoft.Win32.Registry.GetValue(SettingsKey, "CustomIcon", null) as string;
            return !string.IsNullOrEmpty(v) && File.Exists(v) ? v : null;
        }
        catch { return null; }
    }

    static void SaveCustomIcon(string path)
    {
        try { Microsoft.Win32.Registry.SetValue(SettingsKey, "CustomIcon", path ?? ""); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    static string[] CustomIcons()
    {
        try
        {
            Directory.CreateDirectory(IconsDir);
            return Directory.GetFiles(IconsDir)
                .Where(f => ImageExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => File.GetCreationTime(f)).ToArray();
        }
        catch { return new string[0]; }
    }

    // Any image or .ico, scaled to fit a square without stretching; reads bytes so the file isn't locked.
    static Bitmap LoadImage(string path, int size)
    {
        var bmp = new Bitmap(size, size);
        try
        {
            Image src;
            if (Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
                using (var ic = new Icon(path, 256, 256)) src = ic.ToBitmap();
            else
                src = Image.FromStream(new MemoryStream(File.ReadAllBytes(path)));
            using (src)
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                float k = Math.Min((float)size / src.Width, (float)size / src.Height);
                float w = src.Width * k, h = src.Height * k;
                g.DrawImage(src, (size - w) / 2, (size - h) / 2, w, h);
            }
        }
        catch { } // unreadable image → blank tile instead of a crash
        return bmp;
    }

    // Lets the user pick an image; copies it into the icons folder and returns the copy's path.
    static string AddCustomIcon()
    {
        using (var dlg = new OpenFileDialog { Title = L("pickImage"), Filter = L("images") + "|*.png;*.ico;*.jpg;*.jpeg;*.bmp;*.gif" })
        {
            if (dlg.ShowDialog() != DialogResult.OK) return null;
            try
            {
                Directory.CreateDirectory(IconsDir);
                string dest = Path.Combine(IconsDir, Path.GetFileName(dlg.FileName));
                if (File.Exists(dest))
                    dest = Path.Combine(IconsDir, Path.GetFileNameWithoutExtension(dest) + "_" + DateTime.Now.Ticks + Path.GetExtension(dest));
                File.Copy(dlg.FileName, dest);
                return dest;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                return null;
            }
        }
    }

    static Bitmap PlusImage(int size, Color fg)
    {
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        using (var p = RoundPen(fg, Math.Max(1.5f, size / 10f)))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawLine(p, size / 2f, size * .2f, size / 2f, size * .8f);
            g.DrawLine(p, size * .2f, size / 2f, size * .8f, size / 2f);
        }
        return bmp;
    }

    static void Uninstall(NotifyIcon tray)
    {
        if (!ShowMessage(DialogKind.Confirm, L("uninstallTitle"), new[] { L("uninstallLine1"), L("uninstallLine2") }, L("uninstall"))) return;
        try
        {
            DeleteStartLnks(false);
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (k != null) k.DeleteValue("DesktopMenu", false);
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\DesktopMenu", false);
        }
        catch { }
        // A running exe can't delete itself; a hidden cmd removes our own folder after we exit.
        Process.Start(new ProcessStartInfo("cmd.exe", "/c timeout /t 2 /nobreak >nul & rmdir /s /q \"" + InstallDir + "\"")
            { CreateNoWindow = true, UseShellExecute = false });
        tray.Visible = false;
        Application.Exit();
    }

    // ---- Chosen at install: tray icon (hidden under ^ or next to the clock) and/or a Start-menu shortcut ----
    static int Setting(string name, int fallback)
    {
        try { object v = Microsoft.Win32.Registry.GetValue(SettingsKey, name, fallback); return v is int ? (int)v : fallback; }
        catch { return fallback; }
    }
    static bool TrayEnabled() { return Setting("Tray", 1) != 0; }

    // Start-menu shortcut is named in the chosen language (that's what users type in Start search).
    static string StartLnkFor(string name) { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), name + ".lnk"); }
    static string StartLnk { get { return StartLnkFor(L("app")); } }

    // Removes our Start shortcut under any language's name (left over from an install in another language).
    static void DeleteStartLnks(bool keepCurrent)
    {
        foreach (var name in Text["app"])
        {
            string p = StartLnkFor(name);
            if (keepCurrent && p == StartLnk) continue;
            try { if (File.Exists(p)) File.Delete(p); } catch { }
        }
    }
    static readonly string PinnedDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern void SHChangeNotify(int eventId, uint flags, string item1, IntPtr item2);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

    // Multi-size .ico (PNG entries) of the current icon. A new file name each time so Explorer's icon cache can't show the old one.
    static string WriteIco()
    {
        Directory.CreateDirectory(InstallDir);
        string path = Path.Combine(InstallDir, "icon_" + DateTime.Now.Ticks + ".ico");
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
        var pngs = sizes.Select(s =>
        {
            using (var b = IconBitmap(s)) using (var ms = new MemoryStream())
            { b.Save(ms, System.Drawing.Imaging.ImageFormat.Png); return ms.ToArray(); }
        }).ToArray();
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte d = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                w.Write(d); w.Write(d); w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32); w.Write(pngs[i].Length); w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
        }
        return path;
    }

    // Start-menu shortcut (what the user pins) + any pinned copy get the current icon.
    // create=false only refreshes an existing shortcut (icon change / theme change).
    static void UpdateShortcuts(bool create)
    {
        if (!create && !File.Exists(StartLnk)) return;
        try
        {
            string ico = WriteIco();
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic lnk = shell.CreateShortcut(StartLnk);
            lnk.TargetPath = InstalledExe; lnk.Arguments = "--menu"; lnk.IconLocation = ico + ",0";
            lnk.Description = L("app"); lnk.Save();
            if (Directory.Exists(PinnedDir))
                foreach (var p in Directory.GetFiles(PinnedDir, "*.lnk"))
                {
                    dynamic pin = shell.CreateShortcut(p);
                    if (!string.Equals((string)pin.TargetPath, InstalledExe, StringComparison.OrdinalIgnoreCase)) continue;
                    pin.IconLocation = ico + ",0"; pin.Save();
                    SHChangeNotify(0x2000 /*SHCNE_UPDATEITEM*/, 0x5 /*SHCNF_PATHW*/, p, IntPtr.Zero);
                }
            SHChangeNotify(0x08000000 /*SHCNE_ASSOCCHANGED*/, 0, null, IntPtr.Zero);
            foreach (var old in Directory.GetFiles(InstallDir, "icon_*.ico"))
                if (old != ico) try { File.Delete(old); } catch { }
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    // Windows 11 hides new tray icons under the ^ arrow. IsPromoted=1 shows ours next to the clock, 0 keeps it
    // under the arrow (the same setting Windows writes when you drag it yourself).
    static bool PromoteTrayIcon(int promoted)
    {
        bool done = false;
        try
        {
            using (var root = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings"))
            {
                if (root == null) return false;
                foreach (var name in root.GetSubKeyNames())
                    using (var k = root.OpenSubKey(name, true))
                    {
                        var p = k == null ? null : k.GetValue("ExecutablePath") as string;
                        if (p == null || !p.EndsWith(@"\DesktopMenu\DesktopMenu.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        k.SetValue("IsPromoted", promoted, Microsoft.Win32.RegistryValueKind.DWord);
                        done = true;
                    }
            }
        }
        catch { }
        return done;
    }

    // Windows creates our entry a moment after the icon first appears, so retry for up to ~10 s.
    static void PromoteTrayIconSoon(int promoted)
    {
        int tries = 0;
        var t = new System.Windows.Forms.Timer { Interval = 500 };
        t.Tick += (s, e) => { if (PromoteTrayIcon(promoted) || ++tries > 20) { t.Stop(); t.Dispose(); } };
        t.Start();
    }

    // ---- Modern installer windows (custom drawn: rounded, gradient header, bilingual, centered) ----
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

    enum DialogKind { Language, Install, Success, Error, Confirm }

    [DllImport("user32.dll")] static extern IntPtr LoadCursor(IntPtr instance, int id);
    // Windows' own hand pointer (IDC_HAND): follows the user's pointer size/DPI. WinForms' Cursors.Hand is a
    // fixed tiny bitmap that looks shrunken next to the normal arrow on high-DPI screens.
    static readonly Cursor SystemHand = new Cursor(LoadCursor(IntPtr.Zero, 32649));

    // GDI+ has no font fallback, so pick a font that has the script's glyphs.
    static string FontFor(string text)
    {
        if (text.Any(ch => ch >= 0x3040 && ch <= 0x30FF)) return "Yu Gothic UI";       // Japanese kana
        if (text.Any(ch => ch >= 0x4E00 && ch <= 0x9FFF)) return "Microsoft YaHei UI"; // Chinese
        if (text.Any(ch => ch >= 0x0900 && ch <= 0x097F)) return "Nirmala UI";         // Hindi
        return "Segoe UI";
    }

    static bool IsRtlText(string text) { return text.Any(ch => ch >= 0x0600 && ch <= 0x06FF); }

    class Fancy : Form
    {
        const float W = 520, Header = 170;
        readonly DialogKind kind;
        readonly string title;
        readonly string[][] cards;   // Install: {title, description}
        readonly string[] lines;     // messages
        readonly string button;
        public readonly bool[] Checked = { false, true, true };
        public int LangIndex;        // Language page: selected row of LangCodes
        readonly float s;            // DPI scale: everything is drawn in 96-dpi units
        readonly bool light = IsLight("AppsUseLightTheme");
        int hover = -1;              // 0..2 card, 3 button, 4 close, 5 language field

        Color Bg { get { return light ? Color.White : Color.FromArgb(28, 28, 33); } }
        Color Card { get { return light ? Color.FromArgb(245, 246, 249) : Color.FromArgb(38, 38, 45); } }
        Color CardHover { get { return light ? Color.FromArgb(236, 238, 243) : Color.FromArgb(48, 48, 57); } }
        Color Border { get { return light ? Color.FromArgb(221, 224, 230) : Color.FromArgb(62, 62, 72); } }
        Color Fg { get { return light ? Color.FromArgb(26, 26, 31) : Color.FromArgb(242, 242, 245); } }
        Color Sub { get { return light ? Color.FromArgb(107, 111, 122) : Color.FromArgb(154, 154, 168); } }
        static readonly Color Accent1 = Color.FromArgb(0, 120, 212), Accent2 = Color.FromArgb(123, 92, 250);

        public Fancy(DialogKind kind, string title, string[][] cards, string[] lines, string button)
        {
            this.kind = kind; this.title = title; this.cards = cards; this.lines = lines; this.button = button;
            LangIndex = Math.Max(0, Array.IndexOf(LangCodes, Lang));
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true; KeyPreview = true; Text = L("app"); BackColor = Bg;
            try { Icon = TrayIcon(); } catch { }
            using (var g = CreateGraphics()) s = g.DpiX / 96f;
            ClientSize = new Size((int)(W * s), (int)(ButtonRect.Bottom * s + 32 * s));
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; /* CS_DROPSHADOW */ return cp; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int round = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
        }

        float BodyTop { get { return Header + 28; } }
        RectangleF CardRect(int i) { return new RectangleF(40, BodyTop + 52 + i * 76, W - 80, 64); }
        RectangleF FieldRect { get { return new RectangleF(40, BodyTop + 34, W - 80, 56); } }
        RectangleF ButtonRect
        {
            get
            {
                float top = kind == DialogKind.Install ? CardRect(2).Bottom + 28
                    : kind == DialogKind.Language ? FieldRect.Bottom + 28
                    : BodyTop + lines.Length * 52 + 12;
                return new RectangleF(40, top, W - 80, 52);
            }
        }
        RectangleF CloseRect { get { return new RectangleF(14, 14, 32, 32); } }
        bool CanSubmit { get { return kind != DialogKind.Install || Checked.Any(c => c); } }

        int HitTest(Point p)
        {
            var q = new PointF(p.X / s, p.Y / s);
            if (CloseRect.Contains(q)) return 4;
            if (ButtonRect.Contains(q)) return 3;
            if (kind == DialogKind.Install) for (int i = 0; i < 3; i++) if (CardRect(i).Contains(q)) return i;
            if (kind == DialogKind.Language && FieldRect.Contains(q)) return 5;
            return -1;
        }

        void Toggle(int i)
        {
            Checked[i] = !Checked[i];
            if (i == 0 && Checked[0]) Checked[1] = false;   // hidden vs next-to-clock: same tray icon, pick one
            if (i == 1 && Checked[1]) Checked[0] = false;
            Invalidate();
        }

        void SetLang(int i)
        {
            LangIndex = i; Lang = LangCodes[i]; Text = L("app"); Invalidate();
        }

        // The language list: a themed popup menu under the field (same look as the app's menus).
        void OpenLanguages()
        {
            ToolStripManager.Renderer = new ThemeRenderer(light);
            var m = new ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };
            for (int i = 0; i < LangCodes.Length; i++)
            {
                int idx = i;
                var item = new ToolStripMenuItem(LangNames[i], null, (s2, e2) => SetLang(idx))
                    { Checked = i == LangIndex, ShortcutKeyDisplayString = LangEnglish[i] };
                if (FontFor(LangNames[i]) != "Segoe UI") item.Font = new Font(FontFor(LangNames[i]), item.Font.Size);
                m.Items.Add(item);
            }
            m.MinimumSize = new Size((int)(FieldRect.Width * s), 0);
            m.Show(this, new Point((int)(FieldRect.X * s), (int)(FieldRect.Bottom * s + 4 * s)));
        }

        void Activate(int h)
        {
            if (h == 4) DialogResult = DialogResult.Cancel;
            else if (h == 3 && CanSubmit) DialogResult = DialogResult.OK;
            else if (h == 5) OpenLanguages();
            else if (h >= 0 && h < 3) Toggle(h);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Cursor = h >= 0 ? SystemHand : Cursors.Default; Invalidate(); }
        }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Borderless window: drag it by the header.
            if (e.Button == MouseButtons.Left && HitTest(e.Location) < 0 && e.Y < Header * s)
            { ReleaseCapture(); SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, (IntPtr)2 /*HTCAPTION*/, IntPtr.Zero); }
        }
        protected override void OnMouseClick(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Activate(HitTest(e.Location)); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Activate(4);
            else if (e.KeyCode == Keys.Enter) Activate(3);
            else if (kind == DialogKind.Install && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D3) Toggle(e.KeyCode - Keys.D1);
            else if (kind == DialogKind.Language && (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up))
                SetLang((LangIndex + (e.KeyCode == Keys.Down ? 1 : LangCodes.Length - 1)) % LangCodes.Length);
        }

        void Say(Graphics g, string text, float size, FontStyle style, Color c, RectangleF r)
        {
            // Right-to-left only for Arabic-script text, so other punctuation stays at the end ("go?" not "?go").
            // Pixel units (96-dpi) because the Graphics is already scaled by DPI; points would scale twice.
            using (var f = new Font(FontFor(text), size * 96f / 72f, style, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(c))
            using (var sf = new StringFormat(IsRtlText(text) ? StringFormatFlags.DirectionRightToLeft : 0)
                { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(text, f, b, r, sf);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.ScaleTransform(s, s);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Bg);

            // Header: gradient band, round badge, title + brand line.
            var head = new RectangleF(0, 0, W, Header);
            using (var b = new System.Drawing.Drawing2D.LinearGradientBrush(head, Accent1, Accent2, 20f)) g.FillRectangle(b, head);
            using (var b = new SolidBrush(Color.FromArgb(50, 255, 255, 255))) g.FillEllipse(b, W / 2 - 32, 22, 64, 64);
            var state = g.Save();
            g.TranslateTransform(W / 2 - 18, 36);
            using (var p = RoundPen(Color.White, kind == DialogKind.Language ? 3f : 4f))
            {
                if (kind == DialogKind.Install) Chevron(g, Color.White, 36, 4f, .2f, .3f, .68f);
                else if (kind == DialogKind.Success) g.DrawLines(p, new[] { new PointF(7, 19), new PointF(15, 27), new PointF(30, 10) });
                else if (kind == DialogKind.Language)
                {   // globe
                    g.DrawEllipse(p, 4, 4, 28, 28); g.DrawEllipse(p, 12, 4, 12, 28); g.DrawLine(p, 4, 18, 32, 18);
                }
                else { g.DrawLine(p, 18, 6, 18, 21); g.DrawLine(p, 18, 29, 18, 30); }
            }
            g.Restore(state);
            string head1 = kind == DialogKind.Language ? L("chooseLang") : title;
            string head2 = kind == DialogKind.Language && Lang != "en" ? "Choose your language" : "Desktop Menu  ·  v" + AppVersion;
            Say(g, head1, 17f, FontStyle.Bold, Color.White, new RectangleF(0, 94, W, 36));
            Say(g, head2, 10.5f, FontStyle.Regular, Color.FromArgb(225, 255, 255, 255), new RectangleF(0, 128, W, 24));

            // Close (X), top-left.
            if (hover == 4) using (var b = new SolidBrush(Color.FromArgb(45, 255, 255, 255))) g.FillEllipse(b, CloseRect);
            using (var p = RoundPen(Color.White, 2f))
            {
                var c = CloseRect; float m = 11;
                g.DrawLine(p, c.Left + m, c.Top + m, c.Right - m, c.Bottom - m);
                g.DrawLine(p, c.Right - m, c.Top + m, c.Left + m, c.Bottom - m);
            }

            if (kind == DialogKind.Language)
            {
                Say(g, L("language"), 11f, FontStyle.Regular, Sub, new RectangleF(0, BodyTop, W, 26));
                var r = FieldRect;
                Fill(g, hover == 5 ? CardHover : Card, RoundRect(r.X, r.Y, r.Width, r.Height, 14));
                using (var p = new Pen(Accent1, 2f)) using (var path = RoundRect(r.X, r.Y, r.Width, r.Height, 14)) g.DrawPath(p, path);
                Say(g, LangNames[LangIndex], 13f, FontStyle.Bold, Fg, r);
                using (var p = RoundPen(Sub, 2.5f))   // dropdown chevron
                    g.DrawLines(p, new[] { new PointF(r.Right - 40, r.Y + 24), new PointF(r.Right - 32, r.Y + 32), new PointF(r.Right - 24, r.Y + 24) });
            }
            else if (kind == DialogKind.Install)
            {
                Say(g, L("where"), 13.5f, FontStyle.Bold, Fg, new RectangleF(0, BodyTop, W, 30));
                for (int i = 0; i < 3; i++)
                {
                    var r = CardRect(i);
                    Fill(g, hover == i ? CardHover : Card, RoundRect(r.X, r.Y, r.Width, r.Height, 14));
                    using (var p = new Pen(Checked[i] ? Accent1 : Border, Checked[i] ? 2f : 1f))
                    using (var path = RoundRect(r.X, r.Y, r.Width, r.Height, 14)) g.DrawPath(p, path);
                    Say(g, cards[i][0], 12f, FontStyle.Bold, Fg, new RectangleF(r.X + 50, r.Y + 8, r.Width - 100, 26));
                    Say(g, cards[i][1], 9f, FontStyle.Regular, Sub, new RectangleF(r.X + 50, r.Y + 34, r.Width - 100, 20));
                    var dot = new RectangleF(r.Right - 40, r.Y + r.Height / 2 - 12, 24, 24);
                    if (Checked[i])
                    {
                        using (var b = new SolidBrush(Accent1)) g.FillEllipse(b, dot);
                        using (var p = RoundPen(Color.White, 2.4f))
                            g.DrawLines(p, new[] { new PointF(dot.X + 6.5f, dot.Y + 12.5f), new PointF(dot.X + 10.5f, dot.Y + 16.5f), new PointF(dot.X + 17.5f, dot.Y + 8.5f) });
                    }
                    else using (var p = new Pen(Sub, 1.6f)) g.DrawEllipse(p, dot);
                }
            }
            else
                for (int i = 0; i < lines.Length; i++)   // 2 rows per line so long text (e.g. system errors) wraps
                    Say(g, lines[i], 12f, FontStyle.Regular, Fg, new RectangleF(30, BodyTop + i * 52, W - 60, 48));

            // Primary button: gradient pill, lighter on hover, grey when nothing is selected.
            var br = ButtonRect;
            using (var path = RoundRect(br.X, br.Y, br.Width, br.Height, 14))
            {
                if (!CanSubmit) using (var b = new SolidBrush(Border)) g.FillPath(b, path);
                else
                {
                    Color a = Accent1, z = Accent2;
                    if (hover == 3) { a = ControlPaint.Light(a, .25f); z = ControlPaint.Light(z, .25f); }
                    using (var b = new System.Drawing.Drawing2D.LinearGradientBrush(br, a, z, 0f)) g.FillPath(b, path);
                }
            }
            Say(g, kind == DialogKind.Language ? L("next") : button, 12f, FontStyle.Bold, CanSubmit ? Color.White : Sub, br);
        }
    }

    // Installer page 1: language. Saves nothing yet; returns false if closed.
    static bool AskLanguage()
    {
        using (var f = new Fancy(DialogKind.Language, null, null, null, null)) return f.ShowDialog() == DialogResult.OK;
    }

    // Installer page 2: tick where the icon goes. 1 and 2 are the same tray icon (hidden vs shown), so only one of them.
    static bool AskInstall(out bool hidden, out bool clock, out bool start)
    {
        using (var f = new Fancy(DialogKind.Install, L("installTitle"), new[] {
                new[] { L("hidden"), L("hiddenSub") }, new[] { L("clock"), L("clockSub") }, new[] { L("start"), L("startSub") } },
                null, L("install")))
        {
            bool ok = f.ShowDialog() == DialogResult.OK;
            hidden = f.Checked[0]; clock = f.Checked[1]; start = f.Checked[2];
            return ok;
        }
    }

    // Every message the app shows uses the same themed window. Returns true when the main button was pressed.
    static bool ShowMessage(DialogKind kind, string title, string[] lines, string button)
    {
        using (var f = new Fancy(kind, title, null, lines, button)) return f.ShowDialog() == DialogResult.OK;
    }

    static void ShowError(string message) { ShowMessage(DialogKind.Error, L("errTitle"), new[] { message }, L("ok")); }

    // What the success window lists: where the icon ended up.
    static string[] DoneLines(bool hidden, bool clock, bool start)
    {
        var l = new System.Collections.Generic.List<string>();
        if (clock) l.Add(L("doneClock"));
        if (hidden) l.Add(L("doneHidden"));
        if (start) l.Add(L("doneStart"));
        l.Add(L("doneAuto"));
        return l.ToArray();
    }

    // Run from anywhere else (download, USB, WhatsApp): copy into AppData, start at sign-in, launch that copy.
    static bool InstallIfNeeded()
    {
        if (string.Equals(Application.ExecutablePath, InstalledExe, StringComparison.OrdinalIgnoreCase)) return false;
        SetProcessDPIAware(); // installer windows are drawn at real DPI (crisp); the menu app itself stays as before
        Application.EnableVisualStyles();
        bool hidden, clock, start;
        if (!AskLanguage() || !AskInstall(out hidden, out clock, out start)) return true;
        try
        {
            Microsoft.Win32.Registry.SetValue(SettingsKey, "Lang", Lang);
            Microsoft.Win32.Registry.SetValue(SettingsKey, "Tray", hidden || clock ? 1 : 0);
            Microsoft.Win32.Registry.SetValue(SettingsKey, "Promote", clock ? 1 : 0); // applied once by the installed copy
            foreach (var p in Process.GetProcessesByName("DesktopMenu"))
                if (p.Id != Process.GetCurrentProcess().Id) { p.Kill(); p.WaitForExit(3000); } // replace an older running copy
            Directory.CreateDirectory(InstallDir);
            File.Copy(Application.ExecutablePath, InstalledExe, true);
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                k.SetValue("DesktopMenu", "\"" + InstalledExe + "\"");
            DeleteStartLnks(start);
            if (start) UpdateShortcuts(true);
            Process.Start(InstalledExe, "--installed");
            ShowMessage(DialogKind.Success, L("doneTitle"), DoneLines(hidden, clock, start), L("done"));
        }
        catch (Exception ex)
        {
            ShowMessage(DialogKind.Error, L("failTitle"), new[] { ex.Message, L("failHint") }, L("ok"));
        }
        return true;
    }

    static readonly Color Blue = Color.FromArgb(0, 120, 212), Orange = Color.FromArgb(255, 140, 0),
        Green = Color.FromArgb(16, 185, 129), Purple = Color.FromArgb(140, 90, 230), Yellow = Color.FromArgb(255, 196, 60);

    static Pen RoundPen(Color c, float w)
    {
        var p = new Pen(c, w);
        p.StartCap = p.EndCap = System.Drawing.Drawing2D.LineCap.Round;
        p.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
        return p;
    }

    static System.Drawing.Drawing2D.GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // inset: how far the chevron sits from the edges (smaller inside a filled shape).
    static void Chevron(Graphics g, Color c, float n, float w, float side, float top, float bottom)
    {
        using (var p = RoundPen(c, w))
            g.DrawLines(p, new[] { new PointF(n * side, n * bottom), new PointF(n / 2f, n * top), new PointF(n - n * side, n * bottom) });
    }

    static void Fill(Graphics g, Color c, System.Drawing.Drawing2D.GraphicsPath path)
    {
        using (var b = new SolidBrush(c)) using (path) g.FillPath(b, path);
    }

    // fg = colour for the plain white/black icons, so they stay visible on light or dark backgrounds.
    static Bitmap DrawStyle(int style, int size, Color fg)
    {
        var bmp = new Bitmap(size, size);
        float n = size;
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            switch (style)
            {
                case 0: Chevron(g, Blue, n, Math.Max(2f, n / 6f), .2f, .3f, .68f); break;
                case 1: Chevron(g, Orange, n, Math.Max(2f, n / 6f), .2f, .3f, .68f); break;
                case 2:
                    Fill(g, Blue, RoundRect(n * .06f, n * .06f, n * .88f, n * .88f, n * .2f));
                    Chevron(g, Color.White, n, Math.Max(1.5f, n / 8f), .25f, .36f, .64f); break;
                case 3:
                    using (var b = new SolidBrush(Green)) g.FillEllipse(b, n * .05f, n * .05f, n * .9f, n * .9f);
                    Chevron(g, Color.White, n, Math.Max(1.5f, n / 8f), .25f, .36f, .64f); break;
                case 4:
                    using (var p = RoundPen(fg, Math.Max(1.2f, n / 12f)))
                    using (var screen = RoundRect(n * .12f, n * .18f, n * .76f, n * .5f, n * .06f))
                    {
                        g.DrawPath(p, screen);
                        g.DrawLine(p, n * .5f, n * .68f, n * .5f, n * .82f);
                        g.DrawLine(p, n * .32f, n * .84f, n * .68f, n * .84f);
                    }
                    break;
                case 5:
                    Fill(g, Blue, RoundRect(n * .1f, n * .16f, n * .8f, n * .54f, n * .08f));
                    using (var p = RoundPen(fg, Math.Max(1.2f, n / 12f)))
                    {
                        g.DrawLine(p, n * .5f, n * .7f, n * .5f, n * .82f);
                        g.DrawLine(p, n * .32f, n * .84f, n * .68f, n * .84f);
                    }
                    break;
                case 6:
                    foreach (float x in new[] { .14f, .54f })
                        foreach (float y in new[] { .14f, .54f })
                            Fill(g, fg, RoundRect(n * x, n * y, n * .32f, n * .32f, n * .07f));
                    break;
                case 7:
                    Fill(g, Yellow, RoundRect(n * .08f, n * .2f, n * .4f, n * .2f, n * .05f));
                    Fill(g, Yellow, RoundRect(n * .08f, n * .3f, n * .84f, n * .52f, n * .08f));
                    break;
                case 8:
                    using (var p = RoundPen(fg, Math.Max(1.5f, n / 9f)))
                        foreach (float o in new[] { 0f, .26f })
                            g.DrawLines(p, new[] { new PointF(n * (.24f + o), n * .28f), new PointF(n * (.46f + o), n * .5f), new PointF(n * (.24f + o), n * .72f) });
                    break;
                case 10:
                    Fill(g, Purple, RoundRect(n * .06f, n * .06f, n * .88f, n * .88f, n * .2f));
                    Chevron(g, Color.White, n, Math.Max(1.5f, n / 8f), .25f, .36f, .64f); break;
                case 11:
                    using (var b = new SolidBrush(Yellow))
                        g.FillPolygon(b, new[] { new PointF(n * .6f, n * .05f), new PointF(n * .22f, n * .55f), new PointF(n * .48f, n * .55f),
                            new PointF(n * .4f, n * .95f), new PointF(n * .78f, n * .42f), new PointF(n * .52f, n * .42f) });
                    break;
                default:
                    var pts = new PointF[10];
                    for (int i = 0; i < 10; i++)
                    {
                        double r = i % 2 == 1 ? n * .2 : n * .45, a = Math.PI * i / 5 - Math.PI / 2;
                        pts[i] = new PointF((float)(n / 2 + r * Math.Cos(a)), (float)(n / 2 + r * Math.Sin(a)));
                    }
                    using (var b = new SolidBrush(Purple)) g.FillPolygon(b, pts);
                    break;
            }
        }
        return bmp;
    }

    // The chosen icon (built-in or custom) at any size, coloured for the current taskbar theme.
    static Bitmap IconBitmap(int size)
    {
        string custom = CustomIcon();
        return custom != null ? LoadImage(custom, size)
            : DrawStyle(IconStyle(), size, IsLight("SystemUsesLightTheme") ? Color.Black : Color.White);
    }

    static Icon TrayIcon()
    {
        using (var bmp = IconBitmap(SystemInformation.SmallIconSize.Width))
        {
            IntPtr h = bmp.GetHicon();
            var icon = (Icon)Icon.FromHandle(h).Clone();
            DestroyIcon(h);
            return icon;
        }
    }

    class ThemeColors : ProfessionalColorTable
    {
        readonly Color back, hover, border, sep;
        public ThemeColors(bool light)
        {
            UseSystemColors = false;
            back = light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(32, 32, 32);
            hover = light ? Color.FromArgb(225, 225, 225) : Color.FromArgb(61, 61, 61);
            border = light ? Color.FromArgb(200, 200, 200) : Color.FromArgb(70, 70, 70);
            sep = light ? Color.FromArgb(215, 215, 215) : Color.FromArgb(70, 70, 70);
        }
        public override Color ToolStripDropDownBackground { get { return back; } }
        public override Color ImageMarginGradientBegin { get { return back; } }
        public override Color ImageMarginGradientMiddle { get { return back; } }
        public override Color ImageMarginGradientEnd { get { return back; } }
        public override Color MenuBorder { get { return border; } }
        public override Color MenuItemBorder { get { return hover; } }
        public override Color MenuItemSelected { get { return hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return hover; } }
        public override Color MenuItemPressedGradientBegin { get { return hover; } }
        public override Color MenuItemPressedGradientEnd { get { return hover; } }
        public override Color SeparatorDark { get { return sep; } }
        public override Color SeparatorLight { get { return sep; } }
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;

    class ThemeRenderer : ToolStripProfessionalRenderer
    {
        readonly Color text, dim, hover;
        public ThemeRenderer(bool light) : base(new ThemeColors(light))
        {
            RoundedEdges = false;
            text = light ? Color.Black : Color.White;
            dim = light ? Color.Gray : Color.FromArgb(140, 140, 140);
            hover = light ? Color.FromArgb(225, 225, 225) : Color.FromArgb(61, 61, 61);
        }

        // Windows 11 rounds the popup window itself (and draws its own thin border + shadow).
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown)
            {
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(e.ToolStrip.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4);
            }
            base.OnRenderToolStripBackground(e);
        }

        // Skip the old square 1px border on popups; DWM draws a rounded one.
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (!(e.ToolStrip is ToolStripDropDown)) base.OnRenderToolStripBorder(e);
        }

        // Icon grid tiles: rounded hover fill, blue rounded outline on the current icon.
        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var b = e.Item as ToolStripButton;
            if (b == null || !(b.Selected || b.Checked)) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (b.Selected) Fill(e.Graphics, hover, RoundRect(1, 1, b.Width - 2, b.Height - 2, 6));
            if (b.Checked)
                using (var pen = new Pen(Blue, 2))
                using (var path = RoundRect(1, 1, b.Width - 3, b.Height - 3, 6))
                    e.Graphics.DrawPath(pen, path);
        }

        // Hover highlight as a soft rounded pill, inset from the menu edges.
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Fill(e.Graphics, hover, RoundRect(4, 1, e.Item.Width - 8, e.Item.Height - 2, 4));
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? text : dim;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = text;
            base.OnRenderArrow(e);
        }
    }

    // ---- Show desktop on the main screen only (other extended screens are left alone) ----
    delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int max);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);

    const int SW_SHOWMINNOACTIVE = 7, SW_RESTORE = 9, GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80, DWMWA_CLOAKED = 14;
    static readonly string[] ShellClasses = { "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW" };
    static readonly System.Collections.Generic.List<IntPtr> minimized = new System.Collections.Generic.List<IntPtr>();

    // Normal app windows (what you'd see in the taskbar) that sit on the primary screen.
    static bool IsAppWindowOnPrimary(IntPtr h)
    {
        if (!IsWindowVisible(h) || IsIconic(h) || GetWindow(h, 4 /*GW_OWNER*/) != IntPtr.Zero) return false;
        if ((GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0) return false;
        int cloaked;
        if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return false;
        uint pid; GetWindowThreadProcessId(h, out pid);
        if (pid == (uint)Process.GetCurrentProcess().Id) return false;
        var cls = new System.Text.StringBuilder(64); GetClassName(h, cls, 64);
        if (Array.IndexOf(ShellClasses, cls.ToString()) >= 0) return false;
        return Screen.FromHandle(h).Primary;
    }

    // True when the last "show desktop" can still be undone.
    static bool CanRestore() { return minimized.Exists(h => IsWindow(h) && IsIconic(h)); }

    static void ToggleDesktop()
    {
        if (CanRestore())
        {
            // EnumWindows listed them top-first; restore bottom-first so the old top window ends on top.
            for (int i = minimized.Count - 1; i >= 0; i--)
                if (IsWindow(minimized[i]) && IsIconic(minimized[i])) ShowWindowAsync(minimized[i], SW_RESTORE);
            minimized.Clear();
            return;
        }
        minimized.Clear();
        EnumWindows((h, l) => { if (IsAppWindowOnPrimary(h)) minimized.Add(h); return true; }, IntPtr.Zero);
        foreach (var h in minimized) ShowWindowAsync(h, SW_SHOWMINNOACTIVE);
    }

    static ToolStripMenuItem ShowDesktopItem()
    {
        return new ToolStripMenuItem(CanRestore() ? L("restore") : L("showDesktop"), null, (s, e) => ToggleDesktop());
    }

    static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    [STAThread]
    static void Main(string[] args)
    {
        if (InstallIfNeeded()) return;
        bool first;
        using (new Mutex(true, "DesktopMenuTray_Single", out first))
        {
            if (!first)
            {
                // Taskbar click while we're already running: hand the click to the running copy (keeps "show desktop" state).
                if (args.Contains("--menu"))
                {
                    AllowSetForegroundWindow(-1 /*ASFW_ANY*/);
                    try { EventWaitHandle.OpenExisting("DesktopMenu_ShowMenu").Set(); } catch { }
                }
                return;
            }
            Application.EnableVisualStyles();

            var menu = new ContextMenuStrip();
            var settings = new ContextMenuStrip();
            var tray = new NotifyIcon { Icon = TrayIcon(), Text = L("app"), Visible = TrayEnabled() };
            Action refreshIcon = () =>
            {
                var old = tray.Icon; tray.Icon = TrayIcon(); old.Dispose();
                UpdateShortcuts(false);
            };

            // Fresh install: put the tray icon where the user ticked (next to the clock or under the ^ arrow).
            if (args.Contains("--installed") && TrayEnabled()) PromoteTrayIconSoon(Setting("Promote", 1));

            // Plain white/black icons follow the taskbar colour when the Windows theme changes.
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) => refreshIcon();

            // Settings items: tray right-click menu (full), or the Settings submenu when opened from Start/taskbar.
            Action<ToolStripItemCollection, bool> addSettings = (items, full) =>
            {
                bool light = IsLight("AppsUseLightTheme");
                // Icon picker: icons only, 4 per row.
                var grid = new ToolStripDropDown { LayoutStyle = ToolStripLayoutStyle.Table, Padding = new Padding(6), ShowItemToolTips = false };
                ((TableLayoutSettings)grid.LayoutSettings).ColumnCount = 4;
                Color fg = light ? Color.Black : Color.White;
                int current = IconStyle();
                string custom = CustomIcon();
                Func<Image, bool, EventHandler, ToolStripButton> tile = (img, isCurrent, click) =>
                    new ToolStripButton(null, img, click)
                    {
                        DisplayStyle = ToolStripItemDisplayStyle.Image, ImageScaling = ToolStripItemImageScaling.None,
                        Padding = new Padding(6), Margin = new Padding(2), Checked = isCurrent
                    };
                for (int i = 0; i < IconCount; i++)
                {
                    int style = i;
                    grid.Items.Add(tile(DrawStyle(i, 24, fg), custom == null && i == current,
                        (s2, e2) => { SaveIconStyle(style); SaveCustomIcon(null); refreshIcon(); }));
                }
                foreach (var f in CustomIcons())
                {
                    string file = f;
                    grid.Items.Add(tile(LoadImage(file, 24), string.Equals(custom, file, StringComparison.OrdinalIgnoreCase),
                        (s2, e2) => { SaveCustomIcon(file); refreshIcon(); }));
                }
                grid.Items.Add(tile(PlusImage(24, fg), false, (s2, e2) =>
                {
                    string added = AddCustomIcon();
                    if (added != null) { SaveCustomIcon(added); refreshIcon(); }
                }));
                items.Add(new ToolStripMenuItem(L("changeIcon")) { DropDown = grid });
                items.Add(new ToolStripMenuItem(L("openIcons"), null, (s2, e2) => { Directory.CreateDirectory(IconsDir); Open(IconsDir); }));
                if (full) // the desktop menu already has these two
                {
                    items.Add(ShowDesktopItem());
                    items.Add(new ToolStripMenuItem(L("openDesktop"), null, (s2, e2) => Open(UserDesktop)));
                }
                items.Add(new ToolStripSeparator());
                items.Add(new ToolStripMenuItem(L("closeMenu"), null, (s2, e2) => { tray.Visible = false; Application.Exit(); }));
                items.Add(new ToolStripMenuItem(L("uninstall"), null, (s2, e2) => Uninstall(tray)));
            };

            settings.Opening += (s, e) =>
            {
                ToolStripManager.Renderer = new ThemeRenderer(IsLight("AppsUseLightTheme"));
                settings.Items.Clear();
                addSettings(settings.Items, true);
                e.Cancel = false;
            };

            bool viaShortcut = false; // menu opened from the Start/taskbar shortcut, where right-click belongs to Windows

            // Rebuild every time so the menu always matches the current Desktop.
            menu.Opening += (s, e) =>
            {
                ToolStripManager.Renderer = new ThemeRenderer(IsLight("AppsUseLightTheme"));
                menu.Items.Clear();
                Fill(menu.Items, UserDesktop, PublicDesktop);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(ShowDesktopItem());
                menu.Items.Add(new ToolStripMenuItem(L("openDesktop"), null, (s2, e2) => Open(UserDesktop)));
                if (viaShortcut || !TrayEnabled())
                {
                    var more = new ToolStripMenuItem(L("settings"));
                    more.DropDownItems.Add("...");
                    more.DropDownOpening += (s2, e2) => { more.DropDownItems.Clear(); addSettings(more.DropDownItems, false); };
                    menu.Items.Add(more);
                }
                e.Cancel = false;
            };

            // Taking the foreground lets the menu close when you click elsewhere.
            var anchor = new Form();
            IntPtr anchorHandle = anchor.Handle;
            Action<ContextMenuStrip> popUp = m =>
            {
                SetForegroundWindow(anchorHandle);
                var at = Cursor.Position;   // the click is on our icon (tray or pinned taskbar button)
                var area = Screen.FromPoint(at).WorkingArea;
                if (at.Y >= area.Bottom)
                    // Taskbar at the bottom: open on the desktop just above it, menu's right edge at the icon.
                    m.Show(new Point(Math.Min(at.X + 16, area.Right - 8), area.Bottom - 8), ToolStripDropDownDirection.AboveLeft);
                else
                    m.Show(at);   // taskbar on another edge: let Windows place it at the mouse
            };

            // The Start/taskbar shortcut runs "--menu", which signals this event.
            Action showFromShortcut = () => { viaShortcut = true; popUp(menu); };
            var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "DesktopMenu_ShowMenu");
            new Thread(() => { while (true) { showEvent.WaitOne(); anchor.BeginInvoke(showFromShortcut); } }) { IsBackground = true }.Start();
            if (args.Contains("--menu")) anchor.BeginInvoke(showFromShortcut);

            // Left click = desktop items, right click = settings (tray icon has no ContextMenuStrip, so nothing auto-opens).
            tray.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;
                viaShortcut = false;
                popUp(e.Button == MouseButtons.Left ? menu : settings);
            };

            Application.Run();
            tray.Visible = false;
        }
    }
}
