using System;
using System.Collections.Generic;
using DolocTown.Config.Weather;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 时间同步(主机权威)。
    ///
    /// 农场游戏里两人处于不同日期是最破坏体验的,所以这是世界同步的第一项。
    ///
    /// 做法:主机每 2 秒广播一次自己的 `timeData.totalSeconds`;
    /// 客机拿到后和本地比,**只在偏差超过阈值时才纠正**——
    /// 两端时钟本来就以相同速率走,频繁校正反而会让画面一跳一跳。
    /// 纠正用游戏自己的 PassTimeNoControl / TrackBackTime,
    /// 这样日夜渲染、事件刷新等副作用都由游戏内部处理,不用我们操心。
    /// </summary>
    internal static class TimeSync
    {
        /// <summary>广播间隔(现实秒)。</summary>
        private const float BroadcastInterval = 2f;

        /// <summary>容忍的时间偏差(游戏内秒)。小于它就不动,避免画面抖动。</summary>
        private const int ToleranceSeconds = 60;

        /// <summary>单次纠正上限,防止异常数据把玩家甩到几年后。</summary>
        private const int MaxCorrection = 60 * 60 * 24 * 3;   // 3 游戏日

        /// <summary>即使时间没变,也至少这么久发一次(游戏失焦会暂停时间)。</summary>
        private const float HeartbeatInterval = 10f;

        private static float _timer;
        private static int _lastSent = -1;
        private static float _lastSentAt = -999f;
        private static bool _forceNextSend;
        private static int _corrections;

        public static void TickHost(CoopCore.CoopSession session)
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < BroadcastInterval) return;
            _timer = 0f;

            if (!TryGetLocalSeconds(out int now))
            {
                NetLog.Sample("world-skip", 30, "WORLD_SKIP 未进存档,读不到时间");
                return;
            }

            // 时间没变就不发 —— 但必须留一条心跳:游戏失焦时时间会暂停,
            // 如果只在"时间变了"才发,刚进房的客机会永远等不到世界状态。
            bool changed = now != _lastSent;
            bool heartbeatDue = Time.unscaledTime - _lastSentAt >= HeartbeatInterval;
            if (!changed && !heartbeatDue && !_forceNextSend) return;

            _lastSent = now;
            _lastSentAt = Time.unscaledTime;
            _forceNextSend = false;

            var weather = ReadLocalWeather();
            session.SendWorldSync(now, weather);
            NetLog.Sample("world-send", 5, $"WORLD_SEND time={now} regions={weather.Count} " +
                                           $"reason={(changed ? "changed" : "heartbeat")}");
        }

        /// <summary>有新玩家进房时调用:下一帧立刻补发一次世界状态,别让人干等心跳。</summary>
        public static void ForceSendNext()
        {
            _forceNextSend = true;
            _timer = BroadcastInterval;   // 让下一次 TickHost 立即触发
        }

        // ---------- 天气 ----------

        /// <summary>读出各天气区域的当前天气。区域数量很少,整包下发比做增量简单可靠。</summary>
        public static List<CoopCore.WeatherEntry> ReadLocalWeather()
        {
            var list = new List<CoopCore.WeatherEntry>();
            try
            {
                var cm = DolocAPI.archiveHandle?.timeData?.climateManager;
                if (cm == null || cm.weatherSystems == null) return list;
                foreach (var kv in cm.weatherSystems)
                {
                    var w = cm.GetCurrentWeather(kv.Key);
                    list.Add(new CoopCore.WeatherEntry { RegionId = kv.Key, WeatherType = (int)w });
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[WorldSync] 读天气失败: " + e.Message); }
            return list;
        }

        /// <summary>客机应用主机天气,只在不一致时才设置(SetWeather 会触发重渲染)。</summary>
        public static void ApplyRemoteWeather(List<CoopCore.WeatherEntry> weather)
        {
            if (weather == null || weather.Count == 0) return;
            try
            {
                var handle = DolocAPI.archiveHandle;
                var cm = handle?.timeData?.climateManager;
                if (handle == null || cm == null) return;

                foreach (var entry in weather)
                {
                    if (string.IsNullOrEmpty(entry.RegionId)) continue;
                    int local = (int)cm.GetCurrentWeather(entry.RegionId);
                    if (local == entry.WeatherType) continue;

                    handle.SetWeather(entry.RegionId, (WeatherType)entry.WeatherType, shouldRender: true);
                    Plugin.Log.LogInfo($"[WorldSync] 天气校正 {entry.RegionId}: {(WeatherType)local} → {(WeatherType)entry.WeatherType}");
                    NetLog.Log($"WEATHER_SET region={entry.RegionId} {local}->{entry.WeatherType}");
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[WorldSync] 应用天气失败: " + e.Message); }
        }

        /// <summary>面板用:当前所在区域的天气描述。</summary>
        public static string DescribeWeather()
        {
            try { return DolocAPI.archiveHandle?.LocalWeatherType.ToString() ?? "?"; }
            catch { return "?"; }
        }

        /// <summary>客机收到主机时间后的处理。</summary>
        public static void ApplyRemote(int hostSeconds)
        {
            if (!TryGetLocalSeconds(out int local)) return;

            int diff = hostSeconds - local;
            if (Math.Abs(diff) <= ToleranceSeconds) return;

            if (Math.Abs(diff) > MaxCorrection)
            {
                Plugin.Log.LogWarning($"[TimeSync] 偏差过大({diff}s),拒绝纠正 —— 可能不是同一份存档");
                NetLog.Log($"TIME_REJECT diff={diff}");
                return;
            }

            try
            {
                var handle = DolocAPI.archiveHandle;
                if (handle == null) return;

                if (diff > 0) handle.PassTimeNoControl(diff, null);
                else handle.TrackBackTime(-diff, null);

                _corrections++;
                Plugin.Log.LogInfo($"[TimeSync] 已校时 {diff:+#;-#;0}s (本地 {local} → 主机 {hostSeconds})");
                NetLog.Log($"TIME_CORRECT diff={diff} local={local} host={hostSeconds} count={_corrections}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[TimeSync] 校时失败: " + e.Message);
            }
        }

        /// <summary>当前游戏内累计秒数;没进存档时返回 false。</summary>
        public static bool TryGetLocalSeconds(out int seconds)
        {
            seconds = 0;
            try
            {
                var handle = DolocAPI.archiveHandle;
                if (handle == null || handle.timeData == null) return false;
                seconds = handle.timeData.totalSeconds;
                return true;
            }
            catch { return false; }
        }

        /// <summary>给面板显示用的时间描述。</summary>
        public static string Describe()
        {
            try
            {
                var handle = DolocAPI.archiveHandle;
                if (handle == null || handle.timeData == null) return "未进存档";
                var d = handle.timeData.dateNow;   // DateInfo 是结构体,不能用 ?.
                return $"第 {d.TotalDays} 天  {d.Hour:00}:{d.Minute:00}";
            }
            catch { return "读取失败"; }
        }
    }
}

