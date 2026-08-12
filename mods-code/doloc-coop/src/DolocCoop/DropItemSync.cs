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
