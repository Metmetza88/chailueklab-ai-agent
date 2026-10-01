using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// ระบบควบคุมการยิงเลเซอร์ AR ผ่าน Touch Input
/// รองรับการตรวจจับระนาบโลกจริง (AR Trackables) และวัตถุเสมือน (Virtual Enemies/Cyber Parasites)
/// </summary>
public class ARLaserShooter : MonoBehaviour
{
    [Header("AR Core Components")]
    [Tooltip("กล้องหลักของ AR Session (AR Camera)")]
    [SerializeField] private Camera arCamera;
    
    [Tooltip("AR Raycast Manager บน AR Session Origin")]
    [SerializeField] private ARRaycastManager raycastManager;

    [Header("Projectile Configuration")]
    [Tooltip("พรีแฟบกระสุนเลเซอร์ 3D")]
    [SerializeField] private GameObject laserProjectilePrefab;

    [Tooltip("ความเร็วในการพุ่งของเลเซอร์ (หน่วย: เมตร/วินาที)")]
    [SerializeField] private float projectileSpeed = 25.0f;

    [Tooltip("ระยะห่างจากกล้องในการเกิดกระสุนเลเซอร์")]
    [SerializeField] private float spawnOffsetDistance = 0.2f;

    [Header("Impact & Visual Effects")]
    [Tooltip("พรีแฟบเอฟเฟกต์ระเบิดอนุภาค (Particle Explosion VFX)")]
    [SerializeField] private GameObject explosionEffectPrefab;

    [Tooltip("เสียงเมื่อทำการยิงเลเซอร์")]
    [SerializeField] private AudioClip shootSound;

    [Tooltip("เสียงเมื่อกระสุนกระทบเป้าหมาย")]
    [SerializeField] private AudioClip impactSound;

    [Header("Gameplay Settings")]
    [Tooltip("หน่วงเวลาระหว่างการยิงแต่ละนัด (วินาที)")]
    [SerializeField] private float fireRate = 0.2f;

    private float nextFireTime = 0.0f;
    private AudioSource audioSource;
    private static readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();

    private void Awake()
    {
        // กำหนดกล้องหลักอัตโนมัติหากไม่ได้ตั้งค่าไว้ใน Inspector
        if (arCamera == null)
        {
            arCamera = Camera.main;
        }

        // จัดเตรียม AudioSource สำหรับเล่นเสียงประกอบ
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        // ตรวจจับการแตะหน้าจออุปกรณ์มือถือ (Mobile Touch Input)
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // ยิงเมื่อผู้เล่นเริ่มแตะนิ้วลงบนหน้าจอ (TouchPhase.Began)
            if (touch.phase == TouchPhase.Began && Time.time >= nextFireTime)
            {
                // ตรวจสอบว่าแตะทับ UI หรือไม่ (ถ้ามี EventSystem)
                if (!IsPointerOverUI(touch.position))
                {
                    FireLaser(touch.position);
                    nextFireTime = Time.time + fireRate;
                }
            }
        }
#if UNITY_EDITOR
        // รองรับการคลิกเมาส์สำหรับการทดสอบใน Unity Editor
        else if (Input.GetMouseButtonDown(0) && Time.time >= nextFireTime)
        {
            FireLaser(Input.mousePosition);
            nextFireTime = Time.time + fireRate;
        }
#endif
    }

    /// <summary>
    /// ทำการสร้างกระสุนเลเซอร์และส่งทิศทางพุ่งไปข้างหน้า
    /// </summary>
    /// <param name="screenPosition">พิกัดบนหน้าจอที่แตะ</param>
    private void FireLaser(Vector2 screenPosition)
    {
        if (laserProjectilePrefab == null || arCamera == null)
        {
            Debug.LogWarning("[ARLaserShooter] ขาดการตั้งค่า Prefab หรือ AR Camera");
            return;
        }

        // คำนวณรังสี (Ray) จากจุดที่แตะบนหน้าจอพุ่งเข้าไปในพื้นที่ 3D
        Ray ray = arCamera.ScreenPointToRay(screenPosition);
        Vector3 fireDirection = ray.direction;

        // คำนวณจุดปล่อยกระสุนด้านหน้ากล้องเล็กน้อยเพื่อป้องกันการชนกับกล้องเอง
        Vector3 spawnPosition = arCamera.transform.position + (fireDirection * spawnOffsetDistance);

        // หมุนกระสุนให้หันหน้าไปตามทิศทางการยิง
        Quaternion spawnRotation = Quaternion.LookRotation(fireDirection);

        // สร้าง Object กระสุนเลเซอร์
        GameObject projectileInstance = Instantiate(laserProjectilePrefab, spawnPosition, spawnRotation);

        // กำหนดค่าและสั่งให้กระสุนเคลื่อนที่
        LaserProjectile projectileScript = projectileInstance.GetComponent<LaserProjectile>();
        if (projectileScript != null)
        {
            projectileScript.Initialize(fireDirection, projectileSpeed, explosionEffectPrefab, impactSound);
        }
        else
        {
            // Fallback หากตัวกระสุนไม่มี Script LaserProjectile ติดอยู่
            Rigidbody rb = projectileInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = fireDirection * projectileSpeed;
            }
        }

        // เล่นเสียง Effect การยิง
        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // เพิ่มการสั่นของตัวเครื่องเมื่อกดยิง (Haptic Feedback สำหรับ Mobile)
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// ตรวจสอบการสัมผัสทับส่วนของ UI
    /// </summary>
    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;
        return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(
            Input.touchCount > 0 ? Input.GetTouch(0).fingerId : -1
        );
    }
}

