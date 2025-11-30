using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnPoints : MonoBehaviour
{
    public List<Transform> spawnPoint;

    public void OnEnable()
    {
        foreach (Transform child in this.transform)
        {
            spawnPoint.Add(child);
        }
    }
    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
