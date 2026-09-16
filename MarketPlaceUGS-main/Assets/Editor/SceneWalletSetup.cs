using System;
using System.Linq;
using SimpleMarket;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SceneWalletSetup
{
    [MenuItem("Simple Market/Scene 1 - Connect Selected WalletPanel")]
    public static void ConnectSelectedPanel()
    {
        var panel = Selection.activeGameObject;
        if (panel == null || panel.name != "WalletPanel" || !panel.scene.IsValid())
        {
            Debug.LogError("Hierarchy에서 Scene 1의 WalletPanel을 선택하세요."); return;
        }
        if (EditorApplication.isPlaying) { Debug.LogError("Play를 종료한 뒤 연결하세요."); return; }
        var wallet = panel.GetComponent<SceneWalletPanel>() ?? Undo.AddComponent<SceneWalletPanel>(panel);
        Undo.RecordObject(wallet, "Wire Scene 1 WalletPanel");
        wallet.walletButton = Find<Button>(panel, "WalletButton");
        wallet.paymentCheckButton = Find<Button>(panel, "PaymentCheckButton");
        wallet.buy10000GoldButton = Find<Button>(panel, "Buy10000GoldButton");
        wallet.buyLegendarySwordButton = Find<Button>(panel, "BuyLegendarySwordButton");
        wallet.swordPaymentCheckButton = Find<Button>(panel, "SwordPaymentCheckButton");
        wallet.walletAddressText = Find<TMP_Text>(panel, "WalletAddressText");
        wallet.sepoliaEthText = Find<TMP_Text>(panel, "Sepolia ETH Text", "SepoliaETHText", "SepoliaEthText");
        wallet.paymentStatusText = Find<TMP_Text>(panel, "PaymentStatusText");
        wallet.transactionHashInput = Find<TMP_InputField>(panel, "TransactionHashInput");
        wallet.forgetPaymentButton = Find<Button>(panel, "ForgetPaymentButton");
        var markets = panel.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PortfolioMarketDemo>(true)).ToArray();
        if (markets.Length == 1) wallet.marketDemo = markets[0];
        EditorUtility.SetDirty(wallet); EditorSceneManager.MarkSceneDirty(panel.scene);
        if (!wallet.walletButton || !wallet.paymentCheckButton || !wallet.buy10000GoldButton || !wallet.walletAddressText || !wallet.sepoliaEthText || !wallet.paymentStatusText || !wallet.marketDemo)
            Debug.LogWarning("일부 필드가 비어 있습니다. Inspector에서 버튼·TMP 텍스트·Market Demo를 확인하세요.", wallet);
        else Debug.Log("기존 WalletPanel 연결 완료. Scene을 Ctrl+S로 저장하세요. On Click은 코드가 연결하므로 비워 둡니다.", wallet);
        Selection.activeGameObject = panel;
    }
    private static T Find<T>(GameObject root, params string[] names) where T : Component =>
        root.GetComponentsInChildren<T>(true).FirstOrDefault(component => names.Any(name =>
            string.Equals(component.name.Replace(" ", ""), name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)));
}
