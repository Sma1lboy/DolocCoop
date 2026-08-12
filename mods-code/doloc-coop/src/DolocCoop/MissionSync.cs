using System;
using System.Collections.Generic;
using CoopCore;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 任务同步(主机权威)。
    ///
    /// 主机每 5 秒把「已完成任务 id 列表」广播出去,客机对比后补齐:
    /// 凡是主机已完成、而本地正在进行中的任务,调 `CompleteMission` 结掉。
    ///
    /// 只同步"完成"这一个方向,不回退。原因:任务完成往往伴随奖励发放、
    /// 剧情推进、解锁标记,回退没有对应的逆操作,强行回滚只会把存档搞坏。
    /// 客机比主机多完成的任务就留着 —— 顶多是它提前做了,不影响主机。
    ///
    /// `IsMissionListening` 是必要的前置检查:任务没在进行中就调完成,
    /// 游戏自己也会报错(见 complete_mission 控制台命令的实现)。
    /// </summary>
    internal static class MissionSync
    {
        private const float BroadcastInterval = 5f;

        private static float _timer;

        // 同 DropItemSync:用 null 表示"还没发过"。空串是"一个任务都没完成"的
        // 合法指纹,拿它当初值会让这种存档永远不广播。
        private static string _lastSignature;
        private static int _completed;

        // ---------- 主机侧 ----------

        public static void TickHost(CoopSession session)
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < BroadcastInterval) return;
            _timer = 0f;

            try
            {
                var finished = ReadFinished();
                if (finished == null) return;

                string sig = SyncMath.MissionSignature(finished);   // 顺序无关的指纹,纯函数有测试守着
                if (sig == _lastSignature) return;   // 没有新完成的任务就不发
                _lastSignature = sig;

                session.SendMissions(finished);
                NetLog.Log($"MISSION_SEND count={finished.Count}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[MissionSync] 广播失败: " + e.Message);
            }
        }

        /// <summary>新人进房:清掉指纹,下一轮重发完整列表。</summary>
        public static void ResendAll()
        {
            _lastSignature = null;
            _timer = BroadcastInterval;
        }

        // ---------- 客机侧 ----------

        public static void ApplyRemote(List<string> finishedIds)
        {
            if (finishedIds == null || finishedIds.Count == 0) return;
            try
            {
                var mm = DolocAPI.archiveHandle?.farmData?.missionManager;
                if (mm == null) return;

                foreach (var id in finishedIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (mm.IsMissionComplete(id)) continue;      // 本地已完成
                    if (!mm.IsMissionListening(id)) continue;    // 没在进行中,不能强行完成

                    mm.CompleteMission(id);
                    _completed++;
                    Plugin.Log.LogInfo($"[MissionSync] 跟随主机完成任务 {id}");
                    NetLog.Log($"MISSION_COMPLETE id={id} total={_completed}");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[MissionSync] 应用失败: " + e.Message);
            }
        }

        // ---------- 工具 ----------

        private static List<string> ReadFinished()
        {
            try
            {
                var mm = DolocAPI.archiveHandle?.farmData?.missionManager;
                if (mm == null) return null;
                return new List<string>(mm.FinishMissions);
            }
            catch { return null; }
        }

        /// <summary>面板用:进行中 / 已完成的任务数。</summary>
        public static string Describe()
        {
            try
            {
                var mm = DolocAPI.archiveHandle?.farmData?.missionManager;
                if (mm == null) return "未进存档";
                return $"进行中 {mm.TotalExplicitMissions.Length} · 已完成 {mm.FinishMissions.Length}";
            }
            catch { return "读取失败"; }
        }
    }
}

