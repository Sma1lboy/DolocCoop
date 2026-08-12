using System;
using System.Collections.Generic;
using CoopCore;
using Steamworks;

namespace DolocCoop
{
    /// <summary>
    /// ITransport 的 Steam 实现:SteamMatchmaking 大厅 + 经典 SteamNetworking P2P
    /// (与鸭科夫 COOP Mod 同方案:AllowP2PPacketRelay 走 Steam 中继穿 NAT)。
    /// </summary>
    public sealed class SteamTransport : ITransport
    {
        private const string LobbyModIdKey = "mod_id";
        private const string LobbyVersionKey = "version";
        private const string LobbyModIdentifier = "DolocCoop";

        private readonly Action<string> _log;
        private readonly List<CSteamID> _peers = new List<CSteamID>();
        private byte[] _recvBuffer = new byte[16 * 1024];   // 不够会自动扩容,见 Pump

        private CSteamID _lobbyId = CSteamID.Nil;
        private bool _isHost;

        private CallResult<LobbyCreated_t> _lobbyCreated;
        private Callback<LobbyEnter_t> _lobbyEnter;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<P2PSessionRequest_t> _p2pSessionRequest;
        private Callback<P2PSessionConnectFail_t> _p2pConnectFail;

        public bool IsHost => _isHost;
        public ulong SelfId => SteamUser.GetSteamID().m_SteamID;
        public bool IsInLobby => _lobbyId != CSteamID.Nil;
        public string LobbyIdText => _lobbyId.m_SteamID.ToString();

        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action<ulong, byte[], int> MessageReceived;

        /// <summary>进入大厅(自己建的或接受邀请加入的)。参数:是否房主。</summary>
        public event Action<bool> LobbyEntered;

        public SteamTransport(Action<string> log)
        {
            _log = log ?? (_ => { });
            SteamNetworking.AllowP2PPacketRelay(true);
            _lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            _lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _p2pSessionRequest = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
            _p2pConnectFail = Callback<P2PSessionConnectFail_t>.Create(OnP2PConnectFail);
        }

        // ---- 大厅 ----

        public void CreateLobby(int maxMembers = 4)
        {
            _isHost = true;
            var call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers);
            _lobbyCreated.Set(call);
            _log("创建大厅中…");
        }

        public void LeaveLobby()
        {
            if (_lobbyId != CSteamID.Nil)
            {
                SteamMatchmaking.LeaveLobby(_lobbyId);
                _lobbyId = CSteamID.Nil;
            }
            foreach (var p in _peers) SteamNetworking.CloseP2PSessionWithUser(p);
            _peers.Clear();
            _isHost = false;
        }

        public void OpenInviteDialog()
        {
            if (_lobbyId != CSteamID.Nil)
                SteamFriends.ActivateGameOverlayInviteDialog(_lobbyId);
        }

        // ---- 好友列表与直接邀请(不依赖 Steam 覆盖层) ----

        public struct FriendInfo
        {
            public ulong Id;
            public string Name;
            public bool InThisGame;    // 正在玩多洛可小镇
            public bool InOtherGame;   // 在玩别的游戏
            public bool Online;
        }

