using System;

namespace Titan.Core
{
    /// <summary>The first minutes (FTUE-1, DESIGN.md 8): which runs are scripted and the hint bits of profile.ftueFlags.</summary>
    public static class Ftue
    {
        /// <summary>Runs (by profile.totalRuns at their start) whose titan 1 is the scripted tutorial titan.</summary>
        public const int ScriptedRuns = 3;
        /// <summary>The scripted titan waits for the first tap (FTUE only; see <see cref="TitanGame.HoldApproach"/>).</summary>
        public const bool HoldApproachUntilFirstDeploy = true;

        public const int H1TapYellow = 1, H2Zzz = 2, H3DontFill = 3, H4GameOver = 4;

        public static int Bit(int step) => 1 << (step - 1);
        public static bool Seen(Profile p, int step) => (p.ftueFlags & Bit(step)) != 0;
        public static bool IsScriptedRun(Profile p) => p.totalRuns < ScriptedRuns;

        /// <summary>The plan of a new endless run from titan 1: the tutorial titan for the first runs, else none.</summary>
        public static RunPlan PlanFor(Profile p) => IsScriptedRun(p) ? RunPlan.Tutorial() : null;
    }

    /// <summary>The hint texts (Romanian; UX-1 moves them into the RO/EN table).</summary>
    public static class FtueText
    {
        public const string H1 = "Atinge tunul galben";
        public const string H2 = "zzz = culoarea e ascunsă în spate. Va trage când apare.";
        public const string H3 = "Nu umple toate cele 5 locuri!";
        public const string H4Tip = "Sfat: lasă mereu 1-2 locuri libere.";

        /// <summary>H4: the one-line cause of the first game over.</summary>
        public static string Cause(LoseReason reason)
        {
            switch (reason)
            {
                case LoseReason.WallReached: return "Titanul a ajuns la zid.";
                case LoseReason.Deadlock: return "Niciun tun nu mai avea în ce trage.";
                case LoseReason.SlotsFull: return "Toate cele 5 locuri erau pline cu tunuri care așteptau.";
                default: return null;
            }
        }

        public static string Text(int step)
        {
            switch (step)
            {
                case Ftue.H1TapYellow: return H1;
                case Ftue.H2Zzz: return H2;
                case Ftue.H3DontFill: return H3;
                default: return H4Tip;
            }
        }
    }

    /// <summary>
    /// The 4 contextual hints and the FTUE funnel (FTUE-1). At most one hint at a time; each shows once per install
    /// (profile.ftueFlags, set when it shows); none blocks input (the game only draws them). Lives for the app session.
    ///   H1  titan 1 of a scripted run, until the first tap: ring tray slot 0, "Atinge tunul galben" above the tray.
    ///   H2  the first time a line cannon is idle ≥ 1.0 s: ring that slot for 2.5 s.
    ///   H3  the first time ≥ 3 line cannons are idle: arrow at the slot pips for 2.5 s.
    ///   H4  the first game over ever: its cause and "Sfat: lasă mereu 1-2 locuri libere." on the results panel.
    /// Telemetry: tut_step_N_shown / tut_step_N_done, run_3_started_same_session.
    /// </summary>
    public sealed class FtueHints
    {
        public const float IdleSeconds = 1.0f;
        public const float TimedHintSeconds = 2.5f;
        public const int IdleCannonsForH3 = 3;

        readonly Profile profile;
        readonly Telemetry telemetry;
        int runsThisSession;

        /// <summary>The hint on screen (0: none, else 1-4).</summary>
        public int Active { get; private set; }
        /// <summary>H2: the line slot to ring.</summary>
        public int Slot { get; private set; } = -1;
        /// <summary>H4: the cause line of the game over.</summary>
        public string CauseLine { get; private set; }
        /// <summary>Seconds a timed hint (H2, H3) stays.</summary>
        public float TimeLeft { get; private set; }

