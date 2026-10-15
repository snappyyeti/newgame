using System;
using System.Collections.Generic;

namespace Titan.Core
{
    public sealed class TitanConfig
    {
        public int TraySize = 3;
        public int LineSlots = 5;
        /// <summary>X position of each firing slot, in cube units, used to pick the nearest target.</summary>
        public float SlotSpacing = 2.4f;

        public float FireInterval = 0.23f;
        public float DeployDelay = 0.25f;

        /// <summary>The titan's front starts this far from the wall (in cubes) and loses when it reaches 0.</summary>
        public float StartDistance = 24f;
        public float StepInterval = 1.0f;

        // ------------------------------------------------------------ titan clock (DESIGN.md 4.2.5, CORE-3 B)
        /// <summary>
        /// Approach time of titan n = max(MinApproachSeconds, (base + perCube × cubes) × max(MinApproachFactor, ApproachSpeedup^(n−1))),
        /// with perCube = TutorialSecondsPerCube for the first TutorialTitans titans. n is the titan number, not its build level.
        /// </summary>
        public float ApproachBaseSeconds = 4f;
        public float ApproachSecondsPerCube = 0.06f;
        public float ApproachSpeedup = 0.992f;
        public float MinApproachSeconds = 12f;
        public float MinApproachFactor = 0.6f;
        public int TutorialTitans = 3;
        public float TutorialSecondsPerCube = 0.11f;

        /// <summary>Every fallen piece pushes the titan back by clamp(cubes × per cube, min, max) (never past the start).</summary>
        public float KnockbackPerCube = 0.03f;
        public float KnockbackMin = 0.3f;
        public float KnockbackMax = 1.5f;
        /// <summary>…and delays its next step by min(StunMax, StunBase + StunPerCube × cubes) seconds.</summary>
        public float StunBase = 0.5f;
        public float StunPerCube = 0.01f;
        public float StunMax = 1.0f;

        /// <summary>"LA MUSTAȚĂ!": a titan defeated within this fraction of the start distance gets its defeat bonus × mult.</summary>
        public float NearMissFraction = 0.15f;
        public float NearMissBonusMult = 1.5f;

        // ------------------------------------------------------------ cycles (DESIGN.md 5.1, CORE-3 C)
        /// <summary>Titan n is built at a level from the 5-titan cycle (warm, build, push, SPRINT, BOSS) instead of level n.</summary>
        public bool UseCycles = true;
        public int BossBonusMult = 2;
        /// <summary>Boss titans use one more color than their build level (not validated in the sim; off).</summary>
        public bool BossExtraColor = false;

        public int PointsPerShot = 10;
        public int PointsPerFallenCube = 25;

        // ------------------------------------------------------------ cut scoring and stars (DESIGN.md 6.2-6.3, CORE-5)
        /// <summary>A piece is clean when it still has at least this fraction of its part's original cubes.</summary>
        public float CleanCutThreshold = 0.8f;
        /// <summary>Piece points = cubes × PointsPerFallenCube × (clean ? CleanCutMult : 1) × min(chain, ChainMaxMult).</summary>
        public int CleanCutMult = 2;
        /// <summary>Pieces at most this many seconds apart extend the chain ("COMBO ×N").</summary>
        public float ChainWindowSeconds = 4f;
        public int ChainMaxMult = 5;
        /// <summary>Star 3 "Linie curată": never more than this many cannons on the wall during the titan.</summary>
        public int CleanLineMax = 4;
        public int DefeatBonusPerLevel = 200;
        public int PointsPerLeftoverAmmo = 2;

        /// <summary>Chance that a drawn cannon has a color that is visible right now (the rest is pure luck).</summary>
        public float VisibleColorBias = 0.65f;
        public int[] AmmoSizes = { 8, 12, 16, 20, 24 };

        // ------------------------------------------------------------ tray (DESIGN.md 4.2.4, CORE-3 A)
        /// <summary>PerSlot: a deployed cannon is replaced at once (conveyor). Batch: 3 new cannons when all 3 are used (A/B and tests only).</summary>
        public TrayRefill TrayRefill = TrayRefill.PerSlot;
        /// <summary>The tray never holds 3 cannons of one color while the deck has another color.</summary>
        public bool NoTripleColor = true;

        /// <summary>
        /// Salvage (DESIGN.md 4.2.2): ammo that has nothing left to hit leaves with the fallen piece. Line cannons whose
        /// color is gone fly off ("RECUPERAT"), surplus ammo comes off the deck and then the tray. Also the ship gate of
        /// the rescue ads (<see cref="TitanGame.SalvageActive"/>).
        /// </summary>
        public bool SalvageEnabled = true;
        /// <summary>Points per salvaged shot taken from a line or tray cannon (deck salvage is silent and gives nothing).</summary>
        public int PointsPerSalvagedAmmo = 5;

        /// <summary>Which visible cube a cannon shoots first (DESIGN.md 4.2.3).</summary>
        public TargetOrder TargetOrder = TargetOrder.FarthestFirst;

        /// <summary>"BLOCAT!" (DESIGN.md 4.2.9): nothing fires and no useful tap exists for this long → the run ends.</summary>
        public float DeadlockSeconds = 1.5f;

        /// <summary>X position of firing slot <paramref name="slot"/> (the line is centered on X = 0).</summary>
        public float SlotX(int slot) => (slot - (LineSlots - 1) / 2f) * SlotSpacing;

        public float ApproachSeconds(int titan, int cubes)
        {
            float perCube = titan <= TutorialTitans ? TutorialSecondsPerCube : ApproachSecondsPerCube;
            float factor = Math.Max(MinApproachFactor, (float)Math.Pow(ApproachSpeedup, titan - 1));
            return Math.Max(MinApproachSeconds, (ApproachBaseSeconds + perCube * cubes) * factor);
        }

        /// <summary>Position of titan n in its 5-titan cycle: 0 warm, 1 build, 2 push, 3 sprint, 4 boss.</summary>
        public static int CyclePosition(int titan) => (Math.Max(1, titan) - 1) % 5;
        /// <summary>0-based cycle of titan n.</summary>
        public static int CycleIndex(int titan) => (Math.Max(1, titan) - 1) / 5;

