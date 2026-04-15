using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

#if SOLANA_SDK_INSTALLED
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Programs;
#endif

/// <summary>
/// SolanaManager handles wallet connectivity for Solana Seeker dApp Store.
/// Integrates with Solana.Unity-SDK for real blockchain transactions.
/// </summary>
public class SolanaManager : MonoBehaviour
{
    public static SolanaManager Instance { get; private set; }

    [Header("Wallet Configuration")]
    [SerializeField] private string _rpcUrl = "https://api.mainnet-beta.solana.com";
    [SerializeField] private bool _useDevnet = true; // Use devnet for testing

    [Header("Payment Configuration")]
    [SerializeField] private string _merchantWalletAddress = "DfMxre4cKmvogbLrPigxmibVTTQDuzjdXojWzjCXXhzj"; // Devnet test wallet

    [Header("Deep Link Configuration")]
    [SerializeField] private string _appScheme = "planes-solana";

    // Wallet state
    private bool _isWalletConnected = false;
    private string _walletAddress = "";
    private float _walletBalance = 0f;

    // Events
    public event Action<string> OnWalletConnected;
    public event Action OnWalletDisconnected;
    public event Action<string> OnTransactionSuccess;
    public event Action<string> OnTransactionFailed;
    public event Action<string> OnError;
    public event Action<float> OnBalanceUpdated;

    // Properties
    public bool IsWalletConnected => _isWalletConnected;
    public string WalletAddress => _walletAddress;
    public float WalletBalance => _walletBalance;
    public string RpcUrl => _useDevnet ? "https://api.devnet.solana.com" : _rpcUrl;
    public string MerchantWallet => _merchantWalletAddress;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Debug.Log("[SolanaManager] Initialized");
        Debug.Log("[SolanaManager] RPC: " + RpcUrl);
        Debug.Log("[SolanaManager] Network: " + (_useDevnet ? "Devnet (Testing)" : "Mainnet"));

