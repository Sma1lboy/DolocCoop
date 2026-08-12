using UnityEngine;
using UnityEngine.SceneManagement;

namespace DolocDevTools
{
    /// <summary>
    /// 自动 dump:场景加载后按时间表多次导出 UI(启动画面 → 标题界面 → 存档界面
    /// 是同一个场景里陆续构建出来的,只 dump 一次会抓到半成品),
    /// 玩家就绪后再导出一次运行时状态。
    /// 由 LoopDriver 驱动(不能用 MonoBehaviour,游戏会销毁 Mod 的 GameObject)。
    /// </summary>
    public static class AutoDump
    {
        /// <summary>场景加载后的 dump 时间点(秒)。</summary>
        private static readonly float[] Schedule = { 6f, 18f, 35f, 60f };

        private static string _scene = "";
        private static float _sceneAt;
        private static int _nextIndex;
        private static bool _playerDumped;
        private static float _stateDumpAt = -1f;

        public static void Tick()
        {
            float now = Time.unscaledTime;

            string scene = SceneManager.GetActiveScene().name;
            if (scene != _scene)
            {
                _scene = scene;
                _sceneAt = now;
                _nextIndex = 0;
                _playerDumped = false;
                Plugin.Log.LogInfo($"[AutoDump] 场景 {scene},将在 6/18/35/60 秒各 dump 一次 UI");
            }

            if (_nextIndex < Schedule.Length && now - _sceneAt >= Schedule[_nextIndex])
            {
                int sec = (int)Schedule[_nextIndex];
                _nextIndex++;
                string tag = (string.IsNullOrEmpty(_scene) ? "unknown" : _scene) + "-t" + sec;
                DebugDump.DumpUiTree(tag);
            }

            if (!_playerDumped && PlayerReady())
            {
                _playerDumped = true;
                _stateDumpAt = now + 2f;
                Plugin.Log.LogInfo("[AutoDump] 玩家已就绪,2 秒后 dump 运行时状态");
            }

            if (_stateDumpAt > 0f && now >= _stateDumpAt)
            {
                _stateDumpAt = -1f;
                DebugDump.DumpGameState();
            }
        }

        private static bool PlayerReady()
        {
            try { return DolocAPI.agent != null; }
            catch { return false; }
        }
    }
}
