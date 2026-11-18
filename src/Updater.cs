// Self-update from GitHub releases: check the latest release, download Clawdling.exe,
// verify it, swap it with the running exe (Windows allows renaming a running exe) and restart.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Clawdling
{
    static class Updater
    {
        const string Repo = "Loadercode/Clawdling";
        const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";
        const string DownloadPrefix = "https://github.com/" + Repo + "/releases/download/";
        const string AssetName = "Clawdling.exe";

        public class Release { public string Tag; public Version Ver; public string Url; public long Size; }

        public static Version Current
        {
            get { var v = Assembly.GetExecutingAssembly().GetName().Version; return new Version(v.Major, v.Minor, Math.Max(0, v.Build)); }
        }
        public static string CurrentText { get { return Current.ToString(3); } }

        static string ExePath { get { return Application.ExecutablePath; } }
        static string NewPath { get { return Path.Combine(Path.GetDirectoryName(ExePath), "Clawdling.new.exe"); } }
        static string OldPath { get { return ExePath + ".old"; } }

        static HttpWebRequest Req(string url)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            var r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "Clawdling-Updater";
            r.Timeout = 20000; r.ReadWriteTimeout = 30000;
            return r;
        }

        /// <summary>Latest published release that carries Clawdling.exe, or null.</summary>
        public static Release FetchLatest()
        {
            var r = Req(Api);
            r.Accept = "application/vnd.github+json";
            string json;
            using (var resp = r.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream())) json = sr.ReadToEnd();
            var d = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (d == null) return null;
            object o;
            string tag = d.TryGetValue("tag_name", out o) && o != null ? o.ToString() : "";
            Version ver;
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out ver)) return null;
            ver = new Version(ver.Major, ver.Minor, Math.Max(0, ver.Build));
            if (!d.TryGetValue("assets", out o) || !(o is IEnumerable)) return null;
            foreach (var a in (IEnumerable)o)
            {
                var ad = a as Dictionary<string, object>;
                if (ad == null || !ad.TryGetValue("name", out o) || (o ?? "").ToString() != AssetName) continue;
                string url = ad.TryGetValue("browser_download_url", out o) && o != null ? o.ToString() : "";
                long size = ad.TryGetValue("size", out o) && o != null ? Convert.ToInt64(o) : 0;
                if (!url.StartsWith(DownloadPrefix, StringComparison.Ordinal)) return null; // only our own release files
                return new Release { Tag = tag, Ver = ver, Url = url, Size = size };
            }
            return null;
        }

        public static bool IsNewer(Release r) { return r != null && r.Ver > Current; }

        /// <summary>Downloads and verifies the release exe next to the running one. Returns its path.</summary>
        public static string Download(Release r)
        {
            string tmp = NewPath;
            try { File.Delete(tmp); } catch { }
            using (var resp = Req(r.Url).GetResponse())
            using (var src = resp.GetResponseStream())
            using (var dst = File.Create(tmp))
            {
                var buf = new byte[65536]; int n; long total = 0;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    total += n;
                    if (total > 20 * 1024 * 1024) throw new IOException("file too large");
                    dst.Write(buf, 0, n);
                }
            }
            if (!Verify(tmp, r)) { try { File.Delete(tmp); } catch { } throw new IOException("downloaded file failed verification"); }
            return tmp;
        }

        static bool Verify(string path, Release r)
        {
            var fi = new FileInfo(path);
            if (fi.Length < 32 * 1024 || (r.Size > 0 && fi.Length != r.Size)) return false;
            using (var f = File.OpenRead(path)) { if (f.ReadByte() != 'M' || f.ReadByte() != 'Z') return false; }
            try
            {
                var an = AssemblyName.GetAssemblyName(path);
                if (an.Name != Assembly.GetExecutingAssembly().GetName().Name) return false;
            }
            catch { return false; }
            // the new exe must report exactly the released version
            var psi = new ProcessStartInfo(path, "version") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using (var p = Process.Start(psi))
            {
                string outp = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(10000)) { try { p.Kill(); } catch { } return false; }
                return outp.Trim() == "Clawdling " + r.Ver.ToString(3);
            }
        }

        /// <summary>Swaps the running exe with the downloaded one. Rolls back on failure.</summary>
        public static bool Install(string newPath)
        {
            string exe = ExePath, old = OldPath;
            try { if (File.Exists(old)) File.Delete(old); } catch { }
            try { File.Move(exe, old); }
            catch { return false; }
            try { File.Move(newPath, exe); return true; }
            catch
            {
                try { File.Move(old, exe); } catch { }
                return false;
            }
        }

        public static void Relaunch(string fromVersion)
        {
            Process.Start(new ProcessStartInfo(ExePath, "updated " + (fromVersion ?? "")) { UseShellExecute = false });
        }

        /// <summary>Removes leftovers of a previous update (the old exe may still be locked for a moment).</summary>
        public static void CleanupLater()
        {
            var th = new Thread(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        if (File.Exists(OldPath)) File.Delete(OldPath);
                        if (!File.Exists(OldPath)) break;
                    }
                    catch { }
                    Thread.Sleep(1500);
                }
            });
            th.IsBackground = true; th.Start();
        }

        /// <summary>CLI: Clawdling.exe update</summary>
        public static int RunCli()
        {
            try
            {
                HookClient.WriteOut("Clawdling " + CurrentText + ": проверяю обновления...\n");
                var r = FetchLatest();
                if (r == null) { HookClient.WriteOut("Не нашёл релиз с Clawdling.exe\n"); return 1; }
                if (!IsNewer(r)) { HookClient.WriteOut("Уже последняя версия (" + r.Tag + ")\n"); return 0; }
                HookClient.WriteOut("Скачиваю " + r.Tag + "...\n");
                string p = Download(r);
                if (!Install(p)) { HookClient.WriteOut("Не смог заменить exe: нет доступа к папке\n"); return 1; }
                HookClient.WriteOut("Обновлено до " + r.Tag + "\n");
                // restart the running pet only if it runs from this very exe
                HookClient.Send("restart", CurrentText, ExePath);
                return 0;
            }
            catch (Exception e) { HookClient.WriteOut("Ошибка обновления: " + e.Message + "\n"); return 1; }
        }
    }

    partial class Pet
    {
        float updT = 45, updGreetT; volatile bool updBusy; string pendingUpdate;
        ToolStripMenuItem miAutoUpd;

        /// <summary>Called from the main pet's Tick.</summary>
        void TickUpdates(float dt)
        {
            if (updGreetT > 0 && (updGreetT -= dt) <= 0) GreetAfterUpdate();
            if (pendingUpdate != null && !(bubble.Active && bubble.Kind == 3) && !dragging) { var p = pendingUpdate; pendingUpdate = null; ApplyUpdate(p); return; }
            updT -= dt;
            if (updT > 0) return;
            updT = 6 * 3600;
            if (cfg.AutoUpdate) StartUpdateCheck(false);
        }

        void StartUpdateCheck(bool manual)
        {
            if (updBusy) { if (manual) Say("Уже проверяю...", 1.4f); return; }
            updBusy = true;
            if (manual) Say("Проверяю обновления...", 1.6f);
            var th = new Thread(() =>
            {
                try
                {
                    var r = Updater.FetchLatest();
                    if (!Updater.IsNewer(r))
                    {
                        if (manual) UI(() => { Go(St.Happy, 1f); Say("У меня последняя версия (" + Updater.CurrentText + ")", 2f); });
                        return;
                    }
                    UI(() => Say("Вышла версия " + r.Tag + "! Обновляюсь...", 2.2f));
                    string path = Updater.Download(r);
                    UI(() => ApplyUpdate(path));
                }
                catch
                {
                    if (manual) UI(() => { Go(St.Sad, 1.2f); Say("Не получилось проверить обновления", 2f); });
                }
                finally { updBusy = false; }
            });
            th.IsBackground = true; th.Start();
        }

        void UI(Action a) { try { if (!IsDisposed) BeginInvoke(a); } catch { } }

        void ApplyUpdate(string path)
        {
            // never interrupt a pending permission question or a drag
            if ((bubble.Active && bubble.Kind == 3) || dragging) { pendingUpdate = path; return; }
            if (!Updater.Install(path)) { Go(St.Sad, 1.2f); Say("Не смог обновиться: нет доступа к папке", 2.4f); return; }
            string from = Updater.CurrentText;
            ExitApp(() => { Program.ReleaseInstance(); Updater.Relaunch(from); });
        }

        void RestartFromCli(string fromVersion, string exePath)
        {
            if (!string.Equals(Path.GetFullPath(exePath ?? ""), Path.GetFullPath(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase)) return;
            ExitApp(() => { Program.ReleaseInstance(); Updater.Relaunch(fromVersion); });
        }

        void GreetAfterUpdate()
        {
            if (Program.UpdatedFrom == null) return;
            Updater.CleanupLater();
            if (OnGround) Go(St.Celebrate, 1.8f);
            Say("Я обновился до версии " + Updater.CurrentText + "!", 2.6f);
            if (buddy != null) buddy.SayLater(2.2f, U.Pick("Ого, новая версия!", "А я тоже новый?", "Чувствую себя свежим!"));
            Program.UpdatedFrom = null;
        }

        ToolStripMenuItem BuildUpdateMenu()
        {
            var m = new ToolStripMenuItem("Обновления");
            miAutoUpd = new ToolStripMenuItem("Обновлять автоматически", null, (s, e) => { cfg.AutoUpdate = !cfg.AutoUpdate; cfg.Save(); });
            m.DropDownItems.Add(miAutoUpd);
            m.DropDownItems.Add("Проверить сейчас", null, (s, e) => StartUpdateCheck(true));
            m.DropDownItems.Add(new ToolStripSeparator());
            m.DropDownItems.Add(new ToolStripMenuItem("Версия " + Updater.CurrentText) { Enabled = false });
            return m;
        }
    }
}
