# SzeGames (HelloTheSzeGame)

Egy 2D felülnézetes RPG / túlélőjáték, amely **Godot Engine 4** (C#) segítségével készült. A játékos célja, hogy túléljen egy zombiapokalipszist az egyetem (SZE) területén, megoldja a bejutáshoz szükséges feladatokat, és felfedezze az épületet.

## 🌟 Főbb funkciók (Features)
* **Karakterfejlődés és RPG elemek:** Tapasztalati pontok (XP gömbök) gyűjtése, szintlépés, HP (életerő), sebzés és támadási sebesség fejlesztése.
* **Inventory és Kraftolás:** Tárgyak (pl. gyógyitalok, kulcsdarabok) felvétele és tárolása. A törött kulcsdarabokból (`KeyPart1`, `KeyPart2`, `KeyPart3`) egyetemi kulcs (`UniversityKey`) kraftolása az ajtók kinyitásához.
* **Mentés és Betöltés (Perzisztencia):** Teljes értékű, JSON-alapú mentési rendszer (`SaveSystem`). Elmenti a játékos statisztikáit, pozícióját és az inventory tartalmát is. Különböző mentési fájlok kezelése, átnevezése és törlése a Főmenüből.
* **Ellenségek és Harcrendszer:** Különböző típusú zombik (`ZombieSmall`, `ZombieBig`), hullámokban támadó ellenfelek (`WaveManager`), és közelharci rendszer.
* **Több Jelenet (Pályák):** Főmenü, Külső udvar / Parkoló (`World.tscn`), Földszinti folyosók (`GroundFloor.tscn`) és specifikus termek (pl. `C100.tscn`).

## 🎮 Irányítás (Controls)
* **W, A, S, D** vagy **Nyilak**: Mozgás
* **Egér bal gomb**: Támadás (közelharc)
* **ESC**: Szünet menü (Pause) és játék mentése
* **E**: Inventory megnyitása

## 📂 Projekt Struktúra
A projekt moduláris felépítést követ:
* `/src/`: A játék logikáját adó C# szkriptek (pl. `BasePlayer.cs`, `InventoryManager.cs`, `WorldController.cs`).
* `/scenes/`: A Godot vizuális jelenetei (`.tscn` fájlok).
* `/kepek/`: Textúrák, sprite-ok, UI elemek és térképek.
* `/audio/`: Hangeffektek (ütés, szintlépés, tárgy felvétel, kattintás).
* `/saves/`: A játék által generált `.json` mentési fájlok helye.

## 🛠️ Telepítés és Futtatás (Fejlesztőknek)
Mivel a játék C# szkripteket használ, a Godot **.NET (C#) verziójára** van szükség a futtatáshoz.

1.  Töltsd le és telepítsd a **Godot 4.x .NET** verzióját a [hivatalos weboldalról](https://godotengine.org/download).
2.  Bizonyosodj meg róla, hogy a gépeden telepítve van a **.NET SDK** (ajánlott a .NET 6.0 vagy 8.0).
3.  Nyisd meg a Godot-t, és importáld a projektet a `project.godot` fájl kiválasztásával.
4.  A jobb felső sarokban (vagy alul az MSBuild fülön) kattints a **Build** gombra, hogy a C# kódok leforduljanak.
5.  Nyomd meg az **F5**-öt (Play) a játék indításához!

## 🎓 Készítők
*A projekt egyetemi beadandó / szkeleton feladatként készült.*
