// THIS IS A FREE AI MADE BY OUTER, IF YOU HAD TO PAY FOR THIS, YOU HAVE BEEN SCAMMED.
// THIS WATERMARK IS NOT NEEDED, BUT IT WOULD BE HELPFUL IF YOU KEPT THE WATER MARK IN IF YOU ARE SHARING IT TO OTHERS
// IF YOU HAVE ANY OTHER ISSUES, JOIN THIS SERVER FOR HELP: https://discord.gg/heDBJGkJUA
// THIS HORROR AI REQUIRES PHOTON
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Photon.Pun;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
public class OuterAI : MonoBehaviourPunCallbacks, IPunObservable
{
// UPDATE LOG VERSION: change this text whenever you release an update. When someone imports the new script and this no longer matches what their project last saw, the Update Log screen opens once.
public const string UpdateLogVersion = "1.1";
[HideInInspector] public int ActiveInspectorTab = 0;
public float ConeVisionRange = 5f;
public float ChaseSphereRange = 8f;
[Range(0f, 360f)] public float ViewAngle = 90f;
[Tooltip("Off: it only sees in front of it. On: it can see all around it, but walls still block it.")]
public bool Use360Vision = false;
public string[] VisionBlockLayerNames = { "Default" };
public float ChaseMemoryDuration = 2.0f;
public bool UseBlindAudioAI = false;
[Range(0f, 1f)] public float MinVoiceVolumeRadius = 0.02f;
public float AudioDetectionRadius = 12f;
public bool DebugBlindLog = false;
public float MonsterSpeedWander = 5f;
public float MonsterSpeedChase = 7.5f;
public float RotationSpeed = 15f;
public bool UseRandomRoaming = false;
public float RandomRoamingRadius = 15f;
public Transform[] points;
public AudioSource IdleSound;
public AudioSource ChaseSound;
public float KillDistanceRadius = 1.2f;
public GameObject WalkObject;
public GameObject KillObject;
[FormerlySerializedAs("KillAudioSource")]
[Tooltip("Only the player who got caught hears this. It follows their camera. Turn off Play On Awake on it.")]
public AudioSource FirstPersonKillAudio;
[Tooltip("Everyone except the caught player hears this, from the jumpscare model. Turn off Play On Awake on it.")]
public AudioSource NetworkedKillAudio;
public List<AudioSource> AudioSourcesToSkipMute;
public Camera JumpscareCamera;
public Camera PlayerMainCamera;
public float JumpscareDuration = 2.5f;
public Transform RespawnPoint;
public bool UseDetectionAnim = false;
public GameObject DetectObject;
public AudioSource DetectSound;
public float DetectDuration = 1f;
public float DetectCooldown = 6f;
public bool FreezeDuringDetect = false;
[Tooltip("After a jumpscare, the monster can't kill anyone for this many seconds.")]
public float PostRespawnGraceSeconds = 3f;
[System.Serializable]
public class StepSoundEntry
{
public Material material;
public AudioClip[] sounds;
[Range(0f, 10f)] public float loudness = 1f;
}
public bool UseFootsteps = false;
public Transform LeftHand;
public Transform RightHand;
public AudioSource LeftStepSource;
public AudioSource RightStepSource;
public LayerMask StepGroundLayers = -1;
public StepSoundEntry[] StepSounds;
public AudioClip[] DefaultStepSounds;
public float StepCheckDistance = 0.15f;
public float StepCooldown = 0.25f;
[Range(0f, 10f)] public float StepLoudness = 1f;
public float StepRayHeight = 0f;
[Header("Anti Lag")]
[Tooltip("Master switch for every speed-up in this section.")]
public bool UseAntiLag = true;
[Tooltip("How many times per second it checks for players while a player is near. Lower = less lag.")]
[Range(0.5f, 60f)] public float NearChecksPerSecond = 10f;
[Tooltip("How many times per second it checks for players while nobody is near. This is the big one for lag.")]
[Range(0.2f, 60f)] public float FarChecksPerSecond = 2.5f;
[Tooltip("How many times per second it updates a chase once it has spotted someone. Keep this fairly high so chases feel sharp.")]
[Range(1f, 60f)] public float ChaseChecksPerSecond = 10f;
[Tooltip("Most players it looks at per check while nobody is spotted. 0 = all of them. It rotates through the players so nobody is skipped forever.")]
public int MaxPlayersPerCheck = 0;
[Tooltip("Most line-of-sight raycasts (or voice reads in Hearing Mode) per check. 0 = no limit. Raycasts are the most expensive part of a check.")]
public int MaxHeavyChecks = 0;
[Tooltip("Most brains allowed to think in one frame across ALL monsters in the scene. 0 = no limit. Helps a lot with many monsters.")]
public int GlobalThinkBudgetPerFrame = 0;
[HideInInspector] public int StatChecksPerSec;
[HideInInspector] public int StatRaycastsPerSec;
[Tooltip("Players farther than this count as far away. It is always kept bigger than your sight and hearing ranges.")]
public float FarDistance = 40f;
[Tooltip("How often it looks for players in the scene (seconds).")]
public float PlayerScanInterval = 1f;
[Tooltip("Footstep ground checks per second for each foot. 0 = every frame.")]
public int FootstepChecksPerSecond = 30;
[Tooltip("Smallest move before the position is sent over the network (meters).")]
public float SyncMoveThreshold = 0.02f;
[Tooltip("Stops animating the monster while nobody can see it. Turned off by itself when footsteps are on.")]
public bool CullAnimatorsWhenHidden = true;
[Tooltip("Other players snap to the real position when the monster is farther than this from where they see it (meters).")]
public float SnapDistance = 6f;
[Header("Extra Features")]
[Tooltip("After it loses you it walks around the last place it saw you before going back to patrol.")]
public bool UseSearchBehavior = false;
public float SearchDuration = 8f;
public float SearchRadius = 6f;
public int SearchPoints = 3;
[Tooltip("The longer it chases, the faster it gets.")]
public bool UseChaseRamp = false;
[Tooltip("0.25 = up to 25% faster.")]
public float ChaseRampBonus = 0.25f;
public float ChaseRampSeconds = 6f;
[Tooltip("Gives up after a long chase and rests, so players can breathe.")]
public bool UseChaseFatigue = false;
public float MaxChaseSeconds = 25f;
public float RestSeconds = 6f;
[Tooltip("Gets faster after every kill.")]
public bool UseKillRage = false;
[Range(0f, 0.5f)] public float RagePerKill = 0.05f;
[Range(0f, 2f)] public float MaxRage = 0.5f;
[Tooltip("A looping heartbeat that gets louder and faster as the monster gets close. Turn off Play On Awake on it.")]
public AudioSource ProximityHeartbeat;
public float HeartbeatRange = 15f;
[Range(0f, 1f)] public float HeartbeatMaxVolume = 1f;
public bool ShowDebugInfo = true;
// Gizmo and inspector-theme settings now live in OuterAIManager (Tools > Outer AI > Manager).
[HideInInspector] public NavMeshAgent agent;
[HideInInspector] public string tagString = "Player";
private const float ThinkInterval = 0.1f;
private float nextThink;
private const string LogoAssetGuid = "6f1f1be22cb22ecc016c92efbef7cdc4";
private const string ProfileAssetGuid = "4f3c5c2de228d7b321dc615c2957789e";
private const string PrefabAssetGuid = "32369f26a1f477139f6049d0357bd01c";
private static bool linksChecked;
private LayerMask combinedObstacleMask;
private Vector3 lastKnownPlayerPosition;
private float loseVisionTimer;
private float killGraceUntil;
private bool isPlayerSpotted;
private bool isCurrentlyChasingSound;
private bool isExecutingKillState;
private int currentWaypointIndex;
private Vector3 networkPosition;
private Quaternion networkRotation;
private bool receivedFirstUpdate;
private GameObject[] cachedPlayers = new GameObject[0];
private PlayerDeathHandler[] cachedHandlers = new PlayerDeathHandler[0];
private PlayerDeathHandler localDeathHandler;
private float nextPlayerScan;
private class VoiceEntry
{
public PhotonVoiceView view;
public PhotonView pv;
public PlayerDeathHandler handler;
public AudioSource src;
}
private VoiceEntry[] voices = new VoiceEntry[0];
private float nextVoiceScan;
private float nextBlindLog;
private readonly float[] audioBuffer = new float[256];
private readonly List<AudioSource> mutedByJumpscare = new List<AudioSource>();
private bool firstPersonMoved;
private bool netAudioMoved;
private Transform fpOriginalParent;
private Vector3 fpOriginalLocalPos;
private Vector3 netOriginalLocalPos;
private bool leftDown;
private bool rightDown;
private float lastLeftStep;
private float lastRightStep;
private AudioClip lastStepClip;
private bool detecting;
private Coroutine detectRoutine;
private float nextDetectAt;
private float detectHoldUntil;
private float lastThinkTime;
private int checkStart;
private int heavyLeft = int.MaxValue;
private int statThinks;
private int statRays;
private float statNext;
private static int budgetFrame = -1;
private static int budgetUsed;
private float thinkDelta = 0.1f;
private float nextIdleRpc;
private float nextStepCheck;
private float nextRepath;
private float nextHeart;
private float chaseStart = -1f;
private float restUntil;
private float searchUntil;
private int searchLeft;
private float rage;
private Transform heartListener;
private Vector3 lastSentPos;
private float lastSentYaw = 9999f;
private float networkYaw;
private static readonly Dictionary<Collider, Renderer> rendererCache = new Dictionary<Collider, Renderer>();
private void Start()
{
if (Tripped()) { Shout(); return; }
agent = GetComponent<NavMeshAgent>();
agent.speed = WanderSpeedNow();
agent.updateRotation = false;
networkPosition = transform.position;
networkRotation = transform.rotation;
networkYaw = transform.eulerAngles.y;
if (UseAntiLag && CullAnimatorsWhenHidden && !UseFootsteps) ApplyAnimatorCulling();
nextThink = Time.time + Random.value * 0.3f;
if (DetectObject != null) DetectObject.SetActive(false);
if (DetectSound != null) DetectSound.Stop();
if (FirstPersonKillAudio != null) FirstPersonKillAudio.Stop();
if (NetworkedKillAudio != null)
{
NetworkedKillAudio.Stop();
netOriginalLocalPos = NetworkedKillAudio.transform.localPosition;
}
int mask = 0;
foreach (string layerName in VisionBlockLayerNames)
{
int layer = LayerMask.NameToLayer(layerName);
if (layer != -1) mask |= 1 << layer;
else Debug.LogWarning("Layer name '" + layerName + "' was not found in your Unity project settings!");
}
combinedObstacleMask = mask;
if (PhotonNetwork.IsMasterClient)
{
if (WalkObject != null) WalkObject.SetActive(true);
if (KillObject != null) KillObject.SetActive(false);
ChooseNextPatrolDestination();
}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
CheckAssetLinks();
#endif
}
#if UNITY_EDITOR
private void OnValidate()
{
CheckAssetLinks();
}
#endif
[Header("Walk / Run Animator (Auto Setup fills this in)")]
[Tooltip("Feeds the Walk object's Animator: a float speed parameter and a bool/float run parameter. Works on every client.")]
public bool UseAnimParams = false;
public string AnimSpeedParam = "Speed";
public string AnimRunParam = "Run";
private Animator animWalk;
private int animSpeedHash, animRunHash;
private bool animHasSpeed, animHasRun, animRunIsBool, animCached;
private Vector3 animLastPos;
private float animSmooth;
private void LateUpdate()
{
if (!UseAnimParams || WalkObject == null || !WalkObject.activeInHierarchy) { animLastPos = transform.position; return; }
if (!animCached || animWalk == null)
{
animWalk = WalkObject.GetComponentInChildren<Animator>(true);
animHasSpeed = animHasRun = false;
if (animWalk != null && animWalk.runtimeAnimatorController != null)
{
animCached = true;
foreach (AnimatorControllerParameter p in animWalk.parameters)
{
if (!string.IsNullOrEmpty(AnimSpeedParam) && p.name == AnimSpeedParam && p.type == AnimatorControllerParameterType.Float)
{ animSpeedHash = p.nameHash; animHasSpeed = true; }
if (!string.IsNullOrEmpty(AnimRunParam) && p.name == AnimRunParam &&
(p.type == AnimatorControllerParameterType.Bool || p.type == AnimatorControllerParameterType.Float))
{ animRunHash = p.nameHash; animHasRun = true; animRunIsBool = p.type == AnimatorControllerParameterType.Bool; }
}
}
}
if (animWalk == null || !animWalk.gameObject.activeInHierarchy) { animLastPos = transform.position; return; }
float dt = Time.deltaTime;
if (dt <= 0f) return;
float inst = (transform.position - animLastPos).magnitude / dt;
animLastPos = transform.position;
animSmooth = Mathf.Lerp(animSmooth, inst, 1f - Mathf.Exp(-12f * dt));
bool running = animSmooth > (MonsterSpeedWander + MonsterSpeedChase) * 0.5f;
if (animHasSpeed) animWalk.SetFloat(animSpeedHash, animSmooth);
if (animHasRun)
{
if (animRunIsBool) animWalk.SetBool(animRunHash, running);
else animWalk.SetFloat(animRunHash, running ? 1f : 0f);
}
}
private void Update()
{
if (Tripped())
{
Shout();
if (agent != null && agent.enabled) agent.enabled = false;
return;
}
if (photonView == null) return;
UpdateHeartbeat();
if (UseFootsteps && !isExecutingKillState) UpdateFootsteps();
if (isExecutingKillState)
{
if (agent != null && agent.enabled) agent.enabled = false;
return;
}
if (!PhotonNetwork.IsMasterClient)
{
if (agent != null && agent.enabled) agent.enabled = false;
if (receivedFirstUpdate && !HoldForDetect())
{
if (UseAntiLag && (transform.position - networkPosition).sqrMagnitude > SnapDistance * SnapDistance)
transform.position = networkPosition;
else
transform.position = Vector3.Lerp(transform.position, networkPosition, Mathf.Clamp01(Time.deltaTime * MonsterSpeedChase));
transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Mathf.Clamp01(Time.deltaTime * RotationSpeed));
}
return;
}
if (agent == null) return;
if (!agent.enabled) agent.enabled = true;
if (!agent.isOnNavMesh) return;
if (HoldForDetect())
{
agent.isStopped = true;
agent.velocity = Vector3.zero;
}
if (Time.time >= statNext)
{
StatChecksPerSec = statThinks;
StatRaycastsPerSec = statRays;
statThinks = 0;
statRays = 0;
statNext = Time.time + 1f;
}
if (Time.time >= nextThink && TakeThinkBudget())
{
thinkDelta = Mathf.Clamp(Time.time - lastThinkTime, 0.02f, 0.5f);
lastThinkTime = Time.time;
nextThink = Time.time + GetThinkInterval();
Think();
}
Steer();
}
private void Think()
{
statThinks++;
heavyLeft = (UseAntiLag && MaxHeavyChecks > 0) ? MaxHeavyChecks : int.MaxValue;
RefreshPlayerCache();
if (Time.time >= killGraceUntil && TryKill()) return;
bool resting = UseChaseFatigue && Time.time < restUntil;
Vector3 targetPos = Vector3.zero;
bool hasTarget = false;
if (!resting) hasTarget = UseBlindAudioAI ? TryHearPlayer(out targetPos) : TrySeePlayer(out targetPos);
bool busy = DetectBusy();
bool hold = HoldForDetect();
if (hasTarget)
{
bool firstSpot = !isPlayerSpotted;
isPlayerSpotted = true;
loseVisionTimer = 0f;
lastKnownPlayerPosition = targetPos;
searchUntil = 0f;
if (chaseStart < 0f) chaseStart = Time.time;
if (UseChaseFatigue && Time.time - chaseStart > MaxChaseSeconds)
{
restUntil = Time.time + RestSeconds;
chaseStart = -1f;
isPlayerSpotted = false;
agent.isStopped = false;
agent.speed = WanderSpeedNow();
nextIdleRpc = Time.time + 1f;
photonView.RPC("PlayIdleSound", RpcTarget.All);
ChooseNextPatrolDestination();
return;
}
if (firstSpot && UseDetectionAnim && Time.time >= nextDetectAt)
{
nextDetectAt = Time.time + DetectDuration + DetectCooldown;
detectHoldUntil = Time.time + DetectDuration;
if (FreezeDuringDetect)
{
agent.isStopped = true;
agent.velocity = Vector3.zero;
}
photonView.RPC("PlayDetection", RpcTarget.All);
}
busy = DetectBusy();
hold = HoldForDetect();
if (!isCurrentlyChasingSound && !busy) photonView.RPC("PlayChaseSound", RpcTarget.All);
agent.isStopped = hold;
if (hold) agent.velocity = Vector3.zero;
agent.speed = ChaseSpeedNow();
SetChaseTarget(lastKnownPlayerPosition);
}
else if (isPlayerSpotted && (busy || loseVisionTimer < ChaseMemoryDuration))
{
if (!busy) loseVisionTimer += thinkDelta;
if (!isCurrentlyChasingSound && !busy) photonView.RPC("PlayChaseSound", RpcTarget.All);
agent.isStopped = hold;
if (hold) agent.velocity = Vector3.zero;
agent.speed = ChaseSpeedNow();
SetChaseTarget(lastKnownPlayerPosition);
if (!busy && !agent.pathPending && agent.remainingDistance < 0.75f) loseVisionTimer = ChaseMemoryDuration;
}
else
{
chaseStart = -1f;
agent.isStopped = false;
agent.speed = WanderSpeedNow();
if (!busy)
{
if (isPlayerSpotted || isCurrentlyChasingSound)
{
isPlayerSpotted = false;
if (UseSearchBehavior)
{
searchUntil = Time.time + SearchDuration;
searchLeft = Mathf.Max(1, SearchPoints);
SearchNearLastKnown();
}
nextIdleRpc = Time.time + 1f;
photonView.RPC("PlayIdleSound", RpcTarget.All);
}
else if (IdleSound != null && !IdleSound.isPlaying && Time.time >= nextIdleRpc)
{
nextIdleRpc = Time.time + (UseAntiLag ? 1.5f : 0.1f);
photonView.RPC("PlayIdleSound", RpcTarget.All);
}
}
if (!agent.pathPending && agent.remainingDistance < 0.5f)
{
if (UseSearchBehavior && Time.time < searchUntil && searchLeft > 0)
{
searchLeft--;
SearchNearLastKnown();
}
else ChooseNextPatrolDestination();
}
}
}
private bool TakeThinkBudget()
{
if (!UseAntiLag || GlobalThinkBudgetPerFrame <= 0) return true;
if (budgetFrame != Time.frameCount)
{
budgetFrame = Time.frameCount;
budgetUsed = 0;
}
if (budgetUsed >= GlobalThinkBudgetPerFrame) return false;
budgetUsed++;
return true;
}
private float GetThinkInterval()
{
if (!UseAntiLag) return ThinkInterval;
if (isPlayerSpotted || isCurrentlyChasingSound || DetectBusy()) return 1f / Mathf.Max(1f, ChaseChecksPerSecond);
float nearInterval = 1f / Mathf.Max(0.5f, NearChecksPerSecond);
float far = Mathf.Max(FarDistance, ConeVisionRange + 5f, ChaseSphereRange + 5f, UseBlindAudioAI ? AudioDetectionRadius + 5f : 0f);
float farSqr = far * far;
for (int i = 0; i < cachedPlayers.Length; i++)
{
GameObject p = cachedPlayers[i];
if (p == null) continue;
if ((transform.position - p.transform.position).sqrMagnitude < farSqr) return nearInterval;
}
return Mathf.Max(nearInterval, 1f / Mathf.Max(0.2f, FarChecksPerSecond));
}
private float ChaseSpeedNow()
{
float sp = MonsterSpeedChase;
if (UseChaseRamp && chaseStart >= 0f)
sp *= 1f + ChaseRampBonus * Mathf.Clamp01((Time.time - chaseStart) / Mathf.Max(0.1f, ChaseRampSeconds));
if (UseKillRage) sp *= 1f + rage;
return sp;
}
private float WanderSpeedNow()
{
return UseKillRage ? MonsterSpeedWander * (1f + rage) : MonsterSpeedWander;
}
private void SearchNearLastKnown()
{
if (agent == null || !agent.isActiveAndEnabled) return;
NavMeshHit hit;
Vector3 c = lastKnownPlayerPosition + Random.insideUnitSphere * SearchRadius;
if (NavMesh.SamplePosition(c, out hit, SearchRadius, -1))
{
agent.destination = hit.position;
agent.speed = WanderSpeedNow();
}
}
private void ApplyAnimatorCulling()
{
GameObject[] roots = { WalkObject, KillObject, DetectObject };
for (int r = 0; r < roots.Length; r++)
{
if (roots[r] == null) continue;
foreach (Animator a in roots[r].GetComponentsInChildren<Animator>(true))
a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
foreach (SkinnedMeshRenderer smr in roots[r].GetComponentsInChildren<SkinnedMeshRenderer>(true))
smr.updateWhenOffscreen = false;
}
}
private void UpdateHeartbeat()
{
if (ProximityHeartbeat == null) return;
if (Time.time < nextHeart) return;
nextHeart = Time.time + 0.1f;
if (isExecutingKillState)
{
if (ProximityHeartbeat.isPlaying) ProximityHeartbeat.Stop();
return;
}
if (heartListener == null)
{
Camera c = PlayerMainCamera != null ? PlayerMainCamera : Camera.main;
if (c == null) return;
heartListener = c.transform;
}
float d = Vector3.Distance(transform.position, heartListener.position);
if (d > HeartbeatRange)
{
if (ProximityHeartbeat.isPlaying) ProximityHeartbeat.Stop();
return;
}
float t = 1f - d / Mathf.Max(0.1f, HeartbeatRange);
ProximityHeartbeat.loop = true;
ProximityHeartbeat.spatialBlend = 0f;
ProximityHeartbeat.volume = t * t * HeartbeatMaxVolume;
ProximityHeartbeat.pitch = Mathf.Lerp(0.9f, 1.5f, t);
if (!ProximityHeartbeat.isPlaying) ProximityHeartbeat.Play();
}
private bool DetectBusy()
{
return UseDetectionAnim && (detecting || Time.time < detectHoldUntil);
}
private bool HoldForDetect()
{
return FreezeDuringDetect && UseDetectionAnim && (detecting || Time.time < detectHoldUntil);
}
private void SetChaseTarget(Vector3 p)
{
float thresh = UseAntiLag ? 0.25f : 0.09f;
if ((agent.hasPath || agent.pathPending) && (p - agent.destination).sqrMagnitude < thresh) return;
if (UseAntiLag && agent.hasPath && Time.time < nextRepath) return;
nextRepath = Time.time + 0.15f;
agent.destination = p;
}
private void Steer()
{
if (HoldForDetect()) return;
Vector3 dir;
if (agent.hasPath) dir = agent.desiredVelocity;
else return;
dir.y = 0f;
if (dir.sqrMagnitude > 0.01f)
transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), RotationSpeed * Time.deltaTime);
}
private const string IconAssetGuid = "11e1e3d5196bd2977f76cc2a3ff7149b";
private const string SkinAssetGuid = "722b110ce247cc5974e01ec0b0deae34";
private const string MaterialAssetGuid = "a9eb6eb79c93c23e4bda9447fb991d6e";
private bool TryKill()
{
float killSqr = KillDistanceRadius * KillDistanceRadius;
for (int i = 0; i < cachedPlayers.Length; i++)
{
GameObject p = cachedPlayers[i];
if (p == null || p.transform.IsChildOf(transform)) continue;
if ((transform.position - p.transform.position).sqrMagnitude > killSqr) continue;
PlayerDeathHandler h = cachedHandlers[i];
if (h == null || h.IsDead) continue;
photonView.RPC("NetworkedTriggerKillSequence", RpcTarget.All, h.photonView.ViewID);
return true;
}
return false;
}
private void RefreshPlayerCache()
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
if (!linksChecked) CheckAssetLinks();
#endif
if (Time.time < nextPlayerScan) return;
nextPlayerScan = Time.time + (UseAntiLag ? Mathf.Max(0.25f, PlayerScanInterval) : 0.5f);
cachedPlayers = GameObject.FindGameObjectsWithTag(tagString);
if (!UseAntiLag || localDeathHandler == null)
{
localDeathHandler = null;
foreach (PlayerDeathHandler h in FindObjectsByType<PlayerDeathHandler>(FindObjectsSortMode.None))
{
if (h.photonView != null && h.photonView.IsMine) { localDeathHandler = h; break; }
}
}
if (cachedHandlers.Length != cachedPlayers.Length) cachedHandlers = new PlayerDeathHandler[cachedPlayers.Length];
for (int i = 0; i < cachedPlayers.Length; i++)
{
if (cachedPlayers[i] == null) { cachedHandlers[i] = null; continue; }
PlayerDeathHandler h = cachedPlayers[i].GetComponentInParent<PlayerDeathHandler>();
cachedHandlers[i] = h != null ? h : localDeathHandler;
}
}
private bool TrySeePlayer(out Vector3 position)
{
position = Vector3.zero;
float minSqr = float.MaxValue;
float coneSqr = ConeVisionRange * ConeVisionRange;
float chaseSqr = ChaseSphereRange * ChaseSphereRange;
bool found = false;
int total = cachedPlayers.Length;
int budget = (UseAntiLag && !isPlayerSpotted && MaxPlayersPerCheck > 0) ? Mathf.Min(MaxPlayersPerCheck, total) : total;
for (int n = 0; n < budget; n++)
{
int i = (checkStart + n) % total;
GameObject p = cachedPlayers[i];
if (p == null || p.transform.IsChildOf(transform)) continue;
PlayerDeathHandler h = cachedHandlers[i];
if (h != null && h.IsDead) continue;
float sqr = (transform.position - p.transform.position).sqrMagnitude;
if (sqr >= minSqr) continue;
bool seen = isPlayerSpotted
? sqr < chaseSqr && HasLineOfSight(p.transform, ChaseSphereRange)
: sqr < coneSqr && IsPlayerInVisionCone(p.transform);
if (seen)
{
minSqr = sqr;
position = p.transform.position;
found = true;
}
}
checkStart = total > 0 ? (checkStart + budget) % total : 0;
return found;
}
private bool TryHearPlayer(out Vector3 position)
{
position = Vector3.zero;
if (Time.time >= nextVoiceScan)
{
nextVoiceScan = Time.time + (UseAntiLag ? 3f : 2f);
PhotonVoiceView[] found = FindObjectsByType<PhotonVoiceView>(FindObjectsSortMode.None);
VoiceEntry[] fresh = new VoiceEntry[found.Length];
for (int i = 0; i < found.Length; i++)
{
VoiceEntry keep = null;
for (int k = 0; k < voices.Length; k++)
if (voices[k] != null && voices[k].view == found[i]) { keep = voices[k]; break; }
fresh[i] = keep != null ? keep : new VoiceEntry
{
view = found[i],
pv = found[i].GetComponent<PhotonView>(),
handler = found[i].GetComponentInParent<PlayerDeathHandler>()
};
}
voices = fresh;
}
bool logNow = DebugBlindLog && Time.time >= nextBlindLog;
if (logNow)
{
nextBlindLog = Time.time + 2f;
if (voices.Length == 0) Debug.Log("[BlindAI] No PhotonVoiceView found in the scene. Is Photon Voice on your player prefab?");
}
float radiusSqr = AudioDetectionRadius * AudioDetectionRadius;
float bestSqr = float.MaxValue;
bool found2 = false;
foreach (VoiceEntry e in voices)
{
if (e.view == null) continue;
if (e.handler != null && e.handler.IsDead) continue;
Vector3 hearPos = GetVoicePosition(e);
float sqr = (transform.position - hearPos).sqrMagnitude;
bool inRange = sqr <= radiusSqr;
if (!inRange && !logNow) continue;
if (!logNow && heavyLeft <= 0) continue;
heavyLeft--;
statRays++;
float loudness = GetVoiceLoudness(e);
if (logNow && (loudness > 0.005f || inRange))
{
string state = !inRange ? "OUT OF RANGE" : loudness < MinVoiceVolumeRadius ? "too quiet" : "HEARD";
Debug.Log("[BlindAI] " + e.view.name + " dist=" + Mathf.Sqrt(sqr).ToString("F1") + "/" + AudioDetectionRadius
+ " loudness=" + loudness.ToString("F3") + "/" + MinVoiceVolumeRadius + " -> " + state
+ " | usedPos=" + hearPos.ToString("F1") + " voiceObjPos=" + e.view.transform.position.ToString("F1")
+ " monsterPos=" + transform.position.ToString("F1"));
}
if (inRange && loudness >= MinVoiceVolumeRadius && sqr < bestSqr)
{
bestSqr = sqr;
position = hearPos;
found2 = true;
}
}
return found2;
}
private Vector3 GetVoicePosition(VoiceEntry e)
{
Vector3 localHead;
if (e.pv != null && e.pv.IsMine && LocalPlayerTeleport.TryGetLocalHead(out localHead))
return localHead;
return e.view.transform.position;
}
private float GetVoiceLoudness(VoiceEntry e)
{
if (e.pv != null && e.pv.IsMine)
{
Recorder rec = e.view.RecorderInUse;
if (rec == null || !rec.TransmitEnabled) return 0f;
var meter = rec.LevelMeter;
return meter != null ? meter.CurrentPeakAmp : 0f;
}
if (e.src == null)
{
Speaker spk = e.view.SpeakerInUse;
if (spk == null) return 0f;
e.src = spk.GetComponent<AudioSource>();
if (e.src == null) e.src = spk.GetComponentInChildren<AudioSource>();
if (e.src == null) return 0f;
}
if (!e.src.isPlaying) return 0f;
e.src.GetOutputData(audioBuffer, 0);
float peak = 0f;
for (int i = 0; i < audioBuffer.Length; i++)
{
float a = Mathf.Abs(audioBuffer[i]);
if (a > peak) peak = a;
}
return peak;
}
private void ChooseNextPatrolDestination()
{
if (agent == null || !agent.isActiveAndEnabled) return;
if (UseRandomRoaming)
{
NavMeshHit hit;
if (NavMesh.SamplePosition(transform.position + Random.insideUnitSphere * RandomRoamingRadius, out hit, RandomRoamingRadius, -1))
{
agent.destination = hit.position;
agent.speed = WanderSpeedNow();
}
}
else if (points != null && points.Length > 0)
{
agent.destination = points[currentWaypointIndex].position;
agent.speed = WanderSpeedNow();
currentWaypointIndex = (currentWaypointIndex + 1) % points.Length;
}
}
[PunRPC]
private void NetworkedTriggerKillSequence(int caughtPlayerViewID)
{
if (Tripped()) return;
if (isExecutingKillState) return;
isExecutingKillState = true;
if (UseKillRage) rage = Mathf.Min(MaxRage, rage + RagePerKill);
CancelDetection();
if (agent != null)
{
if (agent.isActiveAndEnabled) agent.isStopped = true;
agent.enabled = false;
}
if (ChaseSound != null) ChaseSound.Stop();
if (IdleSound != null) IdleSound.Stop();
PhotonView caughtView = PhotonView.Find(caughtPlayerViewID);
PlayerDeathHandler handler = caughtView != null ? caughtView.GetComponent<PlayerDeathHandler>() : null;
bool caughtIsLocal = caughtView != null && caughtView.IsMine;
if (handler != null) handler.BeginDeath(RespawnPoint);
if (caughtIsLocal)
{
if (FirstPersonKillAudio != null)
{
FirstPersonKillAudio.mute = false;
if (!firstPersonMoved)
{
fpOriginalParent = FirstPersonKillAudio.transform.parent;
fpOriginalLocalPos = FirstPersonKillAudio.transform.localPosition;
}
FirstPersonKillAudio.spatialBlend = 0f;
FirstPersonKillAudio.transform.SetParent(PlayerMainCamera != null ? PlayerMainCamera.transform : (caughtView != null ? caughtView.transform : transform));
FirstPersonKillAudio.transform.localPosition = Vector3.zero;
firstPersonMoved = true;
FirstPersonKillAudio.Play();
}
}
else if (NetworkedKillAudio != null)
{
NetworkedKillAudio.mute = false;
NetworkedKillAudio.spatialBlend = 1f;
if (KillObject != null) NetworkedKillAudio.transform.position = KillObject.transform.position;
netAudioMoved = true;
NetworkedKillAudio.Play();
}
if (caughtIsLocal)
{
MuteWorldSounds();
if (PlayerMainCamera != null) PlayerMainCamera.enabled = false;
if (JumpscareCamera != null) JumpscareCamera.enabled = true;
}
if (WalkObject != null) WalkObject.SetActive(false);
if (KillObject != null) KillObject.SetActive(true);
StartCoroutine(JumpscareTimerRoutine(caughtPlayerViewID, caughtIsLocal));
}
private IEnumerator JumpscareTimerRoutine(int caughtPlayerViewID, bool caughtIsLocal)
{
yield return new WaitForSeconds(JumpscareDuration);
if (FirstPersonKillAudio != null)
{
FirstPersonKillAudio.Stop();
if (firstPersonMoved)
{
FirstPersonKillAudio.transform.SetParent(fpOriginalParent != null ? fpOriginalParent : transform);
FirstPersonKillAudio.transform.localPosition = fpOriginalLocalPos;
firstPersonMoved = false;
}
}
if (NetworkedKillAudio != null)
{
NetworkedKillAudio.Stop();
if (netAudioMoved)
{
NetworkedKillAudio.transform.localPosition = netOriginalLocalPos;
netAudioMoved = false;
}
}
if (caughtIsLocal)
{
if (JumpscareCamera != null) JumpscareCamera.enabled = false;
if (PlayerMainCamera != null) PlayerMainCamera.enabled = true;
foreach (AudioSource src in mutedByJumpscare) if (src != null) src.mute = false;
mutedByJumpscare.Clear();
}
if (KillObject != null) KillObject.SetActive(false);
if (WalkObject != null) WalkObject.SetActive(true);
PhotonView caughtView = PhotonView.Find(caughtPlayerViewID);
PlayerDeathHandler handler = caughtView != null ? caughtView.GetComponent<PlayerDeathHandler>() : null;
if (handler != null) handler.EndDeath();
isExecutingKillState = false;
isPlayerSpotted = false;
loseVisionTimer = 0f;
killGraceUntil = Time.time + PostRespawnGraceSeconds;
if (PhotonNetwork.IsMasterClient)
{
if (agent != null)
{
agent.enabled = true;
agent.isStopped = false;
agent.ResetPath();
agent.speed = WanderSpeedNow();
}
photonView.RPC("PlayIdleSound", RpcTarget.All);
ChooseNextPatrolDestination();
}
}
private void MuteWorldSounds()
{
mutedByJumpscare.Clear();
foreach (AudioSource src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
{
if (src == null || src == FirstPersonKillAudio || src == NetworkedKillAudio || src.mute) continue;
if (AudioSourcesToSkipMute != null && AudioSourcesToSkipMute.Contains(src)) continue;
src.mute = true;
mutedByJumpscare.Add(src);
}
}
[PunRPC]
private void PlayIdleSound()
{
if (Tripped()) return;
if (UseDetectionAnim && detecting) return;
if (isExecutingKillState) return;
isCurrentlyChasingSound = false;
if (ChaseSound != null) ChaseSound.Stop();
if (IdleSound != null && !IdleSound.isPlaying) IdleSound.Play();
}
[PunRPC]
private void PlayDetection()
{
if (Tripped()) return;
if (isExecutingKillState || !UseDetectionAnim) return;
if (detectRoutine != null) StopCoroutine(detectRoutine);
detecting = true;
isCurrentlyChasingSound = false;
if (IdleSound != null) IdleSound.Stop();
if (ChaseSound != null) ChaseSound.Stop();
if (DetectObject != null)
{
foreach (Animator a in DetectObject.GetComponentsInChildren<Animator>(true)) a.applyRootMotion = false;
if (WalkObject != null)
DetectObject.transform.SetPositionAndRotation(WalkObject.transform.position, WalkObject.transform.rotation);
DetectObject.SetActive(true);
if (WalkObject != null) WalkObject.SetActive(false);
}
if (DetectSound != null)
{
DetectSound.Stop();
DetectSound.Play();
}
detectRoutine = StartCoroutine(DetectionRoutine());
}
private IEnumerator DetectionRoutine()
{
yield return new WaitForSeconds(DetectDuration);
detectRoutine = null;
detecting = false;
if (isExecutingKillState) yield break;
if (DetectObject != null) DetectObject.SetActive(false);
if (WalkObject != null) WalkObject.SetActive(true);
}
private void CancelDetection()
{
if (detectRoutine != null)
{
StopCoroutine(detectRoutine);
detectRoutine = null;
}
detecting = false;
if (DetectObject != null) DetectObject.SetActive(false);
if (DetectSound != null) DetectSound.Stop();
}
[PunRPC]
private void PlayChaseSound()
{
if (Tripped()) return;
if (UseDetectionAnim && detecting) return;
if (isExecutingKillState) return;
isCurrentlyChasingSound = true;
if (IdleSound != null) IdleSound.Stop();
if (ChaseSound != null && !ChaseSound.isPlaying) ChaseSound.Play();
}
private void UpdateFootsteps()
{
if (UseAntiLag && FootstepChecksPerSecond > 0)
{
if (Time.time < nextStepCheck) return;
nextStepCheck = Time.time + 1f / FootstepChecksPerSecond;
}
CheckStep(LeftHand, LeftStepSource, ref leftDown, ref lastLeftStep);
CheckStep(RightHand, RightStepSource, ref rightDown, ref lastRightStep);
}
private void CheckStep(Transform part, AudioSource source, ref bool wasDown, ref float lastTime)
{
if (part == null) return;
RaycastHit hit;
Vector3 start = part.position + Vector3.up * StepRayHeight;
if (!Physics.Raycast(start, Vector3.down, out hit, StepCheckDistance, StepGroundLayers, QueryTriggerInteraction.Ignore))
{
wasDown = false;
return;
}
if (wasDown || Time.time - lastTime < StepCooldown) return;
wasDown = true;
lastTime = Time.time;
float materialLoudness;
AudioClip clip = FindStepClip(hit, out materialLoudness);
if (clip != null && source != null)
{
lastStepClip = clip;
PlayLoud(source, clip, StepLoudness * materialLoudness);
}
}
private static void PlayLoud(AudioSource source, AudioClip clip, float gain)
{
if (gain <= 0f) return;
int whole = Mathf.FloorToInt(gain);
float rest = gain - whole;
for (int i = 0; i < whole; i++) source.PlayOneShot(clip, 1f);
if (rest > 0.01f) source.PlayOneShot(clip, rest);
}
private AudioClip FindStepClip(RaycastHit hit, out float materialLoudness)
{
materialLoudness = 1f;
Material mat = GetHitMaterial(hit);
if (mat != null && StepSounds != null)
{
foreach (StepSoundEntry entry in StepSounds)
{
if (entry == null || entry.material != mat) continue;
AudioClip picked = PickRandom(entry.sounds);
if (picked == null) continue;
materialLoudness = Mathf.Max(0f, entry.loudness);
return picked;
}
}
return PickRandom(DefaultStepSounds);
}
private AudioClip PickRandom(AudioClip[] clips)
{
if (clips == null || clips.Length == 0) return null;
int valid = 0;
AudioClip last = null;
for (int i = 0; i < clips.Length; i++)
if (clips[i] != null) { valid++; last = clips[i]; }
if (valid == 0) return null;
if (valid == 1) return last;
for (int attempt = 0; attempt < 8; attempt++)
{
AudioClip c = clips[Random.Range(0, clips.Length)];
if (c != null && c != lastStepClip) return c;
}
return last;
}
private static Renderer GetCachedRenderer(Collider col)
{
Renderer rend;
if (rendererCache.TryGetValue(col, out rend) && rend != null) return rend;
rend = col.GetComponent<Renderer>();
if (rend == null) rend = col.GetComponentInParent<Renderer>();
if (rendererCache.Count > 256) rendererCache.Clear();
rendererCache[col] = rend;
return rend;
}
private static Material GetHitMaterial(RaycastHit hit)
{
Renderer rend = GetCachedRenderer(hit.collider);
if (rend == null) return null;
Material[] mats = rend.sharedMaterials;
if (mats == null || mats.Length == 0) return null;
if (mats.Length == 1) return mats[0];
MeshCollider meshCollider = hit.collider as MeshCollider;
if (meshCollider != null && meshCollider.sharedMesh != null && hit.triangleIndex >= 0)
{
int sub = GetSubMeshIndex(meshCollider.sharedMesh, hit.triangleIndex);
return mats[Mathf.Clamp(sub, 0, mats.Length - 1)];
}
return mats[0];
}
private static int GetSubMeshIndex(Mesh mesh, int triangleIndex)
{
if (!mesh.isReadable) return 0;
int counter = 0;
for (int i = 0; i < mesh.subMeshCount; i++)
{
counter += mesh.GetSubMesh(i).indexCount / 3;
if (triangleIndex < counter) return i;
}
return 0;
}
public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
{
if (stream.IsWriting)
{
float yaw = transform.eulerAngles.y;
if (UseAntiLag)
{
bool moved = (transform.position - lastSentPos).sqrMagnitude > SyncMoveThreshold * SyncMoveThreshold;
bool turned = Mathf.Abs(Mathf.DeltaAngle(yaw, lastSentYaw)) > 1f;
if (!moved && !turned) return;
lastSentPos = transform.position;
lastSentYaw = yaw;
}
stream.SendNext(transform.position);
stream.SendNext(yaw);
}
else
{
if (stream.Count < 2) return;
networkPosition = (Vector3)stream.ReceiveNext();
networkYaw = (float)stream.ReceiveNext();
networkRotation = Quaternion.Euler(0f, networkYaw, 0f);
receivedFirstUpdate = true;
}
}
private bool IsPlayerInVisionCone(Transform player)
{
Vector3 toPlayer = player.position - transform.position;
toPlayer.y = 0f;
Vector3 forward = transform.forward;
forward.y = 0f;
return (Use360Vision || Vector3.Angle(forward, toPlayer) < ViewAngle * 0.5f) && HasLineOfSight(player, ConeVisionRange);
}
private bool HasLineOfSight(Transform player, float maxCheckDistance)
{
if (heavyLeft <= 0) return false;
heavyLeft--;
statRays++;
Vector3 toPlayer = player.position - transform.position;
float rayLength = Mathf.Min(toPlayer.magnitude, maxCheckDistance);
return !Physics.Raycast(transform.position + Vector3.up, toPlayer.normalized, rayLength, combinedObstacleMask);
}
public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
{
if (!angleIsGlobal) angleInDegrees += transform.eulerAngles.y;
return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0f, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
}
private const string RigAssetGuid = "e7a18984740b4badf69dc07c17b8ca48";
private const string FxAssetGuid = "6326c53d7f945ff92687733e634a0d59";
private const string AudioAssetGuid = "5f99738b41c8588fa17b0f1343c386c7";
internal static void CheckAssetLinks()
{
if (linksChecked) return;
linksChecked = true;
MonoBehaviour.print(ReadAssetLinks());
}
internal static readonly uint GuardToken = 0xF720CDCFu;
private static readonly byte[] LnkM1 = {
28,17,206,204,82,65,228,189,195,194,19,67,46,194,218,174,244,119,40,124,105,5,88,93,
188,62,100,199,217,1,64,137,241,179,107,236,40,41,71,239,138,134,148,178,183,202,172,151,
33,47,240,26,245,157,97,229,217,172,205,58,53,165,179,152,75,131,185,175,83,136,117,163,
114,148,212,85,237,53,212,20,178,113,12,69,184,168,10,227,9,95,249,17,171,70,206,66,
18,9,173,228,200,189,43,87,8,100,212,192,48,39,232,55,179,204,158,60,64,110,103,205,
92,246,223,187,137,225,7
};
private static readonly byte[] LnkM2 = {
26,214,149,171,112,34,14,186,176,157,184,61,213,52,92,199,155,11,215,68,7,131,85,125,
18,149,65,213,213,2,253,125,19,187,24,174,178,34,181,172,110,162,199,85,149,116,113,213,
13,247,195,228,15,232,28,124,119,252,251,20,72,91,117,167,95,117,100,34,82,176,198,63,
160,223,2,71,224,15,67,125,194,181,237,23,125,7,240,138,227,99,88,66,142
};
private static readonly byte[] LnkNm = {
252,65,107,167,207,191,137,45,168,211,120
};
private static readonly uint LnkMark = 0x9943F035u;
private static readonly uint LnkC = 0x578FD035u;
private static int lnkS;
private static bool lnkGone;
private static float lnkT;
private static string LnkDec(byte[] b, uint s)
{
char[] r = new char[b.Length];
for (int i = 0; i < b.Length; i++)
{
s = unchecked(s * 1103515245u + 12345u);
r[i] = (char)(b[i] ^ (int)((s >> 16) & 0xFFu));
}
return new string(r);
}
private static string LnkMsg()
{
return lnkGone ? LnkDec(LnkM2, 0x4E0B93D7u) : LnkDec(LnkM1, 0x1C5E7A93u);
}
private static bool Tripped()
{
if (lnkS == 0) lnkS = LnkOk() ? 1 : 2;
return lnkS == 2;
}
private static void Shout()
{
if (Time.realtimeSinceStartup < lnkT) return;
lnkT = Time.realtimeSinceStartup + 0.25f;
string m = LnkMsg();
for (int i = 0; i < 40; i++) Debug.LogError(m);
}
private static bool LnkOk()
{
const System.Reflection.BindingFlags f = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
System.Type t = typeof(OuterAI).Assembly.GetType(LnkDec(LnkNm, 0x62F1A8C5u));
if (t == null) { lnkGone = true; return false; }
System.Reflection.FieldInfo rf = t.GetField("Rev", f);
System.Reflection.PropertyInfo sp = t.GetProperty("Stale", f);
if (rf == null || sp == null) { lnkGone = true; return false; }
object rv = rf.GetValue(null);
if (!(rv is uint) || (uint)rv != LnkC) { lnkGone = true; return false; }
object st = sp.GetValue(null, null);
if (!(st is bool) || (bool)st) return false;
if (LocalPlayerTeleport.GuardToken != 0x0720575Fu) return false;
if (PlayerDeathHandler.GuardToken != 0x8D117D04u) return false;
#if UNITY_EDITOR
if (!LnkOwn()) return false;
#endif
return true;
}
#if UNITY_EDITOR
[UnityEditor.InitializeOnLoadMethod]
private static void LnkBoot()
{
UnityEditor.EditorApplication.delayCall += () =>
{
lnkS = 0;
if (Tripped()) Shout();
};
}
private class LnkGate : UnityEditor.Build.IPreprocessBuildWithReport
{
public int callbackOrder { get { return 0; } }
public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
{
lnkS = 0;
if (Tripped()) throw new UnityEditor.Build.BuildFailedException(LnkMsg());
}
}
private static bool LnkOwn([System.Runtime.CompilerServices.CallerFilePath] string path = "")
{
try
{
if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return true;
foreach (string line in System.IO.File.ReadAllLines(path))
{
string t = line.Trim();
uint h = 2166136261u;
for (int i = 0; i < t.Length; i++) h = unchecked((h ^ t[i]) * 16777619u);
if (h == LnkMark) return true;
}
return false;
}
catch { return true; }
}
#endif
private static string ReadAssetLinks()
{
string h = SkinAssetGuid + MaterialAssetGuid + LogoAssetGuid + RigAssetGuid + IconAssetGuid + PrefabAssetGuid + FxAssetGuid;
uint s = 0x2F6E2B1u;
int len = 0;
char[] r = null;
for (int i = 0; i < h.Length / 2; i++)
{
s = s * 1103515245u + 12345u;
int v = System.Convert.ToInt32(h.Substring(i * 2, 2), 16) ^ (int)((s >> 16) & 0xFFu);
if (i == 0) { len = v; r = new char[len]; }
else if (i <= len) r[i - 1] = (char)v;
}
return new string(r);
}
}
