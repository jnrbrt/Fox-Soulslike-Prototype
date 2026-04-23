using UnityEngine;

// --- IDLE STATE ---
public class ShadowBossIdleState : EnemyState
{
    public ShadowBossIdleState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void LogicUpdate()
    {
        // If the boss takes damage or spots the player, draw aggro immediately
        if (enemy.CanSeePlayer() || enemy.currentHealth < 100f)
        {
            stateMachine.ChangeState(new ShadowBossChaseState(enemy, stateMachine));
        }
    }
}

// --- CHASE STATE (Improved distance check & jump cooldown) ---
public class ShadowBossChaseState : EnemyState
{
    private ShadowBossController boss;

    public ShadowBossChaseState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine)
    {
        boss = enemy as ShadowBossController;
    }

    public override void Enter()
    {
        enemy.Agent.isStopped = false;
        enemy.Agent.speed = 2.5f;
    }

    public override void LogicUpdate()
    {
        float distance = Vector3.Distance(enemy.transform.position, enemy.PlayerTarget.position);
        enemy.Agent.SetDestination(enemy.PlayerTarget.position);

        // --- PHASE 2 ATTACK: Jump slam based on cooldown ---
        if (boss.isPhase2 && Time.time > boss.lastJumpTime + 5f)
        {
            stateMachine.ChangeState(new ShadowBossJumpSlamState(boss, stateMachine));
            return;
        }

        // --- NORMAL ATTACKS ---
        // Check if within attack range OR the NavMeshAgent has physically reached the target
        bool isCloseEnough = distance <= enemy.attackRange ||
                             (!enemy.Agent.pathPending && enemy.Agent.remainingDistance <= enemy.Agent.stoppingDistance + 0.2f);

        if (isCloseEnough)
        {
            if (Random.value > 0.6f && !boss.isPhase2)
                stateMachine.ChangeState(new ShadowBossSlamState(boss, stateMachine));
            else
                stateMachine.ChangeState(new ShadowBossMeleeState(boss, stateMachine));
        }
    }
}

// --- PHASE 1 AoE SLAM (Stationary heavy attack) ---
public class ShadowBossSlamState : EnemyState
{
    private ShadowBossController boss;
    private float timer;
    private bool hasSlammed;

    public ShadowBossSlamState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine)
    {
        boss = enemy as ShadowBossController;
    }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        timer = 0f;
        hasSlammed = false;
        Debug.Log("<color=orange>Boss: slam attack wind-up</color>");

        // Face the target before executing the slam
        Vector3 dir = (enemy.PlayerTarget.position - enemy.transform.position).normalized;
        dir.y = 0;
        enemy.transform.rotation = Quaternion.LookRotation(dir);
    }

    public override void LogicUpdate()
    {
        timer += Time.deltaTime;

        // 1.2s readable wind-up for telegraphing the attack
        if (timer >= 1.2f && !hasSlammed)
        {
            hasSlammed = true;
            boss.ExecuteAoESlam();
            Debug.Log("<color=red>Boss: slam attack</color>");
        }
        else if (timer >= 2.5f) // Recovery phase
        {
            stateMachine.ChangeState(new ShadowBossChaseState(enemy, stateMachine));
        }
    }
}

// --- PHASE TRANSITION (Immediate jump execution) ---
public class ShadowBossPhaseTransitionState : EnemyState
{
    private float timer;

    public ShadowBossPhaseTransitionState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        timer = 0f;
        Debug.Log("<color=magenta>Boss: phase 2 transition begins</color>");
        if (CombatEffectsManager.Instance != null) CombatEffectsManager.Instance.TriggerHitStop(0.5f);
    }

    public override void LogicUpdate()
    {
        timer += Time.deltaTime;
        if (timer >= 3f)
        {
            // Instantly transition to the signature Jump Slam attack instead of chasing
            stateMachine.ChangeState(new ShadowBossJumpSlamState(enemy, stateMachine));
        }
    }
}

// --- PHASE 2: JUMP SLAM (Parabolic trajectory calculation) ---
public class ShadowBossJumpSlamState : EnemyState
{
    private ShadowBossController boss;
    private float timer;
    private int phase;

    private Vector3 startPos;
    private Vector3 jumpTargetPos;
    private readonly float jumpDuration = 0.8f;
    private readonly float jumpHeight = 6.0f;   // Maximum height of the jump arc

