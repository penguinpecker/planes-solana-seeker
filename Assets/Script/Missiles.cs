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
    private Rigidbody2D _rb;
    #endregion


    #region Unity_callback
    public void OnEnable()
    {
        foreach (Transform child in this.transform)
        {
            spawnStaticPoint.Add(child);
        }
        spawnStatus = true;
        StartCoroutine(Spawnobj());
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
        //StopCoroutine(Spawnobj());
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
            spawnStaticPoint[0].transform.position = spawnPoint[Random.Range(0, 9)].transform.position;
            GameObject SpawnObj1 = Instantiate(missileobj, spawnStaticPoint[0]) as GameObject;

            yield return new WaitForSeconds(2.0f);
            spawnStaticPoint[1].transform.position = spawnPoint[Random.Range(0, 9)].transform.position;
            GameObject SpawnObj2 = Instantiate(missileobj, spawnStaticPoint[1]) as GameObject;

            yield return new WaitForSeconds(2.2f);
            spawnStaticPoint[2].transform.position = spawnPoint[Random.Range(0, 9)].transform.position;
            GameObject SpawnObj3 = Instantiate(Missilethree, spawnStaticPoint[2]) as GameObject;

            yield return new WaitForSeconds(6.2f);
            spawnStaticPoint[3].transform.position = spawnPoint[Random.Range(0, 9)].transform.position;
            GameObject SpawnObj4 = Instantiate(newMissileObj, spawnStaticPoint[3]) as GameObject;

            yield return new WaitForSeconds(spawnTime);
        }
    }
    #endregion
}