        readonly Cannon[] idleCannon = new Cannon[16];
        readonly float[] idleFor = new float[16];

        public FtueHints(Profile profile, Telemetry telemetry)
        {
            this.profile = profile;
            this.telemetry = telemetry;
        }

        public string Text => Active == 0 ? null : FtueText.Text(Active);

        /// <summary>A run starts on screen: per-run timers reset; H1 shows on the scripted titan. True when the profile changed (save).</summary>
        public bool BeginRun(TitanGame game, bool countsAsRun = true)
        {
            LeaveResults();
            Close(false);
            Array.Clear(idleCannon, 0, idleCannon.Length);
            Array.Clear(idleFor, 0, idleFor.Length);
            if (countsAsRun)
            {
                runsThisSession++;
                // All of the install's first 3 runs started in this app session.
                if (profile.totalRuns == 2 && runsThisSession == 3) telemetry?.Track(Telemetry.Run3SameSession);
            }
            if (game.Level == 1 && game.Plan?.Script != null && game.HoldApproach && !Ftue.Seen(profile, Ftue.H1TapYellow)) return Show(Ftue.H1TapYellow);
            return false;
        }

        /// <summary>Every core event of the run on screen.</summary>
        public void OnEvent(GameEvent e)
        {
            if (e.Kind == EventKind.Deployed && Active == Ftue.H1TapYellow) Close(true);
        }

        /// <summary>Once per frame while the run ticks. True when a hint just showed (save the profile).</summary>
        public bool Update(TitanGame g, float dt)
        {
            if (g.IsOver) return false;
            int idle = 0, longIdle = -1;
            for (int s = 0; s < g.ActiveSlots && s < idleCannon.Length; s++)
            {
                var c = g.Line[s];
                bool waiting = c != null && !g.Monster.HasVisibleOfColor(c.Color);
                if (!waiting || idleCannon[s] != c)
                {
                    idleCannon[s] = waiting ? c : null;
                    idleFor[s] = 0f;
                }
                if (!waiting) continue;
                idle++;
                idleFor[s] += dt;
                if (idleFor[s] >= IdleSeconds - 1e-4f && longIdle < 0) longIdle = s;
            }

            if (Active == Ftue.H2Zzz || Active == Ftue.H3DontFill)
            {
                TimeLeft -= dt;
                if (TimeLeft <= 0f) Close(true);
                return false;
            }
            if (Active != 0) return false;
            if (longIdle >= 0 && !Ftue.Seen(profile, Ftue.H2Zzz))
            {
                Slot = longIdle;
                return Show(Ftue.H2Zzz);
            }
            if (idle >= IdleCannonsForH3 && !Ftue.Seen(profile, Ftue.H3DontFill)) return Show(Ftue.H3DontFill);
            return false;
        }

        /// <summary>The run really ended (results follow). H4 on the first game over ever. True when it showed (save).</summary>
        public bool OnRunEnded(LoseReason reason)
        {
            Close(Active != Ftue.H1TapYellow);
            string cause = FtueText.Cause(reason);
            if (cause == null || Ftue.Seen(profile, Ftue.H4GameOver)) return false;
            CauseLine = cause;
            return Show(Ftue.H4GameOver);
        }

        /// <summary>The player left the results panel (H4 done) or the run was abandoned.</summary>
        public void LeaveResults()
        {
            if (Active == Ftue.H4GameOver) Close(true);
        }

        bool Show(int step)
        {
            Active = step;
            TimeLeft = step == Ftue.H2Zzz || step == Ftue.H3DontFill ? TimedHintSeconds : 0f;
            profile.ftueFlags |= Ftue.Bit(step);
            telemetry?.Track(Telemetry.TutStep(step, false));
            return true;
        }

        void Close(bool done)
        {
            if (Active != 0 && done) telemetry?.Track(Telemetry.TutStep(Active, true));
            Active = 0;
            Slot = -1;
            TimeLeft = 0f;
            CauseLine = null;
        }
    }
}