        // Check for deep link on start (wallet callback)
        CheckDeepLink();
    }

    private void CheckDeepLink()
    {
        // Handle deep link callback from wallet app
        string deepLink = Application.absoluteURL;
        if (!string.IsNullOrEmpty(deepLink) && deepLink.Contains(_appScheme))
        {
            Debug.Log("[SolanaManager] Deep link received: " + deepLink);
            HandleWalletCallback(deepLink);
        }
    }

    private void HandleWalletCallback(string deepLink)
    {
        // Parse wallet response from deep link
        // Format: planes-solana://callback?address=WALLET_ADDRESS&signature=TX_SIG
        try
        {
            Uri uri = new Uri(deepLink);
            string query = uri.Query;

            if (query.Contains("address="))
            {
                string address = GetQueryParam(query, "address");
                if (!string.IsNullOrEmpty(address))
                {
                    OnWalletConnectedInternal(address);
                }
            }

            if (query.Contains("signature="))
            {
                string signature = GetQueryParam(query, "signature");
                if (!string.IsNullOrEmpty(signature))
                {
                    OnTransactionSuccess?.Invoke(signature);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[SolanaManager] Failed to parse deep link: " + e.Message);
        }
    }

    private string GetQueryParam(string query, string param)
    {
        string[] pairs = query.TrimStart('?').Split('&');
        foreach (string pair in pairs)
        {
            string[] kv = pair.Split('=');
            if (kv.Length == 2 && kv[0] == param)
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }
        return null;
    }

    /// <summary>
    /// Connect to a Solana wallet using Mobile Wallet Adapter or Phantom deep link
    /// </summary>
    public void ConnectWallet()
    {
        Debug.Log("[SolanaManager] Attempting to connect wallet...");

#if UNITY_ANDROID && !UNITY_EDITOR
        // On Android (Solana Seeker), use Phantom/Solflare deep link
        ConnectViaDeepLink();
#elif UNITY_EDITOR
        // In editor, simulate connection for testing
        SimulateWalletConnection();
#else
        OnError?.Invoke("Wallet connection not supported on this platform");
#endif
    }

    private void ConnectViaDeepLink()
    {
        // Phantom deep link format for connect
        string callbackUrl = Uri.EscapeDataString($"{_appScheme}://callback");
        string cluster = _useDevnet ? "devnet" : "mainnet-beta";

        // Try Phantom first
        string phantomUrl = $"phantom://v1/connect?app_url={callbackUrl}&cluster={cluster}&redirect_link={callbackUrl}";

        Debug.Log("[SolanaManager] Opening wallet: " + phantomUrl);
        Application.OpenURL(phantomUrl);
    }

    private void SimulateWalletConnection()
    {
        // For editor testing - simulate a connected wallet
        Debug.Log("[SolanaManager] Simulating wallet connection for editor testing...");

        // Use a test wallet address (this is just for UI testing in editor)
        string testAddress = "So11111111111111111111111111111111111111112";
        OnWalletConnectedInternal(testAddress);

        // Simulate some balance
        _walletBalance = 1.5f;
        OnBalanceUpdated?.Invoke(_walletBalance);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateSolBalanceDisplay(_walletBalance);
        }
    }

    /// <summary>
    /// Disconnect the wallet
    /// </summary>
    public void DisconnectWallet()
    {
        Debug.Log("[SolanaManager] Disconnecting wallet...");
        _isWalletConnected = false;
        _walletAddress = "";
        _walletBalance = 0f;
        OnWalletDisconnected?.Invoke();
    }

    /// <summary>
    /// Get SOL balance for connected wallet
    /// </summary>
    public void GetBalance(Action<float> callback)
    {
        if (!_isWalletConnected)
        {
            callback?.Invoke(0f);
            return;
        }

        StartCoroutine(FetchBalanceCoroutine(callback));
    }

    private IEnumerator FetchBalanceCoroutine(Action<float> callback)
    {
        string url = RpcUrl;
        string jsonRequest = $"{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"getBalance\",\"params\":[\"{_walletAddress}\"]}}";

        using (var request = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequest);
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    string response = request.downloadHandler.text;
                    // Parse JSON response to get lamports
                    // Response format: {"jsonrpc":"2.0","result":{"context":{"slot":123},"value":1000000000},"id":1}

                    int valueIndex = response.IndexOf("\"value\":");
                    if (valueIndex != -1)
                    {
                        int start = valueIndex + 8;
                        int end = response.IndexOf("}", start);
                        string valueStr = response.Substring(start, end - start).Trim();

                        if (long.TryParse(valueStr, out long lamports))
                        {
                            _walletBalance = lamports / 1000000000f; // Convert lamports to SOL
                            Debug.Log($"[SolanaManager] Balance: {_walletBalance} SOL");
                            callback?.Invoke(_walletBalance);
                            OnBalanceUpdated?.Invoke(_walletBalance);

                            if (GameManager.Instance != null)
                            {
                                GameManager.Instance.UpdateSolBalanceDisplay(_walletBalance);
                            }
                            yield break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[SolanaManager] Failed to parse balance: " + e.Message);
                }
            }
            else
            {
                Debug.LogError("[SolanaManager] Failed to fetch balance: " + request.error);
            }

            callback?.Invoke(0f);
        }
    }

    /// <summary>
    /// Send SOL payment for purchase
    /// </summary>
    public void SendPayment(float solAmount, string itemName, Action<bool, string> callback)
    {
        if (!_isWalletConnected)
        {
            callback?.Invoke(false, "Wallet not connected");
            OnError?.Invoke("Please connect your wallet first");
            return;
        }

        if (string.IsNullOrEmpty(_merchantWalletAddress))
        {
            Debug.LogError("[SolanaManager] Merchant wallet address not set!");
            callback?.Invoke(false, "Payment configuration error");
            return;
        }

        if (_walletBalance < solAmount)
        {
            callback?.Invoke(false, "Insufficient balance");
            OnError?.Invoke($"Insufficient balance. Need {solAmount} SOL, have {_walletBalance} SOL");
            return;
        }

        Debug.Log($"[SolanaManager] Initiating payment of {solAmount} SOL for {itemName}");

#if UNITY_ANDROID && !UNITY_EDITOR
        // On Android, open wallet app for signing
        SendPaymentViaDeepLink(solAmount, itemName, callback);
#elif UNITY_EDITOR
        // In editor, simulate successful payment
        SimulatePayment(solAmount, itemName, callback);
#else
        callback?.Invoke(false, "Payments not supported on this platform");
#endif
    }

    private void SendPaymentViaDeepLink(float solAmount, string itemName, Action<bool, string> callback)
    {
        // Convert SOL to lamports
        long lamports = (long)(solAmount * 1000000000);

        string callbackUrl = Uri.EscapeDataString($"{_appScheme}://callback");
        string cluster = _useDevnet ? "devnet" : "mainnet-beta";

        // Phantom deep link for transaction
        // Note: This is a simplified approach. For production, you'd want to build a proper transaction
        string phantomUrl = $"phantom://v1/signAndSendTransaction?" +
            $"app_url={callbackUrl}" +
            $"&cluster={cluster}" +
            $"&redirect_link={callbackUrl}" +
            $"&transaction=transfer" +
            $"&recipient={_merchantWalletAddress}" +
            $"&amount={lamports}";

        Debug.Log("[SolanaManager] Opening wallet for payment...");
        Application.OpenURL(phantomUrl);

        // Store callback for when we return from wallet
        StartCoroutine(WaitForTransactionCallback(callback));
    }

    private IEnumerator WaitForTransactionCallback(Action<bool, string> callback)
    {
        // Wait for deep link callback (timeout after 60 seconds)
        float timeout = 60f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            // Check if we received a transaction callback
            string deepLink = Application.absoluteURL;
            if (!string.IsNullOrEmpty(deepLink) && deepLink.Contains("signature="))
            {
                string signature = GetQueryParam(new Uri(deepLink).Query, "signature");
                callback?.Invoke(true, signature);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        callback?.Invoke(false, "Transaction timeout");
    }

    private void SimulatePayment(float solAmount, string itemName, Action<bool, string> callback)
    {
        Debug.Log($"[SolanaManager] Simulating payment of {solAmount} SOL for {itemName}");

        // Simulate transaction delay
        StartCoroutine(SimulatePaymentCoroutine(solAmount, callback));
    }

    private IEnumerator SimulatePaymentCoroutine(float solAmount, Action<bool, string> callback)
    {
        yield return new WaitForSeconds(1f);

        // Deduct from simulated balance
        _walletBalance -= solAmount;
        OnBalanceUpdated?.Invoke(_walletBalance);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateSolBalanceDisplay(_walletBalance);
        }

        // Generate fake transaction signature for testing
        string fakeSig = "SimTx_" + UnityEngine.Random.Range(100000, 999999);

        Debug.Log($"[SolanaManager] Payment successful! Signature: {fakeSig}");
        callback?.Invoke(true, fakeSig);
        OnTransactionSuccess?.Invoke(fakeSig);
    }

    /// <summary>
    /// Called when wallet is successfully connected
    /// </summary>
    private void OnWalletConnectedInternal(string publicKey)
    {
        _isWalletConnected = true;
        _walletAddress = publicKey;
        Debug.Log("[SolanaManager] Wallet connected: " + publicKey);
        OnWalletConnected?.Invoke(publicKey);

        // Fetch balance after connection
        GetBalance((balance) => {
            Debug.Log($"[SolanaManager] Wallet balance: {balance} SOL");
        });
    }

    /// <summary>
    /// Refresh the wallet balance
    /// </summary>
    public void RefreshBalance()
    {
        if (_isWalletConnected)
        {
            GetBalance((balance) => {
                Debug.Log($"[SolanaManager] Balance refreshed: {balance} SOL");
            });
        }
    }

    /// <summary>
    /// Check if we have sufficient balance for a purchase
    /// </summary>
    public bool HasSufficientBalance(float amount)
    {
        return _isWalletConnected && _walletBalance >= amount;
    }
}
