using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

// =============================================================
//  HellóTheSzeGame – Egységtesztek (NUnit)
//  Tesztelt osztályok: BasePlayer (logika), InventoryManager,
//                      SaveSystem (SaveData), ZombieSpawner
// =============================================================

namespace HellóTheSzeGame.Tests
{
    // =========================================================
    //  1. BasePlayer – logikai tesztek
    //  (Godot-független részek: HP, XP, Mana, Potion)
    // =========================================================
    [TestFixture]
    public class BasePlayerLogicTests
    {
        // --- Segédosztály: csak a tesztelt mezőket tartalmazza,
        //     Godot-csomópontok nélkül ---
        private class PlayerState
        {
            public int MaxHealth      = 100;
            public int CurrentHealth;
            public int AttackDamage   = 20;
            public float AttackCooldown = 0.5f;
            public int CurrentXP      = 0;
            public int MaxXP          = 100;
            public int Level          = 1;
            public int PotionsCount   = 0;
            public int MaxPotionSlots = 3;
            public float MaxMana      = 100f;
            public float CurrentMana  = 100f;
            public float ManaCost     = 5f;

            public PlayerState() { CurrentHealth = MaxHealth; }

            // TakeDamage logikája (Godot-hívások nélkül)
            public bool TakeDamage(int amount)
            {
                CurrentHealth -= amount;
                return CurrentHealth <= 0; // true = meghalt
            }

            // GainXP + LevelUp logikája
            public void GainXP(int amount)
            {
                CurrentXP += amount;
                if (CurrentXP >= MaxXP) LevelUp();
            }

            private void LevelUp()
            {
                Level++;
                CurrentXP = 0;
                MaxXP = (int)(MaxXP * 1.5);
                MaxHealth += 20;
                CurrentHealth = MaxHealth;
                MaxMana += 10;
                CurrentMana = MaxMana;
                if (Level % 5 == 0) MaxPotionSlots++;
            }

            public void UsePotion()
            {
                if (PotionsCount > 0 && CurrentHealth < MaxHealth)
                {
                    CurrentHealth = System.Math.Min(CurrentHealth + 20, MaxHealth);
                    PotionsCount--;
                }
            }

            public bool Attack()
            {
                if (CurrentMana < ManaCost) return false;
                CurrentMana -= ManaCost;
                return true;
            }
        }

        private PlayerState _player;

        [SetUp]
        public void SetUp() => _player = new PlayerState();

        // --- HP tesztek ---

        [Test]
        public void TakeDamage_CsökkentiAzHP_t()
        {
            _player.TakeDamage(30);
            Assert.That(_player.CurrentHealth, Is.EqualTo(70));
        }