        /// <summary>The MonsterBuilder level titan n is built at (DESIGN.md 5.1); n itself without cycles.</summary>
        public int BuildLevel(int titan)
        {
            if (!UseCycles) return titan;
            int c = CycleIndex(titan);
            switch (CyclePosition(titan))
            {
                case 0: return Math.Min(1 + c, 5);
                case 1: return Math.Min(2 + c, 6);
                case 2: return Math.Min(3 + c, 6);
                case 3: return c >= 2 ? 2 : 1;
                default: return Math.Min(4 + c, 8);
            }
        }

        public TitanRole RoleOf(int titan)
        {
            if (!UseCycles) return TitanRole.Normal;
            int p = CyclePosition(titan);
            return p == 3 ? TitanRole.Sprint : p == 4 ? TitanRole.Boss : TitanRole.Normal;
        }
    }

    public enum TrayRefill
    {
        Batch,
        PerSlot,
    }

    public enum TitanRole
    {
        Normal,
        Sprint,
        Boss,
    }

    /// <summary>Why a tap on a tray cannon is refused (DESIGN.md 4.2.8).</summary>
    public enum DeployBlockReason
    {
        None,
        /// <summary>All 5 slots are taken.</summary>
        LineFull,
        /// <summary>It would fill the last slot with a cannon that has nothing to shoot while nothing else shoots (a sure loss).</summary>
        WouldJam,
        /// <summary>No cannon there, an invalid slot, or the game is over.</summary>
        NotAvailable,
    }

    public sealed class Cannon
    {
        public int Color;
        public int Ammo;
        public float Cooldown;

        public Cannon(int color, int ammo)
        {
            Color = color;
            Ammo = ammo;
        }

        public override string ToString() => $"c{Color}x{Ammo}";
    }

    public enum EventKind
    {
        Deployed,       // TraySlot -> Slot
        Shot,           // Slot shot Cube (Color)
        CannonEmpty,    // Slot ran out of ammo and leaves
        PieceFell,      // Piece broke off, Points
        Step,           // the titan took a step, Distance
        TitanDefeated,  // Level, Points (bonus)
        TitanSpawned,   // Level, Role
        TrayRefilled,   // TraySlot (-1 = every slot, Batch refill or a new titan)
        GameOver,       // Reason
        Salvaged,       // Slot (line cannon leaves) or TraySlot (tray cannon loses Ammo); Color, Ammo, Points
        Staggered,      // a fallen piece knocked the titan back: Distance (new), Piece
        Revived,        // "A doua suflare": the lost run continues (Distance, Ammo = idle line cannons sent back to the deck)
        SlotAdded,      // "+1 Loc": Slot (the 6th firing slot, index LineSlots) opens until the titan falls
    }

    public enum LoseReason
    {
        None,
        WallReached,
        SlotsFull,
        /// <summary>"BLOCAT!": nothing could fire and no useful tap existed for DeadlockSeconds.</summary>
        Deadlock,
        /// <summary>A run with a fixed number of titans (Titanul Zilei) was won: titan #MaxTitans fell. Shown as a win.</summary>
        Completed,
    }

    public sealed class GameEvent
    {
        public EventKind Kind;
        public int Slot = -1;
        public int TraySlot = -1;
        public Int3 Cube;
        public int Color;
        public FallenPiece Piece;
        public int Points;
        public int Level;
        public float Distance;
        public LoseReason Reason;
        /// <summary>Salvaged: the ammo taken away (a line cannon: all it had left).</summary>
        public int Ammo;
        /// <summary>TitanSpawned and TitanDefeated: the titan's role in its cycle.</summary>
        public TitanRole Role;
        /// <summary>TitanDefeated: defeated "la mustață" (bonus × NearMissBonusMult).</summary>
        public bool NearMiss;
        /// <summary>PieceFell: the piece kept ≥ CleanCutThreshold of its part (× CleanCutMult).</summary>
        public bool Clean;
        /// <summary>PieceFell: the chain count of this piece (1 = no chain); the multiplier is min(Chain, ChainMaxMult).</summary>
        public int Chain;
        /// <summary>PieceFell: the body part of the piece (<see cref="FallenPiece.Part"/>).</summary>
        public int Part;
        /// <summary>PieceFell: the total multiplier applied to the piece's cubes (clean × chain).</summary>
        public int Mult;
        /// <summary>TitanDefeated: star bitmask (<see cref="Stars"/>).</summary>
        public int Stars;
        /// <summary>TitanDefeated: the most cannons that stood on the wall at once during this titan.</summary>
        public int MaxLine;
        /// <summary>TitanSpawned: the run score at that moment (what a checkpoint stores).</summary>
        public int Score;
    }

    /// <summary>The titan grade (DESIGN.md 6.3): one bit per star. The stars add no multiplier.</summary>
    public static class Stars
    {
        /// <summary>The titan was defeated.</summary>
        public const int Defeated = 1;
        /// <summary>"Decapitare": a clean head piece fell during this titan.</summary>
        public const int Decapitation = 2;
        /// <summary>"Linie curată": never more than CleanLineMax cannons on the wall during this titan.</summary>
        public const int CleanLine = 4;

        public static int Count(int mask) => (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);
    }

    /// <summary>
    /// The whole game as a deterministic simulation: the titan steps toward the wall, cannons on the firing line
    /// shoot visible cubes of their color, loose pieces fall, and a bigger titan follows each defeated one.
    /// The Unity side calls <see cref="Tick"/> every frame and animates the events.
    /// </summary>
    public sealed class TitanGame
    {
        public readonly TitanConfig Config;
        /// <summary>The stream every random choice uses. Seeded runs replace it at every spawn (one stream per titan).</summary>
        IRandom rng;
        readonly bool seeded;
        readonly List<Cannon> deck = new List<Cannon>();

        public Monster Monster { get; private set; }
        public int Level { get; private set; }
        public float Distance { get; private set; }
        public int Score { get; private set; }
        public LoseReason Lost { get; private set; }
        public bool IsOver => Lost != LoseReason.None;
        public readonly Cannon[] Tray;
        /// <summary>
        /// The firing line: LineSlots + 1 entries, of which only the first <see cref="ActiveSlots"/> are in play. The
        /// last one is the "+1 Loc" slot (ADS-2), open only after <see cref="GrantExtraSlot"/> until the titan falls.
        /// Every rule loops over 0..ActiveSlots-1.
        /// </summary>
        public readonly Cannon[] Line;
        /// <summary>Firing slots in play: LineSlots, or LineSlots + 1 after <see cref="GrantExtraSlot"/> (resets at every spawn).</summary>
        public int ActiveSlots { get; private set; }
        public readonly List<GameEvent> Events = new List<GameEvent>();

