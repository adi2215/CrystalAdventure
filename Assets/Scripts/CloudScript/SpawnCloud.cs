using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnCloud : MonoBehaviour
{
    public GameObject prefab;

    public float spawnRate = 1f;
        
    public float spawnTime = 2f;

    public float minHeight = -1f;

    public float maxHeight = 2f;

    private void OnEnable()
    {
        InvokeRepeating(nameof(Spawn), spawnTime, spawnRate);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(Spawn));
    }

    private void Spawn()
    {
        GameObject clouds = Instantiate(prefab, transform.position, Quaternion.identity);
        clouds.transform.position += Vector3.up * Random.Range(minHeight, maxHeight);
    }
}
