using System;
using System.Collections;
using UnityEngine;

#if SOLANA_SDK_INSTALLED
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;
#endif

// Wallet bridge for the Solana Seeker dApp Store build. Wraps the
// Solana.Unity-SDK's Web3 singleton so the rest of the game (GameManager,
// ShopUIBuilder) can stay agnostic of the SDK. On Seeker / Android this
// routes through Mobile Wallet Adapter via an Android intent handshake,
// which hits Seed Vault + any installed MWA-compatible wallet (Phantom,
// Solflare). The legacy "phantom://" custom URL scheme this class used to
// use does not work on Seeker and has been removed.
public class SolanaManager : MonoBehaviour
{
    public static SolanaManager Instance { get; private set; }

    [Header("Payment Configuration")]
    [SerializeField] private string _merchantWalletAddress = "DfMxre4cKmvogbLrPigxmibVTTQDuzjdXojWzjCXXhzj";

    [Header("Editor Testing")]
    [Tooltip("In the Unity Editor, skip the real wallet flow and pretend a wallet is connected so shop UI can be exercised.")]
    [SerializeField] private bool _simulateInEditor = true;

    private bool _isWalletConnected;
    private string _walletAddress = "";
    private float _walletBalance;

    public event Action<string> OnWalletConnected;
    public event Action OnWalletDisconnected;
    public event Action<string> OnTransactionSuccess;
    public event Action<string> OnTransactionFailed;
    public event Action<string> OnError;
    public event Action<float> OnBalanceUpdated;

    public bool IsWalletConnected => _isWalletConnected;
    public string WalletAddress => _walletAddress;
    public float WalletBalance => _walletBalance;
    public string MerchantWallet => _merchantWalletAddress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if SOLANA_SDK_INSTALLED
        EnsureWeb3Controller();
        Web3.OnLogin += HandleSdkLogin;
        Web3.OnLogout += HandleSdkLogout;
        Web3.OnBalanceChange += HandleSdkBalanceChange;
#endif
    }

    private void OnDestroy()
    {
#if SOLANA_SDK_INSTALLED
        Web3.OnLogin -= HandleSdkLogin;
        Web3.OnLogout -= HandleSdkLogout;
        Web3.OnBalanceChange -= HandleSdkBalanceChange;
#endif
    }

#if SOLANA_SDK_INSTALLED
    private static void EnsureWeb3Controller()
    {
        if (Web3.Instance != null) return;
        var prefab = Resources.Load<GameObject>("SolanaUnitySDK/[WalletController]");
        if (prefab == null)
        {
            Debug.LogError("[SolanaManager] Missing Resources/SolanaUnitySDK/[WalletController] prefab. Import it from the Solana.Unity-SDK sample.");
            return;
        }
        Instantiate(prefab).name = "[WalletController]";
    }

    private void HandleSdkLogin(Account account)
    {
        string publicKey = account.PublicKey.Key;
        _isWalletConnected = true;
        _walletAddress = publicKey;
        Debug.Log("[SolanaManager] Wallet connected via MWA: " + publicKey);
        OnWalletConnected?.Invoke(publicKey);
    }

    private void HandleSdkLogout()
    {
        bool wasConnected = _isWalletConnected;
        _isWalletConnected = false;
        _walletAddress = "";
        _walletBalance = 0f;
        Debug.Log("[SolanaManager] Wallet disconnected");
        if (wasConnected) OnWalletDisconnected?.Invoke();
        OnBalanceUpdated?.Invoke(0f);
    }

    private void HandleSdkBalanceChange(double balance)
    {
        _walletBalance = (float)balance;
        OnBalanceUpdated?.Invoke(_walletBalance);
        if (GameManager.Instance != null) GameManager.Instance.UpdateSolBalanceDisplay(_walletBalance);
    }
