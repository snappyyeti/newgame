using System;
using System.Globalization;

namespace Titan.Core
{
    /// <summary>How a run starts: its seed, first titan, starting score and run_start mode.</summary>
    public struct RunStart
    {
        public long Seed;
        public int Titan;
        public int Score;
        public int Mode;

        /// <summary>A new run from titan 1.</summary>
        public static RunStart Fresh(long seed) => new RunStart { Seed = seed, Titan = 1, Score = 0, Mode = Telemetry.ModeEndless };

        /// <summary>Titan n of the saved run, with the score it had then (no farming: the same score, not more).</summary>
        public static RunStart FromCheckpoint(Checkpoint c) => new RunStart { Seed = c.runSeed, Titan = c.n, Score = c.score, Mode = Telemetry.ModeCheckpoint };
    }

    /// <summary>What the game should do after <see cref="RunSession.Process"/> or <see cref="RunSession.Update"/>.</summary>
    [Flags]
    public enum SessionSignal
    {
        None = 0,
        /// <summary>Save the profile now (titan defeated, checkpoint, game over). Never per frame.</summary>
        Save = 1,
        /// <summary>A checkpoint was saved: toast "Punct de salvare: titanul #n".</summary>
        CheckpointSaved = 2,
        /// <summary>The first titan past the old furthest titan this run: banner "TITAN NOU: #n".</summary>
        NewFurthestTitan = 4,
        /// <summary>The score just passed the record this run started with: "RECORD NOU!" (once per run).</summary>
        NewRecord = 8,
        RunEnded = 16,
    }

    /// <summary>
    /// One run and what it changes in the profile (RET-1, RET-2): telemetry, best score, furthest titan, run count, the
    /// free checkpoints at cycle starts and the record chase. Pure C#: the game feeds it the core's events and acts on
    /// the signals (save, toast, banner). Nothing here knows about ads (ethics lock: checkpoints are never ad-gated).
    /// </summary>
    public sealed class RunSession
    {
        /// <summary>A checkpoint is saved at the start of every cycle after the first: titans 6, 11, 16, …</summary>
        public const int CheckpointEvery = 5;
        /// <summary>The "Continuă (titanul #n)" overlay at app launch needs this many finished runs.</summary>
        public const int LaunchOverlayMinRuns = 3;

        public static bool IsCheckpointTitan(int n) => n > 1 && (n - 1) % CheckpointEvery == 0;

        /// <summary>At app launch: show "Continuă (titanul #n)" / "Joc nou" instead of starting at once.</summary>
        public static bool ShowLaunchOverlay(Profile p) => p.totalRuns >= LaunchOverlayMinRuns && p.HasCheckpoint;

        /// <summary>"Reia ciclul (titanul #n)": the saved checkpoint, or null when there is none.</summary>
        public static RunStart? ResumeStart(Profile p) => p.HasCheckpoint ? RunStart.FromCheckpoint(p.checkpoint) : (RunStart?)null;

        /// <summary>"Joc nou": titan 1 with a new seed; the checkpoint is cleared.</summary>
        public static RunStart NewGameStart(Profile p, long newSeed)
        {
            p.ClearCheckpoint();
            return RunStart.Fresh(newSeed);
        }

        public readonly Profile Profile;
        public readonly TitanGame Game;
        public readonly RunStart Start;
        /// <summary>The record and furthest titan when this run started (what the run chases).</summary>
        public readonly int BestAtStart, BestTitanAtStart;

        readonly Telemetry telemetry;
        float titanStartTime;

        /// <summary>The record banner fired this run (at most once).</summary>
        public bool RecordAnnounced { get; private set; }
        /// <summary>"TITAN NOU" fired this run (at most once).</summary>
        public bool FurthestAnnounced { get; private set; }
        /// <summary>Real seconds of play in this run.</summary>
        public float Seconds { get; private set; }

        public RunSession(Profile profile, Telemetry telemetry, TitanConfig cfg, RunStart start, Func<int, Monster> titanSource = null)
        {
            Profile = profile;
            this.telemetry = telemetry;
            Start = start;
            BestAtStart = profile.bestScore;
            BestTitanAtStart = profile.bestTitanN;
            Game = new TitanGame(cfg, start.Seed, start.Titan, start.Score, titanSource);
            telemetry?.Track(Telemetry.RunStart, Game.StartTitan, start.Mode);
        }

        /// <summary>The score beat the record this run started with (any score beats a record of 0).</summary>
        public bool NewBest => Game.Score > BestAtStart;
        /// <summary>This run reached a titan never reached before.</summary>
        public bool NewFurthest => Game.Level > BestTitanAtStart;

        /// <summary>"Joacă din nou": the same start as this run; a new seed when it started at titan 1.</summary>
        public RunStart PlayAgainStart(long newSeed) => Start.Titan <= 1 ? RunStart.Fresh(newSeed) : Start;