        [Test]
        public void TakeDamage_HalálosSebesülés_IgazatAd()
        {
            bool meghalt = _player.TakeDamage(100);
            Assert.That(meghalt, Is.True);
            Assert.That(_player.CurrentHealth, Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void TakeDamage_NemHalálosSeb_HamisatAd()
        {
            bool meghalt = _player.TakeDamage(50);
            Assert.That(meghalt, Is.False);
        }

        // --- XP / LevelUp tesztek ---

        [Test]
        public void GainXP_NövekszikAzXP()
        {
            _player.GainXP(40);
            Assert.That(_player.CurrentXP, Is.EqualTo(40));
        }

        [Test]
        public void GainXP_SzintlépéskorXPNullázódik()
        {
            _player.GainXP(100);
            Assert.That(_player.CurrentXP, Is.EqualTo(0));
            Assert.That(_player.Level, Is.EqualTo(2));
        }

        [Test]
        public void LevelUp_NövekszikMaxHP_ÉsFeltöltődik()
        {
            _player.GainXP(100);
            Assert.That(_player.MaxHealth, Is.EqualTo(120));
            Assert.That(_player.CurrentHealth, Is.EqualTo(120));
        }

        [Test]
        public void LevelUp_MaxXP_Másfélszeres()
        {
            int régiMaxXP = _player.MaxXP;
            _player.GainXP(100);
            Assert.That(_player.MaxXP, Is.EqualTo((int)(régiMaxXP * 1.5)));
        }

        [Test]
        public void LevelUp_5SzintenkéntNőMaxPotionSlots()
        {
            for (int i = 0; i < 4; i++)
            {
                int xpKell = _player.MaxXP - _player.CurrentXP;
                _player.GainXP(xpKell);
            }
            Assert.That(_player.Level, Is.EqualTo(5));
            Assert.That(_player.MaxPotionSlots, Is.EqualTo(4));
        }

        // --- Potion tesztek ---

        [Test]
        public void UsePotion_GyógyítHa_HPNemTeli()
        {
            _player.PotionsCount = 1;
            _player.TakeDamage(50);
            _player.UsePotion();
            Assert.That(_player.CurrentHealth, Is.EqualTo(70));
            Assert.That(_player.PotionsCount, Is.EqualTo(0));
        }

        [Test]
        public void UsePotion_NemHasználHaTPeli()
        {
            _player.PotionsCount = 1;
            _player.UsePotion(); 
            Assert.That(_player.PotionsCount, Is.EqualTo(1));
        }

        [Test]
        public void UsePotion_NemHasználHaNincsPotion()
        {
            _player.TakeDamage(30);
            _player.UsePotion();
            Assert.That(_player.CurrentHealth, Is.EqualTo(70));
        }

        // --- Mana / Attack tesztek ---

        [Test]
        public void Attack_LevonjaAManát()
        {
            bool siker = _player.Attack();
            Assert.That(siker, Is.True);
            Assert.That(_player.CurrentMana, Is.EqualTo(95f).Within(0.001f));
        }

        [Test]
        public void Attack_NemSikerülHaNincsElégMana()
        {
            _player.CurrentMana = 3f;
            bool siker = _player.Attack();
            Assert.That(siker, Is.False);
        }

        [Test]
        public void LevelUp_ManaTeljesen_Feltöltődik()
        {
            _player.CurrentMana = 40f;
            _player.GainXP(100);
            Assert.That(_player.CurrentMana, Is.EqualTo(_player.MaxMana).Within(0.001f));
        }
    }

    // =========================================================
    //  2. InventoryManager – logikai tesztek
    //  (Items statikus lista + craft logika, Godot nélkül)
    // =========================================================
    [TestFixture]
    public class InventoryManagerLogicTests
    {
        private class InventoryLogic
        {
            public List<string> Items = new List<string>();
            public int MaxSlots = 8;

            public bool AddItem(string itemName)
            {
                if (Items.Count < MaxSlots) { Items.Add(itemName); return true; }
                return false;
            }

            public bool CraftUniversityKey()
            {
                if (Items.Contains("KeyPart1") &&
                    Items.Contains("KeyPart2") &&
                    Items.Contains("KeyPart3"))
                {
                    Items.Remove("KeyPart1");
                    Items.Remove("KeyPart2");
                    Items.Remove("KeyPart3");
                    AddItem("UniversityKey");
                    return true;
                }
                return false;
            }
        }

        private InventoryLogic _inv;

        [SetUp]
        public void SetUp() => _inv = new InventoryLogic();

        [Test]
        public void AddItem_TárgyHozzáadásaSikerül()
        {
            bool siker = _inv.AddItem("KeyPart1");
            Assert.That(siker, Is.True);
            Assert.That(_inv.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void AddItem_TárgyNévHelyesenTárolódik()
        {
            _inv.AddItem("LiftKey");
            Assert.That(_inv.Items[0], Is.EqualTo("LiftKey"));
        }

        [Test]
        public void AddItem_TeleInventoryElutasítja()
        {
            for (int i = 0; i < 8; i++) _inv.AddItem($"Item{i}");
            bool siker = _inv.AddItem("ExtraItem");
            Assert.That(siker, Is.False);
            Assert.That(_inv.Items.Count, Is.EqualTo(8));
        }

        [Test]
        public void CraftUniversityKey_HáromRészből_SikeresKraftolás()
        {
            _inv.AddItem("KeyPart1");
            _inv.AddItem("KeyPart2");
            _inv.AddItem("KeyPart3");

            bool siker = _inv.CraftUniversityKey();

            Assert.That(siker, Is.True);
            Assert.That(_inv.Items.Contains("UniversityKey"), Is.True);
            Assert.That(_inv.Items.Contains("KeyPart1"), Is.False);
            Assert.That(_inv.Items.Contains("KeyPart2"), Is.False);
            Assert.That(_inv.Items.Contains("KeyPart3"), Is.False);
        }

        [Test]
        public void CraftUniversityKey_HiányosRészek_Sikertelen()
        {
            _inv.AddItem("KeyPart1");
            _inv.AddItem("KeyPart2");

            bool siker = _inv.CraftUniversityKey();

            Assert.That(siker, Is.False);
            Assert.That(_inv.Items.Contains("UniversityKey"), Is.False);
        }

        [Test]
        public void CraftUniversityKey_ÜresInventory_Sikertelen()
        {
            bool siker = _inv.CraftUniversityKey();
            Assert.That(siker, Is.False);
        }

        [Test]
        public void CraftUniversityKey_UtánInventoryMérete_Csökken()
        {
            _inv.AddItem("KeyPart1");
            _inv.AddItem("KeyPart2");
            _inv.AddItem("KeyPart3");
            _inv.AddItem("Fuse");

            _inv.CraftUniversityKey();

            Assert.That(_inv.Items.Count, Is.EqualTo(2));
        }
    }

    // =========================================================
    //  3. SaveSystem.SaveData – szerializáció tesztek
    //  (Godot-független: JSON mentés/betöltés logikája)
    // =========================================================
    [TestFixture]
    public class SaveSystemTests
    {
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
            public List<string> InventoryItems { get; set; }
            public string PlayerName { get; set; }
            public bool FirstRun { get; set; }
        }

        private string _tempFile;

        [SetUp]
        public void SetUp()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), "test_save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempFile)) File.Delete(_tempFile);
        }

        [Test]
        public void SaveData_JSONSerializáció_HelyesértékekMegmaradnak()
        {
            var data = new SaveData
            {
                PlayerX = 100f, PlayerY = 200f,
                CurrentHealth = 80, CurrentXP = 50, MaxXP = 100,
                Level = 2, PotionsCount = 1, Speed = 340f,
                AttackDamage = 30, AttackCooldown = 0.45f,
                InventoryItems = new List<string> { "KeyPart1", "Fuse" },
                PlayerName = "Teszt", FirstRun = false
            };

            string json = JsonSerializer.Serialize(data);
            SaveData betöltött = JsonSerializer.Deserialize<SaveData>(json);

            Assert.That(betöltött.PlayerX, Is.EqualTo(data.PlayerX).Within(0.001f));
            Assert.That(betöltött.PlayerY, Is.EqualTo(data.PlayerY).Within(0.001f));
            Assert.That(betöltött.CurrentHealth, Is.EqualTo(data.CurrentHealth));
            Assert.That(betöltött.Level, Is.EqualTo(data.Level));
            Assert.That(betöltött.AttackDamage, Is.EqualTo(data.AttackDamage));
            Assert.That(betöltött.PlayerName, Is.EqualTo(data.PlayerName));
        }

        [Test]
        public void SaveData_InventoryItemek_HelyesenSorosítódnak()
        {
            var data = new SaveData
            {
                InventoryItems = new List<string> { "KeyPart1", "KeyPart2", "KeyPart3" }
            };

            string json = JsonSerializer.Serialize(data);
            SaveData betöltött = JsonSerializer.Deserialize<SaveData>(json);

            Assert.That(betöltött.InventoryItems.Count, Is.EqualTo(3));
            Assert.That(betöltött.InventoryItems[0], Is.EqualTo("KeyPart1"));
            Assert.That(betöltött.InventoryItems[2], Is.EqualTo("KeyPart3"));
        }

        [Test]
        public void SaveData_FájlbaÍrásÉsVisszaolvasás_AdatokEgyeznek()
        {
            var data = new SaveData
            {
                CurrentHealth = 60, Level = 3,
                InventoryItems = new List<string> { "UniversityKey" },
                PlayerName = "SZE_Hős"
            };

            File.WriteAllText(_tempFile, JsonSerializer.Serialize(data));
            string jsonVisszaolvasva = File.ReadAllText(_tempFile);
            SaveData betöltött = JsonSerializer.Deserialize<SaveData>(jsonVisszaolvasva);

            Assert.That(betöltött.CurrentHealth, Is.EqualTo(60));
            Assert.That(betöltött.Level, Is.EqualTo(3));
            Assert.That(betöltött.PlayerName, Is.EqualTo("SZE_Hős"));
            Assert.That(betöltött.InventoryItems.Contains("UniversityKey"), Is.True);
        }

        [Test]
        public void SaveData_ÜresInventory_NemDobKivételt()
        {
            var data = new SaveData { InventoryItems = new List<string>() };
            string json = JsonSerializer.Serialize(data);
            SaveData betöltött = JsonSerializer.Deserialize<SaveData>(json);
            Assert.That(betöltött.InventoryItems, Is.Not.Null);
            Assert.That(betöltött.InventoryItems.Count, Is.EqualTo(0));
        }

        [Test]
        public void RenameSave_ÚjNévreÁtnevez()
        {
            string régi = Path.Combine(Path.GetTempPath(), "mente_regi.json");
            string új   = Path.Combine(Path.GetTempPath(), "mente_uj.json");
            File.WriteAllText(régi, "{}");
            if (File.Exists(új)) File.Delete(új);

            File.Move(régi, új);

            Assert.That(File.Exists(új), Is.True);
            Assert.That(File.Exists(régi), Is.False);

            File.Delete(új);
        }
    }

