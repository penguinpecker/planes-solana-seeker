using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace Amar
{
    public class GameOver : MonoBehaviour
    {

        #region Public_Variables
        #endregion

        #region Private_Variables
        [SerializeField]
        Text _time, _points, _totalPoints, _yourScore, _highScore;
        private float _totalTime;
        private int _totalStar;
        private int _HighScoreValue, _YourScoreValue;


        #endregion

        #region Events
        #endregion

        #region Unity_CallBacks
        void Awake() { }
        void OnEnable()
        {
            // _time sits next to the clock icon; _points sits next to the
            // star icon. Show the raw stats the user recognises (minutes:seconds
            // and star count) instead of the time*1.5 / stars*10 derivatives
            // that used to display there. The total-score math below is
            // unchanged so the leaderboard still ranks on the composite score.
            //
            // Wrap every singleton/PlayerPrefs read in a null-check + try/catch.
            // On-device we hit a case where GameScreen.Instance or
            // GameManager.Instance was momentarily null by the time the panel
            // activated (script execution order on a cold start), which threw
            // before any text got assigned and left the labels blank. Emulator
            // never reproduced it because its init order happened to stabilise
            // sooner. With the guards the texts always render SOMETHING, and
            // any miss is visible in logcat instead of failing silently.
            try
            {
                _totalTime = GameScreen.Instance != null ? GameScreen.Instance.GetScore() : 0f;
                int minutes = Mathf.FloorToInt(_totalTime / 60f);
                int seconds = Mathf.FloorToInt(_totalTime % 60f);
                if (_time != null) _time.text = string.Format("{0}:{1:D2}", minutes, seconds);

                _totalStar = GameManager.Instance != null ? GameManager.Instance.ExtraInt : 0;
                if (_points != null) _points.text = _totalStar.ToString();

                // Earn rate cut 10x from the original (time*1.5 + stars*10)
                // so the SOL-for-coins shop becomes the fast path and the
                // grind-earned planes feel genuinely earned. A typical
                // 60-second / 10-star run now pays ~19 base coins instead
                // of ~190. The plane tier coin-multiplier still stacks on
                // top, so a B52 owner (1.4x) caps at ~27 coins per that
                // run -- ~35 decent runs to afford the next 1000-plane.
                _YourScoreValue = ((int)(_totalTime * 0.15f)) + ((int)(_totalStar * 1));

                // Leaderboard rank is computed from the un-multiplied
                // score so owning a better plane doesn't buy you a
                // higher leaderboard seat -- only a better wallet. The
                // player's coin pile gets the plane's CoinMult applied
                // via PlaneStats.ApplyCoinMultiplier before AddCoins.
                int awarded = PlaneStats.ApplyCoinMultiplier(_YourScoreValue);
                if (GameManager.Instance != null) GameManager.Instance.AddCoins(awarded);

                if (_totalPoints != null) _totalPoints.text = _YourScoreValue.ToString();
                if (_yourScore != null) _yourScore.text = _YourScoreValue.ToString();

                _HighScoreValue = PlayerPrefs.GetInt("HighScore", 0);
                if (_YourScoreValue >= _HighScoreValue)
                {
                    _HighScoreValue = _YourScoreValue;
                    PlayerPrefs.SetInt("HighScore", _HighScoreValue);
                    if (PlayerIdentity.Instance != null) PlayerIdentity.Instance.MarkDirty();
                }
                if (_highScore != null) _highScore.text = _HighScoreValue.ToString();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[GameOver] populate failed: " + e);
                // Fallbacks so the panel never renders with blank labels.
                if (_time != null && string.IsNullOrEmpty(_time.text)) _time.text = "0:00";
                if (_points != null && string.IsNullOrEmpty(_points.text)) _points.text = "0";
                if (_totalPoints != null && string.IsNullOrEmpty(_totalPoints.text)) _totalPoints.text = "0";
                if (_yourScore != null && string.IsNullOrEmpty(_yourScore.text)) _yourScore.text = "0";
                if (_highScore != null && string.IsNullOrEmpty(_highScore.text))
                    _highScore.text = PlayerPrefs.GetInt("HighScore", 0).ToString();
            }
        }

        // Wired to the Submit button inside the in-scene GameOverPanel.
        public void OnSubmitScoreClick()
        {
            if (LeaderboardManager.Instance != null)
                LeaderboardManager.Instance.SubmitCurrentScore(_YourScoreValue);
        }
        // Use this for initialization
        void Start() { }
        // Update is called once per frame
        void Update() { }
        void OnDisable() { }
        #endregion

        #region Private_Methods
        #endregion

        #region Public_Methods
        #endregion

        #region UI_Calls
        #endregion

        #region Coroutines
        #endregion

        #region Custom_CallBacks
        #endregion
    }
}