        /// <summary>取好友列表。正在玩本游戏的排最前,其次在线的。</summary>
        public List<FriendInfo> GetFriends(int max = 200)
        {
            var list = new List<FriendInfo>();
            try
            {
                int n = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
                for (int i = 0; i < n; i++)
                {
                    var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                    var state = SteamFriends.GetFriendPersonaState(id);
                    bool online = state != EPersonaState.k_EPersonaStateOffline;
                    bool inThis = false, inOther = false;
                    if (SteamFriends.GetFriendGamePlayed(id, out FriendGameInfo_t gi) && gi.m_gameID.AppID().m_AppId != 0)
                    {
                        if (gi.m_gameID.AppID() == SteamUtils.GetAppID()) inThis = true;
                        else inOther = true;
                    }

                    if (!online && !inThis) continue;   // 离线的不列
                    list.Add(new FriendInfo
                    {
                        Id = id.m_SteamID,
                        Name = SteamFriends.GetFriendPersonaName(id),
                        InThisGame = inThis,
                        InOtherGame = inOther,
                        Online = online
                    });
                }
                // 排序:在玩本游戏 > 在玩别的游戏 > 单纯在线,同档按名字
                list.Sort((a, b) =>
                {
                    int ra = a.InThisGame ? 0 : (a.InOtherGame ? 1 : 2);
                    int rb = b.InThisGame ? 0 : (b.InOtherGame ? 1 : 2);
                    if (ra != rb) return ra.CompareTo(rb);
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
                if (list.Count > max) list.RemoveRange(max, list.Count - max);
            }
            catch (Exception e)
            {
                _log("读取好友列表失败: " + e.Message);
            }
            return list;
        }

        /// <summary>直接给某个好友发大厅邀请(走 Steam 通知,不需要覆盖层)。</summary>
        public bool InviteToLobby(ulong friendId)
        {
            if (_lobbyId == CSteamID.Nil) { _log("还没创建房间,无法邀请"); return false; }
            bool ok = SteamMatchmaking.InviteUserToLobby(_lobbyId, new CSteamID(friendId));
            _log(ok ? $"已向 {friendId} 发送房间邀请" : $"向 {friendId} 发送邀请失败");
            return ok;
        }

        private void OnLobbyCreated(LobbyCreated_t cb, bool ioFailure)
        {
            if (ioFailure || cb.m_eResult != EResult.k_EResultOK)
            {
                _log($"大厅创建失败: {cb.m_eResult}");
                _isHost = false;
                return;
            }
            _lobbyId = new CSteamID(cb.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(_lobbyId, LobbyModIdKey, LobbyModIdentifier);
            SteamMatchmaking.SetLobbyData(_lobbyId, LobbyVersionKey, Protocol.Version.ToString());
            _log($"大厅已创建 {_lobbyId},按邀请键呼出好友邀请");
        }

        private void OnJoinRequested(GameLobbyJoinRequested_t cb)
        {
            _log($"接受好友邀请,加入大厅 {cb.m_steamIDLobby}");
            _isHost = false;
            SteamMatchmaking.JoinLobby(cb.m_steamIDLobby);
        }

        private void OnLobbyEnter(LobbyEnter_t cb)
        {
            _lobbyId = new CSteamID(cb.m_ulSteamIDLobby);

            // 房主判定以 Steam 为准(自己建的房 owner 就是自己)
            var owner = SteamMatchmaking.GetLobbyOwner(_lobbyId);
            _isHost = owner.m_SteamID == SelfId;

            string ver = SteamMatchmaking.GetLobbyData(_lobbyId, LobbyVersionKey);
            string modId = SteamMatchmaking.GetLobbyData(_lobbyId, LobbyModIdKey);
            _log($"已进入大厅 {_lobbyId} (身份 {(_isHost ? "房主" : "客机")}, 协议 {ver}, mod {modId})");

            if (!_isHost && modId != LobbyModIdentifier)
                _log($"警告:这个房间的 mod 标识是 \"{modId}\",可能版本不匹配");

            // 与既有成员建立 P2P(握手包由 CoopSession 在 PeerConnected 时发)
            int n = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
            for (int i = 0; i < n; i++)
            {
                var member = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyId, i);
                if (member.m_SteamID != SelfId) AddPeer(member);
            }

            LobbyEntered?.Invoke(_isHost);
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t cb)
        {
            var who = new CSteamID(cb.m_ulSteamIDUserChanged);
            var change = (EChatMemberStateChange)cb.m_rgfChatMemberStateChange;
            if (change == EChatMemberStateChange.k_EChatMemberStateChangeEntered)
            {
                _log($"成员加入: {SteamFriends.GetFriendPersonaName(who)}");
                AddPeer(who);
            }
            else
            {
                _log($"成员离开: {SteamFriends.GetFriendPersonaName(who)}");
                RemovePeer(who);
            }
        }

        private void AddPeer(CSteamID id)
        {
            if (_peers.Contains(id)) return;
            _peers.Add(id);
            PeerConnected?.Invoke(id.m_SteamID);
        }

        private void RemovePeer(CSteamID id)
        {
            if (_peers.Remove(id))
            {
                SteamNetworking.CloseP2PSessionWithUser(id);
                PeerDisconnected?.Invoke(id.m_SteamID);
            }
        }

        // ---- P2P ----

        private void OnP2PSessionRequest(P2PSessionRequest_t cb)
        {
            // 判断依据必须是"Steam 说他在我的大厅里",而不是"我本地已经登记了他"。
            //
            // 竞态:对方进大厅后可能**立刻**开始发包,他的 P2P 会话请求会早于
            // 我们处理大厅成员列表到达。这时本地 _peers 还是空的,
            // 按旧逻辑就会拒绝 —— 而 Steam 不会重试,连接从此永远建不起来。
            // 这种问题只在真实联机里出现,本地回环测试完全碰不到。
            if (!IsLobbyMember(cb.m_steamIDRemote))
            {
                _log($"拒绝陌生人的 P2P 请求: {cb.m_steamIDRemote}");
                return;
            }

            SteamNetworking.AcceptP2PSessionWithUser(cb.m_steamIDRemote);
            AddPeer(cb.m_steamIDRemote);   // 顺手补登记,免得后面广播漏掉他
            _log($"接受 P2P 会话: {cb.m_steamIDRemote}");
        }

        /// <summary>直接问 Steam:这个人是不是我当前大厅的成员。</summary>
        private bool IsLobbyMember(CSteamID who)
        {
            if (_lobbyId == CSteamID.Nil) return false;
            try
            {
                int n = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
                for (int i = 0; i < n; i++)
                    if (SteamMatchmaking.GetLobbyMemberByIndex(_lobbyId, i) == who) return true;
            }
            catch (Exception e) { _log("查询大厅成员失败: " + e.Message); }
            return false;
        }

        private void OnP2PConnectFail(P2PSessionConnectFail_t cb)
        {
            _log($"P2P 连接失败: {cb.m_steamIDRemote} err={cb.m_eP2PSessionError}");
        }

        private int _sendFailures;

        /// <summary>
        /// Steam 不可靠通道的实际上限约 1200 字节(一个 MTU),超了会被直接丢弃。
        /// 我们的不可靠包只有位置/动画,远小于这个数;真超了说明协议改坏了,
        /// 与其静默丢包不如降级成可靠通道并报警。
        /// </summary>
        private const int UnreliableMaxBytes = 1100;

        public void Send(ulong peerId, byte[] data, int length, SendMode mode)
        {
            if (mode == SendMode.Unreliable && length > UnreliableMaxBytes)
            {
                _log($"不可靠包超限({length} > {UnreliableMaxBytes}),降级为可靠发送 —— 协议可能改坏了");
                mode = SendMode.Reliable;
            }

            var type = mode == SendMode.Reliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliable;
            if (SteamNetworking.SendP2PPacket(new CSteamID(peerId), data, (uint)length, type)) return;

            // 发送失败要能看见 —— 静默丢包会让"对方状态卡住"变成无从下手的怪现象
            _sendFailures++;
            if (_sendFailures == 1 || _sendFailures % 100 == 0)
                _log($"P2P 发送失败(累计 {_sendFailures} 次) 目标={peerId} 大小={length}");
        }

        public void Broadcast(byte[] data, int length, SendMode mode)
        {
            // 复制一份再遍历:发送过程中回调可能改动 _peers(有人进/退大厅)
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                if (i >= _peers.Count) continue;
                Send(_peers[i].m_SteamID, data, length, mode);
            }
        }

        public void Pump()
        {
            while (SteamNetworking.IsP2PPacketAvailable(out uint size))
            {
                // 缓冲区不够就扩容,**不能丢包**。
                // 箱子同步一次可发 16 个箱子、每箱几十格带物品名,
                // 大基地里完全可能超过十几 KB;丢了的话可靠传输也救不回来
                // —— 那是我们自己扔的,Steam 认为已经送达。
                if (size > _recvBuffer.Length)
                {
                    int grown = 1; while (grown < (int)size) grown <<= 1;   // 向上取到 2 的幂
                    _log($"收包缓冲区扩容 {_recvBuffer.Length} → {grown}(收到 {size} 字节的包)");
                    _recvBuffer = new byte[grown];
                }

                if (SteamNetworking.ReadP2PPacket(_recvBuffer, (uint)_recvBuffer.Length, out uint read, out CSteamID from))
                    MessageReceived?.Invoke(from.m_SteamID, _recvBuffer, (int)read);
            }
        }
    }
}

