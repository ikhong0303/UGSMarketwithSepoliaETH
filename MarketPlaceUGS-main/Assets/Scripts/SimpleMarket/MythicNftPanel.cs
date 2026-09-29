using System;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nethereum.Util;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using System.Collections.Generic;

namespace SimpleMarket
{
    // Issuance display is local; inventory changes are verified by Cloud Code.
    public sealed class MythicNftPanel : MonoBehaviour
    {
        public ReownWalletBridge wallet;
        public TMP_InputField couponInput;
        public TMP_Text statusText;
        public Button connectButton, redeemButton, checkButton;
        public string rpcUrl = "https://ethereum-sepolia-rpc.publicnode.com";
        private bool busy;
        private WebGlWallet browser;
        private Task<WebGlWallet.Result> Wallet(WebGlWallet.Request request)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!browser) browser = GetComponent<WebGlWallet>() ?? gameObject.AddComponent<WebGlWallet>();
            return browser.Call(request);
#else
            return wallet.Call(request);
#endif
        }
        private Task syncTask;
        [Serializable] public class SyncReply { public string status, wallet, message; public int count, added, removed; }
        private static string Player() => AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "";
        public Task SyncInventoryAsync() => syncTask != null && !syncTask.IsCompleted ? syncTask : (syncTask = SyncInventoryCore());
        private async Task SyncInventoryCore()
        {
            string player = Player();
            if (string.IsNullOrEmpty(player)) return;
            try
            {
                var reply = await CloudCodeService.Instance.CallEndpointAsync<SyncReply>("Nft_SyncInventory", new Dictionary<string, object>());
                if (!this || Player() != player) return;
                Message(reply.status == "NOT_LINKED" ? "NFT지갑연결을 눌러 계정과 지갑을 서명으로 연결하세요." :
                    reply.status == "PARTIAL" ? "일부 동기화 완료. 잠시 후 NFT발행확인을 다시 누르세요." :
                    $"신화검NFT 인벤토리 동기화 완료: {reply.count}개\n추가 {reply.added}개 / 제거 {reply.removed}개");
            }
            catch (Exception e)
            {
                if (this && Player() == player) Message("NFT 동기화 실패. 잠시 후 다시 확인하세요.\n" + e.Message);
                Debug.LogWarning("[MythicNFT Sync] " + e.Message);
            }
        }
        private async Task SyncAndRefresh()
        {
            string player = Player();
            await SyncInventoryAsync();
            if (!this || Player() != player) return;
            var market = FindFirstObjectByType<PortfolioMarketDemo>();
            if (market) await market.RefreshInventoryAsync();
        }
        private void Awake()
        {
            if (connectButton) connectButton.onClick.AddListener(Connect);
            if (redeemButton) redeemButton.onClick.AddListener(Redeem);
            if (checkButton) checkButton.onClick.AddListener(Check);
        }
        private void Message(string text) { if (statusText) statusText.text = text; }
        private async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            try
            {
                if (!wallet) throw new Exception("Reown Wallet Bridge를 연결하세요.");
                await action();
            }
            catch (Exception e) { Message(e.Message); Debug.LogWarning("[MythicNFT] " + e.Message); }
            finally { busy = false; }
        }
        public void Connect() => _ = Run(async () =>
        {
            var r = await Wallet(new() { action = "connect", storageKey = "mythic-connect" });
            string player = Player();
            if (string.IsNullOrEmpty(player)) throw new Exception("게임 계정에 먼저 로그인하세요.");
            var challenge = await CloudCodeService.Instance.CallEndpointAsync<SyncReply>("Nft_GetChallenge", new Dictionary<string, object> { { "wallet_address", r.address } });
            if (challenge.status == "SIGN_REQUIRED")
            {
                Message("MetaMask에서 계정 연결 메시지에 서명하세요. 결제나 가스비는 없습니다.");
                var signed = await Wallet(new() { action = "nftSign", storageKey = "mythic-link", from = r.address, data = challenge.message });
                if (Player() != player) throw new Exception("게임 계정이 바뀌었습니다. 다시 연결하세요.");
                await CloudCodeService.Instance.CallEndpointAsync<SyncReply>("Nft_BindWallet", new Dictionary<string, object> { { "signature", signed.signature } });
            }
            if (Player() != player) return;
            await SyncAndRefresh();
        });
        private string[] Coupon()
        {
            var p = (couponInput ? couponInput.text.Trim() : "").Split('|');
            if (p.Length != 5 || p[0] != "MSW1" || p[1] != "11155111" ||
                !Regex.IsMatch(p[2], "^0x[0-9a-fA-F]{40}$") || !Regex.IsMatch(p[3], "^0x[0-9a-fA-F]{40}$") ||
                !Regex.IsMatch(p[4], "^0x[0-9a-fA-F]{64}$") ||
                !string.Equals(p[2], wallet.mythicNftContract, StringComparison.OrdinalIgnoreCase))
                throw new Exception("쿠폰 전체와 Inspector의 Mythic Nft Contract 주소를 확인하세요.");
            return p;
        }
        private async Task<string> Rpc(string method, params object[] args)
        {
            if (!rpcUrl.StartsWith("https://", StringComparison.Ordinal)) throw new Exception("HTTPS RPC URL이 필요합니다.");
            var body = new JObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = JArray.FromObject(args) };
            using var req = new UnityWebRequest(rpcUrl, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json"); req.timeout = 15;
            var operation = req.SendWebRequest();
            while (!operation.isDone) { if (!this) throw new OperationCanceledException(); await Task.Yield(); }
            if (req.result != UnityWebRequest.Result.Success) throw new Exception("NFT RPC 조회 실패. 재송금하지 말고 상태 확인을 다시 누르세요.");
            var json = JObject.Parse(req.downloadHandler.text);
            if (json["error"] != null || json["result"] == null) throw new Exception("NFT 조회 응답 오류. 계약 주소와 RPC를 확인하세요.");
            return json["result"].ToString();
        }
        private static BigInteger Number(string hex) => BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
        private async Task<(string recipient, BigInteger deadline, BigInteger token, bool cancelled)> State(string[] p)
        {
            if (await Rpc("eth_chainId") != "0xaa36a7") throw new Exception("RPC가 Sepolia가 아닙니다.");
            byte[] secret = new byte[32];
            for (int i = 0; i < 32; i++) secret[i] = Convert.ToByte(p[4].Substring(2 + i * 2, 2), 16);
            string hash = BitConverter.ToString(Sha3Keccack.Current.CalculateHash(secret)).Replace("-", "").ToLowerInvariant();
            string data = await Rpc("eth_call", new { to = p[2], data = "0x742b20cd" + hash }, "latest");
            if (!Regex.IsMatch(data, "^0x[0-9a-fA-F]{256}$")) throw new Exception("쿠폰 조회 형식이 다릅니다. 신화검 계약 주소를 확인하세요.");
            return ("0x" + data.Substring(26, 40), Number(data.Substring(66, 64)), Number(data.Substring(130, 64)), Number(data.Substring(194, 64)) != 0);
        }
        private async Task<bool> Show(string[] p)
        {
            var s = await State(p);
            if (s.token > 0)
            {
                var owner = await Rpc("eth_call", new { to = p[2], data = "0x6352211e" + s.token.ToString("x").PadLeft(64, '0') }, "latest");
                if (!Regex.IsMatch(owner, "^0x[0-9a-fA-F]{64}$")) throw new Exception("소유자 조회 실패");
                Message("발행 완료! 신화검NFT tokenId: " + s.token + "\n현재 소유자: 0x" + owner.Substring(26));
                return true;
            }
            ValidateCoupon(p, s);
            Message("등록된 쿠폰입니다. 아직 발행되지 않았습니다.\n수령 지갑: " + s.recipient + "\n승인 대기 거래가 있으면 기다린 뒤 다시 확인하세요.");
            return false;
        }
        private static void ValidateCoupon(string[] p, (string recipient, BigInteger deadline, BigInteger token, bool cancelled) s)
        {
            if (string.Equals(s.recipient, "0x0000000000000000000000000000000000000000", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Sepolia 계약에 등록되지 않은 쿠폰입니다. 관리자 페이지에서 등록 거래의 성공 여부를 확인하세요. 쿠폰 초안만 생성한 상태에서는 수령할 수 없습니다.\n계약: " + p[2]);
            if (s.cancelled) throw new Exception("관리자가 취소한 쿠폰입니다. 새 쿠폰을 요청하세요.");
            if (!string.Equals(p[3], s.recipient, StringComparison.OrdinalIgnoreCase))
                throw new Exception("쿠폰에 적힌 수령 지갑과 계약에 등록된 수령 지갑이 다릅니다. 관리자에게 원본 쿠폰을 확인하세요.\n쿠폰: " + p[3] + "\n계약 등록: " + s.recipient);
            if (s.deadline < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                throw new Exception("만료된 쿠폰입니다. 관리자에게 새 쿠폰을 요청하세요.");
        }
        public void Check() => _ = Run(async () =>
        {
            await SyncAndRefresh();
            // Keep the coupon diagnosis visible after inventory synchronization.
            if (couponInput && !string.IsNullOrWhiteSpace(couponInput.text)) await Show(Coupon());
        });
        public void Redeem() => _ = Run(async () =>
        {
            var p = Coupon();
            if (await Show(p)) { await SyncAndRefresh(); return; }
            var s = await State(p);
            ValidateCoupon(p, s);
            var account = await Wallet(new() { action = "connect", storageKey = "mythic-connect" });
            if (!string.Equals(account.address, p[3], StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(account.address, s.recipient, StringComparison.OrdinalIgnoreCase))
                throw new Exception("쿠폰 수령 지갑과 현재 게임에 연결된 지갑이 다릅니다. MetaMask에서 수령 지갑으로 전환하세요. 기존 연결 주소가 유지되면 게임의 WalletConnect 연결을 해제한 뒤 다시 연결하세요.\n수령 지갑: " + s.recipient + "\n현재 연결: " + account.address);
            Message("MetaMask에서 NFT 발행 가스비를 승인하세요. 상품 가격은 0 ETH입니다.");
            string couponKey = "mythic-coupon:" + Sha3Keccack.Current.CalculateHash(string.Join("|", p));
            var sent = await Wallet(new() { action = "nftRedeem", storageKey = couponKey, from = account.address, to = p[2], value = "0x0", data = "0xeda1122c" + p[4].Substring(2) });
            Message("전송 완료. NFT 발행 확인을 누르세요.\n거래 해시: " + sent.txHash);
            Debug.Log("[MythicNFT] tx=" + sent.txHash);
        });
        private void OnDestroy()
        {
            if (connectButton) connectButton.onClick.RemoveListener(Connect);
            if (redeemButton) redeemButton.onClick.RemoveListener(Redeem);
            if (checkButton) checkButton.onClick.RemoveListener(Check);
        }
    }
}
