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

        public float FireInterval = 0.3f;
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
        public float ApproachSecondsPerCube = 0.075f;
        public float ApproachSpeedup = 0.98f;
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
        public int DefeatBonusPerLevel = 200;
        public int PointsPerLeftoverAmmo = 2;

        /// <summary>Chance that a drawn cannon has a color that is visible right now (the rest is pure luck).</summary>
        public float VisibleColorBias = 0.65f;
        public int[] AmmoSizes = { 10, 15, 20, 25, 30 };

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
    }

    public enum LoseReason
    {
        None,
        WallReached,
        SlotsFull,
        /// <summary>"BLOCAT!": nothing could fire and no useful tap existed for DeadlockSeconds.</summary>
        Deadlock,
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
    }

    /// <summary>
    /// The whole game as a deterministic simulation: the titan steps toward the wall, cannons on the firing line
    /// shoot visible cubes of their color, loose pieces fall, and a bigger titan follows each defeated one.
    /// The Unity side calls <see cref="Tick"/> every frame and animates the events.
    /// </summary>
    public sealed class TitanGame
    {
        public readonly TitanConfig Config;
        readonly IRandom rng;
        readonly List<Cannon> deck = new List<Cannon>();

        public Monster Monster { get; private set; }
        public int Level { get; private set; }
        public float Distance { get; private set; }
        public int Score { get; private set; }
        public LoseReason Lost { get; private set; }
        public bool IsOver => Lost != LoseReason.None;
        public readonly Cannon[] Tray;
        public readonly Cannon[] Line;
        public readonly List<GameEvent> Events = new List<GameEvent>();

        float stepTimer;
        float stepLength;
        float stuckTimer;

        /// <summary>Optional: titan n comes from here instead of MonsterBuilder when it returns non-null (scripted titans, tests).</summary>
        readonly Func<int, Monster> titanSource;

        public TitanGame(TitanConfig config, IRandom rng, int startLevel = 1, Func<int, Monster> titanSource = null)
        {
            Config = config;
            this.rng = rng;
            this.titanSource = titanSource;
            Tray = new Cannon[config.TraySize];
            Line = new Cannon[config.LineSlots];
            SpawnTitan(startLevel);
        }

        /// <summary>Distance covered per step, fixed when the titan spawns.</summary>
        public float StepLength => stepLength;

        /// <summary>Seconds until the titan's next step (longer after a cut: the stun of DESIGN.md 4.2.6).</summary>
        public float NextStepIn => Config.StepInterval - stepTimer;

        /// <summary>Seconds the current titan needs to reach the wall from its spawn point.</summary>
        public float ApproachSeconds { get; private set; }
        public float SlotX(int slot) => (slot - (Config.LineSlots - 1) / 2f) * Config.SlotSpacing;
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
            foreach (var c in Line) if (c != null && c.Color == color) n += c.Ammo;
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
                foreach (var c in Line) if (c == null) return true;
                return false;
            }
        }

        public int FreeSlots
        {
            get
            {
                int n = 0;
                foreach (var c in Line) if (c == null) n++;
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
            foreach (var c in Line)
                if (c != null && Monster.HasVisibleOfColor(c.Color)) return true;
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
            int slot = Array.IndexOf(Line, null);
            var cannon = Tray[traySlot];
            Tray[traySlot] = null;
            cannon.Cooldown = Config.DeployDelay;
            Line[slot] = cannon;
            Events.Add(new GameEvent { Kind = EventKind.Deployed, Slot = slot, TraySlot = traySlot, Color = cannon.Color });

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

        /// <summary>Cubes of each color a cannon could shoot right now (index = color). Computed once per titan change.</summary>
        public int[] VisibleCountByColor()
        {
            if (visibleMonster != Monster || visibleEpoch != Monster.Epoch)
            {
                Array.Clear(visibleCounts, 0, visibleCounts.Length);
                foreach (var kv in Monster.Cubes)
                    if (Monster.IsVisible(kv.Key)) visibleCounts[kv.Value]++;
                visibleMonster = Monster;
                visibleEpoch = Monster.Epoch;
            }
            return (int[])visibleCounts.Clone();
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

        readonly int[] visibleCounts = new int[MonsterBuilder.MaxColors];
        Monster visibleMonster;
        int visibleEpoch;

        public void Tick(float dt)
        {
            if (IsOver) return;

            // The titan steps toward the wall.
            stepTimer += dt;
            while (stepTimer >= Config.StepInterval && !IsOver)
            {
                stepTimer -= Config.StepInterval;
                Distance = Math.Max(0f, Distance - StepLength);
                Events.Add(new GameEvent { Kind = EventKind.Step, Distance = Distance, Level = Level });
                if (Distance <= 0f) Lose(LoseReason.WallReached);
            }
            if (IsOver) return;

            // Cannons on the line shoot.
            for (int s = 0; s < Line.Length; s++)
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
            foreach (var piece in pieces)
            {
                int points = piece.Count * Config.PointsPerFallenCube;
                Score += points;
                Events.Add(new GameEvent { Kind = EventKind.PieceFell, Slot = slot, Piece = piece, Points = points });
            }
            if (pieces.Count == 0 || Monster.Count == 0) return; // an empty titan is defeated instead (no knockback, leftover bonus)
            foreach (var piece in pieces) Knockback(piece);
            Salvage();
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
            for (int s = 0; s < Line.Length; s++)
            {
                var c = Line[s];
                if (c == null || c.Ammo <= 0 || Monster.CountOfColor(c.Color) > 0) continue; // Ammo 0: Tick retires it
                int points = c.Ammo * Config.PointsPerSalvagedAmmo;
                Score += points;
                Events.Add(new GameEvent { Kind = EventKind.Salvaged, Slot = s, Color = c.Color, Ammo = c.Ammo, Points = points });
                Line[s] = null;
            }

            for (int color = 0; color < MonsterBuilder.MaxColors; color++)
            {
                int deckAmmo = 0, trayAmmo = 0, lineAmmo = 0;
                foreach (var c in deck) if (c.Color == color) deckAmmo += c.Ammo;
                foreach (var c in Tray) if (c != null && c.Color == color) trayAmmo += c.Ammo;
                if (deckAmmo + trayAmmo == 0) continue;
                foreach (var c in Line) if (c != null && c.Color == color) lineAmmo += c.Ammo;
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
                int slot = Array.IndexOf(Line, null);
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

        /// <summary>Lose when all five slots hold cannons and none of them has anything visible to shoot.</summary>
        void CheckSlotsFull()
        {
            if (IsOver) return;
            Salvage();
            foreach (var c in Line)
                if (c == null || Monster.HasVisibleOfColor(c.Color)) return;
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
            foreach (var c in Line) if (c != null) leftover += c.Ammo;
            int bonus = DefeatBonus(Level, Role, out bool nearMiss) + leftover * Config.PointsPerLeftoverAmmo;
            Score += bonus;
            Events.Add(new GameEvent { Kind = EventKind.TitanDefeated, Level = Level, Points = bonus, Role = Role, NearMiss = nearMiss, Distance = Distance });
            SpawnTitan(Level + 1);
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
            BuildLevel = Config.BuildLevel(level);
            Role = Config.RoleOf(level);
            int extraColors = Role == TitanRole.Boss && Config.BossExtraColor ? 1 : 0;
            Monster = titanSource?.Invoke(level) ?? MonsterBuilder.Build(BuildLevel, rng, extraColors);
            Monster.Order = Config.TargetOrder;
            Distance = Config.StartDistance;
            stepTimer = 0f;
            stuckTimer = 0f;
            ApproachSeconds = Config.ApproachSeconds(level, Monster.Count);
            stepLength = Config.StartDistance / (ApproachSeconds / Config.StepInterval);
            for (int i = 0; i < Line.Length; i++) Line[i] = null;
            BuildDeck();
            Events.Add(new GameEvent { Kind = EventKind.TitanSpawned, Level = level, Role = Role });
            RefillTray();
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

        void RefillTray()
        {
            for (int i = 0; i < Tray.Length; i++) Tray[i] = null; // a new titan discards the old tray
            for (int i = 0; i < Tray.Length; i++) Tray[i] = Draw(i);
            Events.Add(new GameEvent { Kind = EventKind.TrayRefilled });
        }

        /// <summary>Draws the cannon for tray slot <paramref name="slot"/> (the other slots are already filled), or null if the deck is empty.</summary>
        Cannon Draw(int slot)
        {
            if (deck.Count == 0) TopUpDeck();
            if (deck.Count == 0) return null;

            int index = -1;
            if (rng.Value() < Config.VisibleColorBias)
            {
                var visible = new List<int>();
                for (int i = 0; i < deck.Count; i++)
                    if (Monster.HasVisibleOfColor(deck[i].Color)) visible.Add(i);
                if (visible.Count > 0) index = visible[rng.Range(0, visible.Count)];
            }
            if (index < 0) index = rng.Range(0, deck.Count);
            if (Config.NoTripleColor) index = AvoidTriple(slot, index);
            var cannon = deck[index];
            deck.RemoveAt(index);
            return cannon;
        }

        /// <summary>
        /// No 3 of a kind (DESIGN.md 4.2.4): when the two other tray cannons share color X and the draw is X too, the draw
        /// becomes a uniform pick among the deck cannons of any other color, if there are any.
        /// </summary>
        int AvoidTriple(int slot, int index)
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
            foreach (var c in Line) if (c != null && need.ContainsKey(c.Color)) need[c.Color] -= c.Ammo;
            foreach (var c in Tray) if (c != null && need.ContainsKey(c.Color)) need[c.Color] -= c.Ammo;
            foreach (var kv in need) if (kv.Value > 0) AddCannons(kv.Key, kv.Value);
        }
    }
}
