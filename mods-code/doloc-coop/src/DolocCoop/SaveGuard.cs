using System;
using HarmonyLib;
using DolocTown.GameData;

namespace DolocCoop
{
    /// <summary>
    /// 客机存档保护。
    ///
    /// **这是整个 Mod 最重要的安全措施。** 社区里已有的联机 mod(XvX 那个)
    /// 让客机进房时被主机的世界覆盖,结果把人家自己存档里的建筑全拆平了 ——
    /// 从架构第一版起我们就承诺"客机存档只读、退出不回写",这里把它落实。
    ///
    /// 做法:所有保存路径最终都收口到 `DataPersistenceManager.SaveGame(int)`
    /// (睡觉、退出、手动存、控制台命令都经过它),在这一个点上拦截即可。
    ///
    /// 返回 true 而不是 false:让游戏以为存成功了。
    /// 返回 false 会触发 `ShowMessageBoxErr(存档失败)`,玩家会以为出了故障;
    /// 而实际情况是"我们有意不写盘",不该表现成错误。
    /// </summary>
    internal static class SaveGuard
    {
        private static Harmony _harmony;
        private static int _blocked;

        /// <summary>当前是否处于"客机"身份 —— 只有这时才拦截保存。</summary>
        public static bool IsClient { get; private set; }

        /// <summary>被拦下的保存次数,面板上显示,让玩家知道保护在生效。</summary>
        public static int BlockedCount => _blocked;

        public static void Install()
        {
            if (_harmony != null) return;
            try
            {
                _harmony = new Harmony("sma1lboy.doloctown.coop.saveguard");
                var target = AccessTools.Method(typeof(DataPersistenceManager), nameof(DataPersistenceManager.SaveGame));
                if (target == null)
                {
                    Plugin.Log.LogError("[SaveGuard] 找不到 DataPersistenceManager.SaveGame —— 客机存档保护未生效!");
                    return;
                }
                _harmony.Patch(target, prefix: new HarmonyMethod(typeof(SaveGuard), nameof(BlockSaveOnClient)));
                Plugin.Log.LogInfo("[SaveGuard] 已挂载客机存档保护");
                NetLog.Log("SAVEGUARD 已挂载");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[SaveGuard] 挂载失败,客机存档保护未生效: " + e);
            }
        }

        /// <summary>会话身份变化时调用。只有客机才需要保护。</summary>
        public static void SetClient(bool isClient)
        {
            if (IsClient == isClient) return;
            IsClient = isClient;
            Plugin.Log.LogInfo($"[SaveGuard] 存档保护 {(isClient ? "开启(客机身份,本地存档只读)" : "关闭(房主身份,正常存盘)")}");
            NetLog.Log($"SAVEGUARD client={isClient}");
        }

        /// <summary>Harmony 前缀:客机身份时跳过真正的写盘。</summary>
        private static bool BlockSaveOnClient(ref bool __result)
        {
            if (!IsClient) return true;   // 房主:照常保存

            _blocked++;
            __result = true;              // 对游戏假装成功,避免弹"存档失败"吓玩家
            Plugin.Log.LogInfo($"[SaveGuard] 已拦截第 {_blocked} 次存盘(客机存档只读)");
            NetLog.Log($"SAVEGUARD_BLOCK count={_blocked}");
            return false;                 // 跳过原方法
        }
    }
}
