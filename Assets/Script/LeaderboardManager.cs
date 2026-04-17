using System;
using System.Collections;
using UnityEngine;

// Front-end brain for the leaderboard. GameManager auto-spawns this alongside
// SolanaManager; it orchestrates "user clicks Submit" -> pay SOL -> write to
// Supabase, and "user opens leaderboard panel" -> fetch a page.
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    [Header("Submission Pricing")]
    [Tooltip("SOL fee charged when a player submits a run to the leaderboard.")]
    [SerializeField] private float _submissionPriceSOL = 0.01f;

    [Header("Page Size")]
    [SerializeField] private int _pageSize = 25;

    private SupabaseLeaderboardClient _client;

    public float SubmissionPriceSOL => _submissionPriceSOL;
    public int PageSize => _pageSize;

    public event Action<bool, string> OnSubmitFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _client = gameObject.AddComponent<SupabaseLeaderboardClient>();
    }

    public void FetchPage(int pageIndex, Action<SupabaseLeaderboardClient.LeaderboardEntry[], string> callback)
    {
        int offset = Mathf.Max(0, pageIndex) * _pageSize;
        StartCoroutine(_client.FetchTop(offset, _pageSize, callback));
    }

    // Pay the submission fee, then write the score. Two-step so if the payment
    // succeeds but the Supabase insert fails we still have the on-chain receipt
    // and can retry the insert without charging twice. If the wallet isn't
    // connected, auto-trigger the MWA flow and retry after it connects — so the
    // in-scene Submit button is self-contained and doesn't silently no-op.
    public void SubmitCurrentScore(int score)
    {
        if (SolanaManager.Instance == null)
        {
            OnSubmitFinished?.Invoke(false, "Solana manager missing");
            return;
        }

        if (!SolanaManager.Instance.IsWalletConnected)
        {
            Action<string> once = null;
            once = (addr) =>
            {
                SolanaManager.Instance.OnWalletConnected -= once;
                SubmitCurrentScore(score);
            };
            SolanaManager.Instance.OnWalletConnected += once;
            SolanaManager.Instance.ConnectWallet();
            return;
        }

        if (!SolanaManager.Instance.HasSufficientBalance(_submissionPriceSOL))
        {
            OnSubmitFinished?.Invoke(false, $"Need {_submissionPriceSOL} SOL to submit");
            return;
        }

        SolanaManager.Instance.SendPayment(_submissionPriceSOL, "Leaderboard", (paid, result) =>
        {
            if (!paid)
            {
                OnSubmitFinished?.Invoke(false, "Payment failed: " + result);
                return;
            }
            StartCoroutine(WriteScoreAfterPayment(score, result));
        });
    }

    private IEnumerator WriteScoreAfterPayment(int score, string txSig)
    {
        string wallet = SolanaManager.Instance.WalletAddress;
        string cluster = DetectCluster();
        yield return _client.SubmitScore(wallet, score, txSig, cluster, (ok, err) =>
        {
            if (ok) OnSubmitFinished?.Invoke(true, txSig);
            else OnSubmitFinished?.Invoke(false, "Chain paid but DB write failed: " + err);
        });
    }

    // Best-effort cluster detection so the Supabase row records which network
    // actually settled the payment.
    private static string DetectCluster()
    {
#if SOLANA_SDK_INSTALLED
        if (Solana.Unity.SDK.Web3.Instance != null)
        {
            switch (Solana.Unity.SDK.Web3.Instance.rpcCluster)
            {
                case Solana.Unity.SDK.RpcCluster.MainNet: return "mainnet-beta";
                case Solana.Unity.SDK.RpcCluster.DevNet: return "devnet";
                case Solana.Unity.SDK.RpcCluster.TestNet: return "testnet";
            }
        }
#endif
        return "devnet";
    }
}
