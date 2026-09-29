// Runs the production native wallet bridge against an in-memory wallet transport.
// No RPC, private keys, or real wallet approvals are used.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SimpleMarket;
using Reown.AppKit.Unity;
using Unity.Services.Authentication;

class Program
{
    static int checks;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
        Console.WriteLine("PASS " + name);
    }
    static async Task Reject(Func<Task> action, string name)
    {
        try { await action(); } catch (Exception) { Check(true, name); return; }
        throw new Exception("Expected rejection: " + name);
    }
    static Task<WebGlWallet.Result> Connect(ReownWalletBridge bridge) => bridge.Call(new() { action = "connect" });
    static async Task Main()
    {
        var bridge = new ReownWalletBridge();
        AuthenticationService.Instance.PlayerId = "a";
        // A global session from an older installation must not be adopted.
        AppKit.Approve("legacy");
        AppKit.NextAddress = "A";
        Check((await Connect(bridge)).address == "A" && AppKit.Modals == 1, "new a ignores legacy session and opens QR");
        AuthenticationService.Instance.PlayerId = "b";
        AppKit.NextAddress = "B";
        Check((await Connect(bridge)).address == "B" && AppKit.Modals == 2, "new b opens QR while A remains connected");
        AuthenticationService.Instance.PlayerId = "a";
        bridge = new ReownWalletBridge();
        AppKit.Instance.SignClient.AddressProvider.DefaultNamespace = null;
        Check((await Connect(bridge)).address == "A" && AppKit.Modals == 2, "a restores A after bridge recreation");
        AuthenticationService.Instance.PlayerId = "b";
        Check((await Connect(bridge)).address == "B" && AppKit.Modals == 2, "b restores B without QR");
        AuthenticationService.Instance.PlayerId = "c";
        await Reject(() => bridge.Call(new() { action = "status" }), "unconnected c cannot use b status/session");
        AppKit.NextAddress = "A";
        Check((await Connect(bridge)).address == "A" && AppKit.Modals == 3, "new c can explicitly choose A via QR");
        AuthenticationService.Instance.PlayerId = "b";
        await Connect(bridge);
        AppKit.Instance.SignClient.AddressProvider.DefaultSession.Expiry = 0;
        AppKit.NextAddress = "C";
        await Reject(() => Connect(bridge), "expired b session cannot be replaced with C");
        AppKit.NextAddress = "B";
        Check((await Connect(bridge)).address == "B", "expired b can reapprove B");
        MarketCloudClient.EnvironmentName = "other";
        Check(!bridge.HasSavedWallet, "environment isolates remembered wallet");
        AppKit.NextAddress = "C";
        Check((await Connect(bridge)).address == "C", "same player in another environment chooses fresh wallet");
        MarketCloudClient.EnvironmentName = "production";
        AuthenticationService.Instance.PlayerId = "d";
        AppKit.OnModal = () => AuthenticationService.Instance.PlayerId = "e";
        await Reject(() => Connect(bridge), "account switch during QR approval aborts assignment");
        AppKit.OnModal = null;
        AuthenticationService.Instance.PlayerId = "d";
        Check(!bridge.HasSavedWallet, "interrupted QR does not save d mapping");
        AuthenticationService.Instance.PlayerId = "e";
        Check(!bridge.HasSavedWallet, "interrupted QR does not save e mapping");
        Console.WriteLine($"{checks} checks passed");
    }
}

