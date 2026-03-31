using System.Collections.Generic;
using UnityEngine;

public class WindRibbonSpawner : MonoBehaviour
{
    public static WindRibbonSpawner Instance { get; private set; }

    [Header("Pool")]
    public WindRibbon ribbonPrefab;
    public Material   ribbonMaterial;
    public int        poolSize    = 30;

    [Header("Spawn area")]
    public float spawnRadius  = 20f;     // spawn within this radius of camera
    public float spawnInterval = 0.18f;  // seconds between spawns
    public float spawnHeight   = 1.5f;   // Y offset from ground

    Queue<WindRibbon> _pool    = new();
    List<WindRibbon>  _active  = new();
    float             _timer;
    Camera            _cam;

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;

        for (int i = 0; i < poolSize; i++)
        {
            var r = Instantiate(ribbonPrefab, transform);
            r.gameObject.SetActive(false);
            _pool.Enqueue(r);
        }
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            Spawn();
        }
    }

    void Spawn()
    {
        if (_pool.Count == 0) return;

        // Place spawn point upwind of camera so ribbon flows through frame
        Vector3 wind = WindController.Instance != null
            ? WindController.Instance.windDirection.normalized
            : Vector3.right;

        Vector3 camPos = _cam ? _cam.transform.position : Vector3.zero;

        // Random circle on XZ, bias toward upwind edge
        Vector2 rand    = Random.insideUnitCircle * spawnRadius;
        Vector3 origin  = camPos
                        - wind * spawnRadius          // start upwind
                        + new Vector3(rand.x, 0, rand.y)
                        + Vector3.up * (spawnHeight + Random.Range(-0.5f, 1.5f));

        var ribbon = _pool.Dequeue();
        ribbon.Launch(origin, ribbonMaterial);
        _active.Add(ribbon);
    }

    public void Recycle(WindRibbon r)
    {
        _active.Remove(r);
        r.gameObject.SetActive(false);
        _pool.Enqueue(r);
    }
}