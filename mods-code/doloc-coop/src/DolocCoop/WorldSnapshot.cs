using System;
using System.Diagnostics;
using Newtonsoft.Json;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 世界快照 —— 走游戏自己的存档序列化,一次拿到整个世界。
    ///
    /// 背景:逐个领域写同步(箱子/作物/掉落物/任务…)是"抄骨架"式的,
    /// 每加一类游戏内容就要补一份代码。而游戏本身有统一入口:
    /// `ArchiveDataHandle` 挂着 Farm/City/Time/Dungeon/Extra 五份数据,
    /// `LocalSave` 用 JsonConvert 把它们整个序列化成存档。
    /// 也就是说,一次序列化就覆盖了所有子系统 —— **包括我们还没写同步的那些**
    /// (建筑、动物、NPC、科技树…)。
    ///
    /// 但它替代不了逐域增量,原因有四:
    ///  1. 体积:整份存档 ~1.3MB,不可能每秒发;
    ///  2. 没有差分:一株作物长了一格也要重发全部;
    ///  3. 会覆盖客机自己的玩家数据(位置/背包/金钱都在 farmData.agentData 里),
    ///     而背包是**各人各自**的,不该同步;
    ///  4. 应用时要走 AfterLoadData,等于重新加载,画面会闪、人会被传送。
    ///
    /// 所以正确的用法是**分工**:
    ///  · 快照 → 客机进房时对齐世界(一次性,覆盖面最全)
    ///  · 逐域增量 → 之后的持续同步(轻量、无感)
    ///
    /// 本类先解决第一步:把快照取出来并量出真实体积,好判断要不要分片传输。
    /// </summary>
    internal static class WorldSnapshot
    {
        /// <summary>
        /// 序列化设置尽量贴近游戏自己的存档设置:
        /// 保留类型信息(游戏的存档里有多态字段,比如设备的 function),
        /// 不然反序列化会丢子类。
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };

        /// <summary>把当前世界的农场部分序列化出来,返回 JSON;失败返回 null。</summary>
        public static string CaptureFarm()
        {
            try
            {
                var handle = DolocAPI.archiveHandle;
                if (handle?.farmData == null) return null;

                var sw = Stopwatch.StartNew();
                string json = JsonConvert.SerializeObject(handle.farmData, Formatting.None, Settings);
                sw.Stop();

                NetLog.Log($"SNAPSHOT_FARM 字节={json.Length} 耗时={sw.ElapsedMilliseconds}ms");
                Plugin.Log.LogInfo($"[快照] 农场数据 {json.Length / 1024} KB,序列化耗时 {sw.ElapsedMilliseconds} ms");
                return json;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[快照] 序列化农场失败: " + e.Message);
                NetLog.Log("SNAPSHOT_FARM_FAIL " + e.Message);
                return null;
            }
        }

        /// <summary>量一遍各部分的体积,用来判断快照方案是否可行、要不要分片。</summary>
        public static void Measure()
        {
            var handle = DolocAPI.archiveHandle;
            if (handle == null) { Plugin.Log.LogWarning("[快照] 还没进存档"); return; }

            MeasureOne("farmData", handle.farmData);
            MeasureOne("cityData", handle.cityData);
            MeasureOne("timeData", handle.timeData);
            MeasureOne("dungeonData", handle.dungeonData);
            MeasureOne("extraData", handle.extraData);
        }

        private static void MeasureOne(string name, object obj)
        {
            if (obj == null) { NetLog.Log($"SNAPSHOT_SIZE {name}=null"); return; }
            try
            {
                var sw = Stopwatch.StartNew();
                string json = JsonConvert.SerializeObject(obj, Formatting.None, Settings);
                sw.Stop();
                NetLog.Log($"SNAPSHOT_SIZE {name}={json.Length / 1024}KB 耗时={sw.ElapsedMilliseconds}ms");
                Plugin.Log.LogInfo($"[快照] {name} = {json.Length / 1024} KB ({sw.ElapsedMilliseconds} ms)");
            }
            catch (Exception e)
            {
                NetLog.Log($"SNAPSHOT_SIZE {name}=失败 {e.Message}");
                Plugin.Log.LogWarning($"[快照] {name} 序列化失败: {e.Message}");
            }
        }
    }
}
