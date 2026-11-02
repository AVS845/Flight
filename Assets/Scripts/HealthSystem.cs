using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.UI;

public class HealthSystem : NetworkBehaviour
{
    [Header("Health")]
    public float maxHealth;
    public bool respawn;

    [Header("Health Bar")]
    public float barSpeed;
    public Vector2 offset;

    [Header("Refrences")]
    public GameObject sliderPrefab;
    
    private NetworkVariable<float> health = new NetworkVariable<float>(
            default, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server
        );
    private NetworkObject network;
    private NetworkTransform netTransform;
    private Slider healthBar;
    private Slider ease;
    private float sliderHealth;
    private bool barIsAccurate;
    private GameObject sliderObj;

    void Awake()
    {
        network = GetComponent<NetworkObject>();
        netTransform = GetComponent<NetworkTransform>();
    }

    void HealthChanged(float prev, float current)
    {
        barIsAccurate = false;
    }

    public override void OnNetworkSpawn()
    {
        health.OnValueChanged += HealthChanged;

        GameObject canvas = GameObject.Find("HealthCanvas");
        
        sliderObj = Instantiate(sliderPrefab, transform.position, Quaternion.identity, canvas.transform);
        healthBar = sliderObj.transform.Find("Bar").GetComponent<Slider>();
        ease = sliderObj.transform.Find("Ease").GetComponent<Slider>();

        if (IsServer)
        {
            health.Value = maxHealth;
        }

        sliderHealth = maxHealth;
        healthBar.value = 1;
        ease.value = 1;

        print($"Health: {health.Value}\nBar Health: {sliderHealth}, IsAccurate: {barIsAccurate}");
    }

    public override void OnNetworkDespawn()
    {
        Destroy(sliderObj);

        health.OnValueChanged += HealthChanged;
    }

    void Update()
    {
        if (!barIsAccurate)
        {
            healthBar.value = health.Value / maxHealth;
            sliderHealth = Mathf.Lerp(sliderHealth, health.Value, barSpeed * Time.deltaTime);
            ease.value = sliderHealth / maxHealth;

            if (Mathf.Approximately(sliderHealth, health.Value)) barIsAccurate = true;
        }
    }

    void LateUpdate()
    {
        sliderObj.transform.position = transform.position - new Vector3(offset.x, offset.y);
    }

    void Die()
    {
        if (!IsServer) return;

        if (respawn) ServerRespawn();
        else network.Despawn(true);
    }

    void ServerRespawn()
    {
        health.Value = maxHealth;

        RespawnRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void RespawnRpc()
    {
        if (IsOwner) netTransform.Teleport(Vector3.zero, Quaternion.identity, transform.localScale);
        healthBar.value = 1;
        ease.value = 1;
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return;

        health.Value = Mathf.Max(0f, health.Value - damage);

        if (health.Value <= 0f)
        {
            Die();
        }

        barIsAccurate = false;

        print($"Damage taken: {damage}, Health: {health.Value}\nBar Health: {sliderHealth}, IsAccurate: {barIsAccurate}");
    }
}