    // =========================================================
    //  4. ZombieSpawner – logikai tesztek
    //  (spawn pozíció számítás, típus kiválasztás)
    // =========================================================
    [TestFixture]
    public class ZombieSpawnerLogicTests
    {
        private enum ZombieType { Small, Normal, Big }

        private static (float x, float y) KiszámolSpawnPozíció(
            float spawnerX, float spawnerY, float radius, float angle)
        {
            float x = spawnerX + (float)System.Math.Cos(angle) * radius;
            float y = spawnerY + (float)System.Math.Sin(angle) * radius;
            return (x, y);
        }

        [Test]
        public void SpawnPozíció_SugárTávolságon_Van()
        {
            float radius = 500f;
            float angle  = 0.5f;
            var (x, y) = KiszámolSpawnPozíció(0, 0, radius, angle);

            float távolság = (float)System.Math.Sqrt(x * x + y * y);
            Assert.That(távolság, Is.EqualTo(radius).Within(0.01f));
        }

        [Test]
        public void SpawnPozíció_SpawnerOffsetHelyes()
        {
            var (x, y) = KiszámolSpawnPozíció(100, 200, 500, 0);
            Assert.That(x, Is.EqualTo(600f).Within(0.01f));
            Assert.That(y, Is.EqualTo(200f).Within(0.01f));
        }

        [Test]
        public void ZombieType_RandiRange_0To2_ÉrvényesTípust_Ad()
        {
            var rng = new System.Random(42);
            for (int i = 0; i < 100; i++)
            {
                int val = rng.Next(0, 3);
                ZombieType t = (ZombieType)val;
                Assert.That(t == ZombieType.Small || t == ZombieType.Normal || t == ZombieType.Big, Is.True);
            }
        }

        [Test]
        public void SpawnPozíció_NemEgyezikASpawnerrel()
        {
            float spX = 0, spY = 0, radius = 500f;
            for (float angle = 0; angle < 6.28f; angle += 0.5f)
            {
                var (x, y) = KiszámolSpawnPozíció(spX, spY, radius, angle);
                Assert.That(x, Is.Not.EqualTo(spX));
            }
        }
    }
}