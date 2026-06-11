using Godot;
using System;
using System.Collections.Generic;

public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    private readonly RandomNumberGenerator _rng = new();
    private readonly Dictionary<string, AudioStream> _streams = new();
    private AudioStreamPlayer _backgroundPlayer;
    private string _currentMusicKey;
    private const float MusicGainDb = 12f;

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 1f;

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
    }

    private bool _initialized;

    public override void _Ready()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        ProcessMode = ProcessModeEnum.Always;
        _rng.Randomize();
        BuildStreams();
        ApplyMasterVolume();
        _initialized = true;
    }

    public void PlayUiClick() => PlayGlobal("ui_click", 0.03f);
    public void PlayPlayerAttack(Vector2 position) => Play2D("player_attack", position, 0.05f);
    public void PlayLevelUp() => PlayGlobal("level_up", 0.02f);
    public void PlayZombieAmbient(Vector2 position) => Play2D("zombie_ambient", position, 0.16f);
    public void PlayZombieAttack(Vector2 position) => Play2D("zombie_attack", position, 0.07f);
    public void PlayZombieHit(Vector2 position) => Play2D("zombie_hit", position, 0.10f);
    public void PlayZombieDeath(Vector2 position) => Play2D("death", position, 0.08f);
    public void PlayDropPotion(Vector2 position) => Play2D("drop_potion", position, 0.04f);
    public void PlayDropXp(Vector2 position) => Play2D("drop_xp", position, 0.06f);
    public void PlayPickupPotion(Vector2 position) => Play2D("pickup_potion", position, 0.05f);
    public void PlayPickupXp(Vector2 position) => Play2D("pickup_xp", position, 0.05f);
    public void PlayKeyPickup(Vector2 position) => Play2D("key_pickup", position, 0.05f);
    public void PlayFootstep(Vector2 position) => Play2D("footstep", position, 0.05f);
    public AudioStreamPlayer PlayBackground()
    {
        if (!_streams.TryGetValue("background", out var stream)) return null;

        if (_backgroundPlayer != null && IsInstanceValid(_backgroundPlayer))
        {
            _backgroundPlayer.QueueFree();
            _backgroundPlayer = null;
        }

        _backgroundPlayer = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = "Master",
            ProcessMode = ProcessModeEnum.Always,
            Autoplay = true
        };

        AddChild(_backgroundPlayer);
        ApplyMusicVolume();
        _backgroundPlayer.Play();
        return _backgroundPlayer;
    }

    public AudioStreamPlayer PlayC100Theme()
    {
        return PlayMusic("c100_theme");
    }

    private AudioStreamPlayer PlayMusic(string key)
    {
        Initialize();

        if (!_streams.TryGetValue(key, out var stream))
        {
            GD.PrintErr($"[AudioManager] Music key not found: {key}");
            return null;
        }

        if (stream == null)
        {
            GD.PrintErr($"[AudioManager] Music stream is null: {key}");
            return null;
        }

        EnableLoop(stream);

        if (_backgroundPlayer != null && IsInstanceValid(_backgroundPlayer) && _currentMusicKey == key)
        {
            ApplyMusicVolume();
            if (!_backgroundPlayer.Playing)
            {
                GD.Print($"[AudioManager] Restarting background music: {key}");
                _backgroundPlayer.Play();
            }

            return _backgroundPlayer;
        }

        if (_backgroundPlayer != null && IsInstanceValid(_backgroundPlayer))
        {
            _backgroundPlayer.QueueFree();
            _backgroundPlayer = null;
        }

        int masterBusIndex = AudioServer.GetBusIndex("Master");
        string targetBus = masterBusIndex >= 0 ? "Master" : string.Empty;
        if (string.IsNullOrEmpty(targetBus))
        {
            GD.PrintErr("[AudioManager] Master audio bus not found; using default bus.");
        }

        _backgroundPlayer = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = targetBus,
            ProcessMode = ProcessModeEnum.Always,
            Autoplay = true
        };

        AddChild(_backgroundPlayer);
        _currentMusicKey = key;
        ApplyMusicVolume();
        GD.Print($"[AudioManager] Playing background music: {key}, Stream={stream.GetType().Name}, VolumeDb={_backgroundPlayer.VolumeDb}");
        _backgroundPlayer.Play();
        return _backgroundPlayer;
    }

    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp(volume, 0f, 1f);
        ApplyMasterVolume();
    }

    public void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp(volume, 0f, 1f);
        ApplyMusicVolume();
    }

    private void ApplyMasterVolume()
    {
        int masterBus = AudioServer.GetBusIndex("Master");
        if (masterBus >= 0)
        {
            AudioServer.SetBusVolumeDb(masterBus, LinearToDb(MasterVolume));
        }
    }

    private void ApplyMusicVolume()
    {
        if (_backgroundPlayer != null && IsInstanceValid(_backgroundPlayer))
        {
            _backgroundPlayer.VolumeDb = 0f;
        }
    }

    public static AudioManager EnsureInstance()
    {
        if (Instance != null)
        {
            Instance.Initialize();
            GD.Print("[AudioManager] EnsureInstance found existing instance.");
            return Instance;
        }

        if (Engine.GetMainLoop() is SceneTree tree)
        {
            GD.Print("[AudioManager] EnsureInstance creating new instance.");
            var audioManager = new AudioManager();
            tree.Root.AddChild(audioManager);
            if (Instance != null)
            {
                Instance.Initialize();
            }
            else
            {
                audioManager.Initialize();
            }
            return Instance;
        }

        GD.PrintErr("[AudioManager] EnsureInstance failed: no SceneTree available.");
        return null;
    }

    private static float LinearToDb(float volume)
    {
        return volume <= 0.0001f ? -80f : (float)(20f * Math.Log10(volume));
    }

    private void BuildStreams()
    {
        _streams["ui_click"] = LoadOrFallback(
            new[] { "res://audio/click.wav" },
            () => CreateTone(1300f, 0.05f, 0.20f, false, 18f, 0.0f));

        // A talált hit.wav több eseményhez is felhasználható eltérő pitch jitterrel.
        _streams["player_attack"] = LoadOrFallback(
            new[] { "res://audio/hit.wav" },
            () => CreateTone(840f, 0.10f, 0.35f, true, 10f, 0.0f));

        _streams["level_up"] = LoadOrFallback(
            new[] { "res://audio/levelup.wav" },
            () => CreateTone(1120f, 0.25f, 0.32f, false, 3f, 0.02f));

        // Zombi hangok: ahol nincs fájl, ott marad a generált fallback.
        _streams["zombie_ambient"] = CreateTone(125f, 0.55f, 0.18f, false, 1.7f, 0.45f);
        _streams["zombie_attack"] = LoadOrFallback(
            new[] { "res://audio/hit.wav" },
            () => CreateTone(320f, 0.12f, 0.42f, true, 9f, 0.28f));
        _streams["zombie_hit"] = LoadOrFallback(
            new[] { "res://audio/hit.wav" },
            () => CreateTone(205f, 0.09f, 0.37f, true, 13f, 0.35f));

        // Use death.* if available
        _streams["death"] = LoadOrFallback(
            new[] { "res://audio/death.wav", "res://audio/death.mp3" },
            () => CreateTone(95f, 0.65f, 0.40f, false, 1.9f, 0.50f));

        // Drops and pickups
        _streams["drop_potion"] = LoadOrFallback(
            new[] { "res://audio/potion.wav" },
            () => CreateTone(520f, 0.10f, 0.25f, false, 8f, 0.05f));
        _streams["drop_xp"] = LoadOrFallback(
            new[] { "res://audio/xp.wav", "res://audio/pickup_xp.wav" },
            () => CreateTone(760f, 0.10f, 0.22f, false, 9f, 0.0f));

        _streams["pickup_potion"] = LoadOrFallback(
            new[] { "res://audio/pickup_potion.wav", "res://audio/potion.wav" },
            () => CreateTone(420f, 0.13f, 0.28f, false, 6f, 0.03f));
        _streams["pickup_xp"] = LoadOrFallback(
            new[] { "res://audio/pickup_xp.wav", "res://audio/xp.wav" },
            () => CreateTone(980f, 0.12f, 0.25f, false, 8f, 0.0f));

        // New mappings by filename
        _streams["background"] = LoadOrFallback(
            new[] { "res://audio/background.wav", "res://audio/background.mp3" },
            () => null);
        _streams["c100_theme"] = LoadOrFallback(
            new[] { "res://audio/paalda_boss_theme_2_with_ending.mp3" },
            () => CreateTone(72f, 4f, 0.03f, false, 0.5f, 0.02f));
        _streams["footstep"] = LoadOrFallback(
            new[] { "res://audio/footstep.wav" },
            () => CreateTone(420f, 0.08f, 0.05f, false, 6f, 0.02f));
        _streams["key_pickup"] = LoadOrFallback(
            new[] { "res://audio/key.wav", "res://audio/key.mp3" },
            () => CreateTone(1500f, 0.08f, 0.18f, false, 3f, 0.0f));
        _streams["door"] = LoadOrFallback(
            new[] { "res://audio/door.wav", "res://audio/door.mp3" },
            () => CreateTone(600f, 0.18f, 0.12f, false, 2f, 0.02f));
        _streams["elevator"] = LoadOrFallback(
            new[] { "res://audio/elevator.wav", "res://audio/elevator.mp3" },
            () => CreateTone(220f, 1.2f, 0.06f, false, 0.6f, 0.04f));
        _streams["zombie_spawn"] = LoadOrFallback(
            new[] { "res://audio/zombi.wav", "res://audio/zombi.mp3", "res://audio/zombie.wav", "res://audio/zombie.mp3" },
            () => CreateTone(180f, 0.45f, 0.14f, false, 1.1f, 0.2f));
        _streams["zombie_footstep"] = LoadOrFallback(
            new[] { "res://audio/zombie_footstep.wav", "res://audio/zombi_footstep.wav", "res://audio/footstep.wav" },
            () => CreateTone(220f, 0.06f, 0.05f, false, 2f, 0.02f));
    }

    private AudioStream LoadOrFallback(string[] candidates, Func<AudioStream> fallback)
    {
        foreach (string path in candidates)
        {
            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            AudioStream stream = ResourceLoader.Load<AudioStream>(path);
            if (stream != null)
            {
                return stream;
            }
        }

        return fallback?.Invoke();
    }

    private static void EnableLoop(AudioStream stream)
    {
        if (stream is AudioStreamMP3 mp3)
        {
            mp3.Loop = true;
        }
        else if (stream is AudioStreamWav wav)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        }
        else if (stream is AudioStreamOggVorbis ogg)
        {
            ogg.Loop = true;
        }
    }

    private AudioStream CreateTone(float frequency, float duration, float volume, bool square, float decay, float noise)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, (int)(sampleRate * duration));
        byte[] data = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Exp(-decay * t);
            float sine = Mathf.Sin(2f * Mathf.Pi * frequency * t);
            float wave = square ? (sine >= 0 ? 1f : -1f) : sine;
            float random = _rng.RandfRange(-1f, 1f);
            float mixed = Mathf.Lerp(wave, random, noise);
            float sampleFloat = Mathf.Clamp(mixed * env * volume, -1f, 1f);

            short sample = (short)(sampleFloat * short.MaxValue);
            data[i * 2] = (byte)(sample & 0xFF);
            data[(i * 2) + 1] = (byte)((sample >> 8) & 0xFF);
        }

        var stream = new AudioStreamWav
        {
            Data = data,
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Stereo = false,
            LoopMode = AudioStreamWav.LoopModeEnum.Disabled
        };

        return stream;
    }

    private void PlayGlobal(string key, float pitchJitter)
    {
        Initialize();

        if (!_streams.TryGetValue(key, out AudioStream stream))
        {
            return;
        }

        var player = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = "Master",
            PitchScale = 1f + _rng.RandfRange(-pitchJitter, pitchJitter),
            ProcessMode = ProcessModeEnum.Always
        };

        AddChild(player);
        player.Finished += () => player.QueueFree();
        player.Play();
    }

    private void Play2D(string key, Vector2 worldPosition, float pitchJitter)
    {
        Initialize();

        if (!_streams.TryGetValue(key, out AudioStream stream))
        {
            return;
        }

        Node parent = GetTree().CurrentScene ?? GetTree().Root;
        var player = new AudioStreamPlayer2D
        {
            Stream = stream,
            Bus = "Master",
            PitchScale = 1f + _rng.RandfRange(-pitchJitter, pitchJitter),
            GlobalPosition = worldPosition,
            ProcessMode = ProcessModeEnum.Always
        };

        parent.AddChild(player);
        player.Finished += () => player.QueueFree();
        player.Play();
    }

    public void PlayDoor(Vector2 position) => Play2D("door", position, 0.02f);
    public void PlayElevator(Vector2 position) => Play2D("elevator", position, 0.02f);
    public void PlayZombieSpawn(Vector2 position) => Play2D("zombie_spawn", position, 0.06f);
    public void PlayZombieFootstep(Vector2 position) => Play2D("zombie_footstep", position, 0.04f);
}
