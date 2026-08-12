using System;
using System.Collections.Generic;
using System.Text;
using CoopCore;
using DolocTown;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 掉落物同步(主机权威,全量对账)。
    ///
    /// 这是两人同场景时最刺眼的不同步:一个人把地上的东西捡了,
    /// 另一个人还看得见,走过去捡还能再捡一次 —— 直接变成刷物品。
    ///
    /// 做法:主机每 1 秒把当前房间的掉落物列表整包广播;客机对账:
    ///   · 主机没有、本地有  → 删掉(别人捡走了)
    ///   · 主机有、本地没有  → 补上(别人打掉了新东西)
    /// 掉落物数量通常只有几个到几十个,整包对账比做增量事件可靠得多 ——
    /// 增量一旦丢包或乱序,地上就会留下永远捡不掉的幽灵物品。
    ///
    /// 标识用「物品名@取整坐标」:掉落物没有持久 id,而同一个物品不会
    /// 精确重叠在同一格,取整到 0.5 格足够区分。
    /// </summary>
    internal static class DropItemSync
    {
        private const float BroadcastInterval = 1f;

        /// <summary>坐标量化精度(格)。太细会因浮点抖动误判成"不同的物品"。</summary>
        private const float Quantize = 0.5f;

        private static float _timer;

        // 用 null 当"还没发过"的标记,不能用空串:
        // 地上没有任何掉落物时算出来的指纹**也是空串**,
        // 两者相等就永远不会广播 —— 客机上的残留物品于是永远得不到清理。
        private static string _lastSignature;
        private static int _removed, _created;

        // ---------- 主机侧 ----------

        public static void TickHost(CoopSession session)
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < BroadcastInterval) return;
            _timer = 0f;

            try
            {
                var list = ReadLocal();
                if (list == null)
                {
                    // 区分"当前房间读不到"和"地上确实是空的" —— 两者在日志里长得一样,
                    // 排查掉落物不同步时会白白绕远路
                    NetLog.Sample("drop-noroom", 10, "DROP_SKIP 读不到当前房间或掉落物管理器");
                    return;
                }

                string sig = Signature(list);
                if (sig == _lastSignature) return;   // 地上没变化就不发
                _lastSignature = sig;

                session.SendDropItems(list);
                NetLog.Sample("drop-send", 5, $"DROP_SEND count={list.Count}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[DropItemSync] 广播失败: " + e.Message);
            }
        }

        public static void ResendAll()
        {
            _lastSignature = null;
            _timer = BroadcastInterval;
        }

        // ---------- 客机侧 ----------

        /// <summary>客机上一次已知的地面状态,用来发现"本地少了什么"。</summary>
        private static HashSet<string> _knownKeys = new HashSet<string>();
        private static readonly Dictionary<string, DropEntry> KnownEntries = new Dictionary<string, DropEntry>();
        private static float _clientTimer;

        /// <summary>
        /// 客机每 0.5 秒检查一次:有没有东西从地上消失了。
        ///
        /// 消失只有两个来源 —— 要么是刚才应用主机广播删掉的(那时 _knownKeys 已经同步更新,
        /// 不会误报),要么就是**本地玩家自己捡的**,需要上报给主机,
        /// 否则主机下一轮广播又会把它生成回来,客机等于白得一份(刷物品)。
        ///
        /// 用差分而不是 Harmony 拦截拾取:游戏里能让物件消失的路径不止一条
        /// (走过自动吸附、工具收集、掉落物过期…),逐个打补丁既容易漏又容易被版本更新击穿;
        /// 盯住"结果"比盯住"每一种原因"稳得多。
        /// </summary>
        public static void TickClient(CoopSession session)
        {
            _clientTimer += Time.unscaledDeltaTime;
            if (_clientTimer < 0.5f) return;
            _clientTimer = 0f;

            try
            {
                var list = ReadLocal();
                if (list == null) return;

                var localKeys = new HashSet<string>();
                foreach (var e in list) localKeys.Add(KeyOf(e.ItemName, e.X, e.Y));

                var vanished = new List<DropEntry>();
                foreach (var key in _knownKeys)
                {
                    if (localKeys.Contains(key)) continue;
                    if (KnownEntries.TryGetValue(key, out var entry)) vanished.Add(entry);
                }

                if (vanished.Count > 0)
                {
                    session.SendDropPickup(vanished);
                    NetLog.Log($"DROP_PICKUP_SEND count={vanished.Count} first={vanished[0].ItemName}");
                }

                RememberLocal(list, localKeys);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[DropItemSync] 捡拾检测失败: " + e.Message);
            }
        }

        private static void RememberLocal(List<DropEntry> list, HashSet<string> keys)
        {
            _knownKeys = keys;
            KnownEntries.Clear();
            foreach (var e in list) KnownEntries[KeyOf(e.ItemName, e.X, e.Y)] = e;
        }

        /// <summary>主机侧:某个客机报告它捡走了东西,从世界里移除。</summary>
        public static void HandlePickup(ulong from, List<DropEntry> picked)
        {
            if (picked == null || picked.Count == 0) return;
            try
            {
                var room = DolocAPI.archiveHandle?.currentRoom;
                if (room == null || room.DM_dropitem == null) return;

                var wanted = new HashSet<string>();
                foreach (var e in picked) wanted.Add(KeyOf(e.ItemName, e.X, e.Y));

                var toRemove = new List<DropItemBase>();
                foreach (var d in room.DM_dropitem.AllDatas)
                {
                    if (d == null || d.IsRemoved) continue;
                    var p = d.PositionWS;
                    if (wanted.Contains(KeyOf(d.ItemName, p.x, p.y))) toRemove.Add(d);
                }
                foreach (var d in toRemove) room.DM_dropitem.RemoveDropItem(d);

                if (toRemove.Count > 0)
                {
                    _lastSignature = null;   // 强制下一轮重播,让所有人对齐
                    NetLog.Log($"DROP_PICKUP_APPLY from={from} 请求={picked.Count} 实际移除={toRemove.Count}");
                }
                else
                {
                    // 主机这边已经没有了 —— 多半是两人同时捡同一个,先到先得,后到的静默忽略
                    NetLog.Log($"DROP_PICKUP_MISS from={from} 请求={picked.Count} 主机侧已不存在(可能被人抢先)");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[DropItemSync] 处理捡拾上报失败: " + e.Message);
            }
        }

        public static void ApplyRemote(List<DropEntry> remote)
        {
            if (remote == null) return;
            try
            {
                var room = DolocAPI.archiveHandle?.currentRoom;
                if (room == null || room.DM_dropitem == null) return;

                var wanted = new HashSet<string>();
                foreach (var e in remote) wanted.Add(KeyOf(e.ItemName, e.X, e.Y));

                // 1) 主机没有、本地有 → 删掉(别人捡走了)
                var toRemove = new List<DropItemBase>();
                foreach (var d in room.DM_dropitem.AllDatas)
                {
                    if (d == null || d.IsRemoved) continue;
                    var p = d.PositionWS;
                    if (!wanted.Contains(KeyOf(d.ItemName, p.x, p.y))) toRemove.Add(d);
                }
                foreach (var d in toRemove)
                {
                    room.DM_dropitem.RemoveDropItem(d);
                    _removed++;
                }

                // 2) 主机有、本地没有 → 补上(别人打掉了新东西)
                var localKeys = new HashSet<string>();
                foreach (var d in room.DM_dropitem.AllDatas)
                {
                    if (d == null || d.IsRemoved) continue;
                    var p = d.PositionWS;
                    localKeys.Add(KeyOf(d.ItemName, p.x, p.y));
                }
                int created = 0;
                foreach (var e in remote)
                {
                    if (string.IsNullOrEmpty(e.ItemName)) continue;
                    if (localKeys.Contains(KeyOf(e.ItemName, e.X, e.Y))) continue;
                    room.DM_dropitem.CreateDropItem(room, e.ItemName, new Vector2(e.X, e.Y), false);
                    created++; _created++;
                }

                if (toRemove.Count > 0 || created > 0)
                    NetLog.Log($"DROP_APPLY 删除={toRemove.Count} 新增={created} 累计(删{_removed}/增{_created})");

                // 关键:把"已知地面"更新成主机的版本。
                // 少了这一步,刚被主机广播删掉的物品会被捡拾检测误判成"本地玩家捡的",
                // 于是客机反过来告诉主机"我捡了",两边互相纠正,永远停不下来。
                var afterKeys = new HashSet<string>();
                foreach (var e in remote) afterKeys.Add(KeyOf(e.ItemName, e.X, e.Y));
                RememberLocal(remote, afterKeys);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[DropItemSync] 对账失败: " + e.Message);
            }
        }

        // ---------- 工具 ----------

        private static List<DropEntry> ReadLocal()
        {
            var room = DolocAPI.archiveHandle?.currentRoom;
            if (room == null || room.DM_dropitem == null) return null;

            var list = new List<DropEntry>();
            foreach (var d in room.DM_dropitem.AllDatas)
            {
                if (d == null || d.IsRemoved || !d.IsItem) continue;
                var p = d.PositionWS;
                list.Add(new DropEntry { ItemName = d.ItemName, X = p.x, Y = p.y });
            }
            return list;
        }

        /// <summary>量化坐标后的标识。掉落物没有持久 id,靠"名字+位置"区分。</summary>
        private static string KeyOf(string name, float x, float y)
        {
            return name + "@" + Mathf.RoundToInt(x / Quantize) + "," + Mathf.RoundToInt(y / Quantize);
        }

        private static string Signature(List<DropEntry> list)
        {
            var keys = new List<string>(list.Count);
            foreach (var e in list) keys.Add(KeyOf(e.ItemName, e.X, e.Y));
            keys.Sort(StringComparer.Ordinal);   // 顺序无关,避免因遍历顺序变化误判
            var sb = new StringBuilder();
            foreach (var k in keys) { sb.Append(k); sb.Append('|'); }
            return sb.ToString();
        }

        /// <summary>面板用:当前房间地上有多少掉落物。</summary>
        public static int LocalCount
        {
            get
            {
                var l = ReadLocal();
                return l?.Count ?? 0;
            }
        }
    }
}
