using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PausePanel : MonoBehaviour
{
    #region Public_Variable
    public Text _score,_getScore , _points, _getPoints;
    #endregion

    #region Unity_CallBAck

    void OnEnable()
    {
        _score.text = _getScore.text;
        _points.text = _getPoints.text;
    }
    // Use this for initialization
    void Start ()
    {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
    #endregion
}