        float stepTimer;
        float stepLength;
        float stuckTimer;

        /// <summary>Optional: titan n comes from here instead of MonsterBuilder when it returns non-null (scripted titans, tests).</summary>
        readonly Func<int, Monster> titanSource;

        /// <summary>How this run differs from the endless game (FTUE titan, Titanul Zilei), or null.</summary>
        public RunPlan Plan { get; }
        /// <summary>0 = endless; otherwise the run is won (LoseReason.Completed) when titan #MaxTitans falls.</summary>
        public int MaxTitans => Plan != null ? Plan.MaxTitans : 0;

        /// <summary>
        /// While true the titan does not walk (its step clock stands still). Set at the spawn of a titan the plan holds
        /// (the FTUE titan, the first daily titan) and cleared by the first deploy (FTUE-1, RET-3).
        /// </summary>
        public bool HoldApproach { get; set; }

        /// <summary>One random stream for the whole run (tests, the simulator).</summary>
        public TitanGame(TitanConfig config, IRandom rng, int startLevel = 1, Func<int, Monster> titanSource = null, RunPlan plan = null)
        {
            Config = config;
            this.rng = rng;
            this.titanSource = titanSource;
            Plan = plan;
            Tray = new Cannon[config.TraySize];
            Line = new Cannon[config.LineSlots + 1];
            ActiveSlots = config.LineSlots;
            StartTitan = startLevel;
            SpawnTitan(startLevel);
        }

        /// <summary>
        /// A seeded run (DESIGN.md 9.2, RET-1): titan n builds its shape, its deck and every tray draw from
        /// new SeededRandom(Derive(runSeed, n)), so the same seed gives the same titan n whatever happened before.
        /// A checkpoint restarts at <paramref name="startTitan"/> with the score it saved.
        /// </summary>
        public TitanGame(TitanConfig config, long runSeed, int startTitan = 1, int startScore = 0, Func<int, Monster> titanSource = null, RunPlan plan = null)
        {
            Config = config;
            seeded = true;
            RunSeed = runSeed;
            this.titanSource = titanSource;
            Plan = plan;
            Tray = new Cannon[config.TraySize];
            Line = new Cannon[config.LineSlots + 1];
            ActiveSlots = config.LineSlots;
            StartTitan = Math.Max(1, startTitan);
            Score = Math.Max(0, startScore);
            SpawnTitan(StartTitan);
        }

        /// <summary>The run seed of a seeded run (0 for a run built on one IRandom).</summary>
        public long RunSeed { get; }
        public bool IsSeeded => seeded;
        /// <summary>The titan this run started at (1, or a checkpoint).</summary>
        public int StartTitan { get; }

        // ---------------------------------------------------------------- cut scoring state (CORE-5)

        /// <summary>Seconds of simulated play (the sum of every Tick's dt).</summary>
        public float GameTime { get; private set; }
        /// <summary>GameTime of the last fallen piece (−∞ before the first piece of a titan).</summary>
        public float LastPieceTime { get; private set; } = float.NegativeInfinity;
        /// <summary>The current chain (0 before the first piece of a titan; resets at each spawn).</summary>
        public int Chain { get; private set; }
        /// <summary>Seconds left in the chain window, 0 when it is closed.</summary>
        public float ChainTimeLeft => Chain > 0 ? Math.Max(0f, Config.ChainWindowSeconds - (GameTime - LastPieceTime)) : 0f;

        /// <summary>The most cannons on the wall at once during the current titan (star 3).</summary>
        public int MaxOccupiedLine { get; private set; }
        /// <summary>A clean head piece fell during the current titan (star 2).</summary>
        public bool DecapitatedThisTitan { get; private set; }
        /// <summary>Star 3 is still possible for the current titan.</summary>
        public bool CleanLineAlive => MaxOccupiedLine <= Config.CleanLineMax;

        // Run stats.
        public int MaxPiecePoints { get; private set; }
        public int MaxChain { get; private set; }
        public int PiecesCut { get; private set; }
        /// <summary>Clean head pieces ("DECAPITARE!").</summary>
        public int HeadsCut { get; private set; }
        public int CleanCuts { get; private set; }
        /// <summary>Titans defeated in this run, and the stars they earned.</summary>
        public int TitansDefeated { get; private set; }
        public int StarsEarned { get; private set; }

        /// <summary>
        /// True while cutting this part off now would be clean: it still has ≥ CleanCutThreshold of its cubes and is
        /// attached (the joint stitch shows a gold rim). Always false for the body, which never falls.
        /// </summary>
        public bool IsCleanEligible(int part)
        {
            if (part <= Monster.Body) return false;
            int original = Monster.OriginalPartSize(part);
            int left = Monster.RemainingPartSize(part);
            return original > 0 && left > 0 && IsCleanCount(left, original);
        }

        /// <summary>cubes ≥ CleanCutThreshold × original, with a tolerance so 0.8f × 10 counts 8 cubes as clean.</summary>
        bool IsCleanCount(int cubes, int original) => original > 0 && cubes >= (double)Config.CleanCutThreshold * original - 1e-3;

        /// <summary>Distance covered per step, fixed when the titan spawns.</summary>
        public float StepLength => stepLength;

        /// <summary>Seconds until the titan's next step (longer after a cut: the stun of DESIGN.md 4.2.6).</summary>
        public float NextStepIn => Config.StepInterval - stepTimer;

        /// <summary>Seconds the current titan needs to reach the wall from its spawn point.</summary>
        public float ApproachSeconds { get; private set; }
        public float SlotX(int slot) => Config.SlotX(slot);
        public int DeckCount => deck.Count;

        /// <summary>The level the current titan was built at (DESIGN.md 5.1); equals <see cref="Level"/> without cycles.</summary>
        public int BuildLevel { get; private set; }
        /// <summary>The current titan's role in its 5-titan cycle.</summary>
        public TitanRole Role { get; private set; }
        /// <summary>How close the titan is: 0 at spawn, 1 at the wall.</summary>
        public float Danger => 1f - Distance / Config.StartDistance;
        /// <summary>Seconds the deadlock condition has held (DESIGN.md 4.2.9); the run ends at DeadlockSeconds.</summary>
        public float StuckSeconds => stuckTimer;

