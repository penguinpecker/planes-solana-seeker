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
            _totalTime = GameScreen.Instance.GetScore();
            int minutes = Mathf.FloorToInt(_totalTime / 60f);
            int seconds = Mathf.FloorToInt(_totalTime % 60f);
            _time.text = string.Format("{0}:{1:D2}", minutes, seconds);

            _totalStar = GameManager.Instance.ExtraInt;
            _points.text = _totalStar.ToString();

            _YourScoreValue = ((int)(_totalTime * 1.5f)) + ((int)(_totalStar * 10));

            GameManager.Instance.AddCoins(_YourScoreValue);

            _totalPoints.text = _YourScoreValue.ToString();
            _yourScore.text = _YourScoreValue.ToString();
            _HighScoreValue = PlayerPrefs.GetInt("HighScore");

            if (_YourScoreValue >= _HighScoreValue)
            {
                _HighScoreValue = _YourScoreValue;
                PlayerPrefs.SetInt("HighScore", _HighScoreValue);
                if (PlayerIdentity.Instance != null) PlayerIdentity.Instance.MarkDirty();
            }
            else
            {
                _HighScoreValue = PlayerPrefs.GetInt("HighScore");
            }
            _highScore.text = _HighScoreValue.ToString();
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