using System;
using System.Collections.Generic;
using System.Text;
using CoopCore;
using DolocTown;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 作物同步(主机权威)。
    ///
    /// 农场游戏里这一项的分量仅次于时间:两人看到的作物长势不同,
    /// 一个说"熟了快收",另一个屏幕上还是幼苗 —— 一起种田就没法玩。
    ///
    /// 作物长在 `PlantBasin`(种植槽)上,而 PlantBasin 继承 Equipment,
    /// 是纯数据对象,所以走 `room.DM_equipment.AllEquipments` 枚举
    /// (和箱子同步踩过的坑一样:FindObjectsOfType 扫不到设备)。
    ///
    /// 只同步"长势"这几个标量:阶段、生长值、健康、湿润、枯死、成熟、采收次数。
    /// **不同步种子种类** —— 换种子意味着重新种植,那是交互行为,
    /// 应当由主机结算后通过设备状态自然传过来,而不是客机自己改。
    /// </summary>
    internal static class CropSync
    {
        private const float ScanInterval = 3f;
        private const int MaxPerMessage = 32;

        private static float _timer;
        private static readonly Dictionary<string, string> LastSignature = new Dictionary<string, string>();
        private static int _applied;

        // ---------- 主机侧 ----------

        public static void TickHost(CoopSession session)
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < ScanInterval) return;
            _timer = 0f;

            try
            {
                var basins = FindBasins();
                NetLog.Sample("crop-scan", 15, $"CROP_SCAN 种植槽={basins.Count}");

                var changed = new List<CropState>();
                foreach (var b in basins)
                {
                    string id = IdOf(b);
                    if (string.IsNullOrEmpty(id)) continue;

                    var st = Read(b, id);
                    string sig = Signature(st);
                    if (LastSignature.TryGetValue(id, out string prev) && prev == sig) continue;

                    LastSignature[id] = sig;
                    changed.Add(st);
                    if (changed.Count >= MaxPerMessage) break;
                }

                if (changed.Count > 0)
                {
                    session.SendCrops(changed);
                    NetLog.Sample("crop-send", 5, $"CROP_SEND count={changed.Count} first={changed[0].BasinId}");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[CropSync] 扫描失败: " + e.Message);
            }
        }

        public static void ResendAll()
        {
            LastSignature.Clear();
            _timer = ScanInterval;
        }

        // ---------- 客机侧 ----------

        public static void ApplyRemote(List<CropState> states)
        {
            if (states == null || states.Count == 0) return;
            try
            {
                var byId = new Dictionary<string, PlantBasin>();
                foreach (var b in FindBasins())
                {
                    string id = IdOf(b);
                    if (!string.IsNullOrEmpty(id)) byId[id] = b;
                }

                int matched = 0, applied = 0;
                foreach (var st in states)
                {
                    if (st == null || string.IsNullOrEmpty(st.BasinId)) continue;
                    if (!byId.TryGetValue(st.BasinId, out var basin)) continue;
                    matched++;

                    var crop = basin.Crop;
                    if (crop == null || !crop.IsValid) continue;   // 本地这个槽还是空的,等交互同步补

                    // 种的不是同一种东西就别硬套数值 —— 阶段数、成熟条件都不一样
                    if (!string.IsNullOrEmpty(st.SeedId) && crop.SeedId != st.SeedId)
                    {
                        NetLog.Sample($"crop-seed-{st.BasinId}", 10,
                            $"CROP_SEED_MISMATCH id={st.BasinId} 本地={crop.SeedId} 对方={st.SeedId}");
                        continue;
                    }

                    if (Signature(Read(basin, st.BasinId)) == Signature(st)) continue;

                    crop.SetCropData(new CropData(
                        st.IsMature, st.IsDead, st.CurrentLevel, st.Lifespan,
                        st.GrowthValue, st.HealthValue, st.IsMoist, st.IsPolluted, st.HarvestTimes));
                    applied++; _applied++;
                }

                if (applied > 0)
                    NetLog.Log($"CROP_APPLY 收到={states.Count} 匹配={matched} 应用={applied} 累计={_applied}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[CropSync] 应用失败: " + e.Message);
            }
        }

        // ---------- 工具 ----------

        private static List<PlantBasin> FindBasins()
        {
            var result = new List<PlantBasin>();
            try
            {
                var room = DolocAPI.archiveHandle?.currentRoom;
                if (room?.DM_equipment == null) return result;
                foreach (var eq in room.DM_equipment.AllEquipments)
                    if (eq is PlantBasin b) result.Add(b);
            }
            catch { }
            return result;
        }

        private static string IdOf(PlantBasin b)
        {
            try { return $"{b.GetType().Name}#{b.id}"; }
            catch { return null; }
        }

        private static CropState Read(PlantBasin b, string id)
        {
            var st = new CropState { BasinId = id };
            try
            {
                var crop = b.Crop;
                if (crop == null || !crop.IsValid) { st.SeedId = ""; return st; }

                // 直接读公开的 data 结构 —— Crop 上那些只读属性只是它的转发,
                // 而写入必须整块给(SetCropData);两边用同一个结构才不会漏字段
                var d = crop.data;
                st.SeedId = crop.SeedId ?? "";
                st.CurrentLevel = d.currentLevel;
                st.IsMature = d.isMature;
                st.IsDead = d.isDead;
                st.IsMoist = d.isMoist;
                st.IsPolluted = d.isPolluted;
                st.HarvestTimes = d.harvestTimes;
                st.Lifespan = d.lifespan;
                st.GrowthValue = d.currentGrowthValue;
                st.HealthValue = d.currentHealthValue;
            }
            catch { }
            return st;
        }

        private static string Signature(CropState s)
        {
            var sb = new StringBuilder();
            sb.Append(s.SeedId).Append('|').Append(s.CurrentLevel).Append('|')
              .Append(s.IsMature ? 1 : 0).Append(s.IsDead ? 1 : 0)
              .Append(s.IsMoist ? 1 : 0).Append(s.IsPolluted ? 1 : 0).Append('|')
              .Append(s.HarvestTimes).Append('|').Append(s.Lifespan).Append('|')
              // 生长值取整到 0.5,避免每帧的微小变化都触发广播
              .Append(Mathf.RoundToInt(s.GrowthValue * 2f)).Append('|')
              .Append(Mathf.RoundToInt(s.HealthValue * 2f));
            return sb.ToString();
        }

        /// <summary>面板用:当前房间有多少个种植槽在同步。</summary>
        public static int TrackedCount => LastSignature.Count;
    }
}