        /// <summary>Ship gate for the rescue ads (ADS-2): they exist only while salvage is on (DESIGN.md 11.7).</summary>
        public bool SalvageActive => Config.SalvageEnabled;

        /// <summary>Shots of this color still available: deck, tray and firing line.</summary>
        public int AmmoAvailable(int color)
        {
            int n = 0;
            foreach (var c in deck) if (c.Color == color) n += c.Ammo;
            foreach (var c in Tray) if (c != null && c.Color == color) n += c.Ammo;
            for (int s = 0; s < ActiveSlots; s++) if (Line[s] != null && Line[s].Color == color) n += Line[s].Ammo;
            return n;
        }

        /// <summary>Shots of this color still in the (hidden) deck.</summary>
        public int DeckAmmo(int color)
        {
            int n = 0;
            foreach (var c in deck) if (c.Color == color) n += c.Ammo;
            return n;
        }

        /// <summary>Shots of this color in the tray.</summary>
        public int TrayAmmo(int color)
        {
            int n = 0;
            foreach (var c in Tray) if (c != null && c.Color == color) n += c.Ammo;
            return n;
        }

        public bool HasFreeSlot
        {
            get
            {
                for (int s = 0; s < ActiveSlots; s++) if (Line[s] == null) return true;
                return false;
            }
        }

        public int FreeSlots
        {
            get
            {
                int n = 0;
                for (int s = 0; s < ActiveSlots; s++) if (Line[s] == null) n++;
                return n;
            }
        }

        /// <summary>
        /// Why a tap on this tray cannon would be refused (DESIGN.md 4.2.8). WouldJam: exactly one slot is free, no
        /// line cannon has a visible cube of its color, and neither has this one, so deploying it would end the run.
        /// </summary>
        public DeployBlockReason DeployBlock(int traySlot)
        {
            if (IsOver || traySlot < 0 || traySlot >= Tray.Length || Tray[traySlot] == null) return DeployBlockReason.NotAvailable;
            int free = FreeSlots;
            if (free == 0) return DeployBlockReason.LineFull;
            if (free == 1 && !AnyLineCannonFiring() && !Monster.HasVisibleOfColor(Tray[traySlot].Color)) return DeployBlockReason.WouldJam;
            return DeployBlockReason.None;
        }

        public bool CanDeploy(int traySlot) => DeployBlock(traySlot) == DeployBlockReason.None;

        /// <summary>True when some line cannon has a visible cube of its color.</summary>
        bool AnyLineCannonFiring()
        {
            for (int s = 0; s < ActiveSlots; s++)
                if (Line[s] != null && Monster.HasVisibleOfColor(Line[s].Color)) return true;
            return false;
        }

        bool AnyTrayCannonVisible()
        {
            foreach (var c in Tray)
                if (c != null && Monster.HasVisibleOfColor(c.Color)) return true;
            return false;
        }

        /// <summary>Moves a tray cannon onto the first free slot of the firing line.</summary>
        public bool Deploy(int traySlot)
        {
            if (!CanDeploy(traySlot)) return false;
            int slot = FirstFreeSlot();
            var cannon = Tray[traySlot];
            Tray[traySlot] = null;
            cannon.Cooldown = Config.DeployDelay;
            Line[slot] = cannon;
            HoldApproach = false; // the first tap starts the titan's walk
            Events.Add(new GameEvent { Kind = EventKind.Deployed, Slot = slot, TraySlot = traySlot, Color = cannon.Color });
            TrackLine();

            if (Config.TrayRefill == TrayRefill.PerSlot)
            {
                // Conveyor: a new cannon takes its place at once (an empty deck leaves the slot empty: "pachet gol").
                Tray[traySlot] = Draw(traySlot);
                Events.Add(new GameEvent { Kind = EventKind.TrayRefilled, TraySlot = traySlot });
            }
            else
            {
                bool trayEmpty = true;
                foreach (var c in Tray) if (c != null) trayEmpty = false;
                if (trayEmpty) RefillTray();
            }

            CheckSlotsFull();
            return true;
        }

        /// <summary>Cubes of each color a cannon could shoot right now (index = color). A fresh copy; O(colors).</summary>
        public int[] VisibleCountByColor()
        {
            var counts = new int[MonsterBuilder.MaxColors];
            for (int color = 0; color < counts.Length; color++) counts[color] = Monster.VisibleCountOfColor(color);
            return counts;
        }

        /// <summary>Colors that still have cubes but none of them visible (the "? culori ascunse" chip).</summary>
        public int HiddenOnlyColorCount()
        {
            var visible = VisibleCountByColor();
            int n = 0;
            for (int color = 0; color < MonsterBuilder.MaxColors; color++)
                if (Monster.CountOfColor(color) > 0 && visible[color] == 0) n++;
            return n;
        }


        public void Tick(float dt)
        {
            if (IsOver) return;
            GameTime += dt;
            TrackLine();

            // The titan steps toward the wall (not while it waits for the first tap).
            if (!HoldApproach) stepTimer += dt;
            while (stepTimer >= Config.StepInterval && !IsOver)
            {
                stepTimer -= Config.StepInterval;
                Distance = Math.Max(0f, Distance - StepLength);
                Events.Add(new GameEvent { Kind = EventKind.Step, Distance = Distance, Level = Level });
                if (Distance <= 0f) Lose(LoseReason.WallReached);
            }
            if (IsOver) return;

            // Cannons on the line shoot.
            for (int s = 0; s < ActiveSlots; s++)
            {
                var cannon = Line[s];
                if (cannon == null) continue;
                cannon.Cooldown -= dt;
                while (cannon.Cooldown <= 0f && cannon.Ammo > 0)
                {
                    if (!Monster.PickTarget(cannon.Color, SlotX(s), out var target))
                    {
                        cannon.Cooldown = 0f; // waits, ready to shoot as soon as its color shows up
                        break;
                    }
                    Shoot(s, cannon, target);
                    if (s >= Config.LineSlots) ExtraSlotShots++;
                    cannon.Cooldown += Config.FireInterval;
                    if (Monster.Count == 0 || Line[s] != cannon) break; // salvage may have taken it off the line
                }
                if (Line[s] == cannon && cannon.Ammo == 0)
                {
                    Line[s] = null;
                    Events.Add(new GameEvent { Kind = EventKind.CannonEmpty, Slot = s, Color = cannon.Color });
                }
                if (Monster.Count == 0) break;
            }

            if (Monster.Count == 0)
            {
                DefeatTitan();
                return;
            }

            CheckSlotsFull();
            UpdateDeadlock(dt);
        }

