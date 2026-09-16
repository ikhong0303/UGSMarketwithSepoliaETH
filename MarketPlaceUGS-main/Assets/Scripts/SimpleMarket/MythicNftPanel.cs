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

namespace SimpleMarket
{
    // Issuance only. This is not server-authoritative UGS inventory synchronization.
    public sealed class MythicNftPanel : MonoBehaviour
    {
        public ReownWalletBridge wallet;
        public TMP_InputField couponInput;
        public TMP_Text statusText;
        public Button connectButton, redeemButton, checkButton;
        public string rpcUrl = "https://ethereum-sepolia-rpc.publicnode.com";
        private bool busy;
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
            var r = await wallet.Call(new() { action = "connect", storageKey = "mythic-connect" });
            Message("Sepolia 지갑 연결: " + r.address);
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
                Message("발행 완료! 신화검NFT tokenId: " + s.token + "\n현재 소유자: 0x" + owner.Substring(26) + "\nUGS 인벤토리 동기화는 다음 단계입니다.");
                return true;
            }
            Message(s.cancelled ? "취소된 쿠폰입니다." : "아직 발행되지 않았습니다. 승인 대기 거래가 있으면 기다린 뒤 다시 확인하세요.");
            return false;
        }
        public void Check() => _ = Run(async () => { await Show(Coupon()); });
        public void Redeem() => _ = Run(async () =>
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new Exception("이번 NFT 쿠폰 UI는 Editor QR용입니다. WebGL에서는 NFT 실습 웹 화면을 사용하세요.");
#else
            var p = Coupon();
            if (await Show(p)) return;
            var s = await State(p);
            var account = await wallet.Call(new() { action = "status", storageKey = "mythic-connect" });
            if (!string.Equals(account.address, p[3], StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(account.address, s.recipient, StringComparison.OrdinalIgnoreCase))
                throw new Exception("쿠폰 수령 지갑과 연결한 지갑이 다릅니다. 관리자 등록 여부도 확인하세요.");
            if (s.cancelled || s.deadline < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) throw new Exception("취소되었거나 만료된 쿠폰입니다.");
            Message("MetaMask에서 NFT 발행 가스비를 승인하세요. 상품 가격은 0 ETH입니다.");
            var sent = await wallet.Call(new() { action = "nftRedeem", storageKey = "mythic-coupon", from = account.address, to = p[2], value = "0x0", data = "0xeda1122c" + p[4].Substring(2) });
            Message("전송 완료. NFT 발행 확인을 누르세요.\n거래 해시: " + sent.txHash);
            Debug.Log("[MythicNFT] tx=" + sent.txHash);
#endif
        });
        private void OnDestroy()
        {
            if (connectButton) connectButton.onClick.RemoveListener(Connect);
            if (redeemButton) redeemButton.onClick.RemoveListener(Redeem);
            if (checkButton) checkButton.onClick.RemoveListener(Check);
        }
    }
}