/// <summary>
/// คลาสควบคุมพฤติกรรมของกระสุนเลเซอร์ การเคลื่อนที่ และการระเบิดเมื่อกระทบพื้นผิวจริงหรือศัตรู
/// </summary>
[RequireComponent(typeof(Collider))]
public class LaserProjectile : MonoBehaviour
{
    private Vector3 moveDirection;
    private float speed;
    private GameObject explosionPrefab;
    private AudioClip impactAudio;
    private float lifeTimer = 4.0f; // ทำลายตัวเองหลัง 4 วินาทีหากไม่ชนอะไร

    [Tooltip("ระบุ Tag ของศัตรูเสมือน")]
    [SerializeField] private string enemyTag = "CyberParasite";

    /// <summary>
    /// รับค่าการตั้งค่าจากตัวสั่งยิง
    /// </summary>
    public void Initialize(Vector3 direction, float projectileSpeed, GameObject explosionVfx, AudioClip hitSound)
    {
        moveDirection = direction.normalized;
        speed = projectileSpeed;
        explosionPrefab = explosionVfx;
        impactAudio = hitSound;

        // ทำลายตัวเองตามอายุขัยเพื่อป้องกันปัญหา Memory Leak
        Destroy(gameObject, lifeTimer);
    }

    private void Update()
    {
        // คำนวณระยะการเคลื่อนที่ในเฟรมนี้
        float stepDistance = speed * Time.deltaTime;
        Vector3 nextPosition = transform.position + (moveDirection * stepDistance);

        // ใช้ Raycast ช่วยตรวจสอบล่วงหน้าเพื่อป้องกันการทะลุผ่านผนังความเร็วสูง (Tunneling Effect)
        if (Physics.Raycast(transform.position, moveDirection, out RaycastHit hitInfo, stepDistance))
        {
            HandleHit(hitInfo.point, hitInfo.normal, hitInfo.collider.gameObject);
            return;
        }

        transform.position = nextPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(transform.position, -moveDirection, other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ContactPoint contact = collision.contacts.Length > 0 ? collision.contacts[0] : default;
        Vector3 hitPoint = contact.point != Vector3.zero ? contact.point : transform.position;
        Vector3 hitNormal = contact.normal != Vector3.zero ? contact.normal : -moveDirection;

        HandleHit(hitPoint, hitNormal, collision.gameObject);
    }

    /// <summary>
    /// จัดการเหตุการณ์เมื่อกระสุนกระทบเป้าหมาย (ทั้งโลกจริงและศัตรูเสมือน)
    /// </summary>
    private void HandleHit(Vector3 hitPosition, Vector3 hitNormal, GameObject hitObject)
    {
        // 1. สร้างเอฟเฟกต์อนุภาคระเบิด (Particle Explosion Effect)
        if (explosionPrefab != null)
        {
            Quaternion explosionRotation = Quaternion.LookRotation(hitNormal);
            GameObject explosion = Instantiate(explosionPrefab, hitPosition, explosionRotation);
            
            // ลบอนุภาคระเบิดทิ้งอัตโนมัติเมื่อเล่นจบ
            ParticleSystem ps = explosion.GetComponent<ParticleSystem>();
            float duration = ps != null ? ps.main.duration : 2.5f;
            Destroy(explosion, duration);
        }

        // 2. เล่นเสียงระเบิด ณ ตำแหน่ง 3D
        if (impactAudio != null)
        {
            AudioSource.PlayClipAtPoint(impactAudio, hitPosition, 1.0f);
        }

        // 3. ตรวจสอบการทำดาเมจใส่ Cyber Parasite / Virtual Enemy
        if (hitObject.CompareTag(enemyTag) || hitObject.GetComponent<CyberParasiteTarget>() != null)
        {
            CyberParasiteTarget enemy = hitObject.GetComponent<CyberParasiteTarget>();
            if (enemy != null)
            {
                enemy.TakeDamage(25f);
            }
        }

        // 4. ทำลายตัวกระสุนเลเซอร์ทันที
        Destroy(gameObject);
    }
}

/// <summary>
/// คอมโพเนนต์เป้าหมายศัตรูเสมือน (Cyber Parasite) รองรับการรับดาเมจและการสลายตัว
/// </summary>
public class CyberParasiteTarget : MonoBehaviour
{
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private GameObject deathVfxPrefab;
    private float currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        
        // กะพริบสีแดงเมื่อโดนยิง (Damage Flash)
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            StartCoroutine(FlashDamage(rend));
        }

        if (currentHealth <= 0f)
        {
            Eliminate();
        }
    }

    private IEnumerator FlashDamage(Renderer rend)
    {
        Color originalColor = rend.material.color;
        rend.material.color = Color.red;
        yield return new WaitForSeconds(0.08f);
        if (rend != null)
        {
            rend.material.color = originalColor;
        }
    }

    private void Eliminate()
    {
        if (deathVfxPrefab != null)
        {
            Instantiate(deathVfxPrefab, transform.position, transform.rotation);
        }
        Destroy(gameObject);
    }
}