        /// <summary>
        /// "BLOCAT!" (DESIGN.md 4.2.9): no line cannon has a visible color and there is no useful tap (0 free slots, or
        /// 1 free slot and no tray cannon with a visible color; 2+ free slots are never stuck). Held for DeadlockSeconds,
        /// the run ends. Nothing can change by itself in that state: nothing fires, so nothing new becomes visible.
        /// </summary>
        void UpdateDeadlock(float dt)
        {
            if (IsOver) return;
            int free = FreeSlots;
            bool stuck = !AnyLineCannonFiring() && (free == 0 || (free == 1 && !AnyTrayCannonVisible()));
            if (!stuck)
            {
                stuckTimer = 0f;
                return;
            }
            stuckTimer += dt;
            if (stuckTimer >= Config.DeadlockSeconds - 1e-4f) Lose(LoseReason.Deadlock); // float sums of dt must not add a frame
        }

        void Shoot(int slot, Cannon cannon, Int3 target)
        {
            var pieces = Monster.Remove(target);
            cannon.Ammo--;
            Score += Config.PointsPerShot;
            Events.Add(new GameEvent { Kind = EventKind.Shot, Slot = slot, Cube = target, Color = cannon.Color, Points = Config.PointsPerShot });
            foreach (var piece in pieces) ScorePiece(slot, piece);
            if (pieces.Count == 0 || Monster.Count == 0) return; // an empty titan is defeated instead (no knockback, leftover bonus)
            foreach (var piece in pieces) Knockback(piece);
            Salvage();
        }

        /// <summary>
        /// The one in-titan multiplier model (DESIGN.md 6.2): cubes × 25 × (clean ? 2 : 1) × min(chain, 5). A piece is
        /// clean when it kept ≥ 80% of its part's original cubes; pieces ≤ 4 s apart extend the chain (pieces of one
        /// shot too, in order).
        /// </summary>
        void ScorePiece(int slot, FallenPiece piece)
        {
            int part = piece.Part;
            bool clean = part > Monster.Body && IsCleanCount(piece.Count, Monster.OriginalPartSize(part));
            Chain = GameTime - LastPieceTime <= Config.ChainWindowSeconds + 1e-4f ? Chain + 1 : 1;
            LastPieceTime = GameTime;
            int mult = (clean ? Config.CleanCutMult : 1) * Math.Min(Chain, Config.ChainMaxMult);
            int points = piece.Count * Config.PointsPerFallenCube * mult;
            Score += points;
            PiecesCut++;
            if (clean) CleanCuts++;
            if (clean && part == Monster.Head)
            {
                HeadsCut++;
                DecapitatedThisTitan = true;
            }
            MaxPiecePoints = Math.Max(MaxPiecePoints, points);
            MaxChain = Math.Max(MaxChain, Chain);
            Events.Add(new GameEvent { Kind = EventKind.PieceFell, Slot = slot, Piece = piece, Points = points, Clean = clean, Chain = Chain, Part = part, Mult = mult });
        }

        void TrackLine()
        {
            int occupied = ActiveSlots - FreeSlots;
            if (occupied > MaxOccupiedLine) MaxOccupiedLine = occupied;
        }

        /// <summary>The cut buys time (DESIGN.md 4.2.6): the titan staggers back and its next step comes later.</summary>
        void Knockback(FallenPiece piece)
        {
            float back = Math.Min(Config.KnockbackMax, Math.Max(Config.KnockbackMin, piece.Count * Config.KnockbackPerCube));
            Distance = Math.Min(Config.StartDistance, Distance + back);
            stepTimer -= Math.Min(Config.StunMax, Config.StunBase + Config.StunPerCube * piece.Count);
            Events.Add(new GameEvent { Kind = EventKind.Staggered, Distance = Distance, Piece = piece, Level = Level });
        }

        /// <summary>
        /// Takes away ammo that has nothing left to hit (DESIGN.md 4.2.2). Runs after a piece falls and before every
        /// slots-full check, so after every Tick: no line cannon has a vanished color, and for every color
        /// deck + tray ammo &lt;= cubes left (in fact deck + tray + line ammo == cubes left whenever deck or tray still
        /// holds that color).
        /// </summary>
        void Salvage()
        {
            if (!Config.SalvageEnabled || Monster.Count == 0) return;
            bool trayEmptied = false;

            // i) Line cannons whose color is gone fly off the wall, with points.
            for (int s = 0; s < ActiveSlots; s++)
            {
                var c = Line[s];
                if (c == null || c.Ammo <= 0 || Monster.CountOfColor(c.Color) > 0) continue; // Ammo 0: Tick retires it
                int points = c.Ammo * Config.PointsPerSalvagedAmmo;
                Score += points;
                Events.Add(new GameEvent { Kind = EventKind.Salvaged, Slot = s, Color = c.Color, Ammo = c.Ammo, Points = points });
                Line[s] = null;
            }

            // One pass over the deck and the tray: this runs every tick (CheckSlotsFull).
            var deckBy = salvageDeck;
            var trayBy = salvageTray;
            Array.Clear(deckBy, 0, deckBy.Length);
            Array.Clear(trayBy, 0, trayBy.Length);
            foreach (var c in deck) deckBy[c.Color] += c.Ammo;
            foreach (var c in Tray) if (c != null) trayBy[c.Color] += c.Ammo;
            for (int color = 0; color < MonsterBuilder.MaxColors; color++)
            {
                int deckAmmo = deckBy[color], trayAmmo = trayBy[color], lineAmmo = 0;
                if (deckAmmo + trayAmmo == 0) continue;
                for (int s = 0; s < ActiveSlots; s++) if (Line[s] != null && Line[s].Color == color) lineAmmo += Line[s].Ammo;
                // Surplus counts the line too: ammo already on the wall covers its cubes first, so a falling piece
                // takes all the dead ammo off the deck and tray at once (no trickle of "-1" on later shots).
                int surplus = Math.Min(deckAmmo + trayAmmo + lineAmmo - Monster.CountOfColor(color), deckAmmo + trayAmmo);
                if (surplus <= 0) continue;

                // ii) Deck cannons of that color, smallest first, silently.
                var order = new List<int>();
                for (int i = 0; i < deck.Count; i++) if (deck[i].Color == color) order.Add(i);
                order.Sort((a, b) => deck[a].Ammo != deck[b].Ammo ? deck[a].Ammo.CompareTo(deck[b].Ammo) : a.CompareTo(b));
                foreach (int i in order)
                {
                    if (surplus == 0) break;
                    int take = Math.Min(surplus, deck[i].Ammo);
                    deck[i].Ammo -= take;
                    surplus -= take;
                }
                deck.RemoveAll(c => c.Ammo <= 0);

                // iii) Then tray cannons, left to right, visibly.
                for (int i = 0; i < Tray.Length && surplus > 0; i++)
                {
                    var c = Tray[i];
                    if (c == null || c.Color != color) continue;
                    int take = Math.Min(surplus, c.Ammo);
                    c.Ammo -= take;
                    surplus -= take;
                    int points = take * Config.PointsPerSalvagedAmmo;
                    Score += points;
                    Events.Add(new GameEvent { Kind = EventKind.Salvaged, TraySlot = i, Color = color, Ammo = take, Points = points });
                    if (c.Ammo == 0)
                    {
                        Tray[i] = null;
                        trayEmptied = true;
                    }
                }
            }

            // iv) A tray slot emptied by salvage refills like a slot emptied by a tap (PerSlot: at once; Batch: when all 3 are empty).
            if (!trayEmptied) return;
            if (Config.TrayRefill == TrayRefill.PerSlot)
            {
                for (int i = 0; i < Tray.Length; i++)
                {
                    if (Tray[i] != null) continue;
                    Tray[i] = Draw(i);
                    if (Tray[i] != null) Events.Add(new GameEvent { Kind = EventKind.TrayRefilled, TraySlot = i });
                }
            }
            else
            {
                bool allEmpty = true;
                foreach (var c in Tray) if (c != null) allEmpty = false;
                if (allEmpty) RefillTray();
            }
        }

