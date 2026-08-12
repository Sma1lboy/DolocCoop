using System;

namespace CoopCore
{
    /// <summary>发送语义。位置等高频数据用 Unreliable,事件/交互必须 Reliable。</summary>
    public enum SendMode
    {
        Reliable,
        Unreliable,
    }

    /// <summary>
    /// 传输层抽象。游戏侧提供实现(Doloc 用 Steamworks.NET 的
    /// SteamNetworkingMessages;将来别的游戏可以换 LiteNetLib 等)。
    /// peerId 对 Steam 实现即 CSteamID.m_SteamID。
    /// </summary>
    public interface ITransport
    {
        bool IsHost { get; }
        ulong SelfId { get; }

        event Action<ulong> PeerConnected;
        event Action<ulong> PeerDisconnected;
        event Action<ulong, byte[], int> MessageReceived;

        void Send(ulong peerId, byte[] data, int length, SendMode mode);
        void Broadcast(byte[] data, int length, SendMode mode);

        /// <summary>每帧驱动收包(游戏侧在 Update 里调用)。</summary>
        void Pump();
    }
}
