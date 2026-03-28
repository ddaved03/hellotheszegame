using Godot;
using System;
using System.Collections.Generic;

public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    private readonly RandomNumberGenerator _rng = new();
    private readonly Dictionary<string, AudioStream> _streams = new();

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        _rng.Randomize();
        BuildStreams();
    }

    public void PlayUiClick() => PlayGlobal("ui_click", 0.03f);
    public void PlayPlayerAttack(Vector2 position) => Play2D("player_attack", position, 0.05f);
    public void PlayLevelUp() => PlayGlobal("level_up", 0.02f);
    public void PlayZombieAmbient(Vector2 position) => Play2D("zombie_ambient", position, 0.16f);
    public void PlayZombieAttack(Vector2 position) => Play2D("zombie_attack", position, 0.07f);
    public void PlayZombieHit(Vector2 position) => Play2D("zombie_hit", position, 0.10f);
    public void PlayZombieDeath(Vector2 position) => Play2D("zombie_death", position, 0.08f);
    public void PlayDropPotion(Vector2 position) => Play2D("drop_potion", position, 0.04f);
    public void PlayDropXp(Vector2 position) => Play2D("drop_xp", position, 0.06f);
    public void PlayPickupPotion(Vector2 position) => Play2D("pickup_potion", position, 0.05f);
    public void PlayPickupXp(Vector2 position) => Play2D("pickup_xp", position, 0.05f);

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
        _streams["zombie_death"] = CreateTone(95f, 0.65f, 0.40f, false, 1.9f, 0.50f);

        _streams["drop_potion"] = CreateTone(520f, 0.10f, 0.25f, false, 8f, 0.05f);
        _streams["drop_xp"] = CreateTone(760f, 0.10f, 0.22f, false, 9f, 0.0f);

        _streams["pickup_potion"] = LoadOrFallback(
            new[] { "res://audio/pickup_potion.wav" },
            () => CreateTone(420f, 0.13f, 0.28f, false, 6f, 0.03f));
        _streams["pickup_xp"] = LoadOrFallback(
            new[] { "res://audio/pickup_xp.wav" },
            () => CreateTone(980f, 0.12f, 0.25f, false, 8f, 0.0f));
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

        return fallback();
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
}