using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnPoints : MonoBehaviour
{
    public List<Transform> spawnPoint;

    public void OnEnable()
    {
        // Clear list to prevent duplicates on multiple OnEnable calls
        spawnPoint.Clear();
        foreach (Transform child in this.transform)
        {
            spawnPoint.Add(child);
        }
    }
}
