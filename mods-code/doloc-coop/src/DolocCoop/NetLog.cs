using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 联机会话日志。写到 LocalLow\...\DolocTown\DolocCoop-debug\net-&lt;PID&gt;.log
    /// —— 按进程号分文件,这样本机双开测试时两端的日志不会互相覆盖,可以对照阅读。
    /// </summary>
    public static class NetLog
    {
        private static StreamWriter _w;
        private static readonly object Lock = new object();

        public static string Path_ { get; private set; }

        private static void Ensure()
        {
            if (_w != null) return;
            try
            {
                string dir = System.IO.Path.Combine(Application.persistentDataPath, "DolocCoop-debug");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                int pid = Process.GetCurrentProcess().Id;
                Path_ = System.IO.Path.Combine(dir, $"net-{pid}.log");
                _w = new StreamWriter(Path_, append: true, Encoding.UTF8) { AutoFlush = true };
                _w.WriteLine();
                _w.WriteLine($"===== DolocCoop 会话 PID={pid} 启动 {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
            }
            catch { /* 日志失败不影响游戏 */ }
        }

        public static void Log(string line)
        {
            lock (Lock)
            {
                Ensure();
                try { _w?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {line}"); } catch { }
            }
        }

        /// <summary>
        /// 高频事件采样记录(每 N 次记一条,避免刷爆文件)。
        /// 计数器按 key 分开 —— 用同一个全局计数器时,高频调用点会把低频调用点的
        /// 采样位置挤掉,导致后者几乎永远不落盘(排查 WorldSync 时被坑过)。
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, int> Counters
            = new System.Collections.Generic.Dictionary<string, int>();

        public static void Sample(string key, int everyN, string line)
        {
            lock (Lock)
            {
                Counters.TryGetValue(key, out int n);
                n++;
                Counters[key] = n;

                // 首次必记,之后才采样。
                // 采样的目的只是防刷屏,不该把"这件事开始发生了"这个最关键的信号吃掉 ——
                // 只发生一次的事件(比如地上没东西时只广播一次)在纯取模采样下
                // 永远达不到阈值,日志里一片空白,看起来就像功能没跑。
                // 这个坑在排查 WorldSync / ActionSync / DropItemSync 时连踩了三次。
                if (n != 1 && n % everyN != 0) return;
            }
            Log(line);
        }

        /// <summary>兼容旧调用:用调用内容的前缀当 key。</summary>
        public static void Sample(int everyN, string line)
        {
            int sp = line.IndexOf(' ');
            Sample(sp > 0 ? line.Substring(0, sp) : line, everyN, line);
        }
    }
}
