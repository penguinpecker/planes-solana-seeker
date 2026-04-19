using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance = null;
    #region Public_Variable
    public GameObject Missiles;

    public GameObject HomeScreen;
    public GameObject PlayerObj;
    public GameObject GameOver;
    public GameObject ScreenTouch;
    public GameObject GamePanel;
    public GameObject SettingPanel;
    public GameObject PausePanel;
    public GameObject WatchAd;
    public GameObject RewardPanel;
    public GameObject InappPanel;
    public List<GameObject> controller;
    public Text Score;
    public float time;
    public Text ExtraObj;
    public int ExtraInt;
    public GameObject ExtraObj1;

    [Header("Plane Position")]
    public GameObject _plane;

    public int _planeID = 0;
    public List<Sprite> buttonImage;
    public Image _conImage;
    private int ContNum = 0;
    private int NumOfAd = 3;
    private Vector3 _playerStartPos;
    [SerializeField]
    List<AudioClip> _clip;

    [Header("Total Coins (In-Game)")]
    [SerializeField]
    private Text Coins;

    [Header("Coins Value")]
    [SerializeField]
    private int CoinsValue;

    [Header("SOL Balance")]
    [SerializeField]
    private Text _solBalanceText;
    private float _solBalance = 0f;

    [Header("Buttons")]
    [SerializeField]
    private List<Button> _buttons;

    [Header("Button Text")]
    [SerializeField]
    private Text _buttonText;

    [Header("Coins Value")]
    [SerializeField]
    private List<int> _values;

    [Header("Coin Shop - Buy Coins with SOL")]
    [SerializeField] private float _consumable1000PriceSOL = 0.199f;
    [SerializeField] private float _consumable2000PriceSOL = 0.299f;
    [SerializeField] private float _consumable3000PriceSOL = 0.399f;

    [Header("Sound Control")]
    [SerializeField]
    private Image _soundButtonImage;
    [SerializeField]
    private Sprite _soundOnSprite;
    [SerializeField]
    private Sprite _soundOffSprite;
    [SerializeField]
    private Text _soundStatusText;
    private bool _isSoundOn = true;

    [Header("Solana Wallet")]
    [SerializeField]
    private GameObject _connectWalletButton;
    [SerializeField]
    private Text _walletAddressText;
    #endregion

    #region Unity_CallBack
    void Awake()
    {
        _playerStartPos = PlayerObj.transform.position;
        if (Instance == null)
        {
            Instance = this;
        }

        // Auto-create SolanaManager if it doesn't exist
        EnsureSolanaManagerExists();
        EnsureLeaderboardSingletons();
    }

    private void EnsureSolanaManagerExists()
    {
        if (SolanaManager.Instance == null)
        {
            GameObject solanaManagerObj = new GameObject("SolanaManager");
            solanaManagerObj.AddComponent<SolanaManager>();
            Debug.Log("[GameManager] Created SolanaManager automatically");
        }
    }

    private void EnsureLeaderboardSingletons()
    {
        if (LeaderboardManager.Instance == null)
        {
            var go = new GameObject("LeaderboardManager");
            go.AddComponent<LeaderboardManager>();
        }
        if (LeaderboardPanelBuilder.Instance == null)
        {
            var go = new GameObject("LeaderboardPanel");
            go.AddComponent<LeaderboardPanelBuilder>();
        }
        if (BackgroundMusicManager.Instance == null)
        {
            var go = new GameObject("BackgroundMusicManager");
            go.AddComponent<BackgroundMusicManager>();
        }
        if (PlayerIdentity.Instance == null)
        {
            var go = new GameObject("PlayerIdentity");
            go.AddComponent<PlayerIdentity>();
        }
        if (DifficultyDirector.Instance == null)
        {
            var go = new GameObject("DifficultyDirector");
            go.AddComponent<DifficultyDirector>();
        }
        if (AbilityController.Instance == null)
        {
            var go = new GameObject("AbilityController");
            go.AddComponent<AbilityController>();
        }
        if (AbilitySpawner.Instance == null)
        {
            var go = new GameObject("AbilitySpawner");
            go.AddComponent<AbilitySpawner>();
        }
    }

    // Previously exposed a music-only toggle; removed because the existing
    // sound button in Settings already mutes/unmutes all audio (including
    // the background music loop) via AudioListener.pause. One switch, one
    // source of truth.
    void OnEnable()
    {
        Coins.text = (PlayerPrefs.GetInt("TotalCoins")).ToString();
        _planeID = PlayerPrefs.GetInt("PlaneID");
        ExtraObj1.SetActive(false);
        if (WatchAd != null)
        {
            WatchAd.SetActive(false);
        }
        Missiles.SetActive(false);
        // Force a clean HomeScreen-only state on launch so the SettingPanel,
        // PausePanel, GameOver, etc. left active in the scene don't render on
        // top of the home screen when the game first opens.
        HomeScreen.SetActive(true);
        GamePanel.SetActive(false);
        if (SettingPanel != null) SettingPanel.SetActive(false);
        if (PausePanel != null) PausePanel.SetActive(false);
        if (GameOver != null) GameOver.SetActive(false);
        if (InappPanel != null) InappPanel.SetActive(false);
        if (RewardPanel != null) RewardPanel.SetActive(false);
        CurrentCase();
        _conImage.sprite = buttonImage[ContNum];

        //Set Plane Coins
        SetPlaneCoins();

        // Initialize sound state from saved preference
        InitializeSoundState();
    }
    // Use this for initialization
    void Start()
    {
        // Belt + suspenders for the "app opens into Settings" bug: if anything
        // re-activated SettingPanel after OnEnable (scene asset loading order,
        // third-party Awake), force it back to hidden on the first frame.
        if (SettingPanel != null && SettingPanel.activeSelf) SettingPanel.SetActive(false);
        if (PausePanel != null && PausePanel.activeSelf) PausePanel.SetActive(false);
        if (GameOver != null && GameOver.activeSelf) GameOver.SetActive(false);
        if (InappPanel != null && InappPanel.activeSelf) InappPanel.SetActive(false);
        if (RewardPanel != null && RewardPanel.activeSelf) RewardPanel.SetActive(false);
        if (HomeScreen != null && !HomeScreen.activeSelf) HomeScreen.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {

    }
    #endregion

    #region Public_Method
    public void StartGame()
    {
        ResetRunState();
        GetReward();
        GameScreen.Instance.time = 0;
        // Start the difficulty tier clock from zero so missiles + stars
        // ramp predictably each run.
        if (DifficultyDirector.Instance != null) DifficultyDirector.Instance.StartRun();
        // Clear any leftover ability from a previous run and start
        // dropping power-up pickups into the field.
        if (AbilityController.Instance != null) AbilityController.Instance.Clear();
        if (AbilitySpawner.Instance != null) AbilitySpawner.Instance.BeginRun();
    }

    // On restart the previous run's missiles, stars, coin count, and player
    // transform were persisting, so the plane picked up mid-flight instead of
    // from the start. Wipe them before GetReward resumes time.
    private void ResetRunState()
    {
        PlayerObj.transform.position = _playerStartPos;
        PlayerObj.transform.rotation = Quaternion.identity;

        ExtraInt = 0;
        if (ExtraObj != null) ExtraObj.text = "0";

        // Target the runtime-spawned instances only — the scene's Missile
        // spawner (tagged "Missile" with Missiles.cs) and ExtraObjGenerator
        // (tagged "ExtraObj" with ExtraObj.cs) are the *containers* that keep
        // producing new entities. Destroying them by tag on every restart
        // killed future spawns after the first run. Instead: find spawned
        // missiles by their MissileObj component, and clear stars by wiping
        // the generator's children.
        foreach (MissileObj m in FindObjectsByType<MissileObj>(FindObjectsSortMode.None))
        {
            if (m != null) Destroy(m.gameObject);
        }
        // GameManager has a Text field named ExtraObj, which shadows the
        // ExtraObj class in this scope — use global:: to reach the class.
        if (global::ExtraObj.Instance != null)
        {
            foreach (Transform child in global::ExtraObj.Instance.transform)
            {
                if (child != null) Destroy(child.gameObject);
            }
        }

        NumOfAd = 3;
    }

    public void HomeButton()
    {
        Debug.Log("Home Button Press");
        PlayerObj.transform.position = _playerStartPos;
        PlayerObj.transform.rotation = Quaternion.identity;
        if (WatchAd != null)
        {
            WatchAd.SetActive(false);
        }
        PausePanel.SetActive(false);
        HomeScreen.SetActive(true);
        GameOver.SetActive(false);
        PlayerObj.SetActive(true);
        Missiles.SetActive(false);
        //ScreenTouch.SetActive(false);
        controller[ContNum].SetActive(false);
        GamePanel.SetActive(false);
        ExtraObj1.SetActive(false);
        Time.timeScale = 1;
        GameScreen.Instance.time = 0;
        NumOfAd = 3;
        ExtraObj.text = "0";
    }

    public void GetReward()
    {
        PlayerObj.SetActive(true);
        GameOver.SetActive(false);
        ExtraObj1.SetActive(true);
        Missiles.SetActive(true);
        HomeScreen.SetActive(false);
        // ScreenTouch.SetActive(true);
        GamePanel.SetActive(true);
        controller[ContNum].SetActive(true);
        PausePanel.SetActive(false);
        Time.timeScale = 1;
        RewardPanel.SetActive(false);
    }


    //when player die
    public void OnplayerDie()
    {
        // Notify the parent Farcaster Mini App (Next.js) that the player died
        // so it can trigger an NFT mint on Sepolia via Thirdweb.
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.ExternalEval("window.parent.postMessage({ type: 'REQUEST_MINT_ON_DEATH' }, '*');");
#endif

        // Freeze the tier clock so the Game Over panel doesn't keep
        // ramping while the player is reading their score.
        if (DifficultyDirector.Instance != null) DifficultyDirector.Instance.StopRun();
        // Clear any in-flight ability buff + stop dropping new pickups.
        if (AbilityController.Instance != null) AbilityController.Instance.Clear();
        if (AbilitySpawner.Instance != null) AbilitySpawner.Instance.StopRun();

        Time.timeScale = 0;
        NumOfAd--;
        Debug.Log("watch add" + NumOfAd);
        if (NumOfAd == 0)
        {
            GameOver.SetActive(true);
            GamePanel.SetActive(false);
            NumOfAd = 3;
        }
        else
        {
            // Previously this branch showed a \"Watch Ad\" panel.
            // For the Farcaster/NFT version we remove the ad option
            // and go directly to the game over state instead.
            GameOver.SetActive(true);
            GamePanel.SetActive(false);
            NumOfAd = 3;
        }
    }

    // Called from JavaScript in the WebGL template when the mint finishes.
    // See: public/planes-webgl/index.html where window.unityInstance.SendMessage
    // invokes GameManager.OnMintResult(string).
    public void OnMintResult(string json)
    {
        Debug.Log("Mint result from parent: " + json);

        // Very simple flow for now:
        // - If status === "success", restart the game
        // - Otherwise, just unpause and show GameOver as usual.
        try
        {
            if (json.Contains("\"status\":\"success\""))
            {
                // Reset time scale and use existing home/start logic.
                Time.timeScale = 1;
                HomeButton();
                StartGame();
            }
            else
            {
                // Resume and show regular Game Over UI if needed.
                Time.timeScale = 1;
                GameOver.SetActive(true);
                GamePanel.SetActive(false);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to handle mint result: " + e.Message);
            Time.timeScale = 1;
            GameOver.SetActive(true);
            GamePanel.SetActive(false);
        }
    }

    public void DoNotWatchAd()
    {
        GameOver.SetActive(true);
        GamePanel.SetActive(false);
        WatchAd.SetActive(false);
        Time.timeScale = 1;
        NumOfAd = 3;
    }

    // for watching ad 
    public void OnWacthClick()
    {
        WatchAd.SetActive(false);
       // GoogleMobileAdsDemoScript.Instance.ShowRewardedAd();
        ExtraObj1.SetActive(false);
    }

    //for adding reward point
    public void AdReward()
    {
        RewardPanel.SetActive(true);
    }

    public void ExtraIns()
    {
        ExtraInt++;
        ExtraObj.text = ExtraInt.ToString();
    }

    public void PlayerPosPlus()
    {
        if (_planeID > 6)
            return;
        Debug.Log("Plaane pos plus plus");
        _planeID++;
        Debug.Log("plane ID" + _planeID);
        PlayerPrefs.SetInt("PlaneID", _planeID);
        CurrentCase();
        if (PlayerIdentity.Instance != null) PlayerIdentity.Instance.MarkDirty();
    }

    public void PlayerPosMinus()
    {
        if (_planeID <= 0)
            return;

        _planeID--;
        PlayerPrefs.SetInt("PlaneID", _planeID);
        CurrentCase();
        if (PlayerIdentity.Instance != null) PlayerIdentity.Instance.MarkDirty();
    }

    public void CurrentCase()
    {
        Debug.Log("Current Case Value");
        _plane.transform.position = new Vector3(-20.0f * _planeID, _plane.transform.position.y, _plane.transform.position.z);
    }

    public void settingButton()
    {
        SettingPanel.SetActive(true);
    }

    public void CloseButton()
    {
        SettingPanel.SetActive(false);
        // HomeScreen.SetActive(true);
    }

    public void PauseButton()
    {
        GamePanel.SetActive(false);
        Time.timeScale = 0;
        PausePanel.SetActive(true);
        controller[ContNum].SetActive(false);
    }

    public void ClosePausePanel()
    {
        GamePanel.SetActive(true);
        controller[ContNum].SetActive(true);
        PausePanel.SetActive(false);
        Time.timeScale = 1;
    }

    public void ContButton()
    {
        if (ContNum < 3)
        {
            if (ContNum < 2)
            {
                ContNum++;
                _conImage.sprite = buttonImage[ContNum];
            }
            else
            {
                ContNum = 0;
                _conImage.sprite = buttonImage[ContNum];

            }
        }
    }

    public void AddCoins(int AddCoins)
    {
        int coins = int.Parse(Coins.text) + AddCoins;
        PlayerPrefs.SetInt("TotalCoins", coins);
        Coins.text = PlayerPrefs.GetInt("TotalCoins").ToString();
        if (PlayerIdentity.Instance != null) PlayerIdentity.Instance.MarkDirty();
    }

    public void RemoveCoins(int RemoveCoins)
    {
        int coins = int.Parse(Coins.text) - RemoveCoins;
        PlayerPrefs.SetInt("TotalCoins", coins);
        Coins.text = PlayerPrefs.GetInt("TotalCoins").ToString();
        if (PlayerIdentity.Instance != null) PlayerIdentity.Instance.MarkDirty();
    }

    // Pulled from PlayerIdentity after a remote load so the HUD and
    // plane-chooser pick up the new values the server handed us.
    public void RefreshFromPlayerPrefs()
    {
        if (Coins != null) Coins.text = PlayerPrefs.GetInt("TotalCoins", 0).ToString();
        _planeID = PlayerPrefs.GetInt("PlaneID", 0);
        CurrentCase();
        SetPlaneCoins();
        InitializeSoundState();
    }

    public void SetPlaneCoins()
    {
        // First plane is always free/unlocked
        if (_planeID == 0)
        {
            PlayerPrefs.SetInt("PlaneID" + _planeID, 1);
        }
        SetPlayButton(_planeID);
    }

    private void SetPlayButton(int status)
    {
        if (PlayerPrefs.GetInt("PlaneID"+status) == 1)
        {
            PlayButtonStatusTrue();
        }
        else
        {
            PurachseButtonStatusTrue(status);
        }
    }

    private void PlayButtonStatusTrue()
    {
        _buttons[0].gameObject.SetActive(true);
        _buttons[1].gameObject.SetActive(false);
    }

    private void PurachseButtonStatusTrue(int Coins)
    {
        _buttons[1].gameObject.SetActive(true);
        _buttons[0].gameObject.SetActive(false);
        _buttonText.text = _values[Coins].ToString();
    }

    public void OnPlanePurchaseButtonClick()
    {
        if (int.Parse(Coins.text) >= _values[_planeID])
        {
            PlayerPrefs.SetInt("PlaneID" + _planeID, 1);
            PlayButtonStatusTrue();
            RemoveCoins(_values[_planeID]);
            PlayerPrefs.SetInt("Purchased", _planeID);
        }
    }

    // Coin Shop - Buy coins with SOL (hooked to the InApp panel's Buy buttons)
    public void BuyConsumable1000()
    {
        BuyCoinPackageWithSol(1000, _consumable1000PriceSOL, "Consumable1000");
    }

    public void BuyConsumable2000()
    {
        BuyCoinPackageWithSol(2000, _consumable2000PriceSOL, "Consumable2000");
    }

    public void BuyConsumable3000()
    {
        BuyCoinPackageWithSol(3000, _consumable3000PriceSOL, "Consumable3000");
    }

    private void BuyCoinPackageWithSol(int coinAmount, float solPrice, string packageName)
    {
        // Check if SolanaManager exists
        if (SolanaManager.Instance == null)
        {
            Debug.LogError("SolanaManager not found! Please add SolanaManager to the scene.");
            return;
        }

        // Previously: if the wallet wasn't connected, this method would kick
        // off ConnectWallet and silently return — so the first Buy tap opened
        // the wallet prompt, and the user had to tap Buy a second time after
        // authorizing. Now queue the purchase to auto-retry as soon as
        // OnWalletConnected fires, so a single tap always reaches the payment.
        if (!SolanaManager.Instance.IsWalletConnected)
        {
            Debug.Log("Wallet not connected. Initiating connection and queuing purchase...");
            System.Action<string> once = null;
            once = (_) =>
            {
                SolanaManager.Instance.OnWalletConnected -= once;
                BuyCoinPackageWithSol(coinAmount, solPrice, packageName);
            };
            SolanaManager.Instance.OnWalletConnected += once;
            ConnectSolanaWallet();
            return;
        }

        // Previously: bail if HasSufficientBalance returned false. That killed
        // the second leg of the auto-connect flow because OnWalletConnected
        // fires BEFORE Web3.OnBalanceChange, so _walletBalance is still 0 when
        // the retry runs. Only bail when the balance is actually known
        // (> 0) AND confirmed insufficient — otherwise proceed and let the
        // wallet/RPC reject at signing time if the user is truly underfunded.
        if (SolanaManager.Instance.WalletBalance > 0f
            && !SolanaManager.Instance.HasSufficientBalance(solPrice))
        {
            Debug.Log($"Insufficient SOL balance. Need {solPrice} SOL, have {SolanaManager.Instance.WalletBalance} SOL");
            return;
        }

        // Initiate SOL payment for coins
        StartCoroutine(ProcessCoinPurchase(coinAmount, solPrice, packageName));
    }

    private System.Collections.IEnumerator ProcessCoinPurchase(int coinAmount, float solPrice, string packageName)
    {
        Debug.Log($"Processing payment of {solPrice} SOL for {coinAmount} coins ({packageName})");

        bool paymentComplete = false;
        bool paymentSuccess = false;
        string transactionResult = "";

        // Use SolanaManager to process real payment
        SolanaManager.Instance.SendPayment(solPrice, packageName, (success, result) => {
            paymentComplete = true;
            paymentSuccess = success;
            transactionResult = result;
        });

        // Wait for payment to complete
        while (!paymentComplete)
        {
            yield return null;
        }

        if (paymentSuccess)
        {
            Debug.Log($"Payment successful! Transaction: {transactionResult}");
            // Add coins to player's balance
            AddCoins(coinAmount);
            Debug.Log($"Added {coinAmount} coins to balance!");
            // Close the in-app panel
            InAppPanelClose();

            // Fire-and-forget: record the purchase in Supabase so the backend
            // has an indexed, on-chain-verified row for analytics. The user
            // already got their coins locally — a failed DB write doesn't
            // block gameplay, it just means one row is missing from the
            // dashboard that can be reconciled against the merchant wallet's
            // on-chain history later.
            string pkg = coinAmount == 1000 ? "coins_1000"
                       : coinAmount == 2000 ? "coins_2000"
                       : coinAmount == 3000 ? "coins_3000"
                       : null;
            if (pkg != null) StartCoroutine(RecordPurchase(pkg, transactionResult));
        }
        else
        {
            Debug.LogError($"Payment failed: {transactionResult}");
        }
    }

    [System.Serializable]
    private class PurchasePayload
    {
        public string pl_wallet;
        public string pl_package;
        public string pl_tx_signature;
        public string pl_cluster;
    }

    private System.Collections.IEnumerator RecordPurchase(string pkg, string txSignature)
    {
        if (SolanaManager.Instance == null || !SolanaManager.Instance.IsWalletConnected) yield break;

        string cluster = "mainnet-beta";
#if SOLANA_SDK_INSTALLED
        if (Solana.Unity.SDK.Web3.Instance != null)
        {
            switch (Solana.Unity.SDK.Web3.Instance.rpcCluster)
            {
                case Solana.Unity.SDK.RpcCluster.MainNet: cluster = "mainnet-beta"; break;
                case Solana.Unity.SDK.RpcCluster.DevNet:  cluster = "devnet"; break;
                case Solana.Unity.SDK.RpcCluster.TestNet: cluster = "testnet"; break;
            }
        }
#endif

        var payload = new PurchasePayload
        {
            pl_wallet = SolanaManager.Instance.WalletAddress,
            pl_package = pkg,
            pl_tx_signature = txSignature,
            pl_cluster = cluster
        };
        string json = UnityEngine.JsonUtility.ToJson(payload);
        string url = SupabaseLeaderboardClient.SupabaseUrl + "/functions/v1/pl-submit-purchase";

        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(body);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", SupabaseLeaderboardClient.SupabaseAnonKey);
            req.SetRequestHeader("Authorization", "Bearer " + SupabaseLeaderboardClient.SupabaseAnonKey);
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success && req.responseCode < 300)
            {
                Debug.Log($"[Purchases] Recorded {pkg} for {txSignature.Substring(0, System.Math.Min(10, txSignature.Length))}…");
            }
            else
            {
                Debug.LogWarning($"[Purchases] Record failed ({req.responseCode}): {req.downloadHandler?.text}");
            }
        }
    }

    public void UpdateSolBalanceDisplay(float balance)
    {
        _solBalance = balance;
        if (_solBalanceText != null)
        {
            _solBalanceText.text = "◎ " + balance.ToString("F4") + " SOL";
        }
    }

    public void InappPanelStatus()
    {
        InappPanel.SetActive(true);
    }

    public void OpenLeaderboard()
    {
        if (LeaderboardPanelBuilder.Instance != null)
            LeaderboardPanelBuilder.Instance.Show();
    }

    public void InAppPanelClose()
    {
        InappPanel.SetActive(false);
    }

    // Initialize sound state from PlayerPrefs
    private void InitializeSoundState()
    {
        // Default to sound ON (1) if no preference saved
        _isSoundOn = PlayerPrefs.GetInt("SoundOn", 1) == 1;
        ApplySoundState();
    }

    // Toggle sound on/off - call this from the sound button
    public void ToggleSound()
    {
        _isSoundOn = !_isSoundOn;
        PlayerPrefs.SetInt("SoundOn", _isSoundOn ? 1 : 0);
        PlayerPrefs.Save();
        ApplySoundState();
        if (PlayerIdentity.Instance != null) PlayerIdentity.Instance.MarkDirty();
    }

    // Apply the current sound state to audio and UI
    private void ApplySoundState()
    {
        // Use AudioListener.pause to globally mute/unmute all audio
        AudioListener.pause = !_isSoundOn;

        // Update the sound status text
        if (_soundStatusText != null)
        {
            _soundStatusText.text = _isSoundOn ? "On" : "Off";
        }

        // Update the button image if assigned
        if (_soundButtonImage != null)
        {
            if (_isSoundOn && _soundOnSprite != null)
            {
                _soundButtonImage.sprite = _soundOnSprite;
            }
            else if (!_isSoundOn && _soundOffSprite != null)
            {
                _soundButtonImage.sprite = _soundOffSprite;
            }
        }
    }

    // Solana Wallet Integration for Seeker dApp Store
    public void ConnectSolanaWallet()
    {
        if (SolanaManager.Instance != null)
        {
            SolanaManager.Instance.OnWalletConnected += OnSolanaWalletConnected;
            SolanaManager.Instance.OnWalletDisconnected += OnSolanaWalletDisconnected;
            SolanaManager.Instance.OnError += OnSolanaError;
            SolanaManager.Instance.ConnectWallet();
        }
        else
        {
            Debug.LogWarning("SolanaManager not found in scene");
        }
    }

    public void DisconnectSolanaWallet()
    {
        if (SolanaManager.Instance != null)
        {
            SolanaManager.Instance.DisconnectWallet();
        }
    }

    private void OnSolanaWalletConnected(string walletAddress)
    {
        if (_walletAddressText != null && !string.IsNullOrEmpty(walletAddress))
        {
            // Show truncated address with bounds check
            string truncated;
            if (walletAddress.Length >= 8)
            {
                truncated = walletAddress.Substring(0, 4) + "..." + walletAddress.Substring(walletAddress.Length - 4);
            }
            else
            {
                truncated = walletAddress;
            }
            _walletAddressText.text = truncated;
        }
        if (_connectWalletButton != null)
        {
            _connectWalletButton.SetActive(false);
        }
    }

    private void OnSolanaWalletDisconnected()
    {
        Debug.Log("Wallet disconnected");
        if (_walletAddressText != null)
        {
            _walletAddressText.text = "";
        }
        if (_connectWalletButton != null)
        {
            _connectWalletButton.SetActive(true);
        }
    }

    private void OnSolanaError(string error)
    {
        Debug.LogError("Solana Error: " + error);
    }

    private void OnDestroy()
    {
        if (SolanaManager.Instance != null)
        {
            SolanaManager.Instance.OnWalletConnected -= OnSolanaWalletConnected;
            SolanaManager.Instance.OnWalletDisconnected -= OnSolanaWalletDisconnected;
            SolanaManager.Instance.OnError -= OnSolanaError;
        }
    }
    #endregion
}
