using BepInEx;
using DolocShared;
using BepInEx.Logging;
using UnityEngine.InputSystem;

namespace DolocDevTools
{
    /// <summary>
    /// Mod 开发辅助。
    ///   F8 = 状态面板   F7 = dump UI树   Ctrl+F7 = dump 运行时状态
    ///   F5 = 热重载 Mod 列表   F1 = 官方调试控制台(游戏自带)
    ///
    /// 架构要点:本游戏会销毁 Mod 创建的 GameObject(DontDestroyOnLoad 也无效),
    /// 所以一切都不走 MonoBehaviour,统一由 LoopDriver(PlayerLoop 注入)驱动。
    /// </summary>
    [BepInPlugin("sma1lboy.doloctown.devtools", "DolocDevTools", "0.6.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("DolocDevTools 0.6.0 已加载");
            Log.LogInfo("  F8=状态面板  F7=dump UI树  Ctrl+F7=dump 运行时状态  F5=热重载Mod");
            Log.LogInfo("  dump 目录: " + DebugDump.Root);
            UiFont.SetLogger(s => Log.LogInfo(s));

            UnityLoopDriver.Add(PollInput);
            UnityLoopDriver.Add(StatusOverlay.Tick);
            UnityLoopDriver.Add(AutoDump.Tick);
            UnityLoopDriver.Install(s => Log.LogInfo(s));
        }

        private static void PollInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f8Key.wasPressedThisFrame)
            {
                StatusOverlay.Visible = !StatusOverlay.Visible;
                Log.LogInfo("[F8] 状态面板 " + (StatusOverlay.Visible ? "显示" : "隐藏"));
            }

            if (kb.f5Key.wasPressedThisFrame) ReloadMods();

            if (kb.f7Key.wasPressedThisFrame)
            {
                bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                if (ctrl) { DebugDump.DumpGameState(); StatusOverlay.Note("已 dump 运行时状态"); }
                else { DebugDump.DumpUiTree(); StatusOverlay.Note("已 dump UI 层级树"); }
            }
        }

        private static void ReloadMods()
        {
            var mm = DolocAPI.modManager;
            if (mm == null)
            {
                Log.LogWarning("[F5] ModManager 未初始化");
                StatusOverlay.Note("ModManager 未初始化");
                return;
            }
            mm.ReloadMods();
            int n = mm.GetAllValidModInfos().Count;
            Log.LogInfo($"[F5] Mod 列表已热重载,共 {n} 个有效 Mod");
            StatusOverlay.Note($"已热重载: {n} 个 Mod");
        }
    }
}


