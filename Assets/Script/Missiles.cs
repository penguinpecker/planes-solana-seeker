using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Missiles : MonoBehaviour
{
    #region Public_Varible
    public static Missiles Instance { get; set; }
    public GameObject missileobj;
    public GameObject newMissileObj;
    public GameObject Missilethree;
    public GameObject Star;
    public List<Transform> spawnPoint;
    public List<Transform> spawnStaticPoint;
   
    public GameObject Player;
    public GameObject SpawnObj;
    public float speed;
    public float rotateSpeed;
    public bool spawnStatus;
    public float spawnTime;

    #endregion

    #region Private_Variable
    private int Index;
    private Coroutine _spawnCoroutine;
    #endregion


    #region Unity_callback
    public void OnEnable()
    {
        // Clear to prevent duplicates
        spawnStaticPoint.Clear();
        foreach (Transform child in this.transform)
        {
            spawnStaticPoint.Add(child);
        }
        spawnStatus = true;

        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
        }
        _spawnCoroutine = StartCoroutine(Spawnobj());
    }
    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per fram

    public void Update()
    {

    }

    public void OnDisable()
    {
        spawnStatus = false;
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
    }
    #endregion

    #region Public_Method
    public void StartMissle()
    {
        //StartCoroutine(Spawnobj());
    }
    #endregion

    #region Private_Method
    IEnumerator Spawnobj()
    {
        while (spawnStatus)
        {
            if (spawnPoint == null || spawnPoint.Count == 0 || spawnStaticPoint == null || spawnStaticPoint.Count < 4)
            {
                yield return new WaitForSeconds(1.0f);
                continue;
            }

            int spawnCount = spawnPoint.Count;

            if (spawnStaticPoint[0] != null && missileobj != null)
            {
                spawnStaticPoint[0].transform.position = spawnPoint[Random.Range(0, spawnCount)].transform.position;
                Instantiate(missileobj, spawnStaticPoint[0]);
            }

            yield return new WaitForSeconds(2.0f);
            if (!spawnStatus) yield break;

            if (spawnStaticPoint[1] != null && missileobj != null)
            {
                spawnStaticPoint[1].transform.position = spawnPoint[Random.Range(0, spawnCount)].transform.position;
                Instantiate(missileobj, spawnStaticPoint[1]);
            }

            yield return new WaitForSeconds(2.2f);
            if (!spawnStatus) yield break;

            if (spawnStaticPoint[2] != null && Missilethree != null)
            {
                spawnStaticPoint[2].transform.position = spawnPoint[Random.Range(0, spawnCount)].transform.position;
                Instantiate(Missilethree, spawnStaticPoint[2]);
            }

            yield return new WaitForSeconds(6.2f);
            if (!spawnStatus) yield break;

            if (spawnStaticPoint[3] != null && newMissileObj != null)
            {
                spawnStaticPoint[3].transform.position = spawnPoint[Random.Range(0, spawnCount)].transform.position;
                Instantiate(newMissileObj, spawnStaticPoint[3]);
            }

            yield return new WaitForSeconds(spawnTime);
        }
    }
    #endregion
}