using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Runtime.InteropServices;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TokenPopupForm());
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();
}

internal sealed class TokenPopupForm : Form
{
    private const int PopupWidth = 600;
    private const int PopupHeight = 1040;
    private const int Radius = 34;
    private const int PadX = 42;
    private const int RowHeight = 82;

    private readonly string baseDir;
    private readonly string python;
    private readonly string scanner;
    private readonly string stateJson;
    private readonly string settingsJson;
    private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

    private readonly Label primaryUsage = new Label();
    private readonly Label secondaryUsage = new Label();
    private readonly Panel recentPanel = new Panel();
    private readonly Panel heavyViewport = new Panel();
    private readonly Panel heavyInner = new Panel();
    private readonly Dictionary<int, Button> intervalButtons = new Dictionary<int, Button>();

    private List<RowData> rows = new List<RowData>();
    private int heavyOffset;

    public TokenPopupForm()
    {
        baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        scanner = Path.Combine(baseDir, "codex_token_lights.py");
        stateJson = Path.Combine(baseDir, "tray-state.json");
        settingsJson = Path.Combine(baseDir, "settings.json");
        python = ResolvePythonExecutable(settingsJson);

        Text = "Codex Token Lights";
        Width = PopupWidth;
        Height = PopupHeight;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;
        Font = UiFont("Segoe UI", 22F);

        PositionNearCursor();
        BuildStaticLayout();
        LoadRows();
        RenderData();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width, Height), Radius))
        {
            Region = new Region(path);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = RoundedRect(new Rectangle(1, 1, Width - 3, Height - 3), Radius))
        using (Pen pen = new Pen(ColorTranslator.FromHtml("#D6DCE6"), 2))
        {
            e.Graphics.DrawPath(pen, path);
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Close();
    }

    private void PositionNearCursor()
    {
        Point cursor = Cursor.Position;
        Rectangle area = Screen.FromPoint(cursor).WorkingArea;
        int x = Math.Min(Math.Max(area.Left + 8, cursor.X - PopupWidth / 2), area.Right - PopupWidth - 8);
        int y = cursor.Y - PopupHeight - 12;
        if (y < area.Top + 8)
        {
            y = area.Bottom - PopupHeight - 8;
        }
        if (y < area.Top + 8)
        {
            y = area.Top + 8;
        }
        Location = new Point(x, y);
    }

    private void BuildStaticLayout()
    {
        int contentWidth = Width - (PadX * 2);

        Label title = new Label
        {
            Text = "Codex Token Lights",
            AutoSize = false,
            Location = new Point(PadX, 38),
            Size = new Size(390, 52),
            Font = UiFont("Segoe UI Semibold", 40F),
            ForeColor = ColorTranslator.FromHtml("#111827")
        };
        Controls.Add(title);

        int y = 114;
        Button refresh = MakeButton("Refresh", "#2563EB", "#FFFFFF", 10, 7);
        refresh.Location = new Point(PadX, y);
        refresh.Size = new Size(128, 56);
        refresh.Click += delegate { LoadRows(); RenderData(); };
        Controls.Add(refresh);

        int x = PadX + 136;
        foreach (int seconds in new[] { 5, 10, 30, 60 })
        {
            Button button = MakeButton(seconds + "s", "#EEF0F3", "#111827", 0, 7);
            button.Location = new Point(x, y);
            button.Size = new Size(64, 56);
            int captured = seconds;
            button.Click += delegate
            {
                SaveRefreshSeconds(captured);
                UpdateIntervalButtons();
            };
            intervalButtons[seconds] = button;
            Controls.Add(button);
            x += 70;
        }

        Button exit = MakeButton("Exit", "#FEE2E2", "#991B1B", 0, 7);
        exit.Location = new Point(Width - PadX - 78, y);
        exit.Size = new Size(78, 56);
        exit.Click += delegate { ExitTray(); };
        Controls.Add(exit);

        y = 204;
        Label fiveHour = MakeLabel("5小时", 30F, "#6B7280", false);
        fiveHour.Location = new Point(PadX, y);
        fiveHour.Size = new Size(130, 40);
        Controls.Add(fiveHour);

        primaryUsage.Location = new Point(PadX + 160, y - 4);
        primaryUsage.Size = new Size(contentWidth - 160, 44);
        primaryUsage.TextAlign = ContentAlignment.MiddleRight;
        primaryUsage.Font = UiFont("Segoe UI Semibold", 34F);
        primaryUsage.ForeColor = ColorTranslator.FromHtml("#111827");
        Controls.Add(primaryUsage);

        Label oneWeek = MakeLabel("1周", 30F, "#6B7280", false);
        oneWeek.Location = new Point(PadX, y + 56);
        oneWeek.Size = new Size(130, 40);
        Controls.Add(oneWeek);

        secondaryUsage.Location = new Point(PadX + 160, y + 52);
        secondaryUsage.Size = new Size(contentWidth - 160, 44);
        secondaryUsage.TextAlign = ContentAlignment.MiddleRight;
        secondaryUsage.Font = UiFont("Segoe UI Semibold", 34F);
        secondaryUsage.ForeColor = ColorTranslator.FromHtml("#111827");
        Controls.Add(secondaryUsage);

        Label recent = MakeLabel("Recent", 28F, "#6B7280", true);
        recent.Location = new Point(PadX, 328);
        recent.Size = new Size(contentWidth, 38);
        Controls.Add(recent);

        recentPanel.Location = new Point(PadX, 374);
        recentPanel.Size = new Size(contentWidth, RowHeight * 3);
        recentPanel.BackColor = Color.White;
        Controls.Add(recentPanel);

        Label heavy = MakeLabel("Heavy", 28F, "#6B7280", true);
        heavy.Location = new Point(PadX, 650);
        heavy.Size = new Size(contentWidth, 38);
        Controls.Add(heavy);

        heavyViewport.Location = new Point(PadX, 702);
        heavyViewport.Size = new Size(contentWidth, Height - 760);
        heavyViewport.BackColor = Color.White;
        heavyViewport.MouseWheel += HeavyMouseWheel;
        Controls.Add(heavyViewport);

        heavyInner.Location = new Point(0, 0);
        heavyInner.Width = heavyViewport.Width;
        heavyInner.BackColor = Color.White;
        heavyInner.MouseWheel += HeavyMouseWheel;
        heavyViewport.Controls.Add(heavyInner);

        UpdateIntervalButtons();
    }

    private void LoadRows()
    {
        try
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = python,
                Arguments = "\"" + scanner + "\" --json-path \"" + stateJson + "\" --limit 20",
                WorkingDirectory = baseDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(info))
            {
                process.WaitForExit(5000);
            }

            string json = File.Exists(stateJson) ? File.ReadAllText(stateJson, Encoding.UTF8) : "[]";
            object[] items = serializer.DeserializeObject(json) as object[] ?? new object[0];
            rows = items
                .OfType<Dictionary<string, object>>()
                .Select(RowData.FromDictionary)
                .ToList();
        }
        catch
        {
            rows = new List<RowData>();
        }
    }

    private void RenderData()
    {
        RowData latest = rows.OrderByDescending(row => row.EventTime).FirstOrDefault();
        primaryUsage.Text = latest == null ? "--" : latest.PrimaryRemainingShort + "  " + latest.PrimaryResetShort;
        secondaryUsage.Text = latest == null ? "--" : latest.SecondaryRemainingShort + "  " + latest.SecondaryResetShort;

        recentPanel.Controls.Clear();
        int index = 0;
        foreach (RowData row in rows.OrderByDescending(item => item.EventTime).Take(3))
        {
            recentPanel.Controls.Add(MakeRow(row, true, index++));
        }

        heavyInner.Controls.Clear();
        heavyInner.Height = Math.Max(heavyViewport.Height, rows.Take(12).Count() * RowHeight);
        index = 0;
        foreach (RowData row in rows.Take(12))
        {
            heavyInner.Controls.Add(MakeRow(row, false, index++));
        }
        heavyOffset = 0;
        heavyInner.Top = 0;
        UpdateIntervalButtons();
        Invalidate();
    }

    private static string ResolvePythonExecutable(string settingsJson)
    {
        string envPython = Environment.GetEnvironmentVariable("CODEX_TOKEN_SAVER_PYTHON");
        if (!string.IsNullOrWhiteSpace(envPython) && PythonWorks(envPython, ""))
        {
            return envPython;
        }

        try
        {
            if (File.Exists(settingsJson))
            {
                JavaScriptSerializer json = new JavaScriptSerializer();
                Dictionary<string, object> data = json.DeserializeObject(File.ReadAllText(settingsJson, Encoding.UTF8)) as Dictionary<string, object>;
                if (data != null && data.ContainsKey("python_path"))
                {
                    string configured = Convert.ToString(data["python_path"]);
                    if (!string.IsNullOrWhiteSpace(configured) && PythonWorks(configured, ""))
                    {
                        return configured;
                    }
                }
            }
        }
        catch
        {
        }

        string launcherPython = PythonExecutableFromLauncher("py", "-3");
        if (!string.IsNullOrWhiteSpace(launcherPython)) return launcherPython;

        string pathPython = PythonExecutableFromLauncher("python", "");
        if (!string.IsNullOrWhiteSpace(pathPython)) return pathPython;

        return "python.exe";
    }

    private static bool PythonWorks(string executable, string prefixArgs)
    {
        return !string.IsNullOrWhiteSpace(PythonExecutableFromLauncher(executable, prefixArgs));
    }

    private static string PythonExecutableFromLauncher(string executable, string prefixArgs)
    {
        try
        {
            string args = string.IsNullOrWhiteSpace(prefixArgs)
                ? "-c \"import sys; print(sys.executable)\""
                : prefixArgs + " -c \"import sys; print(sys.executable)\"";
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(info))
            {
                if (process == null) return "";
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(3000);
                return process.ExitCode == 0 ? output : "";
            }
        }
        catch
        {
            return "";
        }
    }

    private Control MakeRow(RowData row, bool includeTime, int index)
    {
        Panel panel = new Panel
        {
            Location = new Point(0, index * RowHeight),
            Size = new Size(heavyViewport.Width, RowHeight),
            BackColor = Color.White
        };
        panel.MouseWheel += HeavyMouseWheel;

        Control lamp = MakeLamp(row.Level);
        panel.Controls.Add(lamp);

        Label name = MakeLabel(ShortText(row.Name, 48), 26F, "#111827", true);
        name.Location = new Point(36, 8);
        name.Size = new Size(panel.Width - 152, 34);
        panel.Controls.Add(name);

        string detail = "cached " + FormatTokens(row.CachedInputTokens) + "  uncached " + FormatTokens(row.UncachedInputTokens);
        if (includeTime) detail = FormatTimeShort(row.EventTime) + "  " + detail;
        Label sub = MakeLabel(ShortText(detail, 46), 19F, "#6B7280", false);
        sub.Location = new Point(36, 48);
        sub.Size = new Size(panel.Width - 152, 28);
        panel.Controls.Add(sub);

        Label tokens = MakeLabel(row.InputTokensShort, 30F, "#111827", true);
        tokens.TextAlign = ContentAlignment.MiddleRight;
        tokens.Location = new Point(panel.Width - 104, 22);
        tokens.Size = new Size(104, 40);
        panel.Controls.Add(tokens);

        Panel line = new Panel
        {
            Location = new Point(0, RowHeight - 1),
            Size = new Size(panel.Width, 1),
            BackColor = ColorTranslator.FromHtml("#EEF0F3")
        };
        panel.Controls.Add(line);

        return panel;
    }

    private void HeavyMouseWheel(object sender, MouseEventArgs e)
    {
        int maxOffset = Math.Max(0, heavyInner.Height - heavyViewport.Height);
        heavyOffset = Math.Max(0, Math.Min(maxOffset, heavyOffset - Math.Sign(e.Delta) * 46));
        heavyInner.Top = -heavyOffset;
    }

    private Button MakeButton(string text, string background, string foreground, int padX, int padY)
    {
        Button button = new RoundedButton
        {
            Text = text,
            BackColor = ColorTranslator.FromHtml(background),
            ForeColor = ColorTranslator.FromHtml(foreground),
            Font = UiFont("Segoe UI", 20F),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Padding = new Padding(padX, padY, padX, padY)
        };
        return button;
    }

    private Label MakeLabel(string text, float size, string color, bool semibold)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            BackColor = Color.White,
            ForeColor = ColorTranslator.FromHtml(color),
            Font = UiFont(semibold ? "Segoe UI Semibold" : "Segoe UI", size)
        };
    }

    private Control MakeLamp(string level)
    {
        Panel lamp = new Panel
        {
            Location = new Point(0, 24),
            Size = new Size(24, 24),
            BackColor = Color.White
        };
        Color color = LevelColor(level);
        lamp.Paint += delegate(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(2, 2, 18, 18);
            using (LinearGradientBrush brush = new LinearGradientBrush(
                rect,
                ControlPaint.LightLight(color),
                color,
                45F))
            using (Pen pen = new Pen(ControlPaint.Dark(color), 1))
            {
                e.Graphics.FillEllipse(brush, rect);
                e.Graphics.DrawEllipse(pen, rect);
            }
            using (SolidBrush highlight = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
            {
                e.Graphics.FillEllipse(highlight, new Rectangle(6, 5, 6, 5));
            }
        };
        return lamp;
    }

    private static Font UiFont(string family, float pixels)
    {
        return new Font(family, pixels, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    private void UpdateIntervalButtons()
    {
        int current = ReadRefreshSeconds();
        foreach (KeyValuePair<int, Button> item in intervalButtons)
        {
            bool selected = item.Key == current;
            item.Value.BackColor = ColorTranslator.FromHtml(selected ? "#DBEAFE" : "#EEF0F3");
            item.Value.ForeColor = ColorTranslator.FromHtml(selected ? "#2563EB" : "#111827");
        }
    }

    private int ReadRefreshSeconds()
    {
        try
        {
            if (!File.Exists(settingsJson)) return 10;
            Dictionary<string, object> data = serializer.DeserializeObject(File.ReadAllText(settingsJson, Encoding.UTF8)) as Dictionary<string, object>;
            if (data == null || !data.ContainsKey("refresh_seconds")) return 10;
            int value = Convert.ToInt32(data["refresh_seconds"]);
            return Math.Max(5, Math.Min(300, value));
        }
        catch
        {
            return 10;
        }
    }

    private void SaveRefreshSeconds(int seconds)
    {
        string json = serializer.Serialize(new Dictionary<string, object> { { "refresh_seconds", seconds } });
        File.WriteAllText(settingsJson, json, Encoding.UTF8);
    }

    private void ExitTray()
    {
        try
        {
            string psBase = baseDir.Replace("'", "''");
            string command = "$base = '" + psBase + "'; " +
                "Get-CimInstance Win32_Process | Where-Object { " +
                "$cmd = $_.CommandLine; " +
                "$cmd -and $cmd.Contains($base) -and (" +
                "($_.Name -eq 'codex_token_popup.exe') -or " +
                "($_.Name -in @('python.exe','pythonw.exe','py.exe') -and $cmd -like '*codex_token_lights.py*') -or " +
                "($_.Name -eq 'powershell.exe' -and $cmd -like '*codex_token_tray.ps1*')" +
                ") " +
                "} | ForEach-Object { Stop-Process -Id $_.ProcessId }";
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(info);
        }
        catch
        {
        }
        Close();
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color LevelColor(string level)
    {
        if (level == "red") return ColorTranslator.FromHtml("#EF4444");
        if (level == "yellow") return ColorTranslator.FromHtml("#EAB308");
        return ColorTranslator.FromHtml("#22C55E");
    }

    private static string ShortText(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown";
        return text.Length <= max ? text : text.Substring(0, Math.Max(0, max - 3)) + "...";
    }

    private static string FormatTokens(long value)
    {
        if (value >= 1000000) return (value / 1000000.0).ToString("0.0") + "m";
        if (value >= 10000) return Math.Round(value / 1000.0).ToString("0") + "k";
        if (value >= 1000) return (value / 1000.0).ToString("0.0") + "k";
        return value.ToString();
    }

    private static string FormatTimeShort(double epoch)
    {
        try
        {
            DateTimeOffset dt = DateTimeOffset.FromUnixTimeSeconds((long)epoch).ToLocalTime();
            return dt.ToString("HH:mm");
        }
        catch
        {
            return "--";
        }
    }
}

internal sealed class RowData
{
    public string ThreadId;
    public string Name;
    public string Level;
    public long InputTokens;
    public string InputTokensShort;
    public long CachedInputTokens;
    public long UncachedInputTokens;
    public double EventTime;
    public string PrimaryRemainingShort;
    public string PrimaryResetShort;
    public string SecondaryRemainingShort;
    public string SecondaryResetShort;

    public static RowData FromDictionary(Dictionary<string, object> data)
    {
        return new RowData
        {
            ThreadId = StringValue(data, "thread_id"),
            Name = StringValue(data, "name"),
            Level = StringValue(data, "level"),
            InputTokens = LongValue(data, "input_tokens"),
            InputTokensShort = StringValue(data, "input_tokens_short"),
            CachedInputTokens = LongValue(data, "cached_input_tokens"),
            UncachedInputTokens = LongValue(data, "uncached_input_tokens"),
            EventTime = DoubleValue(data, "event_time"),
            PrimaryRemainingShort = StringValue(data, "primary_remaining_short", "--"),
            PrimaryResetShort = StringValue(data, "primary_reset_short", "--"),
            SecondaryRemainingShort = StringValue(data, "secondary_remaining_short", "--"),
            SecondaryResetShort = StringValue(data, "secondary_reset_short", "--")
        };
    }

    private static string StringValue(Dictionary<string, object> data, string key, string fallback = "")
    {
        if (!data.ContainsKey(key) || data[key] == null) return fallback;
        string text = Convert.ToString(data[key]);
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static long LongValue(Dictionary<string, object> data, string key)
    {
        try
        {
            return data.ContainsKey(key) && data[key] != null ? Convert.ToInt64(data[key]) : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static double DoubleValue(Dictionary<string, object> data, string key)
    {
        try
        {
            return data.ContainsKey(key) && data[key] != null ? Convert.ToDouble(data[key]) : 0;
        }
        catch
        {
            return 0;
        }
    }
}

internal sealed class RoundedButton : Button
{
    private bool hovering;
    private bool pressing;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovering = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovering = false;
        pressing = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        pressing = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        pressing = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color parentBack = Parent == null ? Color.White : Parent.BackColor;
        e.Graphics.Clear(parentBack);

        Color fill = BackColor;
        if (pressing)
        {
            fill = ControlPaint.Dark(fill, 0.05F);
        }
        else if (hovering)
        {
            fill = ControlPaint.Light(fill, 0.08F);
        }

        Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = RoundedRect(rect, 10))
        using (SolidBrush brush = new SolidBrush(fill))
        {
            e.Graphics.FillPath(brush, path);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            rect,
            ForeColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPadding);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