        /// <summary>Feed every core event here, in order.</summary>
        public SessionSignal Process(GameEvent e)
        {
            var signal = SessionSignal.None;
            switch (e.Kind)
            {
                case EventKind.TitanSpawned:
                    telemetry?.Track(Telemetry.TitanSpawn, e.Level, (int)e.Role);
                    titanStartTime = Game.GameTime;
                    if (e.Level > Profile.bestTitanN) Profile.bestTitanN = e.Level;
                    if (e.Level > BestTitanAtStart && BestTitanAtStart > 0 && !FurthestAnnounced)
                    {
                        FurthestAnnounced = true;
                        signal |= SessionSignal.NewFurthestTitan;
                    }
                    // A run that starts on a checkpoint does not save it again.
                    if (IsCheckpointTitan(e.Level) && e.Level != Game.StartTitan)
                    {
                        Profile.SaveCheckpoint(e.Level, e.Score, Game.RunSeed);
                        signal |= SessionSignal.CheckpointSaved | SessionSignal.Save;
                    }
                    break;
                case EventKind.TitanDefeated:
                    telemetry?.Track(Telemetry.TitanDefeat, e.Level, (int)Math.Round(Game.GameTime - titanStartTime));
                    KeepBest();
                    signal |= SessionSignal.Save;
                    break;
                case EventKind.GameOver:
                    telemetry?.Track(Telemetry.RunEnd, e.Level, (int)e.Reason);
                    Profile.totalRuns++;
                    KeepBest();
                    signal |= SessionSignal.Save | SessionSignal.RunEnded;
                    break;
            }
            return signal;
        }

        /// <summary>Once per frame, after the events: play time and the record crossing.</summary>
        public SessionSignal Update(float realDt)
        {
            if (!Game.IsOver)
            {
                Seconds += realDt;
                Profile.totalPlaySeconds += realDt;
            }
            if (!RecordAnnounced && BestAtStart > 0 && Game.Score > BestAtStart)
            {
                RecordAnnounced = true;
                return SessionSignal.NewRecord;
            }
            return SessionSignal.None;
        }

        void KeepBest()
        {
            if (Game.Score > Profile.bestScore) Profile.bestScore = Game.Score;
        }
    }

    /// <summary>
    /// The player-facing texts of the record chase and the cut scoring (RET-2, CORE-5), as pure functions so the rules
    /// ("true numbers only") are unit tested. Romanian; UX-1 moves them into the RO/EN table.
    /// </summary>
    public static class HudText
    {
        /// <summary>The game-over "Ai fost la X% din record!" line needs Score ≥ 85% of the record.</summary>
        public const int NearMissDisplayPercent = 85;
        /// <summary>The in-run "Record în N" marker shows from 80% of the record.</summary>
        public const int PbMarkerPercent = 80;

        /// <summary>Thousands with a dot, Romanian style: 41200 → "41.200".</summary>
        public static string Num(int n) => n.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

        /// <summary>True while 80% of the record ≤ score &lt; record (record &gt; 0).</summary>
        public static bool ShowPbMarker(int score, int best) => best > 0 && score < best && 100L * score >= (long)PbMarkerPercent * best;

        /// <summary>"Record în 2.300", or null when the marker is off.</summary>
        public static string PbMarker(int score, int best) => ShowPbMarker(score, best) ? "Record în " + Num(best - score) : null;

        /// <summary>
        /// "Ai fost la X% din record!" (X = floor(100 × score / best)) only when 85% of the record ≤ score ≤ record;
        /// otherwise null. Never invents an "almost": below 85% there is no such line.
        /// </summary>
        public static string NearMissText(int score, int best)
        {
            if (best <= 0 || score > best || 100L * score < (long)NearMissDisplayPercent * best) return null;
            return "Ai fost la " + (100L * score / best) + "% din record!";
        }

        /// <summary>Game-over line 3: "RECORD NOU!", the near-miss line, or "Record: {best}".</summary>
        public static string RecordLine(int score, int best, bool newBest)
        {
            if (newBest) return "RECORD NOU!";
            return NearMissText(score, best) ?? "Record: " + Num(best);
        }

        /// <summary>Game-over line 4.</summary>
        public static string RunStats(int titans, int stars, int pieces) => "Titani: " + titans + " · Stele: " + stars + " · Bucăți rupte: " + pieces;

        /// <summary>
        /// Game-over line 5, first match wins: the next checkpoint is one titan away (the one the run ended on), the
        /// daily challenge is waiting (RET-3), else the furthest titan ever.
        /// </summary>
        public static string NextGoal(int lostOnTitan, int bestTitanN, bool dailyPending)
        {
            if (RunSession.IsCheckpointTitan(lostOnTitan + 1)) return "Încă un titan până la punctul de salvare #" + (lostOnTitan + 1);
            if (dailyPending) return "Titanul Zilei te așteaptă";
            return "Cel mai departe: titanul #" + bestTitanN;
        }

        /// <summary>The first line of a piece popup.</summary>
        public static string PieceLabel(bool clean, int part)
        {
            if (clean && part == Monster.Head) return "DECAPITARE!";
            return clean ? "TĂIETURĂ CURATĂ!" : "BUCATĂ RUPTĂ!";
        }

        /// <summary>Popup size: 1 + 0.15 × (chain − 1), at most 1.6.</summary>
        public static float PopupScale(int chain) => Math.Min(1.6f, 1f + 0.15f * Math.Max(0, chain - 1));

        /// <summary>The one-line reason under a grey star, or null when the star was earned.</summary>
        public static string MissingStarReason(int star, int maxLine)
        {
            if (star == Stars.Decapitation) return "Capul nu a căzut întreg";
            if (star == Stars.CleanLine) return "Ai avut " + maxLine + " tunuri pe zid";
            return null;
        }
    }
}
