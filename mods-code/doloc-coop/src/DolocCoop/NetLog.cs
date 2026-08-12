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

        /// <summary>高频事件采样记录(每 N 次记一条,避免刷爆文件)。</summary>
        private static int _sampleCounter;

        public static void Sample(int everyN, string line)
        {
            if (++_sampleCounter % everyN == 0) Log(line);
        }
    }
}
