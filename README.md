# SzeGames (HelloTheSzeGame)

A SzeGames egy Godot 4.6 és C# használatával készült, felülnézetes akció-kalandjáték. A játékosnak a Széchenyi István Egyetem zombik által elfoglalt területén kell megszereznie a bejutáshoz szükséges kulcsdarabokat, helyreállítania a földszinti liftet, majd teljesítenie a C100 teremben váró végső feladatot.

## Játékmenet

A történet három egymást követő játéktéren halad végig:

1. `World.tscn`: tutorial, zombik, események és a három kulcsdarab összegyűjtése.
2. `GroundFloor.tscn`: sötét folyosók, földrengés, termek átkutatása, biztosíték és kábel megszerzése, majd a lift megjavítása.
3. `C100.tscn`: párbeszéd és igaz-hamis kérdésekből álló végső próba, amely lezárja a játékot.

A játék tartalmaz közelharcot, eltérő méretű zombikat, tapasztalati pontokat, szintlépést, fejleszthető tulajdonságokat, tárgykezelést, kraftolást, valamint JSON-alapú mentést és betöltést.

## Irányítás

| Művelet | Billentyű vagy egér |
| --- | --- |
| Mozgás | `W`, `A`, `S`, `D` vagy nyílbillentyűk |
| Támadás | Bal egérgomb |
| Gyógyítás | `H` |
| Inventory megnyitása | `E` |
| Interakció, ajtónyitás | `O` |
| Szünet menü megnyitása és bezárása | `Esc` |

A mentés nem automatikusan az ESC megnyomásakor történik: a szünet menü **Mentés** gombját kell használni.

## Követelmények

- Godot Engine `4.6` .NET kiadás
- .NET SDK `8.0`
- Git a forráskód letöltéséhez
- Windows vagy Linux x64 fejlesztői környezet

## Letöltés és fordítás

```powershell
git clone https://github.com/ddaved03/hellotheszegame.git
cd hellotheszegame
dotnet restore .\hellotheszegame.sln
dotnet build .\hellotheszegame.sln
```

Sikeres fordítás után nyisd meg a Godot 4.6 .NET szerkesztőt, importáld a gyökérkönyvtárban található `project.godot` fájlt, majd indítsd el a projektet az `F5` billentyűvel.

## Tesztek futtatása

```powershell
dotnet test .\tests\GameTests.csproj
```

A tesztek külön projektben találhatók. A fő játékprojekt a `tests` könyvtár C# fájljait nem fordítja bele a játékba.

## Projektstruktúra

- `src/`: C# játékmenet- és vezérlőkódok
- `scenes/`: Godot jelenetek és pályák
- `kepek/`: sprite-ok, textúrák és kezelőfelületi elemek
- `audio/`: hangeffektek és a C100 jelenet saját zenéje
- `tests/`: NUnit tesztprojekt
- `saves/`: futás közben létrejövő JSON mentések

## Mentések

A mentési rendszer a játékos statisztikáit, pozícióját, inventoryját, az aktuális jelenetet és a pályák fontos állapotait JSON fájlban tárolja. Mentést a szünet menüből lehet készíteni; a betöltő felületen a mentések kiválaszthatók, átnevezhetők és törölhetők.
