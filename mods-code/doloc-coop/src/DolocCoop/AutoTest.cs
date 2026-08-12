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
        private enum Phase { Idle, WaitInit, Loading, WaitAgent, PlaceBox, StartHost, SpawnDrop, ProbeSave, Done }

        private static Phase _phase = Phase.Idle;
        private static bool _checked;
        private static int _slot;
        private static bool _asClient;
        private static bool _placeBox;
        private static bool _spawnDrop;
        private static bool _dropSpawned;
        private static Vector2 _dropPos;
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
                        if (_placeBox)
                        {
                            // 放箱子前先把存档锁死 —— 宁可多锁,也不能让测试用的箱子写进玩家存档
                            SaveGuard.SetClient(true);
                            Log("已强制开启存档保护(放测试箱子前的安全措施)");
                            Goto(Phase.PlaceBox, 15f);
                        }
                        else Goto(Phase.StartHost, 10f);
                    }
                    break;

                case Phase.PlaceBox:
                    // 临时放一个木箱,专门用来验证箱子同步 —— 开局存档里一个容器都没有。
                    //
                    // **安全前提**:进入这个阶段前已强制开启存档保护,所有写盘都会被拦,
                    // 所以这个箱子只存在于内存里,退出游戏就没了,玩家存档不受影响。
                    if (Elapsed > 1f)
                    {
                        PlaceTestContainer();
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
                            Goto(_spawnDrop ? Phase.SpawnDrop : Phase.ProbeSave, 20f);
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

                case Phase.SpawnDrop:
                    // 在地上造一个掉落物,等客机把它记进"已知地面",再悄悄拿掉 ——
                    // 模拟"本地玩家捡走了",验证客机的差分检测会不会上报主机。
                    // 这是最后一条没验证过的路径:开局存档地上什么都没有。
                    if (Elapsed > 3f && !_dropSpawned)
                    {
                        _dropSpawned = true;
                        SpawnTestDrop();
                        _deadline = 30f;   // 后面还要等记账 + 移除
                    }
                    else if (_dropSpawned && Elapsed > 12f)
                    {
                        RemoveTestDrop();
                        Goto(Phase.ProbeSave, 15f);
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
                _asClient = false; _placeBox = false; _spawnDrop = false;
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].Equals("client", StringComparison.OrdinalIgnoreCase)) _asClient = true;
                    if (parts[i].Equals("box", StringComparison.OrdinalIgnoreCase)) _placeBox = true;
                    if (parts[i].Equals("drop", StringComparison.OrdinalIgnoreCase)) _spawnDrop = true;
                }
                File.Delete(path);   // 一次性,避免下次启动又自动进游戏

                Log($"检测到自测标记,存档位 {_slot},角色 {(_asClient ? "客机" : "主机")}{(_placeBox ? ",放测试箱子" : "")}");
                Goto(Phase.WaitInit, 120f);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[AutoTest] 读取标记失败: " + e.Message);
            }
        }

        /// <summary>
        /// 在玩家脚边放一个木箱(wooden_case,EquipmentFuncCase → 实现 IContainer)。
        /// 只用于自测,依赖调用前已开启存档保护。
        /// </summary>
        private static void PlaceTestContainer()
        {
            try
            {
                var agent = DolocAPI.agent;
                var room = DolocAPI.archiveHandle?.currentRoom;
                if (agent == null || room == null) { Log("放箱子失败:玩家或房间为空"); return; }

                var proto = DolocTown.Config.DolocConfig.Tables.TbEquipment.GetOrDefault("wooden_case");
                if (proto == null) { Log("放箱子失败:配置表里没有 wooden_case"); return; }

                var p = agent.transform.position;
                var wp = new Vector3(p.x + 2f, p.y, p.z);
                var anchor = new Vector2Int(Mathf.RoundToInt(wp.x), Mathf.RoundToInt(wp.y));

                var eq = ((DolocTown.IEquipmentHost)room).CreateEquipment(wp, anchor, proto, false);   // 是接口默认方法,要显式转型
                Log(eq != null
                    ? $"已放置测试木箱于 ({wp.x:F1},{wp.y:F1})"
                    : "放箱子失败:CreateEquipment 返回 null(位置可能不合法)");
            }
            catch (Exception e)
            {
                Log("放箱子异常: " + e.Message);
            }
        }

        /// <summary>在玩家脚边造一个掉落物,用来验证客机的捡拾差分检测。</summary>
        private static void SpawnTestDrop()
        {
            try
            {
                var agent = DolocAPI.agent;
                var room = DolocAPI.archiveHandle?.currentRoom;
                if (agent == null || room?.DM_dropitem == null) { Log("造掉落物失败:房间或玩家为空"); return; }

                var p = agent.transform.position;
                _dropPos = new Vector2(p.x - 2f, p.y);
                var d = room.DM_dropitem.CreateDropItem(room, "wood", _dropPos, false);
                Log(d != null
                    ? $"已在 ({_dropPos.x:F1},{_dropPos.y:F1}) 造一个测试掉落物,等客机记账"
                    : "造掉落物失败:CreateDropItem 返回 null");
            }
            catch (Exception e) { Log("造掉落物异常: " + e.Message); }
        }

        /// <summary>悄悄拿掉它 —— 模拟"本地玩家捡走了",客机应当上报主机。</summary>
        private static void RemoveTestDrop()
        {
            try
            {
                var room = DolocAPI.archiveHandle?.currentRoom;
                if (room?.DM_dropitem == null) return;

                foreach (var d in room.DM_dropitem.AllDatas)
                {
                    if (d == null || d.IsRemoved) continue;
                    var p = d.PositionWS;
                    if ((p - _dropPos).sqrMagnitude > 0.25f) continue;
                    room.DM_dropitem.RemoveDropItem(d);
                    Log("已移除测试掉落物(模拟被捡走),等客机上报");
                    return;
                }
                Log("没找到测试掉落物,可能已被别的逻辑清掉");
            }
            catch (Exception e) { Log("移除掉落物异常: " + e.Message); }
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




