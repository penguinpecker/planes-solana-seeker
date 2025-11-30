using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExtraObj : MonoBehaviour
{
    #region Public_Variable
    public static ExtraObj Instance = null;
    public List<Transform> spawnPoints;
    public List<Transform> spawnStaticPoint;
    public GameObject Player;
    #endregion

    #region Private_Variable
    [SerializeField]
    GameObject _star;
    #endregion


    #region Unity_CallBAck
    public void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
    }

    void OnEnable()
    {
        StartCoroutine(CreateObj());
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
        StopCoroutine(CreateObj());
        foreach(Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }
    #endregion

    #region Coroutine
    IEnumerator CreateObj()
    {
        while (true)
        {
            GameObject _starObj = Instantiate(_star, this.transform );
            _starObj.transform.position = spawnPoints[Random.Range(0, 6)].position;
            yield return new WaitForSeconds(2.0f);
        }
    }
    #endregion
}
