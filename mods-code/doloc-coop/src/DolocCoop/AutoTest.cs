using System;
using System.IO;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 无人值守自测钩子。
    ///
    /// 为什么需要:游戏只能单开,而且开发者(或 AI 助手)无法在游戏里按键,
    /// 所以留一个"放个标记文件就自动跑流程"的入口,配合 tools/CoopSimClient
    /// 就能在无人操作的情况下验证整条联机链路。
    ///
    /// 用法:在 LocalLow\...\DolocTown\DolocCoop-debug\ 放一个 autotest.flag,
    /// 内容写存档位索引(默认 0)。游戏启动后会:
    ///   1. 等游戏初始化完成 → 自动加载该存档
    ///   2. 等玩家实体就绪 → 自动开启回环主机
    ///   3. 把每一步写进 net 日志,便于事后核对
    /// 跑完会自动删除标记文件,避免下次启动又自动进游戏。
    /// </summary>
    internal static class AutoTest
    {
        private enum Phase { Idle, WaitInit, Loading, WaitAgent, StartHost, ProbeSave, Done }

        private static Phase _phase = Phase.Idle;
        private static bool _checked;
        private static int _slot;
        private static bool _asClient;
        private static float _phaseStart;
        private static float _deadline;

        // 用绝对时间戳而不是累加 deltaTime:加载存档时会出现几秒的卡顿帧,
        // unscaledDeltaTime 的尖峰会一次性把累加计时器冲爆,导致刚进阶段就"超时"。
        private static float Elapsed => Time.realtimeSinceStartup - _phaseStart;

        public static void Tick()
        {
            if (!_checked)
            {
                _checked = true;
                CheckFlag();
            }
            if (_phase == Phase.Idle || _phase == Phase.Done) return;

            if (Elapsed > _deadline)
            {
                Log($"超时,自测中止(阶段 {_phase},已等 {Elapsed:F1}s)");
                _phase = Phase.Done;
                return;
            }

            switch (_phase)
            {
                case Phase.WaitInit:
                    if (GameInitialized())
                    {
                        Log("游戏已初始化,加载存档 " + _slot);
                        Goto(Phase.Loading, 5f);
                    }
                    break;

                case Phase.Loading:
                    // 给标题界面一点时间构建完,再触发加载
                    if (Elapsed > 2f)
                    {
                        try
                        {
                            bool ok = DolocAPI.LoadGame(_slot);
                            Log(ok ? "LoadGame 调用成功" : "LoadGame 返回 false(存档可能为空)");
                        }
                        catch (Exception e) { Log("LoadGame 异常: " + e.Message); }
                        Goto(Phase.WaitAgent, 60f);
                    }
                    break;

                case Phase.WaitAgent:
                    if (AgentReady())
                    {
                        Log("玩家实体已就绪");
                        Goto(Phase.StartHost, 10f);
                    }
                    break;

                case Phase.StartHost:
                    if (Elapsed > 2f)
                    {
                        if (_asClient)
                        {
                            Log("以客机身份接入本机回环主机(模拟客机需先用 --host-mode 启动)");
                            CoopRuntime.StartLoopbackClientForTest();
                            Goto(Phase.ProbeSave, 15f);
                            return;
                        }
                        else
                        {
                            Log("开启回环主机,等待模拟客机接入");
                            CoopRuntime.StartLoopbackHostForTest();
                        }
                        _phase = Phase.Done;
                    }
                    break;

                case Phase.ProbeSave:
                    // 客机身份下主动尝试一次存盘,验证 SaveGuard 真的拦得住。
                    // 这是有意为之的破坏性动作 —— 如果保护失效,存档就会被写。
                    // 所以只在自测流程里做,并且写在日志里方便事后核对。
                    if (Elapsed > 4f)
                    {
                        int before = SaveGuard.BlockedCount;
                        try
                        {
                            DolocAPI.SaveGame(_slot);
                            int blocked = SaveGuard.BlockedCount - before;
                            Log(blocked > 0
                                ? $"存档保护验证通过:尝试存盘被拦截({blocked} 次)"
                                : "⚠ 存档保护失效:尝试存盘没有被拦截!");
                        }
                        catch (Exception e) { Log("存档保护验证时异常: " + e.Message); }
                        _phase = Phase.Done;
                    }
                    break;
            }
        }

        private static void Goto(Phase next, float timeoutSeconds)
        {
            _phase = next;
            _phaseStart = Time.realtimeSinceStartup;
            _deadline = timeoutSeconds;
        }

        private static void CheckFlag()
        {
            try
            {
                string path = Path.Combine(
                    Path.Combine(Application.persistentDataPath, "DolocCoop-debug"), "autotest.flag");
                if (!File.Exists(path)) return;

                // 标记内容格式: "<存档位> [client]"   例如 "0" 或 "0 client"
                string body = File.ReadAllText(path).Trim();
                var parts = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || !int.TryParse(parts[0], out _slot)) _slot = 0;
                _asClient = parts.Length > 1 && parts[1].Equals("client", StringComparison.OrdinalIgnoreCase);
                File.Delete(path);   // 一次性,避免下次启动又自动进游戏

                Log($"检测到自测标记,存档位 {_slot},角色 {(_asClient ? "客机" : "主机")}");
                Goto(Phase.WaitInit, 120f);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[AutoTest] 读取标记失败: " + e.Message);
            }
        }

        private static bool GameInitialized()
        {
            try { return DolocAPI.IsGameInitialized; } catch { return false; }
        }

        private static bool AgentReady()
        {
            try
            {
                var a = DolocAPI.agent;
                return a != null && a.gameObject.activeInHierarchy;
            }
            catch { return false; }
        }

        private static void Log(string msg)
        {
            Plugin.Log.LogInfo("[AutoTest] " + msg);
            NetLog.Log("AUTOTEST " + msg);
        }
    }
}
