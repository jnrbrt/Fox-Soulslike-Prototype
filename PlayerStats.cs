using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages player RPG statistics, resources, and level progression.
/// Utilizes an event-driven architecture to automatically update UI elements.
/// </summary>
public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("RPG System (Progression)")]
    public int currentLevel = 1;
    public int currentSap = 0; // Currency / XP
    public int tailsCount = 1; // Unlocked magic slots (Max 9)

    [Header("Levels (Stat Points)")]
    public int vitalityLevel = 10;  // Increases Max HP
    public int enduranceLevel = 10; // Increases Max Stamina
    public int spiritLevel = 10;    // Increases Max Spirit (Mana)
    public int strengthLevel = 10;  // Increases Base Damage

    [Header("Current & Max Values")]
    public float maxHealth;
    public float currentHealth;

    [SerializeField] public float maxStamina;
    [SerializeField] private float currentStamina;

    [SerializeField] public float maxSpirit;
    [SerializeField] public float currentSpirit;

    public float swordDamage;

    [Header("Stamina Regen Settings")]
    [SerializeField] private float staminaRegenRate = 15f;
    [SerializeField] private float regenDelay = 1.5f;
    private float lastStaminaUseTime;

    public float MaxHealth => maxHealth;
    public float MaxStamina => maxStamina;
    public float MaxSpirit => maxSpirit;
    public float CurrentSpirit => currentSpirit;
    public float CurrentStamina => currentStamina;

    // Buff Variables
    [HideInInspector] public bool isLifeThreadActive = false;
    [HideInInspector] public float currentLifeThreadHealAmount = 5f;
    private float lifeThreadTimer = 0f;

    private PlayerLocomotion player;

    // --- UI EVENTS ---
    public Action<int> OnSapChanged;
    public Action<int> OnLevelChanged;
    public Action<float, float> OnHealthChanged;
    public Action<float, float> OnStaminaChanged;
    public Action<float, float> OnSpiritChanged;

    private void Awake()
    {
        player = GetComponent<PlayerLocomotion>();
        UpdateAllStats(); // Calculate maximums based on initial levels
    }

    private void Start()
    {
        RefillAll();
    }

    private void Update()
    {
        HandleStaminaRegen();

        // Buff duration countdown
        if (isLifeThreadActive)
        {
            lifeThreadTimer -= Time.deltaTime;
            if (lifeThreadTimer <= 0) isLifeThreadActive = false;
        }
    }

    // --- RPG MATH & PROGRESSION ---

    /// <summary>
    /// Recalculates maximum stats based on attribute levels, applying soft caps (Diminishing Returns).
    /// </summary>
    public void UpdateAllStats()
    {
        // Base value + diminishing returns based on level scaling
        maxHealth = 100f + CalculateStat(vitalityLevel, 20f, 15f, 5f);
        maxStamina = 80f + CalculateStat(enduranceLevel, 10f, 5f, 2f);
        maxSpirit = 50f + CalculateStat(spiritLevel, 15f, 8f, 3f);
        swordDamage = 15f + CalculateStat(strengthLevel, 5f, 3f, 1f);

        // Notify UI subscribers to update bar fill amounts
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        OnSpiritChanged?.Invoke(currentSpirit, maxSpirit);
    }

    private float CalculateStat(int level, float earlyGain, float midGain, float lateGain)
    {
        float total = 0;
        // Apply varying gains based on soft cap thresholds
        for (int i = 1; i < level; i++)
        {
            if (i < 20) total += earlyGain;
            else if (i < 50) total += midGain;
            else total += lateGain;
        }
        return total;
    }

    public int GetLevelUpCost()
    {
        // Exponential cost scaling formula
        return Mathf.FloorToInt(100 * Mathf.Pow(1.1f, currentLevel - 1));
    }

    public void AddSap(int amount)
    {
        currentSap += amount;
        OnSapChanged?.Invoke(currentSap);
        Debug.Log($"<color=yellow>Got {amount} Spirit Sap! | Total SS: {currentSap}</color>");
    }

    /// <summary>
    /// Deducts cost and increments base level.
    /// Actual stat allocation is handled by the LevelUpUI script.
    /// </summary>
    public bool TryLevelUp()
    {
        int cost = GetLevelUpCost();
        if (currentSap >= cost)
        {
            currentSap -= cost;
            currentLevel++;

            tailsCount = Mathf.Clamp(tailsCount, 1, 9);

            OnSapChanged?.Invoke(currentSap);
            OnLevelChanged?.Invoke(currentLevel);

            return true;
        }
        return false;
    }

    // --- COMBAT & RESOURCE MANAGEMENT ---

    public void RefillAll()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentSpirit = maxSpirit;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        OnSpiritChanged?.Invoke(currentSpirit, maxSpirit);
    }

    private void HandleStaminaRegen()
    {
        if (currentStamina < maxStamina && Time.time > lastStaminaUseTime + regenDelay)
        {
            float multiplier = 1f;

            if (TryGetComponent(out CharacterStatusManager statusManager))
            {
                multiplier = statusManager.StaminaRegenMultiplier;
            }

            currentStamina += (staminaRegenRate * multiplier) * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }

    public bool TryConsumeStamina(float amount)
    {
        if (currentStamina >= amount)
        {
            currentStamina -= amount;
            lastStaminaUseTime = Time.time;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            return true;
        }
        return false;
    }

    public bool HasEnoughStamina(float amount) => currentStamina >= amount;
    public bool HasEnoughSpirit(float amount) => currentSpirit >= amount;

    public bool TryConsumeSpirit(float amount)
    {
        if (currentSpirit >= amount)
        {
            currentSpirit -= amount;
            OnSpiritChanged?.Invoke(currentSpirit, maxSpirit);
            return true;
        }
        return false;
    }

    public void RestoreSpirit(float amount)
    {
        currentSpirit += amount;
        currentSpirit = Mathf.Clamp(currentSpirit, 0, maxSpirit);
        OnSpiritChanged?.Invoke(currentSpirit, maxSpirit);
    }

    public void ActivateLifeThread(float duration, float healAmount)
    {
        isLifeThreadActive = true;
        lifeThreadTimer = duration;
        currentLifeThreadHealAmount = healAmount;
        Debug.Log($"Spell: Life Thread ACTIVE for {duration} seconds. {healAmount} HP is gained per hit.");
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (player != null && player.isInvulnerable)
        {
            Debug.Log("<color=cyan>Dodged.</color>");
            return;
        }

        currentHealth -= amount;
        Debug.Log($"<color=red>Player got hurt! HP: {currentHealth}</color>");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (CombatEffectsManager.Instance != null)
            CombatEffectsManager.Instance.TriggerHitStop(0.1f);

        if (currentHealth <= 0)
        {
            Debug.Log("<color=black>YOU DIED.</color>");
            HandlePlayerDeath();
        }
    }

    public void TakeDamageOverTime(float amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Debug.Log("<color=purple>YOU DIED FROM POISON.</color>");
            HandlePlayerDeath();
        }
    }

    private void HandlePlayerDeath()
    {
        if (SaveManager.Instance != null)
        {
            // Reloads scene to reset enemy positions, but retains defeated boss data via SaveManager
            SaveManager.Instance.ReloadWorldAndLoadData();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}