public static class MarketCloudClient { public static string EnvironmentName = "production"; }
namespace Unity.Services.Core
{
    public enum ServicesInitializationState { Initialized }
    public static class UnityServices { public static ServicesInitializationState State = ServicesInitializationState.Initialized; }
}
namespace Unity.Services.Authentication
{
    public class AuthenticationService
    {
        public static AuthenticationService Instance = new();
        public bool IsSignedIn = true;
        public string PlayerId;
    }
}
namespace UnityEngine
{
    public class MonoBehaviour { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public static class Application { public static string cloudProjectId = "project"; }
    public static class PlayerPrefs
    {
        static Dictionary<string, string> values = new();
        public static bool HasKey(string key) => values.ContainsKey(key);
        public static string GetString(string key) => values[key];
        public static void SetString(string key, string value) => values[key] = value;
        public static void DeleteKey(string key) => values.Remove(key);
        public static void Save() { }
    }
    public static class JsonUtility
    {
        static System.Text.Json.JsonSerializerOptions options = new() { IncludeFields = true };
        public static string ToJson<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value, options);
        public static T FromJson<T>(string value) => System.Text.Json.JsonSerializer.Deserialize<T>(value, options);
    }
}
namespace SimpleMarket
{
    internal static class ReownQrLayout { public static Action Enlarge() => () => { }; }
    public class WebGlWallet
    {
        public class Request { public string action, storageKey = "", txHash, from, data, to, value; }
        public class Result { public string txHash, status, address, chainId, balanceEth, signature; }
    }
}
namespace Reown.Core.Common.Model.Errors { public class ReownNetworkException : Exception { } }
namespace Reown.AppKit.Unity.Model { }
namespace Reown.AppKit.Unity
{
    public class Account { public string Address, ChainId = "eip155:11155111"; public string AccountId => ChainId + ":" + Address; }
    public class Session
    {
        public string Topic, Address;
        public long? Expiry = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
        public Account CurrentAccount(string chain) => new() { Address = Address, ChainId = chain };
    }
    public class SessionStore
    {
        public Dictionary<string, Session> Values = new();
        public string[] Keys => Values.Keys.ToArray();
        public Session Get(string key) => Values[key];
    }
    public class AddressProvider
    {
        public Session DefaultSession;
        public string DefaultNamespace;
        public Task SetDefaultNamespaceAsync(string value) { DefaultNamespace = value; return Task.CompletedTask; }
        public Task SetDefaultChainIdAsync(string chain) => Task.CompletedTask;
    }
    public class SignClient
    {
        public SessionStore Session = new();
        public AddressProvider AddressProvider = new();
        public Task<R> RequestAsync<T, R>(string topic, string method, T data, string chainId, CancellationToken ct) => Task.FromResult(default(R));
    }
    public class Connector
    {
        public class AccountConnectedEventArgs : EventArgs { }
        public Task<bool> TryResumeSessionAsync()
        {
            if (AppKit.Instance.SignClient.AddressProvider.DefaultNamespace == null)
                throw new ArgumentNullException("@namespace");
            AppKit.IsAccountConnected = true;
            return Task.FromResult(true);
        }
    }
    public class Network
    {
        public Task ChangeActiveChainAsync(Chain chain)
        {
            if (AppKit.Instance.SignClient.AddressProvider.DefaultNamespace == null)
                throw new ArgumentNullException("key");
            return Task.CompletedTask;
        }
    }
    public class EvmApi { public Task<BigInteger> GetBalanceAsync(string address) => Task.FromResult(BigInteger.Zero); }
    public class AppKit
    {
        public static AppKit Instance = new();
        public SignClient SignClient = new();
        public static bool IsInitialized = true, IsAccountConnected, IsModalOpen;
        public static int Modals;
        public static string NextAddress;
        public static Action OnModal;
        public static Account Account => Instance.SignClient.AddressProvider.DefaultSession.CurrentAccount("eip155:11155111");
        public static Connector ConnectorController = new();
        public static Network NetworkController = new();
        public static EvmApi Evm = new();
        public static event EventHandler<Connector.AccountConnectedEventArgs> AccountConnected;
        public static void Approve(string address)
        {
            var session = new Session { Address = address, Topic = Guid.NewGuid().ToString() };
            Instance.SignClient.Session.Values.Add(session.Topic, session);
            Instance.SignClient.AddressProvider.DefaultSession = session;
            Instance.SignClient.AddressProvider.DefaultNamespace = "eip155";
            IsAccountConnected = true;
            AccountConnected?.Invoke(null, new());
        }
        public static void OpenModal(ViewType type) { Modals++; IsModalOpen = true; OnModal?.Invoke(); Approve(NextAddress); }
        public static void CloseModal() => IsModalOpen = false;
        public static Task InitializeAsync(AppKitConfig config) => Task.CompletedTask;
    }
    public enum ViewType { QrCode }
    public enum SocialLogin { }
    public class Chain { public Chain(params object[] args) { } }
    public class Currency { public Currency(params object[] args) { } }
    public class BlockExplorer { public BlockExplorer(params object[] args) { } }
    public class Metadata { public Metadata(params object[] args) { } }
    public class AppKitConfig
    {
        public AppKitConfig(params object[] args) { }
        public Chain[] supportedChains;
        public string[] includedWalletIds;
        public bool enableAnalytics, enableEmail, enableOnramp;
        public SocialLogin[] socials;
    }
    public static class ChainConstants
    {
        public static class Chains { public static class Ethereum { public static string ImageUrl = ""; } }
    }
}
