using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using CoopCore;
using DolocShared;
using CoopCore.Replication;
using DolocCoop.Domains;
using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DolocCoop
{
    /// <summary>
    /// 多洛可小镇 Steam 联机。热键都收在 F4–F9(键盘只有这几个可用)。
    ///   F9 = 创建 Steam 大厅   F4 = 好友邀请
    ///   F6 = 回环主机   Ctrl+F6 = 连接本机回环主机(单机双开测试)
    ///
    /// 架构要点:游戏会销毁 Mod 创建的 GameObject,所以不用 MonoBehaviour,
    /// 一切由 UnityLoopDriver(PlayerLoop 注入)驱动。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "sma1lboy.doloctown.coop";
        public const string PluginName = "DolocCoop";
        public const string PluginVersion = BuildInfo.ModVersion;   // 版本号唯一出处在 CoopCore,测试工具共用同一个值

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{PluginName} {PluginVersion} 已加载");
            Log.LogInfo("  F9=创建Steam大厅  F4=好友邀请");
            Log.LogInfo("  F6=回环主机  Ctrl+F6=连接回环主机(双开测试)");
            NetLog.Log($"{PluginName} {PluginVersion} 已加载");
            Log.LogInfo("  网络日志: " + NetLog.Path_);

            UiFont.SetLogger(s => Log.LogInfo(s));
            CoopPanel.StatusProvider = CoopRuntime.BuildStatusText;
            CoopPanel.FriendProvider = CoopRuntime.GetFriends;
            CoopPanel.OnCreateLobby = CoopRuntime.CreateSteamLobby;
            CoopPanel.OnInviteFriend = CoopRuntime.InviteFriend;
            CoopPanel.OnOpenSteamOverlay = CoopRuntime.OpenInvite;
            ChatUi.Send = CoopRuntime.SendChat;
            ChatUi.IsInSession = CoopRuntime.HasSession;

            CoopSession.GameVersion = Application.version;   // 握手时比对,版本不同直接说清楚
            SaveGuard.Install();   // 客机存档保护:必须在建立任何会话之前挂好


            CoopRuntime.Init();

            UnityLoopDriver.Add(CoopRuntime.Tick);
            UnityLoopDriver.Add(CoopMenu.Tick);
            UnityLoopDriver.Add(CoopPanel.Tick);

            UnityLoopDriver.Add(ChatUi.Tick);
            UnityLoopDriver.Add(AutoTest.Tick);
            UnityLoopDriver.Install(s => Log.LogInfo(s));
        }
    }

    /// <summary>联机运行时:输入轮询 + 网络泵 + 本地状态广播。由 PlayerLoop 驱动。</summary>
    internal static class CoopRuntime
    {
        private const float StateInterval = 1f / 15f;

        private static ITransport _transport;
        private static SteamTransport _steam;
        private static LoopbackTransport _loopback;
        private static CoopSession _session;
        private static SyncRegistry _sync;   // 所有同步域的统一入口
        private static float _stateTimer;

        /// <summary>
        /// 启动时就建好 SteamTransport —— 好友邀请是 Steam 回调推过来的,
        /// 必须在收到邀请之前就注册好回调,否则玩家点了邀请没人接。
        /// </summary>
        private static bool _steamReady;
        private static float _steamRetryTimer;
        private static int _steamAttempts;

        public static void Init()
        {
            TrySetupSteam();   // 大概率会失败,见下
        }

        /// <summary>
        /// 建立 Steam 传输并注册邀请回调。
        ///
        /// **必须重试**:插件 Awake 跑在游戏初始化 Steamworks 之前,
        /// 这时 `SteamAPI.IsSteamRunning()` 返回 true 但 API 还没 Init,
        /// 构造回调会抛 "Steamworks is not initialized"。
        /// 一次失败就放弃的话,邀请回调永远注册不上 —— 别人邀请你,你这边毫无反应。
        /// (这个问题是在多人压力测试的日志里发现的,单人测试完全看不出来。)
        /// </summary>
        private static bool TrySetupSteam()
        {
            if (_steamReady) return true;
            try
            {
                if (!SteamAPI.IsSteamRunning()) return false;
                EnsureSteam();
                _steam.LobbyEntered += OnLobbyEntered;
                _steamReady = true;
                Plugin.Log.LogInfo($"Steam 传输已就绪,可接收好友邀请(第 {_steamAttempts + 1} 次尝试)");
                NetLog.Log("STEAM_READY 已注册邀请回调");
                return true;
            }
            catch (Exception e)
            {
                _steamAttempts++;
                _steam = null;   // 半成品对象要丢掉,否则下次 EnsureSteam 会以为已经建好
                if (_steamAttempts == 1)
                    Plugin.Log.LogInfo("Steamworks 尚未初始化,稍后重试: " + e.Message);
                else if (_steamAttempts % 60 == 0)
                    Plugin.Log.LogWarning($"Steam 传输仍未就绪(已试 {_steamAttempts} 次): {e.Message}");
                return false;
            }
        }

        /// <summary>每帧调用:Steam 没就绪就每秒重试一次。</summary>
        private static void PumpSteamSetup()
        {
            if (_steamReady) return;
            _steamRetryTimer += Time.unscaledDeltaTime;
            if (_steamRetryTimer < 1f) return;
            _steamRetryTimer = 0f;
            TrySetupSteam();
        }

        /// <summary>进入大厅(自建或接受邀请):建立会话并把管理页面弹出来。</summary>
        private static void OnLobbyEntered(bool isHost)
        {
            EnsureSession(_steam);
            NetLog.Log($"LOBBY_ENTERED isHost={isHost}");
            CoopPanel.Show();
            CoopPanel.Toast(isHost ? "房间已创建,去右边邀请好友" : "已加入房间,等待房主开始");

            // 客机进房后把自己的名字报给房主,成员列表才显示得出人名
            try { _session?.SendProfile(SteamFriends.GetPersonaName(), ""); } catch { }
        }

        public static void Tick()
        {
            PollInput();
            PumpSteamSetup();   // Steamworks 比插件晚就绪,要一直重试到成功

            if (_session == null) return;
            _session.Pump();

            _stateTimer += Time.unscaledDeltaTime;
            if (_stateTimer >= StateInterval)
            {
                _stateTimer = 0f;
                if (LocalPlayerBridge.TryGetState(out float x, out float y, out bool faceLeft, out int animHash, out float animTime))
                    _session.SendLocalState(x, y, faceLeft, animHash, animTime);
            }

            ProfileSync.Tick(_session);   // 昵称/帽子变了才发,低频


            


            // 主机权威:只有房主广播世界状态与箱子内容,客机被动跟随
            if (_transport != null && _transport.IsHost)
            {
                TimeSync.TickHost(_session);
                ContainerSync.TickHost(_session);
                MissionSync.TickHost(_session);

                DropItemSync.TickHost(_session);


                _sync?.HostTick(Time.unscaledDeltaTime);
            }

            // 客机:检查地上有没有东西被自己捡走,需要上报主机(否则主机会把它生成回来)


            if (_transport != null && !_transport.IsHost)


                DropItemSync.TickClient(_session);


            


            ActionSync.Tick(_session);   // 行为:只在动作切换时发


            RemotePlayerRenderer.Tick();
        }

        private static void PollInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

            if (kb.f9Key.wasPressedThisFrame) StartSteamHost();
            if (kb.f4Key.wasPressedThisFrame) Invite();
            if (kb.f6Key.wasPressedThisFrame) StartLoopback(ctrl);
        }

        /// <summary>面板上显示的状态文本。</summary>
        public static string BuildStatusText()
        {
            var sb = new System.Text.StringBuilder();
            if (_session == null)
            {
                sb.AppendLine("<b>未连接</b>");
                sb.AppendLine();
                sb.AppendLine("点「创建房间」开一个 Steam 好友大厅,");
                sb.AppendLine("再点「邀请好友」通过 Steam 邀请对方加入。");
                sb.AppendLine();
                sb.AppendLine("<size=15>开发测试:F6 = 本机回环主机,Ctrl+F6 = 回环客机</size>");
                return sb.ToString();
            }

            bool steam = _transport == _steam && _steam != null;
            sb.AppendLine("<b>已连接</b>  传输: " + (steam ? "Steam P2P" : "本机回环"));
            if (steam)
            {
                sb.AppendLine("大厅: " + (_steam.IsInLobby ? _steam.LobbyIdText : "无"));
                sb.AppendLine("身份: " + (_steam.IsHost ? "房主" : "客机"));
            }
            sb.AppendLine("游戏时间: " + TimeSync.Describe() + (steam || _transport != null
                ? (_transport.IsHost ? "  <size=13>(本机为时间权威)</size>" : "  <size=13>(跟随房主)</size>")
                : ""));
            sb.AppendLine();
            sb.AppendLine("天气: " + TimeSync.DescribeWeather());
            sb.AppendLine("任务: " + MissionSync.Describe());
            sb.AppendLine("箱子 " + ContainerSync.TrackedCount + " · 种植槽 " + (_sync?.TrackedOf("作物") ?? 0) + " · 地上掉落物 " + DropItemSync.LocalCount);
            sb.AppendLine("我的动作: " + ActionSync.Friendly(ActionSync.ReadLocalActionState()));
            if (SaveGuard.IsClient)
                sb.AppendLine("<color=#ffd479>存档保护: 本地只读,已拦截 " + SaveGuard.BlockedCount + " 次存盘</color>");
            sb.AppendLine();
            sb.AppendLine("<b>成员 (" + _session.Peers.Count + ")</b>");
            if (_session.Peers.Count == 0) sb.AppendLine("  (还没有其他玩家加入)");
            foreach (var p in _session.Peers.Values)
                sb.AppendLine($"  · {(string.IsNullOrEmpty(p.Name) ? p.Id.ToString() : p.Name)}  {ActionSync.Friendly(p.ActionState)}  pos=({p.X:F1},{p.Y:F1})");
            return sb.ToString();
        }

        public static void CreateSteamLobby() => StartSteamHost();
        public static void OpenInvite() => Invite();

        /// <summary>聊天界面用:当前是否在会话中。</summary>
        public static bool HasSession() => _session != null;

        /// <summary>聊天界面用:发一句话。</summary>
        public static void SendChat(string text)
        {
            if (_session == null) return;
            _session.SendChat(text);
            NetLog.Log($"CHAT_SEND {text}");
        }

        /// <summary>供 AutoTest 无人值守调用:开回环主机(等价于按 F6)。</summary>
        public static void StartLoopbackHostForTest() => StartLoopback(asClient: false);
        public static void StartLoopbackClientForTest() => StartLoopback(asClient: true);

        /// <summary>给面板用的好友列表(需要先有 SteamTransport;没有就临时建一个只为读好友)。</summary>
        public static List<SteamTransport.FriendInfo> GetFriends()
        {
            try
            {
                if (!SteamAPI.IsSteamRunning()) return new List<SteamTransport.FriendInfo>();
                EnsureSteam();
                return _steam.GetFriends();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[好友] 读取失败: " + e.Message);
                return new List<SteamTransport.FriendInfo>();
            }
        }

        /// <summary>页面内直接邀请:不弹 Steam 覆盖层,走 InviteUserToLobby。</summary>
        public static void InviteFriend(ulong friendId)
        {
            try
            {
                if (_steam == null || !_steam.IsInLobby)
                {
                    Plugin.Log.LogInfo("[邀请] 还没有房间,先自动创建一个");
                    CoopPanel.Toast("正在创建房间…");
                    StartSteamHost();
                    return;   // 大厅创建是异步的,建好后再点邀请
                }
                bool ok = _steam.InviteToLobby(friendId);
                CoopPanel.Toast(ok ? "邀请已发送" : "邀请发送失败");
                NetLog.Log($"INVITE to={friendId} ok={ok}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[邀请] 失败: " + e);
                CoopPanel.Toast("邀请出错: " + e.Message);
            }
        }

        private static void EnsureSteam()
        {
            if (_steam == null)
                _steam = new SteamTransport(s => { Plugin.Log.LogInfo("[Net] " + s); NetLog.Log("NET " + s); });
        }

        private static void StartSteamHost()
        {
            Plugin.Log.LogInfo("[F9] 请求创建 Steam 大厅…");
            NetLog.Log("F9 创建大厅");
            try
            {
                if (!SteamAPI.IsSteamRunning())
                {
                    Plugin.Log.LogWarning("[F9] Steam 未运行,无法建大厅");
                    NetLog.Log("失败: Steam 未运行");
                    return;
                }
                if (_steam == null)
                {
                    _steam = new SteamTransport(s => { Plugin.Log.LogInfo("[Net] " + s); NetLog.Log("NET " + s); });
                    Plugin.Log.LogInfo("[F9] SteamTransport 已创建");
                }
                EnsureSession(_steam);
                _steam.CreateLobby();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[F9] 建大厅失败: " + e);
                NetLog.Log("EXCEPTION 建大厅: " + e);
            }
        }

        private static void Invite()
        {
            Plugin.Log.LogInfo("[F4] 打开好友邀请界面");
            NetLog.Log("F4 邀请");
            if (_steam == null) { Plugin.Log.LogWarning("[F4] 还没建大厅(先按 F9)"); return; }
            try { _steam.OpenInviteDialog(); }
            catch (Exception e) { Plugin.Log.LogError("[F4] 邀请失败: " + e); }
        }

        private static void StartLoopback(bool asClient)
        {
            Plugin.Log.LogInfo($"[F6] 回环{(asClient ? "客机" : "主机")}");
            NetLog.Log($"F6 回环 {(asClient ? "client" : "host")}");
            try
            {
                if (_loopback == null)
                    _loopback = new LoopbackTransport(s => { Plugin.Log.LogInfo("[Net] " + s); NetLog.Log("NET " + s); });
                EnsureSession(_loopback);
                if (asClient) _loopback.Connect(); else _loopback.StartHost();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[F6] 回环失败: " + e);
                NetLog.Log("EXCEPTION 回环: " + e);
            }
        }

        private static void EnsureSession(ITransport transport)
        {
            if (_session != null && _transport == transport) return;
            _session?.Dispose();
            _transport = transport;
            _session = new CoopSession(transport, Plugin.PluginVersion);
            // 同步域集中注册:加新领域只需在这里加一行,
            // 计时/差分/重发/路由都由 SyncRegistry 负责
            _sync = new SyncRegistry(_session, s => { Plugin.Log.LogInfo(s); NetLog.Log(s); });
            _sync.Register(new CropDomain());
            SaveGuard.SetClient(!transport.IsHost);   // 客机身份 → 本地存档转只读
            _session.Log += s => { Plugin.Log.LogInfo("[Session] " + s); NetLog.Log("SESSION " + s); };
            _session.Rejected += (id, why) =>
            {
                Plugin.Log.LogWarning($"[Session] 握手被拒 {id}: {why}");
                NetLog.Log($"HANDSHAKE_REJECT id={id} why={why}");
                CoopPanel.Toast("无法联机:" + why);
            };
            _session.PeerJoined += p =>
            {
                Plugin.Log.LogInfo($"[Session] 玩家加入: {p.Id}");
                NetLog.Log($"PEER_JOINED id={p.Id}");
                // 新人进房立刻补发世界状态,不用等心跳
                if (_transport != null && _transport.IsHost) TimeSync.ForceSendNext();
                if (_transport != null && _transport.IsHost) ContainerSync.ResendAll();
                if (_transport != null && _transport.IsHost) MissionSync.ResendAll();
                if (_transport != null && _transport.IsHost) _sync?.ResendAll();
                if (_transport != null && _transport.IsHost) DropItemSync.ResendAll();
                ProfileSync.Resend();   // 新人进房:把自己的昵称与帽子再报一次
            };
            _session.PeerLeft += p =>
            {
                Plugin.Log.LogInfo($"[Session] 玩家离开: {p.Id}");
                NetLog.Log($"PEER_LEFT id={p.Id}");
                RemotePlayerRenderer.Remove(p.Id);
            };
            _session.ChatReceived += (id, text) =>
            {
                Plugin.Log.LogInfo($"[Chat] {id}: {text}");
                NetLog.Log($"CHAT from={id}: {text}");
                string who = _session.Peers.TryGetValue(id, out var p2) && !string.IsNullOrEmpty(p2.Name)
                    ? p2.Name : id.ToString();
                ChatUi.AddIncoming(who, text);
            };
            _session.WorldSyncReceived += (hostSeconds, weather) =>
            {
                NetLog.Sample(5, $"WORLD_RECV time={hostSeconds} regions={weather.Count}");
                if (_transport != null && !_transport.IsHost)
                {
                    TimeSync.ApplyRemote(hostSeconds);
                    TimeSync.ApplyRemoteWeather(weather);
                }
            };
            _session.PeerStateUpdated += p =>
            {
                NetLog.Sample(30, $"PEER_STATE id={p.Id} pos=({p.X:F2},{p.Y:F2}) faceLeft={p.FacingLeft} anim={p.AnimHash}");
                RemotePlayerRenderer.Upsert(p);
            };
            _session.ContainersReceived += states =>
            {
                if (_transport != null && !_transport.IsHost) ContainerSync.ApplyRemote(states);
            };
            _session.MissionsReceived += ids =>
            {
                if (_transport != null && !_transport.IsHost) MissionSync.ApplyRemote(ids);
            };
            _session.DropItemsReceived += drops =>
            {
                if (_transport != null && !_transport.IsHost) DropItemSync.ApplyRemote(drops);
            };
            _session.DropPickupReceived += (from, picked) =>
            {
                if (_transport != null && _transport.IsHost) DropItemSync.HandlePickup(from, picked);
            };
            _session.PeerActionReceived += (peer, state, ax, ay) => ActionSync.OnRemoteAction(peer, state, ax, ay);
            NetLog.Log($"会话已建立 transport={transport.GetType().Name} selfId={transport.SelfId}");
        }
    }

    /// <summary>读取本地玩家状态。入口 DolocAPI.agent (BodyController) + AgentRenderer 的 Animator。</summary>
    internal static class LocalPlayerBridge
    {
        public static bool TryGetState(out float x, out float y, out bool facingLeft, out int animHash, out float animTime)
        {
            x = y = 0f; facingLeft = false; animHash = 0; animTime = 0f;
            BodyControllerRef agent;
            try { agent = new BodyControllerRef(DolocAPI.agent); }
            catch { return false; }
            if (!agent.Valid) return false;

            var pos = agent.Value.transform.position;
            x = pos.x; y = pos.y;
            facingLeft = !agent.Value.IsFaceRight;
            try
            {
                var info = DolocAPI.AgentRenderer.currentAnimatorStateInfo;
                animHash = info.shortNameHash;
                animTime = info.normalizedTime % 1f;
            }
            catch { }
            return true;
        }

        private readonly struct BodyControllerRef
        {
            public readonly DolocTown.BodyController Value;
            public BodyControllerRef(DolocTown.BodyController v) { Value = v; }
            public bool Valid => Value != null && Value.gameObject.activeInHierarchy;
        }
    }
}





















