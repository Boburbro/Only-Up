using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace OnlyUp.Net
{
    /// <summary>
    /// Bitta xona. Har kim o'z personajini o'zi boshqaradi va holatini xona egasiga yuboradi;
    /// xona egasi hammaning holatini qolganlarga tarqatadi. O'yinchilar bir-biriga to'qnashmaydi.
    /// </summary>
    public class NetRoom : MonoBehaviour
    {
        public const int MaxPlayers = 8;
        public const float SendInterval = 1f / 15f;
        const string Version = "tepaga-2";

        const string HelloMessage = "hello";
        const string RosterMessage = "roster";
        const string PoseMessage = "pose";
        const string SnapshotMessage = "snapshot";

        readonly Dictionary<ulong, PlayerInfo> players = new Dictionary<ulong, PlayerInfo>();
        readonly Dictionary<ulong, PlayerPose> poses = new Dictionary<ulong, PlayerPose>();

        NetworkManager network;
        UnityTransport transport;
        PlayerInfo me;
        PlayerPose myPose;
        bool hasPose;
        bool closed;
        bool leaving;
        double clockOffset;
        bool clockSynced;
        float sendTimer;

        /// <summary>O'yin qatnashayotgan xona; yolg'iz o'yinda null.</summary>
        public static NetRoom Current { get; set; }
        /// <summary>Xona yopilganda bosh menyuda ko'rsatiladigan sabab.</summary>
        public static string LastMessage;

        public string Code { get; private set; } = "";
        public bool IsHost => network != null && network.IsServer;
        /// <summary>Xonaga to'liq kirilgan: o'yinni boshlash mumkin.</summary>
        public bool Ready { get; private set; }
        public bool Closed => closed;
        public string CloseReason { get; private set; } = "";
        public ulong LocalId => network != null ? network.LocalClientId : 0;
        public IReadOnlyDictionary<ulong, PlayerInfo> Players => players;
        /// <summary>Boshqa o'yinchilarning oxirgi kelgan holati.</summary>
        public IReadOnlyDictionary<ulong, PlayerPose> Poses => poses;

        public event Action<string> WasClosed;

        /// <summary>Xona egasi ochgandan beri o'tgan vaqt (hamma uchun bir xil).</summary>
        public double Time => UnityEngine.Time.realtimeSinceStartupAsDouble + clockOffset;

        public static NetRoom Create()
        {
            var go = new GameObject("NetRoom");
            DontDestroyOnLoad(go);
            var room = go.AddComponent<NetRoom>();
            room.Setup();
            return room;
        }

        void Setup()
        {
            // NetworkManager birinchi qo'shiladi: obyekt o'chirilsa, u transportdan oldin yopilishi kerak.
            network = gameObject.AddComponent<NetworkManager>();
            transport = gameObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                // Har kim sahnani o'zi yuklaydi, tarmoq faqat o'yinchilar holatini tashiydi.
                EnableSceneManagement = false,
                ConnectionApproval = true
            };
            network.OnClientConnectedCallback += OnConnected;
            network.OnClientDisconnectCallback += OnDisconnected;
            network.OnTransportFailure += () => Close("Network connection lost");
        }

        // ---------- Ulanish ----------

        /// <summary>Internet orqali xona ochadi (Unity Relay). Kod <see cref="Code"/> da bo'ladi.</summary>
        public async Task HostOnlineAsync(PlayerInfo info)
        {
            var (data, code) = await RelayConnector.CreateAsync(MaxPlayers - 1);
            if (this == null || closed) return;
            transport.UseWebSockets = true;
            transport.SetRelayServerData(data);
            Code = code;
            StartHost(info);
        }

        public async Task JoinOnlineAsync(string code, PlayerInfo info)
        {
            var data = await RelayConnector.JoinAsync(code);
            if (this == null || closed) return;
            transport.UseWebSockets = true;
            transport.SetRelayServerData(data);
            Code = code;
            StartClient(info);
        }

        /// <summary>Relay'siz to'g'ridan-to'g'ri ulanish (testlar va mahalliy tarmoq uchun).</summary>
        public void HostDirect(ushort port, PlayerInfo info, bool webSockets = false)
        {
            transport.UseWebSockets = webSockets;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            Code = "LOCAL";
            StartHost(info);
        }

        public void JoinDirect(string address, ushort port, PlayerInfo info, bool webSockets = false)
        {
            transport.UseWebSockets = webSockets;
            transport.SetConnectionData(address, port);
            Code = "LOCAL";
            StartClient(info);
        }

        void StartHost(PlayerInfo info)
        {
            me = info;
            clockOffset = -UnityEngine.Time.realtimeSinceStartupAsDouble;
            clockSynced = true;
            network.ConnectionApprovalCallback = Approve;
            network.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes(Version);
            if (!network.StartHost())
            {
                Close("Could not create the room");
                return;
            }
            Register();
            players[LocalId] = me;
            Ready = true;
        }

        void StartClient(PlayerInfo info)
        {
            me = info;
            network.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes(Version);
            if (!network.StartClient())
            {
                Close("Could not join the room");
                return;
            }
            Register();
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            string version = System.Text.Encoding.ASCII.GetString(request.Payload ?? new byte[0]);
            bool own = request.ClientNetworkId == NetworkManager.ServerClientId;
            if (!own && version != Version) response.Reason = "Game version mismatch";
            else if (!own && network.ConnectedClientsIds.Count >= MaxPlayers) response.Reason = "The room is full";
            else response.Approved = true;
            response.CreatePlayerObject = false;
        }

        void Register()
        {
            var messages = network.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(HelloMessage, OnHello);
            messages.RegisterNamedMessageHandler(RosterMessage, OnRoster);
            messages.RegisterNamedMessageHandler(PoseMessage, OnPose);
            messages.RegisterNamedMessageHandler(SnapshotMessage, OnSnapshot);
        }

        void OnConnected(ulong id)
        {
            if (IsHost || id != LocalId) return;
            var writer = new FastBufferWriter(256, Allocator.Temp);
            using (writer)
            {
                NetText.WriteName(ref writer, me.Name);
                writer.WriteValueSafe((byte)me.Character);
                network.CustomMessagingManager.SendNamedMessage(HelloMessage, NetworkManager.ServerClientId, writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        void OnDisconnected(ulong id)
        {
            if (closed) return;
            if (IsHost && id != LocalId)
            {
                poses.Remove(id);
                if (players.Remove(id)) SendRoster();
                return;
            }
            if (IsHost) return;
            string reason = network.DisconnectReason;
            // Sababni faqat rad javobi beradi; aks holda xona yopilgan yoki aloqa uzilgan.
            if (string.IsNullOrEmpty(reason) || reason.Contains("Disconnect"))
                reason = Ready ? "The room closed or the connection was lost" : "Could not join the room";
            Close(reason);
        }

        // ---------- Xabarlar ----------

        void OnHello(ulong sender, FastBufferReader reader)
        {
            if (!IsHost) return;
            var info = new PlayerInfo { Name = NetText.ReadName(ref reader) };
            reader.ReadValueSafe(out byte character);
            info.Character = character;
            players[sender] = info;
            SendRoster();
        }

        void SendRoster()
        {
            var writer = new FastBufferWriter(64 + players.Count * 160, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe((byte)players.Count);
                foreach (var pair in players)
                {
                    writer.WriteValueSafe(pair.Key);
                    NetText.WriteName(ref writer, pair.Value.Name);
                    writer.WriteValueSafe((byte)pair.Value.Character);
                }
                SendToClients(RosterMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        void OnRoster(ulong sender, FastBufferReader reader)
        {
            if (IsHost || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out byte count);
            players.Clear();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong id);
                var info = new PlayerInfo { Name = NetText.ReadName(ref reader) };
                reader.ReadValueSafe(out byte character);
                info.Character = character;
                players[id] = info;
            }
            foreach (var gone in poses.Keys.Where(id => !players.ContainsKey(id)).ToArray()) poses.Remove(gone);
            if (players.ContainsKey(LocalId)) Ready = true;
        }

        /// <summary>O'yin har kadrda o'z holatini beradi, yuborish esa belgilangan tezlikda.</summary>
        public void SetLocalPose(PlayerPose pose)
        {
            myPose = pose;
            hasPose = true;
        }

        void OnPose(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || !players.ContainsKey(sender)) return;
            var pose = PlayerPose.Read(ref reader);
            if (pose.IsValid) poses[sender] = pose;
        }

        void OnSnapshot(ulong sender, FastBufferReader reader)
        {
            if (IsHost || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out double hostTime);
            SyncClock(hostTime);
            reader.ReadValueSafe(out byte count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong id);
                var pose = PlayerPose.Read(ref reader);
                if (id != LocalId && pose.IsValid) poses[id] = pose;
            }
        }

        void SyncClock(double hostTime)
        {
            double delay = transport.GetCurrentRtt(NetworkManager.ServerClientId) * 0.0005;
            double offset = hostTime + delay - UnityEngine.Time.realtimeSinceStartupAsDouble;
            // Birinchi marta darhol, keyin silliq: platforma o'yinchi ostida sakrab ketmasligi kerak.
            if (!clockSynced || Math.Abs(offset - clockOffset) > 0.5) clockOffset = offset;
            else clockOffset += (offset - clockOffset) * 0.05;
            clockSynced = true;
        }

        void Update()
        {
            if (!Ready || closed || (sendTimer += UnityEngine.Time.unscaledDeltaTime) < SendInterval) return;
            sendTimer = Mathf.Min(sendTimer - SendInterval, SendInterval);

            if (IsHost) SendSnapshot();
            else if (hasPose)
            {
                var writer = new FastBufferWriter(128, Allocator.Temp);
                using (writer)
                {
                    myPose.Write(ref writer);
                    network.CustomMessagingManager.SendNamedMessage(PoseMessage, NetworkManager.ServerClientId,
                        writer, NetworkDelivery.UnreliableSequenced);
                }
            }
        }

        void SendSnapshot()
        {
            // 8 o'yinchi uchun paket bitta tarmoq paketiga (~1200 bayt) sig'adi.
            var writer = new FastBufferWriter(1200, Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(Time);
                writer.WriteValueSafe((byte)(poses.Count + (hasPose ? 1 : 0)));
                if (hasPose)
                {
                    writer.WriteValueSafe(LocalId);
                    myPose.Write(ref writer);
                }
                foreach (var pair in poses)
                {
                    writer.WriteValueSafe(pair.Key);
                    pair.Value.Write(ref writer);
                }
                SendToClients(SnapshotMessage, writer, NetworkDelivery.UnreliableSequenced);
            }
        }

        void SendToClients(string name, FastBufferWriter writer, NetworkDelivery delivery)
        {
            foreach (var id in network.ConnectedClientsIds)
                if (id != LocalId)
                    network.CustomMessagingManager.SendNamedMessage(name, id, writer, delivery);
        }

        // ---------- Yopish ----------

        /// <summary>O'yinchi o'zi chiqadi. Xona egasi chiqsa, xona hamma uchun yopiladi.</summary>
        public void Leave()
        {
            Shutdown();
            if (Current == this)
            {
                Current = null;
                WorldClock.Shared = null;
            }
            if (leaving) return;
            leaving = true;
            StartCoroutine(DestroyWhenStopped());
        }

        /// <summary>Tarmoq to'liq yopilmasdan obyekt o'chirilsa, transport xato beradi.</summary>
        IEnumerator DestroyWhenStopped()
        {
            yield return null;
            while (network != null && (network.ShutdownInProgress || network.IsListening)) yield return null;
            Destroy(gameObject);
        }

        /// <summary>
        /// Xona o'z-o'zidan yopildi. Obyekt va <see cref="Current"/> saqlanib qoladi:
        /// o'yin yoki menyu sababni o'qib, <see cref="Leave"/> chaqiradi.
        /// </summary>
        void Close(string reason)
        {
            if (closed) return;
            CloseReason = reason;
            Shutdown();
            WasClosed?.Invoke(reason);
        }

        void Shutdown()
        {
            if (closed) return;
            closed = true;
            Ready = false;
            if (network != null) network.Shutdown();
        }

        void OnDestroy()
        {
            if (Current != this) return;
            Current = null;
            WorldClock.Shared = null;
        }
    }
}
