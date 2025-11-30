using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameScreen : MonoBehaviour
{
    public static GameScreen Instance = null;
    public Text Score;
    public float time;

    #region Private_Variable
    private float minutes;
    private float seconds;
    #endregion

    void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
    }

    void OnEnable()
    {
        //time = 0f;
    }
    // Use this for initialization
    void Start ()
    {
		
	}
	
	// Update is called once per frame
	void Update ()
    {
        time += Time.deltaTime;
        minutes = Mathf.Floor(time / 60);
        seconds = time % 60;
        int secondsV = (int)seconds;

        if (secondsV < 10)
        {
            Score.text = minutes + ":0" + secondsV.ToString();

        }
        else
        {
            Score.text = minutes + ":" + secondsV.ToString();
           
        }
    }

    public float GetScore()
    {
        return ((minutes * 60) + seconds) ;
    }
}
