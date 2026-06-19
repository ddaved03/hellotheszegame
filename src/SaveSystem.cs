using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

public static class SaveSystem
{
    public static string CurrentSaveFileName { get; set; } = "savegame.json";
    private const string RelativeSaveDirectory = "saves";

    private static string _saveDirectory;
    private static string SaveDirectory
    {
        get
        {
            if (_saveDirectory == null)
            {
                string baseDir;

                // Ellenőrizzük, hogy a szerkesztőből futtatjuk-e a játékot
                if (OS.HasFeature("editor"))
                {
                    baseDir = ProjectSettings.GlobalizePath("res://");
                }
                else
                {
                    baseDir = OS.GetExecutablePath().GetBaseDir();
                }

                _saveDirectory = Path.Combine(baseDir, RelativeSaveDirectory).Replace("\\", "/");

                if (!Directory.Exists(_saveDirectory))
                {
                    Directory.CreateDirectory(_saveDirectory);
                }
            }
            return _saveDirectory;
        }
    }

    private static string FullSavePath => Path.Combine(SaveDirectory, CurrentSaveFileName).Replace("\\", "/");

    public static bool LoadRequested { get; set; } = false;

    // 1. JAVÍTÁS: A private szót public-ra cseréltük, hogy a JSON mentés működjön!
    public class SaveData
    {
        public string ScenePath { get; set; }
        public float PlayerX { get; set; }
        public float PlayerY { get; set; }
        public int MaxHealth { get; set; }
        public int CurrentHealth { get; set; }
        public int CurrentXP { get; set; }
        public int MaxXP { get; set; }
        public int Level { get; set; }
        public int PotionsCount { get; set; }
        public int MaxPotionSlots { get; set; }
        public float MaxMana { get; set; }
        public float CurrentMana { get; set; }
        public float Speed { get; set; }
        public int AttackDamage { get; set; }
        public float AttackCooldown { get; set; }
        public List<string> InventoryItems { get; set; } // Hátizsák tartalma
        public string PlayerName { get; set; }
        public bool FirstRun { get; set; }
        public WorldStateData WorldState { get; set; }
        public GroundFloorStateData GroundFloorState { get; set; }
    }

    public class WorldStateData
    {
        public bool ParkingEventStarted { get; set; }
        public int ParkingZombiesAlive { get; set; }
        public bool RocksEventStarted { get; set; }
        public bool RocksTimerActive { get; set; }
        public float RocksTimeLeft { get; set; }
        public float RocksSpawnCooldown { get; set; }
        public float RocksSpawnX { get; set; }
        public float RocksSpawnY { get; set; }
        public int RocksZombiesAlive { get; set; }
        public bool RocksEventCompleted { get; set; }
        public int TutorialZombiesAlive { get; set; }
        public float TrafficTimer { get; set; }
        public string QuestText { get; set; }
    }

    public class GroundFloorStateData
    {
        public bool DarknessVisible { get; set; }
        public float DarknessAlpha { get; set; }
        public bool EarthquakeTriggered { get; set; }
        public bool ElevatorFound { get; set; }
        public bool FlashlightPickupSpawned { get; set; }
        public bool QuestInitialized { get; set; }
        public string QuestText { get; set; }
        public List<RoomStateData> Rooms { get; set; } = new();
    }

    public class RoomStateData
    {
        public string Name { get; set; }
        public bool IsInside { get; set; }
        public bool ZombiesSpawned { get; set; }
        public bool RewardSpawned { get; set; }
        public bool Cleared { get; set; }
        public int AliveZombies { get; set; }
    }

    public static SaveData LastLoadedData { get; private set; }

    // Ideiglenes játékindítási adatok
    public static string PlayerName { get; set; } = "Player";
    public static bool FirstRun { get; set; } = false;

    public static List<string> GetSaveFiles()
    {
        List<string> saveFiles = new List<string>();
        if (Directory.Exists(SaveDirectory))
        {
            string[] files = Directory.GetFiles(SaveDirectory, "*.json");
            foreach (string file in files) saveFiles.Add(Path.GetFileName(file));
        }
        return saveFiles;
    }

    public static DateTime GetSaveDate(string fileName)
    {
        string path = Path.Combine(SaveDirectory, fileName);
        if (File.Exists(path)) return File.GetLastWriteTime(path);
        return DateTime.MinValue;
    }

