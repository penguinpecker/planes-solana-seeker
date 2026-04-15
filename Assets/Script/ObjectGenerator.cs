using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectGenerator : MonoBehaviour
{
    #region Public_Variable
    public GameObject Star;
    public GameObject Protector;
    public GameObject Boost;
    #endregion

    #region Private_Variable
    [SerializeField]
    List<Transform> _points;
    #endregion

    #region Private_Method

    public void OnEnable()
    {
        StartCoroutine(GenerateObj());
    }
    // Use this for initialization
    void Start ()
    {
		
	}
	
	// Update is called once per frame
	void Update ()
    {
		
	}

    public void OnDisable()
    {
        StopCoroutine(GenerateObj());
    }
    #endregion

    #region Coroutine
    IEnumerator GenerateObj()
    {
        while (true)
        {
            Instantiate(Star, _points[0], false);
            yield return new WaitForSeconds(1.0f);
            Instantiate(Star, _points[1], false);
            yield return new WaitForSeconds(1.5f);
            Instantiate(Star, _points[2], false);
            yield return new WaitForSeconds(5.0f);
        }
    }
    #endregion
}
