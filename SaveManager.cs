using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles serialization and deserialization of the game state using JSON.
/// Persists data across scene loads utilizing the Singleton pattern.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Save Settings")]
    public int currentSlot = 1;
    private string savePath;

    private void Awake()
    {
        // Enforce Singleton pattern and ensure persistence across scene loads
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Set save path (e.g., AppData/LocalLow/[CompanyName]/[GameName] on PC)
            savePath = Application.persistentDataPath + "/save_slot_";
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>Checks if a save file exists for a given slot.</summary>
    public bool DoesSaveExist(int slot)
    {
        return File.Exists(savePath + slot + ".json");
    }

    /// <summary>Gathers data from world managers and serializes it to JSON.</summary>
    public void SaveGame()
    {
        SaveData data = new SaveData();
        data.slotIndex = currentSlot;
        data.lastSaveDate = System.DateTime.Now.ToString("yyyy.MM.dd. HH:mm");

        // --- 1. PLAYER STATS & POSITION ---
        PlayerStats stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null)
        {
            data.currentLevel = stats.currentLevel;
            data.currentSap = stats.currentSap;
            data.vitalityLevel = stats.vitalityLevel;
            data.enduranceLevel = stats.enduranceLevel;
            data.spiritLevel = stats.spiritLevel;
            data.strengthLevel = stats.strengthLevel;

            data.posX = stats.transform.position.x;
            data.posY = stats.transform.position.y;
            data.posZ = stats.transform.position.z;
        }

        // --- 2. INVENTORY (Bag contents) ---
        if (InventoryManager.Instance != null)
        {
            foreach (var slot in InventoryManager.Instance.slots)
            {
                // Note: Saving by file name rather than internal instance ID
                data.inventoryItemIDs.Add(slot.itemData.name);
                data.inventoryItemAmounts.Add(slot.amount);
            }
        }

        // --- 3. MAGIC SYSTEM (Spells) ---
        PlayerMagicSystem magicSystem = FindFirstObjectByType<PlayerMagicSystem>();
        if (magicSystem != null)
        {
            foreach (var unlocked in magicSystem.unlockedSpells)
            {
                // Store ScriptableObject file name as safe reference string
                data.unlockedSpellIDs.Add(unlocked.spellAsset.name);
                data.unlockedSpellLevels.Add(unlocked.currentLevel);
            }
        }

        // --- 4. QUESTS & WORLD STATE (Defeated Bosses) ---
        if (QuestManager.Instance != null)
        {
            // WORKAROUND: Unity's JsonUtility does not natively serialize Dictionaries.
            // We split the KeyValuePair into two separate lists for serialization.
            foreach (var kvp in QuestManager.Instance.GetAllQuestStates())
            {
                data.questKeys.Add(kvp.Key);
                data.questValues.Add(kvp.Value);
            }

            data.defeatedTargets = new List<string>(QuestManager.Instance.GetDefeatedTargets());
            data.collectedPickups = new List<string>(QuestManager.Instance.GetCollectedPickups());
        }

        // Save Shop Stocks (Parsing the composite keys safely)
        data.savedShopStocks.Clear();
        foreach (var stock in QuestManager.Instance.GetAllShopStocks())
        {
            // Find the delimiter linking the NPC ID and Item Name
            int lastUnderscore = stock.Key.LastIndexOf('_');
            if (lastUnderscore != -1)
            {
                string parsedNpcID = stock.Key.Substring(0, lastUnderscore);
                string parsedItemID = stock.Key.Substring(lastUnderscore + 1);

                data.savedShopStocks.Add(new ShopStockSave
                {
                    npcID = parsedNpcID,
                    itemID = parsedItemID,
                    remainingQuantity = stock.Value
                });
            }
        }

        // --- WRITE TO FILE ---
        string json = JsonUtility.ToJson(data, true); // 'true' enables formatted, human-readable JSON
        File.WriteAllText(savePath + currentSlot + ".json", json);

        Debug.Log($"<color=cyan>Game saved to Slot {currentSlot}.</color>");
    }

    /// <summary>Reads the JSON file and returns the deserialized SaveData object.</summary>
    public SaveData LoadData(int slot)
    {
        if (!DoesSaveExist(slot))
        {
            Debug.LogWarning($"<color=yellow>There is no save on this slot.: {slot}</color>");
            return null;
        }

        string json = File.ReadAllText(savePath + slot + ".json");
        SaveData loadedData = JsonUtility.FromJson<SaveData>(json);
        return loadedData;
    }

    /// <summary>Restores the world state and Player based on the JSON save.</summary>
    public void LoadGame()
    {
        SaveData data = LoadData(currentSlot);
        if (data == null)
        {
            Debug.LogError("<color=red>Error: No data to load on this slot!</color>");
            return;
        }

        // --- 1. RESTORE PLAYER STATS & POSITION ---
        PlayerLocomotion player = FindFirstObjectByType<PlayerLocomotion>();
        if (player != null && player.stats != null)
        {
            // CRITICAL: CharacterController must be disabled before teleporting via script!
            player.controller.enabled = false;
            player.transform.position = new Vector3(data.posX, data.posY, data.posZ);
            player.controller.enabled = true;

            player.stats.currentLevel = data.currentLevel;
            player.stats.currentSap = data.currentSap;
            player.stats.vitalityLevel = data.vitalityLevel;
            player.stats.enduranceLevel = data.enduranceLevel;
            player.stats.spiritLevel = data.spiritLevel;
            player.stats.strengthLevel = data.strengthLevel;

            // Recalculate Max HP/Stamina/Mana based on loaded levels
            player.stats.UpdateAllStats();

            // Simulate resting (Refill all bars)
            player.stats.RefillAll();

            if (player.TryGetComponent(out CharacterStatusManager statusManager))
                statusManager.ClearAllEffects(); // Clear debuffs upon load
        }

        // --- 2. RESTORE INVENTORY ---
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.slots.Clear();
            for (int i = 0; i < data.inventoryItemIDs.Count; i++)
            {
                // Locate ItemData from Resources/Items folder using the saved ID string
                ItemData loadedItem = Resources.Load<ItemData>("Items/" + data.inventoryItemIDs[i]);
                if (loadedItem != null)
                {
                    InventoryManager.Instance.slots.Add(new InventorySlot(loadedItem, data.inventoryItemAmounts[i]));
                }
            }
            InventoryManager.Instance.OnInventoryChanged?.Invoke(); // Update UI
        }

        // --- 3. RESTORE MAGIC SYSTEM ---
        PlayerMagicSystem magicSystem = FindFirstObjectByType<PlayerMagicSystem>();
        if (magicSystem != null)
        {
            magicSystem.unlockedSpells.Clear();
            for (int i = 0; i < data.unlockedSpellIDs.Count; i++)
            {
                Spell loadedSpellAsset = Resources.Load<Spell>("Spells/" + data.unlockedSpellIDs[i]);
                if (loadedSpellAsset != null)
                {
                    UnlockedSpell restoredSpell = new UnlockedSpell();
                    restoredSpell.spellAsset = loadedSpellAsset;
                    restoredSpell.currentLevel = data.unlockedSpellLevels[i];
                    magicSystem.unlockedSpells.Add(restoredSpell);
                }
            }

            if (player != null && player.stats != null)
                player.stats.tailsCount = Mathf.Clamp(magicSystem.unlockedSpells.Count + 1, 1, 9);

            // Automatically equip the first available spell
            magicSystem.currentSpell = magicSystem.unlockedSpells.Count > 0 ? magicSystem.unlockedSpells[0].spellAsset : null;
        }

        // --- 4. RESTORE QUESTS & WORLD STATE ---
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.GetAllQuestStates().Clear();
            for (int i = 0; i < data.questKeys.Count; i++)
            {
                QuestManager.Instance.GetAllQuestStates().Add(data.questKeys[i], data.questValues[i]);
            }

            QuestManager.Instance.GetDefeatedTargets().Clear();
            QuestManager.Instance.GetDefeatedTargets().AddRange(data.defeatedTargets);

            QuestManager.Instance.GetCollectedPickups().Clear();
            QuestManager.Instance.GetCollectedPickups().AddRange(data.collectedPickups);

            // Destroy targets that were defeated in previous sessions
            EnemyQuestTarget[] allTargets = FindObjectsByType<EnemyQuestTarget>(FindObjectsSortMode.None);
            foreach (var target in allTargets)
            {
                if (QuestManager.Instance.IsTargetDefeated(target.targetID))
                {
                    Destroy(target.gameObject);
                }
            }
        }

        // Restore Shop Stocks
        QuestManager.Instance.GetAllShopStocks().Clear();
        foreach (var s in data.savedShopStocks)
        {
            QuestManager.Instance.UpdateStock(s.npcID, s.itemID, s.remainingQuantity);
        }

        foreach (var npc in FindObjectsByType<NPCController>(FindObjectsSortMode.None))
        {
            npc.SyncPositionWithQuestState();
            npc.LoadSavedShopStock();
        }

        // Close UI if open
        if (ShopUIManager.Instance != null) ShopUIManager.Instance.CloseShop();

        Debug.Log($"<color=green>Game successfully loaded from slot {currentSlot}.</color>");
    }

    /// <summary>Invoked by the Main Menu. Loads the scene first, then applies the save data.</summary>
    public void LoadGameAfterSceneLoad(string sceneName)
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        LoadGame();
    }

    /// <summary>
    /// Invoked upon player death. Reloads the current scene to reset enemies, 
    /// then applies save data to restore player progression while maintaining defeated bosses.
    /// </summary>
    public void ReloadWorldAndLoadData()
    {
        SceneManager.sceneLoaded += OnSceneLoadedAfterDeath;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoadedAfterDeath(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedAfterDeath;
        LoadGame();
    }

    /// <summary>Deletes the save file associated with the specified slot.</summary>
    public void DeleteSave(int slot)
    {
        string path = savePath + slot + ".json";
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"<color=red>Save deleted from slot {slot}.</color>");
        }
    }
}