    public ShadowBossJumpSlamState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine)
    {
        boss = enemy as ShadowBossController;
    }

    public override void Enter()
    {
        // 1. FULL DETACHMENT FROM NAVMESH for manual physics manipulation
        boss.Agent.updatePosition = false;
        boss.Agent.updateRotation = false;
        boss.Agent.isStopped = true;
        boss.Agent.enabled = false;

        timer = 0f;
        phase = 0;
        boss.lastJumpTime = Time.time;

        Vector3 dir = (boss.PlayerTarget.position - boss.transform.position).normalized;
        dir.y = 0;
        boss.transform.rotation = Quaternion.LookRotation(dir);

        Debug.Log("<color=orange>Boss: jump attack wind-up</color>");
    }

    public override void LogicUpdate()
    {
        timer += Time.deltaTime;

        if (phase == 0 && timer >= 0.8f) // Wind-up
        {
            phase = 1;
            timer = 0f;

            startPos = boss.transform.position;
            // Lock onto the player's CURRENT position as the landing spot
            jumpTargetPos = boss.PlayerTarget.position;

            Debug.Log("Boss: jumping");
        }
        else if (phase == 1) // Airborne trajectory logic
        {
            float progress = timer / jumpDuration;
            if (progress > 1f) progress = 1f;

            // 1. Calculate horizontal interpolation (X and Z axis)
            Vector3 groundPos = Vector3.Lerp(startPos, jumpTargetPos, progress);

            // 2. Calculate vertical height using a sine wave for the parabola (Y axis)
            float heightOffset = Mathf.Sin(progress * Mathf.PI) * jumpHeight;

            // 3. Apply the calculated trajectory position
            boss.transform.position = new Vector3(groundPos.x, startPos.y + heightOffset, groundPos.z);

            if (progress >= 1f)
            {
                phase = 2;
                timer = 0f;
                boss.ExecuteAoESlam(); // Impact!
                Debug.Log("<color=red>Boss: impact</color>");
            }
        }
        else if (phase == 2 && timer >= 1.5f) // Recovery
        {
            // RE-ATTACH TO NAVMESH
            boss.Agent.enabled = true;
            boss.Agent.updatePosition = true;
            boss.Agent.updateRotation = true;
            boss.Agent.isStopped = false;

            stateMachine.ChangeState(new ShadowBossChaseState(boss, stateMachine));
        }
    }
}

// --- PHASE 1 & 2: MELEE ATTACK (Standard strike) ---
public class ShadowBossMeleeState : EnemyState
{
    private ShadowBossController boss;
    private float timer;
    private int phase; // 0: Windup, 1: Active, 2: Recovery

    public ShadowBossMeleeState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine)
    {
        boss = enemy as ShadowBossController;
    }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        timer = 0f;
        phase = 0;

        Vector3 dir = (enemy.PlayerTarget.position - enemy.transform.position).normalized;
        dir.y = 0;
        enemy.transform.rotation = Quaternion.LookRotation(dir);

        // Visual telegraphing for the attack
        if (boss.weapon != null) boss.weapon.ShowWindup();

        Debug.Log("Boss: melee attack wind-up");
    }

    public override void LogicUpdate()
    {
        timer += Time.deltaTime;

        if (phase == 0 && timer >= 0.7f) // Windup phase
        {
            phase = 1;
            if (boss.weapon != null) boss.weapon.EnableHitbox();

            // Slight forward lunge
            boss.Agent.Move(boss.transform.forward * 3f * Time.deltaTime);
            Debug.Log("Boss: Attack active");
        }
        else if (phase == 1 && timer >= 1.0f) // Active frames end
        {
            phase = 2;
            if (boss.weapon != null) boss.weapon.DisableHitbox();
        }
        else if (phase == 2 && timer >= 1.8f) // Recovery ends
        {
            stateMachine.ChangeState(new ShadowBossChaseState(boss, stateMachine));
        }
    }

    public override void Exit()
    {
        if (boss.weapon != null) boss.weapon.DisableHitbox();
    }
}

// --- HURT / STAGGER STATE ---
public class ShadowBossHurtState : EnemyState
{
    private float timer;
    private ShadowBossController boss;

    public ShadowBossHurtState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine)
    {
        boss = enemy as ShadowBossController;
    }

    public override void Enter()
    {
        enemy.Agent.isStopped = true;
        timer = 0f;

        if (boss.weapon != null) boss.weapon.DisableHitbox();

        // Hyper Armor logic: Phase 2 boss cannot be interrupted/staggered
        if (boss.isPhase2)
        {
            Debug.Log("<color=magenta>Boss: Cannot be staggered (Hyper Armor active in phase 2)</color>");
            stateMachine.ChangeState(new ShadowBossChaseState(boss, stateMachine));
            return;
        }

        Debug.Log("<color=yellow>Boss: staggered</color>");
    }

    public override void LogicUpdate()
    {
        timer += Time.deltaTime;
        if (timer >= 1.2f) // Stagger duration
        {
            stateMachine.ChangeState(new ShadowBossChaseState(boss, stateMachine));
        }
    }
}

// --- DEAD STATE ---
public class ShadowBossDeadState : EnemyState
{
    public ShadowBossDeadState(EnemyController enemy, EnemyStateMachine stateMachine) : base(enemy, stateMachine) { }

    public override void Enter()
    {
        Debug.Log("<color=black>Boss died</color>");

        // EnemyQuestTarget component handles the quest event propagation
        Object.Destroy(enemy.gameObject, 8f); // 8 seconds delay for dramatic fade-out
    }
}