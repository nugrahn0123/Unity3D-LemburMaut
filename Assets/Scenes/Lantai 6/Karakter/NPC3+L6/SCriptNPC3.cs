using UnityEngine;
using UnityEngine.AI;

public class SCriptNPC3 : NPCController
{
	[Header("NPC3 Defaults")]
	public bool applyRecommendedDefaults = true;

	[Header("Target")]
	public bool npc3AutoFindPlayer = true;
	public string npc3PlayerTag = "Player";

	[Header("Movement")]
	public float npc3PatrolSpeed = 4f;
	public float npc3ChaseSpeed = 40f;
	public float npc3WaypointStopDistance = 1f;
	public float npc3AgentStoppingDistance = 1.25f;

	[Header("Detection")]
	public float npc3DetectionRange = 30f;
	public float npc3DetectionAngle = 70f;

	[Header("Attack")]
	public float npc3AttackRange = 4.5f;
	public float npc3AttackDistanceBuffer = 1f;
	public string npc3AttackParameter = "IsAttacking";

	[Header("Vision Light")]
	public bool npc3AutoCreateVisionLight = true;
	public float npc3PatrolLightIntensity = 3f;
	public float npc3ChaseLightIntensity = 5f;
	public float npc3AttackLightIntensity = 6f;

	void Awake()
	{
		if (!applyRecommendedDefaults) return;

		autoFindPlayer = npc3AutoFindPlayer;
		playerTag = npc3PlayerTag;

		patrolSpeed = npc3PatrolSpeed;
		chaseSpeed = npc3ChaseSpeed;
		waypointStopDistance = npc3WaypointStopDistance;

		detectionRange = npc3DetectionRange;
		detectionAngle = npc3DetectionAngle;

		attackRange = npc3AttackRange;
		attackDistanceBuffer = npc3AttackDistanceBuffer;
		attackParameter = npc3AttackParameter;

		autoCreateVisionLight = npc3AutoCreateVisionLight;
		patrolLightIntensity = npc3PatrolLightIntensity;
		chaseLightIntensity = npc3ChaseLightIntensity;
		attackLightIntensity = npc3AttackLightIntensity;

		NavMeshAgent navAgent = GetComponent<NavMeshAgent>();
		if (navAgent != null)
			navAgent.stoppingDistance = npc3AgentStoppingDistance;
	}
}
