using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.Core;
using UnityEngine;

// Push is an invalidation hint. Periodic reads also recover missed notifications.
public sealed class MarketLiveUpdates : MonoBehaviour
{
    public event Action Changed;
    private ISubscriptionEvents subscription;
    private string player;
    private bool connecting, destroyed, dirty;
    private float nextRefresh, nextSubscribe;
    private static DateTime nextNotifyAttempt;
    private static bool warned;

    private void Update()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) return;
        string current = AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : null;
        if (current != player) { player = current; _ = Disconnect(); nextSubscribe = 0; dirty = true; }
        if (player == null) return;
        if (subscription == null && !connecting && Time.unscaledTime >= nextSubscribe) _ = Connect();
        if (Time.unscaledTime >= nextRefresh || dirty)
        {
            dirty = false; nextRefresh = Time.unscaledTime + 5f;
            Changed?.Invoke();
        }
    }
    private async Task Connect()
    {
        connecting = true; nextSubscribe = Time.unscaledTime + 30f;
        string expected = player;
        try
        {
            var callbacks = new SubscriptionEventCallbacks();
            callbacks.MessageReceived += message => { if (message.MessageType == "MarketChanged") dirty = true; };
            callbacks.ConnectionStateChanged += _ => { dirty = true; };
            callbacks.Kicked += () => { _ = Disconnect(); };
            var result = await CloudCodeService.Instance.SubscribeToProjectMessagesAsync(callbacks);
            if (destroyed || expected != player) await result.UnsubscribeAsync(); else subscription = result;
        }
        catch (Exception) { /* Five-second refresh remains available. */ }
        finally { connecting = false; }
    }
    private async Task Disconnect()
    {
        var previous = subscription; subscription = null;
        if (previous != null) try { await previous.UnsubscribeAsync(); } catch (Exception) { }
    }
    private void OnDestroy() { destroyed = true; _ = Disconnect(); }
    public static async void NotifyCommitted()
    {
        if (DateTime.UtcNow < nextNotifyAttempt) return;
        try { await CloudCodeService.Instance.CallModuleEndpointAsync<object>("MarketNotifications", "NotifyChanged", new()); }
        catch (Exception)
        {
            nextNotifyAttempt = DateTime.UtcNow.AddSeconds(30);
            if (!warned) Debug.Log("거래는 저장되었습니다. Push 모듈 연결 전에는 5초 자동 갱신을 사용합니다.");
            warned = true;
        }
    }
}
