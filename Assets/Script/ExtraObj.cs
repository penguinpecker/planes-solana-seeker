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
    private Coroutine _createObjCoroutine;
    #endregion


    #region Unity_CallBAck
    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    void OnEnable()
    {
        if (_createObjCoroutine != null)
        {
            StopCoroutine(_createObjCoroutine);
        }
        _createObjCoroutine = StartCoroutine(CreateObj());
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
        if (_createObjCoroutine != null)
        {
            StopCoroutine(_createObjCoroutine);
            _createObjCoroutine = null;
        }
        foreach (Transform child in transform)
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
            if (_star != null && spawnPoints != null && spawnPoints.Count > 0)
            {
                GameObject _starObj = Instantiate(_star, this.transform);
                _starObj.transform.position = spawnPoints[Random.Range(0, spawnPoints.Count)].position;
            }
            // Star cadence tightens with the difficulty tier (2.4s at
            // tier 0, 0.8s at tier 8+). Ramping star income alongside
            // missile pressure keeps the mid-game feeling rich instead
            // of just punishing.
            float gap = DifficultyDirector.Instance != null
                ? DifficultyDirector.Instance.StarSpawnInterval : 2.0f;
            yield return new WaitForSeconds(gap);
        }
    }
    #endregion
}
