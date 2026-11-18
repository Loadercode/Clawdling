using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Clawdling
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try { N.SetProcessDPIAware(); } catch { }
            if (args.Length > 0)
            {
                string a = args[0].ToLowerInvariant().TrimStart('-', '/');
                if (a == "hook") return HookClient.RunHook();
                if (a == "version") { HookClient.WriteOut("Clawdling 1.1.0\n"); return 0; }
                if (a == "notify")
                {
                    HookClient.Send(args.Length > 3 ? args[3] : "info", args.Length > 2 ? args[2] : "Claude", args.Length > 1 ? args[1] : "");
                    return 0;
                }
                if (a == "ask")
                {
                    string r = HookClient.Send("ask", "Разрешить действие?", args.Length > 1 ? args[1] : "");
                    HookClient.WriteOut(r + "\n");
                    return r == "allow" ? 0 : 1;
                }
                if (a == "play") { HookClient.Send("play", args.Length > 1 ? args[1] : "", ""); return 0; }
                if (a == "codex") return HookClient.RunCodex(args.Length > 1 ? args[args.Length - 1] : "");
                if (a == "makeicon" && args.Length > 1) { IconMaker.SaveIco(args[1]); return 0; }
            }
            bool created;
            var mtx = new Mutex(true, "Local\\Clawdling_Instance_v1", out created);
            if (!created) { HookClient.Send("say", "", "Я уже здесь!"); return 0; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Pet());
            GC.KeepAlive(mtx);
            return 0;
        }
    }

    class Cfg
    {
        public bool NewYear, Snow = true, Hidden, Codex = true;
        public int Size = 1;
        static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clawdling"); } }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }
        public static Cfg Load()
        {
            var c = new Cfg();
            try
            {
                if (File.Exists(FilePath))
                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        int i = line.IndexOf('='); if (i < 0) continue;
                        string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                        if (k == "newyear") c.NewYear = v == "1";
                        else if (k == "snow") c.Snow = v == "1";
                        else if (k == "codex") c.Codex = v == "1";
                        else if (k == "size") { int s; if (int.TryParse(v, out s)) c.Size = Math.Max(0, Math.Min(2, s)); }
                    }
            }
            catch { }
            return c;
        }
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, "newyear=" + (NewYear ? 1 : 0) + "\r\nsnow=" + (Snow ? 1 : 0) + "\r\nsize=" + Size + "\r\ncodex=" + (Codex ? 1 : 0) + "\r\n");
            }
            catch { }
        }
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool Autostart
        {
            get { try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("Clawdling") != null; } catch { return false; } }
            set
            {
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                        if (value) k.SetValue("Clawdling", "\"" + Application.ExecutablePath + "\"");
                        else k.DeleteValue("Clawdling", false);
                }
                catch { }
            }
        }
    }

    /// <summary>Local named pipe: hook / CLI processes talk to the running pet through it.</summary>
    class PipeServer
    {
        public static string Name { get { return "Clawdling_v1_" + Environment.UserName; } }
        readonly Pet pet;
        public PipeServer(Pet p) { pet = p; }
        public void Start() { var th = new Thread(Loop); th.IsBackground = true; th.Start(); }
        void Loop()
        {
            while (true)
            {
                NamedPipeServerStream srv = null;
                try
                {
                    srv = new NamedPipeServerStream(Name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.None);
                    srv.WaitForConnection();
                }
                catch { if (srv != null) srv.Dispose(); Thread.Sleep(1000); continue; }
                var conn = srv;
                var th = new Thread(() => Handle(conn)); th.IsBackground = true; th.Start();
            }
        }
        void Handle(NamedPipeServerStream c)
        {
            try
            {
                var enc = new UTF8Encoding(false);
                var r = new StreamReader(c, enc);
                string line = r.ReadLine();
                if (line == null) return;
                string[] p = line.Split('\x1F');
                string type = p[0];
                string title = p.Length > 1 ? p[1].Replace('\x1E', '\n') : "";
                string text = p.Length > 2 ? p[2].Replace('\x1E', '\n') : "";
                string ans = "ok";
                if (type == "ask")
                {
                    var ev = new ManualResetEvent(false); string res = "none";
                    pet.BeginInvoke(new Action(() => pet.Notify("ask", title, text, a => { res = a; ev.Set(); })));
                    ev.WaitOne(55000);
                    ans = res;
                }
                else pet.BeginInvoke(new Action(() => pet.Notify(type, title, text, null)));
                var w = new StreamWriter(c, enc);
                w.WriteLine(ans); w.Flush();
                try { c.WaitForPipeDrain(); } catch { }
            }
            catch { }
            finally { c.Dispose(); }
        }
    }

    static class HookClient
    {
        static string Clean(string s) { return s == null ? "" : s.Replace("\r", "").Replace('\n', '\x1E').Replace('\x1F', ' '); }

        public static string Send(string type, string title, string text)
        {
            string line = type + "\x1F" + Clean(title) + "\x1F" + Clean(text);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using (var c = new NamedPipeClientStream(".", PipeServer.Name, PipeDirection.InOut))
                    {
                        c.Connect(attempt == 0 ? 600 : 8000);
                        var enc = new UTF8Encoding(false);
                        var w = new StreamWriter(c, enc); w.WriteLine(line); w.Flush();
                        var r = new StreamReader(c, enc);
                        string ans = r.ReadLine();
                        return ans ?? "none";
                    }
                }
                catch
                {
                    if (attempt == 0)
                    {
                        // pet not running: start it (ShellExecute => it does not inherit our stdio pipes)
                        try { var psi = new ProcessStartInfo(Application.ExecutablePath); psi.UseShellExecute = true; Process.Start(psi); }
                        catch { return "none"; }
                    }
                }
            }
            return "none";
        }

        /// <summary>OpenAI Codex CLI "notify" program: gets one JSON argument, e.g. {"type":"agent-turn-complete","last-assistant-message":"..."}.</summary>
        public static int RunCodex(string json)
        {
            string type = "", msg = "";
            try
            {
                var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (d != null)
                {
                    object o;
                    if (d.TryGetValue("type", out o) && o != null) type = o.ToString();
                    if (d.TryGetValue("last-assistant-message", out o) && o != null) msg = o.ToString();
                }
            }
            catch { }
            msg = msg.Replace("\r", " ").Replace("\n", " ").Trim();
            if (msg.Length > 90) msg = msg.Substring(0, 87).TrimEnd() + "...";
            if (type == "agent-turn-complete" || type == "") Send("codex-done", "Codex: готово!", msg.Length > 0 ? msg : "Я закончил работу");
            else Send("codex-info", "Codex", type);
            return 0;
        }

        public static void WriteOut(string s)
        {
            try { var o = Console.OpenStandardOutput(); var b = new UTF8Encoding(false).GetBytes(s); o.Write(b, 0, b.Length); o.Flush(); } catch { }
        }

        static string ReadStdin()
        {
            string result = "";
            var th = new Thread(() =>
            {
                try { using (var st = Console.OpenStandardInput()) using (var r = new StreamReader(st, new UTF8Encoding(false))) result = r.ReadToEnd(); }
                catch { }
            });
            th.IsBackground = true; th.Start(); th.Join(3000);
            return result ?? "";
        }

        static string S(Dictionary<string, object> d, string k)
        {
            object v; return d != null && d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        static string Translate(string msg, string nt)
        {
            string low = msg.ToLowerInvariant();
            if (low.Contains("permission"))
            {
                int i = msg.IndexOf("to use ", StringComparison.OrdinalIgnoreCase);
                string tool = i >= 0 ? msg.Substring(i + 7).Trim() : "";
                return "Нужно твоё подтверждение" + (tool != "" ? ": " + tool : "");
            }
            if (low.Contains("waiting for your input") || nt == "idle_prompt") return "Жду твоего ответа";
            return msg == "" ? "Нужно твоё внимание" : msg;
        }

        public static int RunHook()
        {
            string input = ReadStdin();
            Dictionary<string, object> d = null;
            try { d = new JavaScriptSerializer().DeserializeObject(input) as Dictionary<string, object>; } catch { }
            if (d == null) // tolerate unescaped Windows paths (C:\Users\...) from hand-written input
                try { d = new JavaScriptSerializer().DeserializeObject(input.Replace("\\", "\\\\")) as Dictionary<string, object>; } catch { }
            if (d == null) d = new Dictionary<string, object>();
            string ev = S(d, "hook_event_name");
            string cwd = S(d, "cwd"), proj = "";
            try { if (cwd != "") proj = Path.GetFileName(cwd.TrimEnd('\\', '/')); } catch { }
            string where = proj != "" ? "\n«" + proj + "»" : "";

            if (ev == "Stop")
                Send("done", "Готово!", "Я закончил работу" + where);
            else if (ev == "Notification")
                Send("attn", "Claude ждёт тебя", Translate(S(d, "message"), S(d, "notification_type")) + where);
            else if (ev == "PermissionRequest")
            {
                string tool = S(d, "tool_name"), sum = "";
                object tiObj; Dictionary<string, object> ti = d.TryGetValue("tool_input", out tiObj) ? tiObj as Dictionary<string, object> : null;
                if (ti != null)
                    foreach (var key in new[] { "command", "file_path", "url", "pattern", "path", "description", "prompt" })
                    {
                        object v; if (ti.TryGetValue(key, out v) && v != null && v.ToString() != "") { sum = v.ToString(); break; }
                    }
                sum = sum.Replace("\r", " ").Replace("\n", " ");
                if (sum.Length > 160) sum = sum.Substring(0, 160) + "…";
                string ans = Send("ask", "Разрешить действие?", tool + (sum != "" ? ": " + sum : "") + where);
                if (ans == "allow")
                    WriteOut("{\"hookSpecificOutput\":{\"hookEventName\":\"PermissionRequest\",\"decision\":{\"behavior\":\"allow\"}}}");
                else if (ans == "deny")
                    WriteOut("{\"hookSpecificOutput\":{\"hookEventName\":\"PermissionRequest\",\"decision\":{\"behavior\":\"deny\",\"message\":\"Denied by the user via Clawdling\"}}}");
                // "none" => print nothing, Claude Code shows its usual dialog
            }
            else if (ev != "")
                Send("info", "Claude", ev);
            return 0;
        }
    }

    static class IconMaker
    {
        static readonly Color Body = Color.FromArgb(255, 217, 119, 87), Dark = Color.FromArgb(255, 184, 92, 62), Eye = Color.FromArgb(255, 35, 24, 21);

        public static Bitmap Render(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.PixelOffsetMode = PixelOffsetMode.Half;
                float c = size / 16f;
                using (var b = new SolidBrush(Body)) using (var d = new SolidBrush(Dark)) using (var e = new SolidBrush(Eye))
                {
                    Action<Brush, float, float, float, float> R = (br, x, y, w, h) =>
                        g.FillRectangle(br, (float)Math.Round(x * c), (float)Math.Round(y * c), (float)Math.Round((x + w) * c) - (float)Math.Round(x * c), (float)Math.Round((y + h) * c) - (float)Math.Round(y * c));
                    R(b, 2, 3, 12, 8);
                    R(d, 2, 10, 12, 1);
                    R(b, 0, 6, 2, 2); R(b, 14, 6, 2, 2);
                    R(d, 3, 11, 1, 2); R(d, 5, 11, 1, 2); R(d, 10, 11, 1, 2); R(d, 12, 11, 1, 2);
                    R(e, 5, 5, 1, 2); R(e, 10, 5, 1, 2);
                }
            }
            return bmp;
        }

        public static Icon TrayIcon(int size)
        {
            using (var bmp = Render(size)) return Icon.FromHandle(bmp.GetHicon());
        }

        public static void SaveIco(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 256 };
            var pngs = new List<byte[]>();
            foreach (int s in sizes)
                using (var bmp = Render(s)) using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (var fs = File.Create(path)) using (var bw = new BinaryWriter(fs))
            {
                bw.Write((short)0); bw.Write((short)1); bw.Write((short)sizes.Length);
                int off = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    int s = sizes[i];
                    bw.Write((byte)(s >= 256 ? 0 : s)); bw.Write((byte)(s >= 256 ? 0 : s));
                    bw.Write((byte)0); bw.Write((byte)0); bw.Write((short)1); bw.Write((short)32);
                    bw.Write(pngs[i].Length); bw.Write(off); off += pngs[i].Length;
                }
                foreach (var p in pngs) bw.Write(p);
            }
        }
    }
}
