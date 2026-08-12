using System;
using CoopCore;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 行为同步:把玩家的动作状态(砍树/浇水/钓鱼/交互…)在两端对齐。
    ///
    /// 位置和动画哈希已经跟着 15Hz 的状态包走了,这里补的是**语义层**:
    /// 对方"正在做什么"。为什么值得单独发一份:
    ///  - 动画哈希只在同一套 AnimatorController 下有意义,状态名是稳定的语义标识,
    ///    将来要按动作触发音效/特效/世界结算,靠的是它而不是哈希;
    ///  - 只在**状态切换时**发(可靠通道),动作切换是低频事件,
    ///    每帧带一个字符串纯属浪费带宽。
    ///
    /// 当前范围是"看得见对方在干什么";动作的**世界后果**(树被砍倒、作物被浇)
    /// 属于主机权威结算,依赖交互拦截,是下一步。
    /// </summary>
    internal static class ActionSync
    {
        private static string _lastSent = "";
        private static float _lastSentAt;

        /// <summary>切换太频繁时的最小间隔,防止状态机抖动刷屏。</summary>
        private const float MinInterval = 0.1f;

        public static void Tick(CoopSession session)
        {
            try
            {
                string state = ReadLocalActionState();
                if (state == _lastSent) return;
                if (Time.unscaledTime - _lastSentAt < MinInterval) return;

                var agent = DolocAPI.agent;
                Vector3 pos = agent != null ? agent.transform.position : Vector3.zero;

                _lastSent = state;
                _lastSentAt = Time.unscaledTime;
                session.SendAction(state, pos.x, pos.y);
                // 动作切换本来就低频,直接全记 —— 之前用采样导致一整局只发一次的动作
                // 永远达不到采样阈值,日志里什么都看不到
                NetLog.Log($"ACTION_SEND state={state} pos=({pos.x:F1},{pos.y:F1})");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[ActionSync] 读取动作失败: " + e.Message);
            }
        }

        /// <summary>本地玩家当前的动作状态名,例如 AgentStateIdle / AgentStateFishing。</summary>
        public static string ReadLocalActionState()
        {
            try
            {
                var agent = DolocAPI.agent;
                if (agent == null) return "";
                var cur = agent.StateManager?.current;
                return cur != null ? cur.GetType().Name : "";
            }
            catch { return ""; }
        }

        /// <summary>收到远端动作:目前只记录,后续用于驱动化身的特效与音效。</summary>
        public static void OnRemoteAction(RemotePeer peer, string state, float x, float y)
        {
            NetLog.Log($"ACTION_RECV id={peer.Id} state={state} pos=({x:F1},{y:F1})");
        }

        /// <summary>把状态名翻译成人话,给面板显示。</summary>
        public static string Friendly(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return "";
            switch (stateName)
            {
                case "AgentStateIdle": return "站着";
                case "AgentStateMove": return "走路";
                case "AgentStateDash": return "冲刺";
                case "AgentStateJump": return "跳跃";
                case "AgentStateClimb": return "攀爬";
                case "AgentStateEat": return "吃东西";
                case "AgentStateSit": return "坐着";
                case "AgentStateInteract": return "交互";
                case "AgentStateFishing":
                case "AgentStateFishingCast":
                case "AgentStateFishingWait":
                case "AgentStateFishingPull":
                case "AgentStateFishingBattle":
                case "AgentStateFishingReady": return "钓鱼";
                case "AgentStateFaint": return "晕倒";
                case "AgentStateHit": return "受伤";
                default:
                    return stateName.StartsWith("AgentState") ? stateName.Substring(10) : stateName;
            }
        }
    }
}

