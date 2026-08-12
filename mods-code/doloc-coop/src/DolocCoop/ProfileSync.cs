using System;
using Steamworks;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 外观同步:昵称与帽子。
    ///
    /// 单独抽出来是因为它和位置/动作的节奏完全不同 —— 一局里可能只变几次,
    /// 塞进 15Hz 的状态包纯属浪费;但又必须在**变化时**及时发出去,
    /// 否则对方看到的还是你换装之前的样子。所以是"轮询 + 变了才发"。
    /// </summary>
    internal static class ProfileSync
    {
        private const float CheckInterval = 1f;

        private static float _timer;
        private static string _lastName = "\u0000";   // 用不可能的初值,保证首次一定发
        private static string _lastHat = "\u0000";

        public static void Tick(CoopCore.CoopSession session)
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < CheckInterval) return;
            _timer = 0f;

            string name = ReadName();
            string hat = ReadHatId();
            if (name == _lastName && hat == _lastHat) return;

            _lastName = name;
            _lastHat = hat;
            session.SendProfile(name, hat);
            NetLog.Log($"PROFILE_SEND name={name} hat={(string.IsNullOrEmpty(hat) ? "(无)" : hat)}");
        }

        /// <summary>重置,让下一轮重发 —— 新人进房时用。</summary>
        public static void Resend()
        {
            _lastName = "\u0000";
            _lastHat = "\u0000";
            _timer = CheckInterval;
        }

        private static string ReadName()
        {
            try { return SteamAPI.IsSteamRunning() ? SteamFriends.GetPersonaName() : "玩家"; }
            catch { return "玩家"; }
        }

        /// <summary>当前戴的帽子 id;没戴返回空串。</summary>
        public static string ReadHatId()
        {
            try
            {
                var agent = DolocAPI.agent;
                if (agent == null) return "";
                var info = agent.CurrentHatRenderInfo;
                return info != null ? (info.Id ?? "") : "";
            }
            catch { return ""; }
        }
    }
}