        readonly int[] salvageDeck = new int[MonsterBuilder.MaxColors], salvageTray = new int[MonsterBuilder.MaxColors];

        // ---------------------------------------------------------------- cut prediction ("TAIE!")

        Monster severMonster;
        int severEpoch;
        /// <summary>Per (color, firing slot): first shot that cuts (0 = none found yet) and how far the look-ahead went.</summary>
        readonly Dictionary<int, int[]> severCache = new Dictionary<int, int[]>();

        /// <summary>Slot a tray cannon would take if deployed now (the first free one; slot 0 when the line is full).</summary>
        public int DeploySlot
        {
            get
            {
                int slot = FirstFreeSlot();
                return slot < 0 ? 0 : slot;
            }
        }

        /// <summary>
        /// Cut prediction for a tray cannon (the "TAIE!" tag): deployed now, alone, with no other cannon firing, the
        /// 0-based index of its shot that makes a piece fall, or -1 if its ammo never cuts. Simulated on a copy of the
        /// titan with the real targeting rule; cached per (color, slot, titan epoch).
        /// </summary>
        public int ShotsToSever(Cannon c)
        {
            if (c == null || c.Ammo <= 0) return -1;
            int shots = ShotsToCut(c.Color, DeploySlot, c.Ammo);
            return shots > 0 ? shots - 1 : -1;
        }

        /// <summary>
        /// Shots of this color, fired alone from <paramref name="slot"/>, until a piece falls (1-based), looking at most
        /// <paramref name="maxShots"/> ahead; 0 when no piece falls within that many shots (or the color runs out).
        /// </summary>
        public int ShotsToCut(int color, int slot, int maxShots)
        {
            if (maxShots <= 0 || Monster.CountOfColor(color) == 0) return 0;
            if (severMonster != Monster || severEpoch != Monster.Epoch)
            {
                severCache.Clear();
                severMonster = Monster;
                severEpoch = Monster.Epoch;
            }
            int key = color * 64 + slot;
            if (severCache.TryGetValue(key, out var hit))
            {
                if (hit[0] > 0) return hit[0] <= maxShots ? hit[0] : 0;
                if (hit[1] >= maxShots || hit[2] == 1) return 0;
            }

            var m = Monster.Clone();
            float x = SlotX(slot);
            int cutAt = 0, fired = 0;
            bool exhausted = false;
            while (fired < maxShots)
            {
                if (!m.PickTarget(color, x, out var target))
                {
                    exhausted = true;
                    break;
                }
                fired++;
                if (m.Remove(target).Count > 0)
                {
                    cutAt = fired;
                    break;
                }
            }
            severCache[key] = new[] { cutAt, fired, exhausted ? 1 : 0 };
            return cutAt;
        }

        // ---------------------------------------------------------------- rescues (ADS-2): pure game rules, no ad code

        /// <summary>Times this run was continued with <see cref="Revive"/>.</summary>
        public int Revives { get; private set; }
        /// <summary>Shots fired from the "+1 Loc" slot during the current titan.</summary>
        public int ExtraSlotShots { get; private set; }
        /// <summary>Cubes the current titan had when it spawned.</summary>
        public int SpawnCubes { get; private set; }
        /// <summary>Share of the current titan already destroyed: (spawn cubes − cubes left) / spawn cubes.</summary>
        public float ProgressFraction => SpawnCubes > 0 ? (SpawnCubes - Monster.Count) / (float)SpawnCubes : 0f;
        public bool ExtraSlotActive => ActiveSlots > Config.LineSlots;

        /// <summary>True when the run can be continued: it is over at the wall, with full slots or blocked.</summary>
        public bool CanRevive => Lost == LoseReason.WallReached || Lost == LoseReason.SlotsFull || Lost == LoseReason.Deadlock;

