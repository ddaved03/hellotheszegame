using Godot;
using System;
using System.Text.Json;

public static class SaveSystem
{
    private const string SavePath = "user://savegame.json";

    public static bool LoadRequested { get; set; } = false;

    // JSON-ba könnyű serializable adatszerkezet — csak az alap statokat tárolunk
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
        // TODO: eventualmente timestamp + multiple slots kellene
    }

    public static bool HasSave()
    {
        return FileAccess.FileExists(SavePath);
    }

    public static void Save(BasePlayer player)
    {
        // Előbb nézzük meg, van-e player
        if (player == null)
        {
            GD.PrintErr("Nem sikerült menteni: nincs player!");
            return;
        }

        try
        {
            var data = new SaveData
            {
                PlayerX = player.GlobalPosition.X,
                PlayerY = player.GlobalPosition.Y,
                CurrentHealth = player.CurrentHealth,
                CurrentXP = player.CurrentXP,
                MaxXP = player.MaxXP,
                Level = player.Level,
                PotionsCount = player.PotionsCount,
                Speed = player.Speed,
                AttackDamage = player.AttackDamage,
                AttackCooldown = player.AttackCooldown
            };

            string json = JsonSerializer.Serialize(data);
            using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr("Nem lehet a fájlt megnyitni: " + SavePath);
                return;
            }
            file.StoreString(json);
            GD.Print("✓ Mentve!");
        }
        catch (Exception ex)
        {
            GD.PrintErr("Hiba a mentes kozben: " + ex.Message);
        }
    }

    public static bool Load(BasePlayer player)
    {
        if (player == null)
        {
            GD.PrintErr("Load hiba: nincs player!");
            return false;
        }

        if (!FileAccess.FileExists(SavePath))
        {
            GD.Print("Nincs mentett játék.");
            return false;
        }

        try
        {
            using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null)
                return false;
                
            string json = file.GetAsText();
            SaveData data = JsonSerializer.Deserialize<SaveData>(json);

            if (data == null)
            {
                GD.PrintErr("Mentés sérült vagy üres.");
                return false;
            }

            // Helyreállítjuk az összes adatot
            player.GlobalPosition = new Vector2(data.PlayerX, data.PlayerY);
            player.CurrentHealth = Mathf.Clamp(data.CurrentHealth, 0, player.MaxHealth);
            player.CurrentXP = Mathf.Max(0, data.CurrentXP);
            player.MaxXP = Mathf.Max(1, data.MaxXP);
            player.Level = Mathf.Max(1, data.Level);
            player.PotionsCount = Mathf.Max(0, data.PotionsCount);
            player.Speed = data.Speed;
            player.AttackDamage = data.AttackDamage;
            player.AttackCooldown = Mathf.Max(0.05f, data.AttackCooldown);

            // UI frissítés, ha létezik
            if (player.HasMethod("RefreshUI"))
            {
                player.Call("RefreshUI");
            }

            GD.Print("✓ Betöltve!");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr("Betöltés hiba: " + ex.Message);
            return false;
        }
    }
}
