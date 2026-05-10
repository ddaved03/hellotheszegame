using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using FileAccess = Godot.FileAccess;

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
                    // Fejlesztés közben a projekt mappájába (res://) mentünk
                    baseDir = ProjectSettings.GlobalizePath("res://");
                }
                else
                {
                    // Exportált játéknál az .exe (vagy futtatható fájl) mellé mentünk
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

    private class SaveData
    {
        public float PlayerX { get; set; }
        public float PlayerY { get; set; }
        public int CurrentHealth { get; set; }
        public int CurrentXP { get; set; }
        public int MaxXP { get; set; }
        public int Level { get; set; }
        public int PotionsCount { get; set; }
        public float Speed { get; set; }
        public int AttackDamage { get; set; }
        public float AttackCooldown { get; set; }
        public List<string> InventoryItems { get; set; } // Hátizsák tartalma
    }

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

    public static void SetNewSaveFile() { CurrentSaveFileName = $"Mentés_{DateTime.Now:yyyyMMdd_HHmmss}.json"; }
    
    public static bool HasAnySave() { return GetSaveFiles().Count > 0; }

    public static void Save(BasePlayer player)
    {
        if (player == null) return;
        try
        {
            var data = new SaveData
            {
                PlayerX = player.GlobalPosition.X, PlayerY = player.GlobalPosition.Y,
                CurrentHealth = player.CurrentHealth, CurrentXP = player.CurrentXP, MaxXP = player.MaxXP,
                Level = player.Level, PotionsCount = player.PotionsCount, Speed = player.Speed,
                AttackDamage = player.AttackDamage, AttackCooldown = player.AttackCooldown,
                InventoryItems = new List<string>(InventoryManager.Items)
            };

            using FileAccess file = FileAccess.Open(FullSavePath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(JsonSerializer.Serialize(data));
                GD.Print("\n✅ SIKERES MENTÉS! Cél: " + FullSavePath);
            }
        }
        catch (Exception ex) { GD.PrintErr("SaveSystem Hiba: " + ex.Message); }
    }

    public static bool Load(BasePlayer player)
    {
        if (player == null || !FileAccess.FileExists(FullSavePath)) return false;
        try
        {
            using FileAccess file = FileAccess.Open(FullSavePath, FileAccess.ModeFlags.Read);
            if (file == null) return false;

            SaveData data = JsonSerializer.Deserialize<SaveData>(file.GetAsText());
            if (data == null) return false;

            player.GlobalPosition = new Vector2(data.PlayerX, data.PlayerY);
            player.CurrentHealth = Mathf.Clamp(data.CurrentHealth, 0, player.MaxHealth);
            player.CurrentXP = Mathf.Max(0, data.CurrentXP);
            player.MaxXP = Mathf.Max(1, data.MaxXP);
            player.Level = Mathf.Max(1, data.Level);
            player.PotionsCount = Mathf.Max(0, data.PotionsCount);
            player.Speed = data.Speed;
            player.AttackDamage = data.AttackDamage;
            player.AttackCooldown = Mathf.Max(0.05f, data.AttackCooldown);

            InventoryManager.Items.Clear();
            if (data.InventoryItems != null) InventoryManager.Items.AddRange(data.InventoryItems);

            if (player.HasMethod("RefreshUI")) player.Call("RefreshUI");
            if (player.Inventory != null) player.Inventory.UpdateUI();

            GD.Print($"SaveSystem: ✓ {CurrentSaveFileName} betöltve!");
            return true;
        }
        catch (Exception ex) { GD.PrintErr("Load Hiba: " + ex.Message); return false; }
    }
}