    public static void DeleteSave(string fileName)
    {
        string path = Path.Combine(SaveDirectory, fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    public static bool RenameSave(string oldFileName, string newFileName)
    {
        if (!newFileName.EndsWith(".json")) newFileName += ".json";
        string oldPath = Path.Combine(SaveDirectory, oldFileName);
        string newPath = Path.Combine(SaveDirectory, newFileName);

        if (File.Exists(oldPath) && !File.Exists(newPath))
        {
            File.Move(oldPath, newPath);
            return true;
        }
        return false;
    }

    public static void SetNewSaveFile()
    {
        string baseName = "Mentés";
        string candidate = $"{baseName}.json";
        int index = 2;

        while (File.Exists(Path.Combine(SaveDirectory, candidate)))
        {
            candidate = $"{baseName} ({index}).json";
            index++;
        }

        CurrentSaveFileName = candidate;
    }
    
    public static bool HasAnySave() { return GetSaveFiles().Count > 0; }

    public static string GetSavedScenePath(string fileName)
    {
        SaveData data = ReadSaveData(fileName);
        return data?.ScenePath == "res://scenes/GroundFloor.tscn"
            ? data.ScenePath
            : "res://scenes/World.tscn";
    }

    public static void Save(
        BasePlayer player,
        WorldStateData worldState = null,
        GroundFloorStateData groundFloorState = null)
    {
        if (player == null) return;
        try
        {
            var data = new SaveData
            {
                ScenePath = player.GetTree().CurrentScene?.SceneFilePath,
                PlayerX = player.GlobalPosition.X, PlayerY = player.GlobalPosition.Y,
                MaxHealth = player.MaxHealth, CurrentHealth = player.CurrentHealth,
                CurrentXP = player.CurrentXP, MaxXP = player.MaxXP,
                Level = player.Level, PotionsCount = player.PotionsCount,
                MaxPotionSlots = player.MaxPotionSlots, MaxMana = player.MaxMana,
                CurrentMana = player.CurrentMana, Speed = player.Speed,
                AttackDamage = player.AttackDamage, AttackCooldown = player.AttackCooldown,
                InventoryItems = new List<string>(InventoryManager.Items)
                , PlayerName = PlayerName
                , FirstRun = FirstRun
                , WorldState = worldState
                , GroundFloorState = groundFloorState
            };

            // 2. JAVÍTÁS: Stabil, hagyományos fájlírás a Godot FileAccess helyett.
            string jsonString = JsonSerializer.Serialize(data);
            File.WriteAllText(FullSavePath, jsonString);
            
            GD.Print("\n✅ SIKERES MENTÉS! Cél: " + FullSavePath);
        }
        catch (Exception ex) { GD.PrintErr("SaveSystem Hiba mentéskor: " + ex.Message); }
    }

    public static bool Load(BasePlayer player)
    {
        // 2. JAVÍTÁS: Fájl beolvasása a File.ReadAllText segítségével
        if (player == null || !File.Exists(FullSavePath)) return false;
        try
        {
            SaveData data = ReadSaveData(CurrentSaveFileName);
            if (data == null) return false;

            LastLoadedData = data;

            player.GlobalPosition = new Vector2(data.PlayerX, data.PlayerY);
            player.CurrentXP = Mathf.Max(0, data.CurrentXP);
            player.MaxXP = Mathf.Max(1, data.MaxXP);
            player.Level = Mathf.Max(1, data.Level);
            player.MaxHealth = data.MaxHealth > 0 ? data.MaxHealth : 100 + ((player.Level - 1) * 20);
            player.CurrentHealth = Mathf.Clamp(data.CurrentHealth, 0, player.MaxHealth);
            player.PotionsCount = Mathf.Max(0, data.PotionsCount);
            player.MaxPotionSlots = data.MaxPotionSlots > 0 ? data.MaxPotionSlots : 3 + ((player.Level - 1) / 5);
            if (data.MaxMana > 0f)
            {
                player.MaxMana = data.MaxMana;
                player.CurrentMana = Mathf.Clamp(data.CurrentMana, 0f, player.MaxMana);
            }
            else
            {
                player.CurrentMana = player.MaxMana;
            }
            player.Speed = data.Speed;
            player.AttackDamage = data.AttackDamage;
            player.AttackCooldown = Mathf.Max(0.05f, data.AttackCooldown);

            InventoryManager.Items.Clear();
            if (data.InventoryItems != null) InventoryManager.Items.AddRange(data.InventoryItems);

            PlayerName = data.PlayerName ?? PlayerName;
            FirstRun = data.FirstRun;

            if (player.HasMethod("RefreshUI")) player.Call("RefreshUI");
            if (player.Inventory != null) player.Inventory.UpdateUI();

            GD.Print($"SaveSystem: ✓ {CurrentSaveFileName} betöltve!");
            return true;
        }
        catch (Exception ex) { GD.PrintErr("SaveSystem Hiba betöltéskor: " + ex.Message); return false; }
    }

    private static SaveData ReadSaveData(string fileName)
    {
        try
        {
            string path = Path.Combine(SaveDirectory, fileName).Replace("\\", "/");
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            GD.PrintErr("SaveSystem Hiba a mentés olvasásakor: " + ex.Message);
            return null;
        }
    }
}
