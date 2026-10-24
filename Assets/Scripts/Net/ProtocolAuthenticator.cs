using System;
using System.Collections;
using UnityEngine;
using FishNet.Authenticating;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;

/// <summary>
/// 协议版本握手认证：客户端连接后上报协议版本，服务器校验。
/// 版本不符立即拒绝；5 秒内未完成认证的连接被踢出。
/// </summary>
public class ProtocolAuthenticator : Authenticator
{
    public int requiredVersion = 2;

    [Serializable]
    public struct VersionBroadcast : IBroadcast
    {
        public int Version;
    }

    [Serializable]
    public struct ResponseBroadcast : IBroadcast
    {
        public bool Passed;
    }

    public override event Action<NetworkConnection, bool> OnAuthenticationResult;

    public override void InitializeOnce(NetworkManager networkManager)
    {
        base.InitializeOnce(networkManager);
        NetworkManager.ClientManager.OnClientConnectionState += HandleClientState;
        NetworkManager.ServerManager.RegisterBroadcast<VersionBroadcast>(HandleVersion, false);
        NetworkManager.ClientManager.RegisterBroadcast<ResponseBroadcast>(HandleResponse);
    }

    private void OnDestroy()
    {
        if (NetworkManager == null) return;
        NetworkManager.ClientManager.OnClientConnectionState -= HandleClientState;
        NetworkManager.ServerManager.UnregisterBroadcast<VersionBroadcast>(HandleVersion);
        NetworkManager.ClientManager.UnregisterBroadcast<ResponseBroadcast>(HandleResponse);
    }

    public override void OnRemoteConnection(NetworkConnection connection)
    {
        base.OnRemoteConnection(connection);
        StartCoroutine(AuthTimeout(connection));
    }

    private IEnumerator AuthTimeout(NetworkConnection conn)
    {
        yield return new WaitForSeconds(5f);
        if (conn != null && conn.IsValid && !conn.IsAuthenticated)
        {
            Debug.Log($"[Net] 客户端 {conn.ClientId} 未在 5s 内完成认证，断开（可能是旧版本构建）");
            conn.Disconnect(true);
        }
    }

    private void HandleClientState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState != LocalConnectionState.Started) return;
        NetworkManager.ClientManager.Broadcast(new VersionBroadcast { Version = requiredVersion });
    }

    private void HandleVersion(NetworkConnection conn, VersionBroadcast msg, Channel channel)
    {
        if (conn.IsAuthenticated)
        {
            conn.Disconnect(true);
            return;
        }

        bool passed = msg.Version == requiredVersion;
        NetworkManager.ServerManager.Broadcast(conn, new ResponseBroadcast { Passed = passed }, false);
        OnAuthenticationResult?.Invoke(conn, passed);

        if (!passed)
            Debug.LogError($"[Net] 版本不匹配：客户端 v{msg.Version} != 服务器 v{requiredVersion}，拒绝连接");
        else
            Debug.Log($"[Net] 客户端 v{msg.Version} 认证通过");
    }

    private void HandleResponse(ResponseBroadcast msg, Channel channel)
    {
        if (!msg.Passed)
            Debug.LogError("[Net] 认证失败：协议版本不匹配，请使用与服务器一致的构建");
        else
            Debug.Log("[Net] 认证通过");
    }
}