        /// <summary>
        /// "A doua suflare" (DESIGN.md 10.2 A): continues a lost run on the same titan with all its damage. Idle line
        /// cannons (no visible color) go back into the deck when their color still has cubes, the titan is pushed back
        /// by <paramref name="pushBackFraction"/> of the start distance and waits <paramref name="graceSeconds"/>
        /// before its next step, and a tray with no visible color is redrawn from visible colors. Every cube stays;
        /// ammo moves between line, deck and tray only (the salvage invariant holds). Returns false when the run is not
        /// over by a revivable loss. How often it may happen is not a game rule: the caller decides (once per run).
        /// </summary>
        public bool Revive(float pushBackFraction, float graceSeconds)
        {
            if (!CanRevive) return false;
            Lost = LoseReason.None;

            int returned = 0;
            for (int s = 0; s < Line.Length; s++)
            {
                var c = Line[s];
                if (c == null || Monster.HasVisibleOfColor(c.Color)) continue;
                Line[s] = null;
                if (c.Ammo <= 0 || Monster.CountOfColor(c.Color) == 0) continue; // nothing left to hit: dropped
                c.Cooldown = 0f;
                deck.Insert(rng.Range(0, deck.Count + 1), c);
                returned++;
            }
            Salvage(); // a returned cannon may hold more than its color's cubes: the deck surplus goes, silently

            Distance = Math.Min(Config.StartDistance, Distance + pushBackFraction * Config.StartDistance);
            stepTimer = -graceSeconds;

            bool trayVisible = AnyTrayCannonVisible();
            if (!trayVisible)
            {
                for (int i = 0; i < Tray.Length; i++)
                {
                    if (Tray[i] == null) continue;
                    deck.Insert(rng.Range(0, deck.Count + 1), Tray[i]);
                    Tray[i] = null;
                }
                for (int i = 0; i < Tray.Length; i++) Tray[i] = Draw(i, true);
            }

            stuckTimer = 0f;
            Revives++;
            Events.Add(new GameEvent { Kind = EventKind.Revived, Distance = Distance, Level = Level, Ammo = returned });
            if (!trayVisible) Events.Add(new GameEvent { Kind = EventKind.TrayRefilled });
            return true;
        }

        /// <summary>
        /// "+1 Loc" (DESIGN.md 10.2 B): opens the 6th firing slot (index LineSlots, at SlotX = 7.2 so the others do not
        /// move) until the current titan falls. Once per titan; false when it is already open or the run is over.
        /// </summary>
        public bool GrantExtraSlot()
        {
            if (IsOver || ActiveSlots != Config.LineSlots) return false;
            ActiveSlots++;
            stuckTimer = 0f;
            Events.Add(new GameEvent { Kind = EventKind.SlotAdded, Slot = Config.LineSlots, Level = Level });
            return true;
        }

        /// <summary>The first free slot among the active ones, or -1.</summary>
        int FirstFreeSlot()
        {
            for (int s = 0; s < ActiveSlots; s++) if (Line[s] == null) return s;
            return -1;
        }

        /// <summary>Lose when every active slot holds a cannon and none of them has anything visible to shoot.</summary>
        void CheckSlotsFull()
        {
            if (IsOver) return;
            Salvage();
            for (int s = 0; s < ActiveSlots; s++)
                if (Line[s] == null || Monster.HasVisibleOfColor(Line[s].Color)) return;
            Lose(LoseReason.SlotsFull);
        }

        void Lose(LoseReason reason)
        {
            Lost = reason;
            Events.Add(new GameEvent { Kind = EventKind.GameOver, Reason = reason, Level = Level });
        }

        void DefeatTitan()
        {
            int leftover = 0;
            for (int s = 0; s < ActiveSlots; s++) if (Line[s] != null) leftover += Line[s].Ammo;
            int bonus = DefeatBonus(Level, Role, out bool nearMiss) + leftover * Config.PointsPerLeftoverAmmo;
            Score += bonus;
            int stars = TitanStars();
            TitansDefeated++;
            StarsEarned += Stars.Count(stars);
            Events.Add(new GameEvent { Kind = EventKind.TitanDefeated, Level = Level, Points = bonus, Role = Role, NearMiss = nearMiss, Distance = Distance, Stars = stars, MaxLine = MaxOccupiedLine });
            if (MaxTitans > 0 && Level >= MaxTitans)
            {
                Lose(LoseReason.Completed); // the last titan of a fixed run fell: a win
                return;
            }
            SpawnTitan(Level + 1);
        }

        /// <summary>The grade of the titan being defeated now (DESIGN.md 6.3): defeated, decapitation, clean line.</summary>
        int TitanStars()
        {
            int stars = Stars.Defeated;
            if (DecapitatedThisTitan) stars |= Stars.Decapitation;
            if (MaxOccupiedLine <= Config.CleanLineMax) stars |= Stars.CleanLine;
            return stars;
        }

        /// <summary>Defeat bonus without the leftover ammo: 200 × n × (boss ? 2 : 1) × (la mustață ? 1.5 : 1), rounded.</summary>
        int DefeatBonus(int titan, TitanRole role, out bool nearMiss)
        {
            int bonus = titan * Config.DefeatBonusPerLevel * (role == TitanRole.Boss ? Config.BossBonusMult : 1);
            nearMiss = Distance <= Config.NearMissFraction * Config.StartDistance;
            if (nearMiss) bonus = (int)Math.Round(bonus * Config.NearMissBonusMult, MidpointRounding.AwayFromZero);
            return bonus;
        }

        void SpawnTitan(int level)
        {
            Level = level;
            if (seeded) rng = new SeededRandom(SeededRandom.Derive(unchecked((ulong)RunSeed), (ulong)level));
            BuildLevel = Plan != null ? Plan.BuildLevelOf(level, Config) : Config.BuildLevel(level);
            Role = Plan != null ? Plan.RoleOf(level, Config) : Config.RoleOf(level);
            int extraColors = Role == TitanRole.Boss && Config.BossExtraColor ? 1 : 0;
            Monster = Plan?.Script?.BuildTitan(level) ?? titanSource?.Invoke(level) ?? MonsterBuilder.Build(BuildLevel, rng, extraColors);
            Monster.Order = Config.TargetOrder;
            Distance = Config.StartDistance;
            stepTimer = 0f;
            stuckTimer = 0f;
            Chain = 0;
            LastPieceTime = float.NegativeInfinity;
            MaxOccupiedLine = 0;
            DecapitatedThisTitan = false;
            ActiveSlots = Config.LineSlots; // "+1 Loc" lasts until this titan falls
            ExtraSlotShots = 0;
            SpawnCubes = Monster.Count;
            ApproachSeconds = Config.ApproachSeconds(Plan != null ? Plan.ClockNOf(level) : level, Monster.Count);
            HoldApproach = Plan != null && Plan.HoldsApproach(level);
            stepLength = Config.StartDistance / (ApproachSeconds / Config.StepInterval);
            for (int i = 0; i < Line.Length; i++) Line[i] = null;
            BuildDeck();
            Events.Add(new GameEvent { Kind = EventKind.TitanSpawned, Level = level, Role = Role, Score = Score });
            RefillTray(Plan?.Script?.StartingTray(level, Monster, Config));
        }