#endif

    public void ConnectWallet()
    {
        Debug.Log("[SolanaManager] ConnectWallet");

#if UNITY_EDITOR
        if (_simulateInEditor)
        {
            SimulateConnect();
            return;
        }
#endif

#if SOLANA_SDK_INSTALLED
        EnsureWeb3Controller();
        if (Web3.Instance == null)
        {
            OnError?.Invoke("Solana SDK controller missing from scene");
            return;
        }
        Web3.Instance.LoginWithWalletAdapter();
#else
        OnError?.Invoke("Solana SDK not installed (define SOLANA_SDK_INSTALLED)");
#endif
    }

    public void DisconnectWallet()
    {
#if SOLANA_SDK_INSTALLED
        if (Web3.Instance != null && Web3.Instance.WalletBase != null)
        {
            Web3.Instance.Logout();
            return;
        }
#endif
        HandleLocalDisconnect();
    }

    private void HandleLocalDisconnect()
    {
        bool wasConnected = _isWalletConnected;
        _isWalletConnected = false;
        _walletAddress = "";
        _walletBalance = 0f;
        if (wasConnected) OnWalletDisconnected?.Invoke();
        OnBalanceUpdated?.Invoke(0f);
    }

    public void RefreshBalance()
    {
#if SOLANA_SDK_INSTALLED
        if (Web3.Instance != null && Web3.Instance.WalletBase != null)
        {
            _ = Web3.UpdateBalance();
        }
#endif
    }

    public bool HasSufficientBalance(float amount) => _isWalletConnected && _walletBalance >= amount;

    public void SendPayment(float solAmount, string itemName, Action<bool, string> callback)
    {
        if (!_isWalletConnected)
        {
            OnError?.Invoke("Please connect your wallet first");
            callback?.Invoke(false, "Wallet not connected");
            return;
        }
        if (string.IsNullOrEmpty(_merchantWalletAddress))
        {
            Debug.LogError("[SolanaManager] Merchant wallet address not set");
            callback?.Invoke(false, "Payment configuration error");
            return;
        }
        if (_walletBalance < solAmount)
        {
            OnError?.Invoke($"Insufficient balance. Need {solAmount} SOL, have {_walletBalance} SOL");
            callback?.Invoke(false, "Insufficient balance");
            return;
        }

#if UNITY_EDITOR
        if (_simulateInEditor)
        {
            StartCoroutine(SimulatePayment(solAmount, itemName, callback));
            return;
        }
#endif

#if SOLANA_SDK_INSTALLED
        StartCoroutine(SendPaymentCoroutine(solAmount, itemName, callback));
#else
        callback?.Invoke(false, "Solana SDK not installed");
#endif
    }

#if SOLANA_SDK_INSTALLED
    private IEnumerator SendPaymentCoroutine(float solAmount, string itemName, Action<bool, string> callback)
    {
        if (Web3.Instance == null || Web3.Instance.WalletBase == null)
        {
            callback?.Invoke(false, "Wallet not active");
            yield break;
        }

        // SOL is 9 decimals; convert to lamports for SystemProgram.Transfer.
        ulong lamports = (ulong)Mathf.RoundToInt(solAmount * 1_000_000_000f);
        PublicKey destination;
        try { destination = new PublicKey(_merchantWalletAddress); }
        catch (Exception e)
        {
            Debug.LogError($"[SolanaManager] Bad merchant pubkey: {e.Message}");
            callback?.Invoke(false, "Bad merchant address");
            yield break;
        }

        Debug.Log($"[SolanaManager] Signing {solAmount} SOL ({lamports} lamports) transfer for {itemName}");
        var task = Web3.Instance.WalletBase.Transfer(destination, lamports, Commitment.Confirmed);
        while (!task.IsCompleted) yield return null;

        if (task.IsFaulted || task.Result == null)
        {
            string err = task.Exception?.InnerException?.Message ?? "Unknown wallet error";
            Debug.LogError($"[SolanaManager] Transfer faulted: {err}");
            OnTransactionFailed?.Invoke(err);
            callback?.Invoke(false, err);
            yield break;
        }

        var result = task.Result;
        if (!result.WasSuccessful || string.IsNullOrEmpty(result.Result))
        {
            string err = result.Reason ?? result.ErrorData?.ToString() ?? "Transfer rejected";
            Debug.LogError($"[SolanaManager] Transfer rejected: {err}");
            OnTransactionFailed?.Invoke(err);
            callback?.Invoke(false, err);
            yield break;
        }

        Debug.Log($"[SolanaManager] Transfer success. Signature: {result.Result}");
        OnTransactionSuccess?.Invoke(result.Result);
        callback?.Invoke(true, result.Result);

        _ = Web3.UpdateBalance();
    }
#endif

    // --- Editor-only simulation ---

    private void SimulateConnect()
    {
        const string testAddress = "So11111111111111111111111111111111111111112";
        _isWalletConnected = true;
        _walletAddress = testAddress;
        _walletBalance = 1.5f;
        OnWalletConnected?.Invoke(testAddress);
        OnBalanceUpdated?.Invoke(_walletBalance);
        if (GameManager.Instance != null) GameManager.Instance.UpdateSolBalanceDisplay(_walletBalance);
    }

    private IEnumerator SimulatePayment(float solAmount, string itemName, Action<bool, string> callback)
    {
        yield return new WaitForSeconds(1f);
        _walletBalance -= solAmount;
        OnBalanceUpdated?.Invoke(_walletBalance);
        if (GameManager.Instance != null) GameManager.Instance.UpdateSolBalanceDisplay(_walletBalance);
        string fakeSig = "SimTx_" + UnityEngine.Random.Range(100000, 999999);
        Debug.Log($"[SolanaManager] (sim) Paid {solAmount} SOL for {itemName}. Sig: {fakeSig}");
        OnTransactionSuccess?.Invoke(fakeSig);
        callback?.Invoke(true, fakeSig);
    }
}
