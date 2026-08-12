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
        private readonly byte[] _recvBuffer = new byte[16 * 1024];

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
            // 只接受同大厅成员
            if (_peers.Contains(cb.m_steamIDRemote))
            {
                SteamNetworking.AcceptP2PSessionWithUser(cb.m_steamIDRemote);
                _log($"接受 P2P 会话: {cb.m_steamIDRemote}");
            }
        }

        private void OnP2PConnectFail(P2PSessionConnectFail_t cb)
        {
            _log($"P2P 连接失败: {cb.m_steamIDRemote} err={cb.m_eP2PSessionError}");
        }

        public void Send(ulong peerId, byte[] data, int length, SendMode mode)
        {
            var type = mode == SendMode.Reliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliable;
            SteamNetworking.SendP2PPacket(new CSteamID(peerId), data, (uint)length, type);
        }

        public void Broadcast(byte[] data, int length, SendMode mode)
        {
            foreach (var p in _peers) Send(p.m_SteamID, data, length, mode);
        }

        public void Pump()
        {
            while (SteamNetworking.IsP2PPacketAvailable(out uint size))
            {
                if (size > _recvBuffer.Length)
                {
                    // 超大包直接丢弃(协议里不该出现)
                    SteamNetworking.ReadP2PPacket(new byte[size], size, out _, out _);
                    continue;
                }
                if (SteamNetworking.ReadP2PPacket(_recvBuffer, (uint)_recvBuffer.Length, out uint read, out CSteamID from))
                    MessageReceived?.Invoke(from.m_SteamID, _recvBuffer, (int)read);
            }
        }
    }
}
