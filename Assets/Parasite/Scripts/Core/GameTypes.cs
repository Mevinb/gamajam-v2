using System;
using System.Collections.Generic;
using UnityEngine;

namespace Parasite
{
    public enum Profession { Civilian, Guard, Scientist, Maintenance }

    [Flags]
    public enum AccessPermission
    {
        None = 0,
        SecurityGate = 1 << 0,   // guard badge: main gate / security checkpoint
        StaffTerminal = 1 << 1,  // scientist credentials: staff terminal
        ServiceTunnel = 1 << 2,  // maintenance keys: service tunnel
        LabInterior = 1 << 3,    // allowed to be inside the lab without raising suspicion
    }

    public enum EntryRoute { None, MainGate, StaffTerminal, ServiceTunnel }

    public enum HostState { Normal, Possessed, Unconscious, Captured }

    public enum SuspicionLevel { Calm, Curious, Investigating, Pursuing }

    public enum ViolationType
    {
        WitnessedPossession,
        UnconsciousHostFound,
        UnauthorizedAccess,
        BlobSighted,
        Trespassing,
        WitnessedSplit,
    }

    public enum TargetKind { None, Possess, Interact, Eject }

    [Serializable]
    public class ProfessionProfile
    {
        public Profession profession;
        public string title;
        public AccessPermission permissions;
        public string abilityText;
        public float walkSpeed = 2.0f;
        public float runSpeed = 5.0f;
        public Color primary = Color.gray;
        public Color secondary = Color.white;
    }

    public static class ProfessionCatalog
    {
        static readonly Dictionary<Profession, ProfessionProfile> profiles = new()
        {
            [Profession.Civilian] = new ProfessionProfile
            {
                profession = Profession.Civilian, title = "Civilian", permissions = AccessPermission.None,
                abilityText = "Blends in on the street. No restricted access.",
                walkSpeed = 1.9f, runSpeed = 5.2f,
                primary = new Color(0.32f, 0.36f, 0.45f), secondary = new Color(0.20f, 0.20f, 0.24f),
            },
            [Profession.Guard] = new ProfessionProfile
            {
                profession = Profession.Guard, title = "Security Guard",
                permissions = AccessPermission.SecurityGate | AccessPermission.LabInterior,
                abilityText = "Security badge: opens the Main Gate checkpoint.",
                walkSpeed = 2.0f, runSpeed = 5.6f,
                primary = new Color(0.10f, 0.14f, 0.26f), secondary = new Color(0.07f, 0.07f, 0.09f),
            },
            [Profession.Scientist] = new ProfessionProfile
            {
                profession = Profession.Scientist, title = "Scientist",
                permissions = AccessPermission.StaffTerminal | AccessPermission.LabInterior,
                abilityText = "Staff credentials: use the Staff Terminal.",
                walkSpeed = 1.8f, runSpeed = 4.6f,
                primary = new Color(0.92f, 0.93f, 0.94f), secondary = new Color(0.25f, 0.28f, 0.32f),
            },
            [Profession.Maintenance] = new ProfessionProfile
            {
                profession = Profession.Maintenance, title = "Maintenance Worker",
                permissions = AccessPermission.ServiceTunnel | AccessPermission.LabInterior,
                abilityText = "Maintenance keys: open the Service Tunnel.",
                walkSpeed = 1.9f, runSpeed = 5.0f,
                primary = new Color(0.95f, 0.45f, 0.08f), secondary = new Color(0.18f, 0.22f, 0.30f),
            },
        };

        public static ProfessionProfile Get(Profession p) => profiles[p];
    }

    /// <summary>Pure, testable suspicion math.</summary>
    public static class SuspicionRules
    {
        public const float Max = 100f;
        public const int CuriousAt = 30, InvestigatingAt = 60, PursuingAt = 85;
        public const float DecayDelay = 2.5f;     // seconds without observations before decay starts
        public const float DecayPerSecond = 6f;

        public static SuspicionLevel LevelFor(float v) =>
            v >= PursuingAt ? SuspicionLevel.Pursuing :
            v >= InvestigatingAt ? SuspicionLevel.Investigating :
            v >= CuriousAt ? SuspicionLevel.Curious : SuspicionLevel.Calm;

        public static float BaseAmount(ViolationType t) => t switch
        {
            ViolationType.WitnessedPossession => 38f,
            ViolationType.UnconsciousHostFound => 22f,
            ViolationType.UnauthorizedAccess => 16f,
            ViolationType.WitnessedSplit => 30f,
            ViolationType.BlobSighted => 6f,    // per second while in view (scaled by proximity)
            ViolationType.Trespassing => 9f,    // per second while in view
            _ => 10f,
        };

        public static bool IsContinuous(ViolationType t) => t is ViolationType.BlobSighted or ViolationType.Trespassing;

        public static float Add(float current, float amount) => Mathf.Clamp(current + amount, 0f, Max);