        /// <summary>Exactly enough ammo for every cube, split into cannons of 10–30 shots.</summary>
        void BuildDeck()
        {
            deck.Clear();
            var counts = new Dictionary<int, int>();
            foreach (var kv in Monster.Cubes)
            {
                counts.TryGetValue(kv.Value, out int n);
                counts[kv.Value] = n + 1;
            }
            foreach (var kv in counts) AddCannons(kv.Key, kv.Value);
        }

        void AddCannons(int color, int total)
        {
            int start = deck.Count;
            while (total > 0)
            {
                int ammo = Config.AmmoSizes[rng.Range(0, Config.AmmoSizes.Length)];
                if (total - ammo < Config.AmmoSizes[0]) ammo = total; // no tiny leftovers
                ammo = Math.Min(ammo, total);
                deck.Add(new Cannon(color, ammo));
                total -= ammo;
            }
            // A last chunk above 30 is split in two so numbers stay readable.
            for (int i = start; i < deck.Count; i++)
            {
                if (deck[i].Ammo <= 30) continue;
                int halfAmmo = deck[i].Ammo / 2;
                deck.Add(new Cannon(color, deck[i].Ammo - halfAmmo));
                deck[i].Ammo = halfAmmo;
            }
        }

        /// <param name="fixedCannons">Scripted cannons by tray slot (FTUE-1); their ammo comes out of the deck first.</param>
        void RefillTray(Cannon[] fixedCannons = null)
        {
            for (int i = 0; i < Tray.Length; i++) Tray[i] = null; // a new titan discards the old tray
            if (fixedCannons != null)
                for (int i = 0; i < Tray.Length && i < fixedCannons.Length; i++)
                {
                    var c = fixedCannons[i];
                    if (c == null) continue;
                    int taken = TakeFromDeck(c.Color, c.Ammo);
                    if (taken > 0) Tray[i] = new Cannon(c.Color, taken);
                }
            for (int i = 0; i < Tray.Length; i++) if (Tray[i] == null) Tray[i] = Draw(i);
            Events.Add(new GameEvent { Kind = EventKind.TrayRefilled });
        }

        /// <summary>
        /// Takes up to <paramref name="ammo"/> shots of a color out of the deck (the rest is re-split into cannons of
        /// 10–30), so a scripted cannon keeps ammo == cubes. Returns the ammo taken.
        /// </summary>
        int TakeFromDeck(int color, int ammo)
        {
            int total = 0;
            foreach (var c in deck) if (c.Color == color) total += c.Ammo;
            int taken = Math.Min(Math.Max(0, ammo), total);
            if (taken == 0) return 0;
            deck.RemoveAll(c => c.Color == color);
            if (total - taken > 0) AddCannons(color, total - taken);
            return taken;
        }

        /// <summary>Draws the cannon for tray slot <paramref name="slot"/> (the other slots are already filled), or null if the deck is empty.</summary>
        Cannon Draw(int slot) => Draw(slot, false);

        /// <summary>
        /// <paramref name="visibleOnly"/>: the revive redraw (bias 1.0): a visible-color cannon whenever the deck has
        /// one, else a random one. The normal draw uses VisibleColorBias (ethics lock: never tied to ads).
        /// </summary>
        Cannon Draw(int slot, bool visibleOnly)
        {
            if (deck.Count == 0) TopUpDeck();
            if (deck.Count == 0) return null;

            int index = -1;
            if (visibleOnly || rng.Value() < Config.VisibleColorBias)
            {
                var visible = new List<int>();
                for (int i = 0; i < deck.Count; i++)
                    if (Monster.HasVisibleOfColor(deck[i].Color)) visible.Add(i);
                if (visible.Count > 0) index = visible[rng.Range(0, visible.Count)];
            }
            if (index < 0) index = rng.Range(0, deck.Count);
            if (Config.NoTripleColor) index = AvoidTriple(slot, index, visibleOnly);
            var cannon = deck[index];
            deck.RemoveAt(index);
            return cannon;
        }

        /// <summary>
        /// No 3 of a kind (DESIGN.md 4.2.4): when the two other tray cannons share color X and the draw is X too, the draw
        /// becomes a uniform pick among the deck cannons of any other color, if there are any.
        /// </summary>
        int AvoidTriple(int slot, int index, bool visibleOnly = false)
        {
            int shared = -1, others = 0;
            for (int i = 0; i < Tray.Length; i++)
            {
                if (i == slot || Tray[i] == null) continue;
                if (others == 0) shared = Tray[i].Color;
                else if (Tray[i].Color != shared) return index;
                others++;
            }
            if (others != Tray.Length - 1 || others < 2 || deck[index].Color != shared) return index;
            var pool = new List<int>();
            for (int i = 0; i < deck.Count; i++) if (deck[i].Color != shared) pool.Add(i);
            if (visibleOnly)
            {
                // The revive redraw keeps to visible colors when it can.
                var visiblePool = pool.FindAll(i => Monster.HasVisibleOfColor(deck[i].Color));
                if (visiblePool.Count > 0) pool = visiblePool;
            }
            return pool.Count > 0 ? pool[rng.Range(0, pool.Count)] : index;
        }

        /// <summary>Safety net: if the deck ran dry while cubes remain without ammo in play, add the missing cannons.</summary>
        void TopUpDeck()
        {
            var need = new Dictionary<int, int>();
            foreach (var kv in Monster.Cubes)
            {
                need.TryGetValue(kv.Value, out int n);
                need[kv.Value] = n + 1;
            }
            for (int s = 0; s < ActiveSlots; s++) if (Line[s] != null && need.ContainsKey(Line[s].Color)) need[Line[s].Color] -= Line[s].Ammo;
            foreach (var c in Tray) if (c != null && need.ContainsKey(c.Color)) need[c.Color] -= c.Ammo;
            foreach (var kv in need) if (kv.Value > 0) AddCannons(kv.Key, kv.Value);
        }
    }
}
