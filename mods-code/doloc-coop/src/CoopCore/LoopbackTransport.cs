using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace CoopCore
{
    /// <summary>
    /// 本机/局域网 UDP 传输,用于开发期双开测试 —— 不依赖 Steam。
    /// 同一台机器起两个游戏实例:一个 StartHost(),另一个 Connect("127.0.0.1")。
    /// 仅供开发调试:无加密无重传(SendMode 被忽略),不要用于发布。
    /// </summary>
    public sealed class LoopbackTransport : ITransport, IDisposable
    {
        public const int DefaultPort = 27851;

        private readonly Action<string> _log;
        private UdpClient _udp;
        private readonly List<IPEndPoint> _peers = new List<IPEndPoint>();
        private readonly Dictionary<ulong, IPEndPoint> _peerById = new Dictionary<ulong, IPEndPoint>();
        private bool _isHost;
        private ulong _selfId;

        public bool IsHost => _isHost;
        public ulong SelfId => _selfId;
        public bool IsActive => _udp != null;

        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;   // 回环模式暂不检测断线
        public event Action<ulong, byte[], int> MessageReceived;

        public LoopbackTransport(Action<string> log)
        {
            _log = log ?? (_ => { });
        }

        public void StartHost(int port = DefaultPort)
        {
            _isHost = true;
            _selfId = 1; // 回环模式用小整数当 id:主机=1,客机=端口号
            _udp = new UdpClient(port);
            _log($"[Loopback] 主机监听 UDP {port}");
        }

        public void Connect(string host = "127.0.0.1", int port = DefaultPort)
        {
            _isHost = false;
            _udp = new UdpClient(0); // 随机本地端口
            int localPort = ((IPEndPoint)_udp.Client.LocalEndPoint).Port;
            _selfId = (ulong)localPort;
            var ep = new IPEndPoint(IPAddress.Parse(host), port);
            RegisterPeer(ep);
            // 敲门包让主机登记我们(负载为空的 0 号包)
            _udp.Send(new byte[] { 0 }, 1, ep);
            _log($"[Loopback] 已连接 {host}:{port} (本地端口 {localPort})");
        }

        private ulong IdOf(IPEndPoint ep) => (ulong)ep.Port;

        private void RegisterPeer(IPEndPoint ep)
        {
            ulong id = IdOf(ep);
            if (_peerById.ContainsKey(id)) return;
            _peers.Add(ep);
            _peerById[id] = ep;
            PeerConnected?.Invoke(id);
        }

        public void Send(ulong peerId, byte[] data, int length, SendMode mode)
        {
            if (_udp == null || !_peerById.TryGetValue(peerId, out var ep)) return;
            var packet = new byte[length + 1];
            packet[0] = 1; // 1 = 数据包
            Array.Copy(data, 0, packet, 1, length);
            _udp.Send(packet, packet.Length, ep);
        }

        public void Broadcast(byte[] data, int length, SendMode mode)
        {
            foreach (var ep in _peers)
                Send(IdOf(ep), data, length, mode);
        }

        public void Pump()
        {
            if (_udp == null) return;
            while (_udp.Available > 0)
            {
                IPEndPoint from = null;
                byte[] packet;
                try { packet = _udp.Receive(ref from); }
                catch (SocketException) { continue; }
                if (packet.Length == 0) continue;

                RegisterPeer(from); // 见包即登记(敲门包也走这里)
                if (packet[0] == 1 && packet.Length > 1)
                {
                    var data = new byte[packet.Length - 1];
                    Array.Copy(packet, 1, data, 0, data.Length);
                    MessageReceived?.Invoke(IdOf(from), data, data.Length);
                }
            }
        }

        public void Dispose()
        {
            _udp?.Close();
            _udp = null;
            _peers.Clear();
            _peerById.Clear();
        }
    }
}
