using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Zona jebakan listrik di dekat panel/generator. Jika generator dinyalakan
// saat musuh (satpam/boss/NPC) berada di dalam zona, musuh tersengat dan tumbang.
// Pasang di GameObject dengan collider trigger yang menutupi area panel.
public class ElectricTrapZone : MonoBehaviour
{
    [Header("Sengatan")]
    [Tooltip("Lama musuh kejang tersengat sebelum tumbang.")]
    public float shockDuration = 3f;
    [Tooltip("Jika true, musuh yang tersengat tumbang permanen (AI mati).")]
    public bool defeatPermanently = true;

    [Header("Efek (opsional)")]
    public AudioClip shockClip;
    [Range(0f, 1f)] public float shockVolume = 0.9f;
    public GameObject shockEffect;

    [Header("Debug")]
    public bool showGizmos = true;

    // Dibaca GeneratorExitPortal jika portal mensyaratkan satpam kalah dulu.
    public static bool AnyBossDefeated { get; private set; }

    private readonly HashSet<BossNPCController> bossesInZone = new HashSet<BossNPCController>();
    private readonly HashSet<NPCController> npcsInZone = new HashSet<NPCController>();

    void Start()
    {
        AnyBossDefeated = false;
        EnsureTriggerCollider();
    }

    void OnEnable()
    {
        GeneratorListrik.OnGeneratorActivated += HandleGeneratorActivated;
    }

    void OnDisable()
    {
        GeneratorListrik.OnGeneratorActivated -= HandleGeneratorActivated;
    }

    void OnTriggerEnter(Collider other)
    {
        BossNPCController boss = other.GetComponentInParent<BossNPCController>();
        if (boss != null) bossesInZone.Add(boss);

        NPCController npc = other.GetComponentInParent<NPCController>();
        if (npc != null) npcsInZone.Add(npc);
    }

    void OnTriggerExit(Collider other)
    {
        BossNPCController boss = other.GetComponentInParent<BossNPCController>();
        if (boss != null) bossesInZone.Remove(boss);

        NPCController npc = other.GetComponentInParent<NPCController>();
        if (npc != null) npcsInZone.Remove(npc);
    }

    void HandleGeneratorActivated(GeneratorListrik generator)
    {
        bool anyShocked = false;

        foreach (BossNPCController boss in bossesInZone)
        {
            if (boss == null) continue;
            StartCoroutine(ShockBoss(boss));
            anyShocked = true;
        }

        foreach (NPCController npc in npcsInZone)
        {
            if (npc == null) continue;
            StartCoroutine(ShockNpc(npc));
            anyShocked = true;
        }

        if (anyShocked)
            PlayShockEffects();
    }

    void PlayShockEffects()
    {
        if (shockClip != null)
            AudioSource.PlayClipAtPoint(shockClip, transform.position, Mathf.Clamp01(shockVolume));

        if (shockEffect != null)
        {
            GameObject effect = Instantiate(shockEffect, transform.position, Quaternion.identity);
            Destroy(effect, Mathf.Max(1f, shockDuration));
        }
    }

    IEnumerator ShockBoss(BossNPCController boss)
    {
        boss.ApplyStun(shockDuration);
        Debug.Log($"ElectricTrapZone: {boss.name} tersengat listrik!", this);

        yield return new WaitForSeconds(shockDuration);

        if (boss == null || !defeatPermanently)
            yield break;

        boss.Deactivate();
        boss.enabled = false;

        // Bekukan pose terakhir agar terlihat tumbang.
        Animator animator = boss.GetComponentInChildren<Animator>();
        if (animator != null)
            animator.enabled = false;

        AnyBossDefeated = true;
        Debug.Log($"ElectricTrapZone: {boss.name} tumbang. Satpam dikalahkan!", this);
    }

    IEnumerator ShockNpc(NPCController npc)
    {
        npc.ApplyStun(shockDuration);
        Debug.Log($"ElectricTrapZone: {npc.name} tersengat listrik!", this);

        yield return new WaitForSeconds(shockDuration);

        if (npc == null || !defeatPermanently)
            yield break;

        UnityEngine.AI.NavMeshAgent agent = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        npc.enabled = false;

        Animator animator = npc.GetComponentInChildren<Animator>();
        if (animator != null)
            animator.enabled = false;
    }

    void EnsureTriggerCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4f, 3f, 4f);
            box.center = Vector3.up * 1.5f;
        }
        else if (!col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.6f);
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
    }
}
