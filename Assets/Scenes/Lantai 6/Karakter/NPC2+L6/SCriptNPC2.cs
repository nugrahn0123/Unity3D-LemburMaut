using UnityEngine;
using UnityEngine.AI;

public class SCriptNPC2 : NPCController
{
	[Header("NPC2 Defaults")]
	public bool applyRecommendedDefaults = true;

	[Header("Target")]
	public bool npc2AutoFindPlayer = true;
	public string npc2PlayerTag = "Player";

	[Header("Movement")]
	public float npc2PatrolSpeed = 4f;
	public float npc2ChaseSpeed = 40f;
	public float npc2WaypointStopDistance = 1f;
	public float npc2AgentStoppingDistance = 1.25f;

	[Header("Detection")]
	public float npc2DetectionRange = 30f;
	public float npc2DetectionAngle = 70f;

	[Header("Attack")]
	public float npc2AttackRange = 4.5f;
	public float npc2AttackDistanceBuffer = 1f;
	public string npc2AttackParameter = "IsAttacking";

	[Header("Vision Light")]
	public bool npc2AutoCreateVisionLight = true;
	public float npc2PatrolLightIntensity = 3f;
	public float npc2ChaseLightIntensity = 5f;
	public float npc2AttackLightIntensity = 6f;

	void Awake()
	{
		if (!applyRecommendedDefaults) return;

		autoFindPlayer = npc2AutoFindPlayer;
		playerTag = npc2PlayerTag;

		patrolSpeed = npc2PatrolSpeed;
		chaseSpeed = npc2ChaseSpeed;
		waypointStopDistance = npc2WaypointStopDistance;

		detectionRange = npc2DetectionRange;
		detectionAngle = npc2DetectionAngle;

		attackRange = npc2AttackRange;
		attackDistanceBuffer = npc2AttackDistanceBuffer;
		attackParameter = npc2AttackParameter;

		autoCreateVisionLight = npc2AutoCreateVisionLight;
		patrolLightIntensity = npc2PatrolLightIntensity;
		chaseLightIntensity = npc2ChaseLightIntensity;
		attackLightIntensity = npc2AttackLightIntensity;

		NavMeshAgent navAgent = GetComponent<NavMeshAgent>();
		if (navAgent != null)
			navAgent.stoppingDistance = npc2AgentStoppingDistance;
	}
}
