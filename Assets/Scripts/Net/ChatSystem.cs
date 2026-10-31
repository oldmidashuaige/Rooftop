using UnityEngine;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;

/// <summary>
/// 队内聊天（房间内备战）：客户端广播 → 服务器查名字转发 → 所有客户端显示。
/// 非 NetworkObject（像 GameFlow 一样用 Broadcast）。LobbyUI 调用 SendLocal。
/// </summary>
public class ChatSystem : MonoBehaviour
{
    public static ChatSystem Instance;

    [System.Serializable]
    public struct ChatBroadcast : IBroadcast
    {
        public int fromClientId;
        public string name;
        public string text;
    }

    public static void Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("ChatSystem");
            go.AddComponent<ChatSystem>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (InstanceFinder.NetworkManager == null) return;
        InstanceFinder.ServerManager.RegisterBroadcast<ChatBroadcast>(HandleServerChat, false);
        InstanceFinder.ClientManager.RegisterBroadcast<ChatBroadcast>(HandleClientChat);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (InstanceFinder.NetworkManager == null) return;
        InstanceFinder.ServerManager.UnregisterBroadcast<ChatBroadcast>(HandleServerChat);
        InstanceFinder.ClientManager.UnregisterBroadcast<ChatBroadcast>(HandleClientChat);
    }

    public void SendLocal(string text)
    {
        var cm = InstanceFinder.ClientManager;
        if (cm == null || string.IsNullOrEmpty(text)) return;
        cm.Broadcast(new ChatBroadcast { text = text });
    }

    private void HandleServerChat(NetworkConnection conn, ChatBroadcast msg, Channel channel)
    {
        string name = "玩家";
        var nm = NetworkGameManager.Instance;
        if (nm != null)
        {
            var go = nm.GetPlayer(conn.ClientId);
            if (go != null)
            {
                var rc = go.GetComponent<RoleComponent>();
                if (rc != null) name = rc.DisplayName;
            }
        }
        var relay = new ChatBroadcast { fromClientId = conn.ClientId, name = name, text = msg.text };
        var sm = InstanceFinder.ServerManager;
        if (sm == null) return;
        sm.Broadcast(relay);
        var local = InstanceFinder.ClientManager != null ? InstanceFinder.ClientManager.Connection : null;
        if (local != null && local.IsValid && local.IsActive)
            sm.Broadcast(local, relay);
    }

    private void HandleClientChat(ChatBroadcast msg, Channel channel)
    {
        if (LobbyUI.Instance != null) LobbyUI.Instance.OnChatMessage(msg.name, msg.text);
    }
}