        public static float Decay(float current, float timeSinceObserved, float dt) =>
            timeSinceObserved < DecayDelay ? current : Mathf.Max(0f, current - DecayPerSecond * dt);

        public static string Label(SuspicionLevel l) => l switch
        {
            SuspicionLevel.Calm => "CALM",
            SuspicionLevel.Curious => "CURIOUS",
            SuspicionLevel.Investigating => "INVESTIGATING",
            _ => "PURSUIT",
        };
    }

    /// <summary>Pure split cap logic (max two amoebas).</summary>
    public static class SplitRules
    {
        public const int MaxAmoebas = 2;
        public static bool CanSplit(int count) => count < MaxAmoebas;
        public static int NextIndex(int current, int count) => count <= 0 ? -1 : (current + 1) % count;
    }

    public static class AccessRules
    {
        public static bool Has(AccessPermission have, AccessPermission need) =>
            need == AccessPermission.None || (have & need) == need;
    }

    /// <summary>Per-frame input routed to the currently controlled body.</summary>
    public struct ControlInput
    {
        public Vector3 worldMove;   // camera-relative, y = 0, magnitude 0..1
        public bool sprint;
        public bool jump;           // pressed this frame
    }

    public interface IControllable
    {
        Transform Root { get; }
        Transform CameraTarget { get; }
        float CameraDistance { get; }
        Vector3 EyePosition { get; }
        void SetControlInput(in ControlInput input);
        void ClearControlInput();
    }

    public struct InteractContext
    {
        public Possession.AmoebaAgent agent;
        public Characters.HostCharacter host;   // null when acting as a free amoeba
        public AccessPermission permissions;
        public bool IsBlob => host == null;
        public Vector3 position;
    }

    public interface IInteractable
    {
        Vector3 InteractPoint { get; }
        float InteractRange { get; }
        bool IsAvailable { get; }
        string GetPrompt(in InteractContext ctx);
        bool CanInteract(in InteractContext ctx, out string deniedReason);
        void Interact(in InteractContext ctx);
    }

    public static class Interactables
    {
        public static readonly List<IInteractable> All = new();
        public static void Register(IInteractable i) { if (!All.Contains(i)) All.Add(i); }
        public static void Unregister(IInteractable i) => All.Remove(i);
    }

    public struct TargetInfo
    {
        public TargetKind kind;
        public Characters.HostCharacter host;
        public IInteractable interactable;
        public string prompt;
        public bool allowed;
        public Vector3 worldPos;
        public static TargetInfo None => new() { kind = TargetKind.None, prompt = "" };
    }

    public struct EndingData
    {
        public EntryRoute route;
        public float timeSeconds;
        public int hostsPossessed;
        public float peakSuspicion;
        public int captures;
    }

    public static class Layers
    {
        public static int Characters => Get("Characters");
        public static int Amoeba => Get("Amoeba");
        public static int Interactable => Get("Interactable");
        static int Get(string n) { int l = LayerMask.NameToLayer(n); return l < 0 ? 0 : l; }
        public static int EnvironmentMask => ~((1 << Characters) | (1 << Amoeba) | (1 << 2) | (1 << 5));
    }

    /// <summary>Global decoupled event hub used by UI/FX/audio.</summary>
    public static class GameEvents
    {
        public static event Action<string, Color> Toast;
        public static event Action ControlChanged;          // active amoeba / body changed, split, possess, release
        public static event Action<string> ObjectiveChanged;
        public static event Action<string, string> Subtitle; // speaker, line (empty line clears)
        public static event Action<EndingData> Ending;
        public static event Action<bool> PauseChanged;
        public static event Action<Vector3> PossessionSplash;
        public static event Action Captured;

        public static void RaiseToast(string msg, Color c) => Toast?.Invoke(msg, c);
        public static void RaiseToast(string msg) => Toast?.Invoke(msg, new Color(0.75f, 1f, 0.6f));
        public static void RaiseControlChanged() => ControlChanged?.Invoke();
        public static void RaiseObjective(string text) => ObjectiveChanged?.Invoke(text);
        public static void RaiseSubtitle(string speaker, string line) => Subtitle?.Invoke(speaker, line);
        public static void RaiseEnding(EndingData d) => Ending?.Invoke(d);
        public static void RaisePause(bool p) => PauseChanged?.Invoke(p);
        public static void RaisePossessionSplash(Vector3 p) => PossessionSplash?.Invoke(p);
        public static void RaiseCaptured() => Captured?.Invoke();

        public static void ClearAll()
        {
            Toast = null; ControlChanged = null; ObjectiveChanged = null; Subtitle = null;
            Ending = null; PauseChanged = null; PossessionSplash = null; Captured = null;
        }
    }

    public static class Palette
    {
        public static readonly Color Amoeba = new(0.35f, 1f, 0.35f);
        public static readonly Color Warning = new(1f, 0.75f, 0.2f);
        public static readonly Color Danger = new(1f, 0.25f, 0.2f);
        public static readonly Color Info = new(0.6f, 0.85f, 1f);
    }
}
