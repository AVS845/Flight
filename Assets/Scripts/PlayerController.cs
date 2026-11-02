using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections.Generic;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float reverseMoveSpeed = 3f;
    public float acceleration = 0.8f;
    public float damping = 0.7f;
    public float rotDamping = 0.4f;

    [Header("Rotation")]
    public float turretSpeed;
    public float turnSpeed;
    public float turnSmoothing;
    public float lookAmmount = 0.1f;
    public float shootingLookAmmount = 0.3f;

    [Header("Shooting")]
    public float shootingRate;
    public float shotVelocity;
    public float damage;
    public float knockback;

    [Header("References")]
    public GameObject lookCenterObj;
    public GameObject turretObj;
    public GameObject bulletObj;

    private CinemachineImpulseSource source;
    private Rigidbody2D rb;
    private float moveInput = 0;
    private float turnInput = 0;
    private Vector3 mousePos = Vector2.zero;

    private Camera mainCam;
    private CinemachineCamera cam;
    private Transform lookTransform;
    private Vector2 rawMouse;
    private readonly NetworkVariable<NetworkObjectReference> turretReference = 
        new NetworkVariable<NetworkObjectReference>(
            default, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server
        );

    private Transform localTurretTransform;
    private bool shooting = false;
    private float shootTimer = 0f;
    private float look;
    private PolygonCollider2D col;
    private List<GameObject> bulletPool = new List<GameObject>();
    private int poolSize = 100;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<PolygonCollider2D>();

        GameObject cineCam = GameObject.Find("CinemachineCamera");
        cam = cineCam.GetComponent<CinemachineCamera>();
        source = cineCam.GetComponent<CinemachineImpulseSource>();

        mainCam = Camera.main;

        // Prewarm the bullet pool
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(bulletObj);
            obj.SetActive(false);
            bulletPool.Add(obj);
        }
    }


    public override void OnNetworkSpawn()
    {
        turretReference.OnValueChanged += OnTurretChanged;
        if (!IsOwner) return;

        GameObject look = Instantiate(lookCenterObj);
        lookTransform = look.transform;

        cam.Follow = lookTransform;
        SpawnObjectServerRpc();
    }

    private void OnTurretChanged(NetworkObjectReference prev, NetworkObjectReference next)
    {
        UpdateLocalTurretReference(next);
    }

    private void UpdateLocalTurretReference(NetworkObject reference)
    {
        NetworkObject net = reference;
        if (NetworkObject != null)
        {
            localTurretTransform = net.transform;
        }
        else
        {
            localTurretTransform = null;
        }
    }

    [ServerRpc]
    private void SpawnObjectServerRpc(ServerRpcParams rpcParams = default)
    {
        GameObject go = Instantiate(turretObj);

        NetworkObject network = go.GetComponent<NetworkObject>();
        ulong cliendId = rpcParams.Receive.SenderClientId;
        network.SpawnWithOwnership(cliendId);

        NetworkObject net = go.GetComponent<NetworkObject>();
        turretReference.Value = new NetworkObjectReference(net);
    }
    void OnMove(InputValue value)
    {
        if (!IsOwner) return;
        moveInput = value.Get<float>();
    }

    void OnTurn(InputValue value)
    {
        if (!IsOwner) return;
        turnInput = value.Get<float>();
    }

    void OnLook(InputValue value)
    {
        if (!IsOwner) return;
        rawMouse = value.Get<Vector2>();
    }

    void OnShoot(InputValue value)
    {
        if (!IsOwner) return;
        shooting = value.isPressed;
    }

    void FixedUpdate()
    {
        if (moveInput != 0)
        {
            if (moveInput > 0)
            {
                rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, transform.up * moveInput * moveSpeed, acceleration);
            }
            else
            {
                rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, transform.up * moveInput * reverseMoveSpeed, acceleration);
            }
        }
        else
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, damping);
        }

        if (turnInput != 0)
        {
            rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, -turnInput * turnSpeed, turnSmoothing);
        }
        else
        {
            rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, 0f, rotDamping);
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        shootTimer += Time.fixedDeltaTime;
        
        if (shooting)
        {
            if (shootTimer > shootingRate) 
            {
                Shoot();
                shootTimer = 0;
            }

            look = shootingLookAmmount;
        }
        else look = lookAmmount;
    }

    void LateUpdate()
    {
        mousePos = mainCam.ScreenToWorldPoint(rawMouse);
        mousePos.z = 0;

        Vector3 direction = mousePos - transform.position;

        if (localTurretTransform == null) return;
        
        if (direction != Vector3.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            Quaternion target = Quaternion.Euler(0, 0, angle);

            localTurretTransform.rotation = Quaternion.Slerp(localTurretTransform.rotation, target, turretSpeed);
        }

        if (lookTransform != null)
        {
            lookTransform.position = Vector3.Lerp(transform.position, mousePos, look);
        }

        localTurretTransform.position = transform.position;
    }

    void Shoot()
    {
        Vector2 inheritedVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
        Vector2 bulletVelocity = (new Vector2(localTurretTransform.up.x, localTurretTransform.up.y) * shotVelocity) + inheritedVelocity;

        SpawnPoolBullet(localTurretTransform.position, bulletVelocity, true);
        
        source.GenerateImpulse();

        SendShootSignalServerRpc(localTurretTransform.position, bulletVelocity);
    }

    [ServerRpc]
    private void SendShootSignalServerRpc(Vector3 origin, Vector2 velocity)
    {
        ReceiveShootSignalClientRpc(origin, velocity);
    }

    [ClientRpc]
    private void ReceiveShootSignalClientRpc(Vector3 origin, Vector2 velocity)
    {
        if (IsOwner) return;

        // Remote clients spawn a dummy local bullet for visual syncing
        SpawnPoolBullet(origin, velocity, false);
    }

    private void SpawnPoolBullet(Vector3 position, Vector2 velocity, bool isOwnerBullet)
    {
        GameObject bulletObj = GetPooledBullet();
        bulletObj.transform.position = position;
        bulletObj.transform.rotation = Quaternion.identity;
        bulletObj.SetActive(true);

        if (bulletObj.TryGetComponent(out LocalBullet2D localBullet))
        {
            localBullet.Initialize(velocity, isOwnerBullet, damage, knockback, col, this);
        }
    }

    private GameObject GetPooledBullet()
    {
        for (int i = 0; i < bulletPool.Count; i++)
        {
            if (!bulletPool[i].activeInHierarchy) return bulletPool[i];
        }
        // Expand pool dynamically if maxed out
        GameObject obj = Instantiate(bulletObj);
        bulletPool.Add(obj);
        return obj;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ReportHitRpc(ulong targetId, float dmg, Vector2 force)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject target))
        {
            if (target.TryGetComponent(out HealthSystem health))
            {
                health.TakeDamage(dmg);
            }

            // Apply physical impact/knockback directly to the target's Rigidbody
            if (target.TryGetComponent(out Rigidbody2D targetRb))
            {
                targetRb.AddForce(force, ForceMode2D.Impulse);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (localTurretTransform != null && localTurretTransform.TryGetComponent<NetworkObject>(out var networkObj))
            {
                if (networkObj.IsSpawned)
                {
                    networkObj.Despawn(true);
                }
            }
        }

        turretReference.OnValueChanged -= OnTurretChanged;
    }
}
