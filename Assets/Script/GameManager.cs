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

    [Header("Total Coins")]
    [SerializeField]
    private Text Coins;

    [Header("Coins Value")]
    [SerializeField]
    private int CoinsValue;

    [Header("Buttons")]
    [SerializeField]
    private List<Button> _buttons;

    [Header("Button Text")]
    [SerializeField]
    private Text _buttonText;

    [Header("Coins Value")]
    [SerializeField]
    private List<int> _values;

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
    #endregion

    #region Unity_CallBack
    void Awake()
    {
        //PlayerPrefs.DeleteAll();
        _playerStartPos = PlayerObj.transform.position;
        // PlayerPrefs.DeleteAll();
        if (Instance == null)
        {
            Instance = this;
        }
    }
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
        HomeScreen.SetActive(true);
        GamePanel.SetActive(false);
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

    }

    // Update is called once per frame
    void Update()
    {

    }
    #endregion

    #region Public_Method
    public void StartGame()
    {
        GetReward();
        GameScreen.Instance.time = 0;
    }

    public void HomeButton()
    {
        Debug.Log("Home Button Press");
        PlayerObj.transform.position = _playerStartPos;
        PlayerObj.transform.rotation = Quaternion.EulerAngles(Vector3.zero);
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
        if (_planeID > 5)
            return;
        Debug.Log("Plaane pos plus plus");
        _planeID++;
        Debug.Log("plane ID" + _planeID);
        PlayerPrefs.SetInt("PlaneID", _planeID);
        CurrentCase();
    }

    public void PlayerPosMinus()
    {
        if (_planeID <= 0)
            return;

        _planeID--;
        PlayerPrefs.SetInt("PlaneID", _planeID);
        CurrentCase();
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
    }

    public void RemoveCoins(int RemoveCoins)
    {
        int coins = int.Parse(Coins.text) - RemoveCoins;
        PlayerPrefs.SetInt("TotalCoins", coins);
        Coins.text = PlayerPrefs.GetInt("TotalCoins").ToString();
    }

    public void SetPlaneCoins()
    {
        if (_planeID == 0)
        {
            PlayerPrefs.SetInt("PlaneID" + _planeID, 1);
            SetPlayButton(_planeID);
        }
        if (_planeID == 1)
        {
            SetPlayButton(_planeID);
        }
        if (_planeID == 2)
        {
            SetPlayButton(_planeID);
        }
        if (_planeID == 3)
        {
            SetPlayButton(_planeID);
        }
        if (_planeID == 4)
        {
            SetPlayButton(_planeID);
        }
        if (_planeID == 5)
        {
            SetPlayButton(_planeID);
        }
        if (_planeID == 6)
        {
            SetPlayButton(_planeID);
        }
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

    public void InappPanelStatus()
    {
        InappPanel.SetActive(true);
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
    #endregion
}
