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
        /// <summary>Approach time = (base + seconds per cube × cubes) × speedup^(level − 1), never below the minimum.</summary>
        public float ApproachBaseSeconds = 25f;
        public float ApproachSecondsPerCube = 0.25f;
        public float ApproachSpeedup = 0.95f;
        public float MinApproachSeconds = 40f;

        public int PointsPerShot = 10;
        public int PointsPerFallenCube = 25;
        public int DefeatBonusPerLevel = 200;
        public int PointsPerLeftoverAmmo = 2;

        /// <summary>Chance that a drawn cannon has a color that is visible right now (the rest is pure luck).</summary>
        public float VisibleColorBias = 0.65f;
        public int[] AmmoSizes = { 10, 15, 20, 25, 30 };

        public float ApproachSeconds(int level, int cubes) =>
            Math.Max(MinApproachSeconds, (ApproachBaseSeconds + ApproachSecondsPerCube * cubes) * (float)Math.Pow(ApproachSpeedup, level - 1));
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
        TitanSpawned,   // Level
        TrayRefilled,
        GameOver,       // Reason
    }

    public enum LoseReason
    {
        None,
        WallReached,
        SlotsFull,
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

        public TitanGame(TitanConfig config, IRandom rng, int startLevel = 1)
        {
            Config = config;
            this.rng = rng;
            Tray = new Cannon[config.TraySize];
            Line = new Cannon[config.LineSlots];
            SpawnTitan(startLevel);
        }

        /// <summary>Distance covered per step, fixed when the titan spawns.</summary>
        public float StepLength => stepLength;

        /// <summary>Seconds the current titan needs to reach the wall from its spawn point.</summary>
        public float ApproachSeconds { get; private set; }
        public float SlotX(int slot) => (slot - (Config.LineSlots - 1) / 2f) * Config.SlotSpacing;
        public int DeckCount => deck.Count;

        /// <summary>Shots of this color still available: deck, tray and firing line.</summary>
        public int AmmoAvailable(int color)
        {
            int n = 0;
            foreach (var c in deck) if (c.Color == color) n += c.Ammo;
            foreach (var c in Tray) if (c != null && c.Color == color) n += c.Ammo;
            foreach (var c in Line) if (c != null && c.Color == color) n += c.Ammo;
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

        public bool CanDeploy(int traySlot) => !IsOver && traySlot >= 0 && traySlot < Tray.Length && Tray[traySlot] != null && HasFreeSlot;

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

            bool trayEmpty = true;
            foreach (var c in Tray) if (c != null) trayEmpty = false;
            if (trayEmpty) RefillTray();

            CheckSlotsFull();
            return true;
        }

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
                    if (Monster.Count == 0) break;
                }
                if (cannon.Ammo == 0)
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
        }

        /// <summary>Lose when all five slots hold cannons and none of them has anything visible to shoot.</summary>
        void CheckSlotsFull()
        {
            if (IsOver) return;
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
            int bonus = Level * Config.DefeatBonusPerLevel + leftover * Config.PointsPerLeftoverAmmo;
            Score += bonus;
            Events.Add(new GameEvent { Kind = EventKind.TitanDefeated, Level = Level, Points = bonus });
            SpawnTitan(Level + 1);
        }

        void SpawnTitan(int level)
        {
            Level = level;
            Monster = MonsterBuilder.Build(level, rng);
            Distance = Config.StartDistance;
            stepTimer = 0f;
            ApproachSeconds = Config.ApproachSeconds(level, Monster.Count);
            stepLength = Config.StartDistance / (ApproachSeconds / Config.StepInterval);
            for (int i = 0; i < Line.Length; i++) Line[i] = null;
            BuildDeck();
            Events.Add(new GameEvent { Kind = EventKind.TitanSpawned, Level = level });
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
            for (int i = 0; i < Tray.Length; i++) Tray[i] = Draw();
            Events.Add(new GameEvent { Kind = EventKind.TrayRefilled });
        }

        Cannon Draw()
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
            var cannon = deck[index];
            deck.RemoveAt(index);
            return cannon;
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
