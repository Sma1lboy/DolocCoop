using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace DolocShared
{
    /// <summary>
    /// 帧循环驱动器 —— 往 Unity 的 PlayerLoop 里插一个 update 回调。
    ///
    /// 为什么不用 MonoBehaviour:多洛可小镇会销毁 Mod 创建的 GameObject
    /// (连 DontDestroyOnLoad 都保不住,由 0.5.0 诊断探针实测确认),
    /// 导致挂在上面的 Start/Update 永远不执行。PlayerLoop 注入不依赖任何 GameObject。
    /// DTMAPI 也是这个思路(其日志写着 source=PlayerLoop.FirstFrame)。
    ///
    /// 本文件被 DolocCoop 与 DolocDevTools 两个工程共同引用。
    /// </summary>
    public static class UnityLoopDriver
    {
        private struct DolocModUpdate { }

        private static readonly List<Action> Callbacks = new List<Action>();
        private static Action<string> _log = _ => { };
        private static bool _installed;

        public static bool Installed => _installed;

        public static void Add(Action callback)
        {
            if (callback != null) Callbacks.Add(callback);
        }

        public static void Install(Action<string> log = null)
        {
            if (log != null) _log = log;
            if (_installed) return;
            try
            {
                var loop = PlayerLoop.GetCurrentPlayerLoop();
                var system = new PlayerLoopSystem
                {
                    type = typeof(DolocModUpdate),
                    updateDelegate = Tick
                };

                bool inserted = false;
                for (int i = 0; i < loop.subSystemList.Length; i++)
                {
                    if (loop.subSystemList[i].type == typeof(Update))
                    {
                        var subs = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList) { system };
                        loop.subSystemList[i].subSystemList = subs.ToArray();
                        inserted = true;
                        break;
                    }
                }
                if (!inserted)
                {
                    var roots = new List<PlayerLoopSystem>(loop.subSystemList) { system };
                    loop.subSystemList = roots.ToArray();
                }

                PlayerLoop.SetPlayerLoop(loop);
                _installed = true;
                _log($"[LoopDriver] 已注入 PlayerLoop ({(inserted ? "Update 阶段" : "根末尾")})");
            }
            catch (Exception e)
            {
                _log("[LoopDriver] 注入失败: " + e);
            }
        }

        private static void Tick()
        {
            for (int i = 0; i < Callbacks.Count; i++)
            {
                try { Callbacks[i](); }
                catch (Exception e)
                {
                    _log($"[LoopDriver] 回调异常(已移除): {e}");
                    Callbacks.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}
