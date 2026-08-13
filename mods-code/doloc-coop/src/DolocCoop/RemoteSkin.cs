using System;
using System.Collections.Generic;
using CoopCore;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 按"对方在用哪套皮肤"取贴图。
    ///
    /// 做法是抄游戏自己的:`PlayerSpriteOverrideHandler` 每帧读基础渲染器上
    /// **Animator 刚写进去的那张原版精灵**的名字(形如 anim_player_walk_2),
    /// 把 "player" 换成 "player_hair" / "player_body" 得到分层键,
    /// 再去 Mod 的贴图字典里查。
    ///
    /// 所以远端化身不需要哈希映射、不需要数帧、不需要对时序 —— Animator 全干了,
    /// 我们只是把这一步查找从"全局 ModManager"改成"对方那套皮肤"。
    ///
    /// 贴图本身一个字节都不过网:进房时的清单校验已经保证了对方启用的 Mod 本机也有,
    /// 而游戏在加载 Mod 时早就把每张图读进 ModInfo.sprites 了,直接拿来用。
    /// </summary>
    internal static class RemoteSkin
    {
        private struct Entry
        {
            public Dictionary<string, Sprite> Sprites;   // null = 本机没有这套皮肤
            public float ResolvedAt;
        }

        /// <summary>
        /// modId → 该 Mod 的贴图字典。
        ///
        /// 带过期时间而不是永久缓存,原因有二:热重载(F5)之后 ModInfo 可能被换成新实例,
        /// 旧字典里的 Sprite 已经被 ClearCache 销毁,再用就是野引用;
        /// 以及"查不到"这个结果本身也会过期 —— 否则玩家中途启用了那套皮肤,
        /// 我们会一直记着"他没装"再也不去看第二眼。
        ///
        /// 用 realtimeSinceStartup 而不是累加 deltaTime:加载场景时后者会突刺,
        /// 这个坑在 AutoTest 的超时判定上已经踩过一次。
        /// </summary>
        private static readonly Dictionary<string, Entry> Cache =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        private const float TtlSeconds = 5f;

        /// <summary>立即失效。热重载后调用,不等自然过期。</summary>
        public static void Invalidate()
        {
            Cache.Clear();
            NetLog.Log("REMOTE_SKIN 缓存已失效");
        }

        /// <summary>
        /// 取某套皮肤里指定键的贴图。皮肤没装、或这套皮肤没有这一帧,都返回 null,
        /// 由调用方回退到本机默认外观。
        /// </summary>
        public static Sprite Get(string modId, string spriteKey)
        {
            if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(spriteKey)) return null;
            var sprites = GetSprites(modId);
            if (sprites == null) return null;
            return sprites.TryGetValue(spriteKey, out var s) ? s : null;
        }

        private static Dictionary<string, Sprite> GetSprites(string modId)
        {
            float now = Time.realtimeSinceStartup;
            if (Cache.TryGetValue(modId, out var cached) && now - cached.ResolvedAt < TtlSeconds)
                return cached.Sprites;

            Dictionary<string, Sprite> found = null;
            try
            {
                var mm = DolocAPI.modManager;
                if (mm != null)
                {
                    foreach (var info in mm.GetAllValidModInfos())
                    {
                        if (info == null || !string.Equals(info.id, modId, StringComparison.OrdinalIgnoreCase)) continue;
                        // sprites 是游戏加载 Mod 时填好的;为空说明这个 Mod 当前没启用
                        // (ClearCache 会把它清掉),那就没得用,按"没装"处理
                        found = info.sprites != null && info.sprites.Count > 0 ? info.sprites : null;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[RemoteSkin] 查找皮肤 " + modId + " 失败: " + e.Message);
            }

            bool changed = !cached.Equals(default(Entry)) ? (cached.Sprites != found) : true;
            Cache[modId] = new Entry { Sprites = found, ResolvedAt = now };
            // 只在结果变化时记日志 —— 缓存每 5 秒重算一次,不然日志会被刷屏
            if (changed)
            {
                if (found == null)
                    NetLog.Log($"REMOTE_SKIN_MISS mod={modId} 本机没有可用贴图,回退默认外观");
                else
                    NetLog.Log($"REMOTE_SKIN_READY mod={modId} 贴图数={found.Count}");
            }
            return found;
        }

        /// <summary>
        /// 把基础精灵名转成分层键,和 PlayerSpriteOverrideHandler 用的是同一个规则。
        /// 注意是 Replace 而不是插入前缀:原版名字形如 anim_player_walk_2,
        /// 替换后得到 anim_player_hair_walk_2 —— 正好是 Mod 里的文件名。
        /// </summary>
        public static void SplitKeys(string baseName, out string hairKey, out string bodyKey)
        {
            hairKey = bodyKey = null;
            if (string.IsNullOrEmpty(baseName) || baseName.IndexOf("player", StringComparison.Ordinal) < 0) return;
            hairKey = baseName.Replace("player", "player_hair");
            bodyKey = baseName.Replace("player", "player_body");
        }
    }
}
