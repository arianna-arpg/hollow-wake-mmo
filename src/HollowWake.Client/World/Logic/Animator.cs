namespace HollowWake.Client.World.Logic;

/// <summary>
/// What the animator reads about a body this frame, every value derived from server state or the drawn
/// motion: nothing here decides anything. Times are seconds; the clock is the server's, so two clients agree.
/// </summary>
public sealed class VisualState
{
    public string Rig = "static";
    public bool Swings;
    public float Height = 1.8f;
    public float Hover;
    public long Key;
    /// <summary>The server clock now, for idle phases.</summary>
    public double Time;
    /// <summary>Ground speed of the drawn body, world units per second.</summary>
    public float Speed;
    public bool Grounded = true;
    public bool Leaping;
    public bool Dashing;
    public bool Dead;
    public float DeadFor;
    /// <summary>The walk cycle in radians, advanced by the distance the body moved over its stride.</summary>
    public float WalkPhase;
    public bool Casting;
    public float CastProgress;
    /// <summary>cast, channel, charge, overcharge, guard or instant; empty when unknown.</summary>
    public string CastMode = "";
    /// <summary>Seconds since confirmed emission (legacy servers infer it), or a large number.</summary>
    public float ReleaseAge = 1e9f;
    /// <summary>Seconds since the body was last hit, or a large number; and how hard, from zero to one.</summary>
    public float HitAge = 1e9f;
    public float HitStrength;
    /// <summary>Where the last hit came from, as a yaw in the body's frame (zero is ahead); NaN when unknown.</summary>
    public float HitFrom = float.NaN;
    /// <summary>Sideways and forward lean from the drawn acceleration, in degrees.</summary>
    public float LeanForward;
    public float LeanSide;
    public bool ReducedMotion;
}

/// <summary>One bone's turn (degrees: roll about x, pitch about z, yaw about y), offset and scale this frame.</summary>
public struct BonePose
{
    public Vec3 Turn;
    public Vec3 Offset;
    public float Scale;

    public static readonly BonePose Rest = new() { Turn = Vec3.Zero, Offset = Vec3.Zero, Scale = 1 };
}

/// <summary>The animator's output: a pose per bone, the root's lift, and the flash and fade of the whole body.</summary>
public sealed class Pose
{
    public readonly Dictionary<string, BonePose> Bones = new(StringComparer.Ordinal);
    /// <summary>How white the body flashes, from zero to one.</summary>
    public float Flash;
    /// <summary>The body's opacity, for corpses that fade.</summary>
    public float Opacity = 1;

    public BonePose this[string bone]
    {
        get => Bones.TryGetValue(bone, out var p) ? p : BonePose.Rest;
        set => Bones[bone] = value;
    }

    public void Clear()
    {
        Bones.Clear();
        Flash = 0;
        Opacity = 1;
    }

    public void Turn(string bone, float roll, float pitch, float yaw)
    {
        var p = this[bone];
        p.Turn = p.Turn + new Vec3(roll, pitch, yaw);
        this[bone] = p;
    }

    public void Move(string bone, float x, float y, float z)
    {
        var p = this[bone];
        p.Offset = p.Offset + new Vec3(x, y, z);
        this[bone] = p;
    }

    public void Scale(string bone, float s)
    {
        var p = this[bone];
        p.Scale *= s;
        this[bone] = p;
    }
}

/// <summary>
/// Procedural animation shared by both clients (docs/design/appearance.md): idle, walk, swing, cast,
/// hit reaction and death as bone turns from the motion timings. Anticipation follows the server's cast
/// progress and release follows confirmed emission; nothing is retimed for beauty.
/// </summary>
public static class Animator
{
    private const float Deg = MathF.PI / 180f;

    /// <summary>Writes the pose for a body's state; the renderer applies it to the plan's bones over their rest turns.</summary>
    public static void Animate(VisualState s, MotionLook m, Pose pose)
    {
        pose.Clear();
        if (s.Dead)
        {
            Death(s, m, pose);
            return;
        }
        var reduced = s.ReducedMotion;
        var t = (float)(s.Time % 3600);
        var phase = Variation.Unit(s.Key, 7) * MathF.PI * 2;
        var H = s.Height;
        switch (s.Rig)
        {
            case "biped":
                Biped(s, m, pose, t, phase, reduced);
                break;
            case "quadruped":
                Quadruped(s, m, pose, t, phase, reduced);
                break;
            case "wisp":
                Wisp(s, m, pose, t, phase, reduced);
                break;
            case "crawler":
                Crawler(s, m, pose, t, phase, reduced);
                break;
            case "tree":
                if (!reduced)
                {
                    pose.Turn("canopy", MathF.Sin(t * 0.7f + phase) * 0.8f, MathF.Sin(t * 0.5f + phase * 1.3f) * 0.6f, 0);
                }
                break;
            case "knot":
                if (!reduced)
                {
                    pose.Turn("root", 0, 0, MathF.Sin(t * 0.4f + phase) * 3f);
                }
                break;
        }
        Hit(s, m, pose, H);
    }

    private static float Ease(float x) => x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x);

    private static float Pulse(float age, float length) => age < 0 || age >= length ? 0 : MathF.Sin(MathF.PI * age / length);

    private static void Biped(VisualState s, MotionLook m, Pose pose, float t, float phase, bool reduced)
    {
        var H = s.Height;
        var run = MathF.Min(1, s.Speed / MathF.Max(0.1f, m.Walk("run_speed", 5)));
        // idle: breath and a slow look around
        if (!reduced)
        {
            var breath = MathF.Sin(t * m.Idle("rate", 0.55f) * MathF.PI * 2 + phase);
            pose.Scale("torso", 1 + breath * m.Idle("breath", 0.02f) * (1 - run));
            pose.Turn("head", 0, 0, MathF.Sin(t * 0.3f + phase) * m.Idle("look", 8) * (1 - run));
            pose.Turn("torso", MathF.Sin(t * 0.4f + phase * 2) * m.Idle("sway", 1.5f) * (1 - run), 0, 0);
        }
        // walk: legs and arms swing in opposition, the body bobs and leans into the step
        if (run > 0.01f)
        {
            var swing = MathF.Sin(s.WalkPhase) * m.Walk("swing", 38) * run;
            var armSwing = MathF.Sin(s.WalkPhase) * m.Walk("arm_swing", 28) * run;
            pose.Turn("leg_l", 0, swing, 0);
            pose.Turn("leg_r", 0, -swing, 0);
            pose.Turn("arm_l", 0, -armSwing, 0);
            pose.Turn("arm_r", 0, armSwing, 0);
            pose.Move("root", 0, MathF.Abs(MathF.Sin(s.WalkPhase)) * m.Walk("bob", 0.035f) * H * run, 0);
            pose.Turn("hips", 0, -m.Walk("lean", 6) * run, 0);
        }
        if (!s.Grounded || s.Leaping)
        {
            pose.Turn("leg_l", 0, -18, 0);
            pose.Turn("leg_r", 0, 22, 0);
            pose.Turn("arm_l", 0, -35, 0);
            pose.Turn("arm_r", 0, -35, 0);
        }
        if (s.Dashing)
        {
            pose.Turn("hips", 0, -14, 0);
        }
        pose.Turn("hips", s.LeanSide, -s.LeanForward, 0);
        // the cast: a swing for a held weapon, a gather for the rest
        if (s.Swings)
        {
            Swing(s, m, pose);
        }
        else
        {
            Gather(s, m, pose);
        }
    }

    /// <summary>A weapon swing: the arm draws back with the cast's progress and comes through on release.</summary>
    private static void Swing(VisualState s, MotionLook m, Pose pose)
    {
        var anticipation = m.Swing("anticipation", 70);
        var follow = m.Swing("follow", 85);
        var release = MathF.Max(0.05f, m.Swing("release", 0.16f));
        var recovery = MathF.Max(0.05f, m.Swing("recovery", 0.3f));
        var lean = m.Swing("lean", 10);
        // An emitted channel beat remains visible while its next preparation is still active.
        var preparing = s.Casting && s.ReleaseAge >= release + recovery;
        if (preparing && s.CastMode != "guard")
        {
            var p = s.CastMode is "channel" ? 0.6f + 0.2f * MathF.Sin((float)(s.Time * 8)) : Ease(s.CastProgress);
            pose.Turn("arm_r", 0, -anticipation * p, 0);
            pose.Turn("arm_l", 0, -anticipation * 0.25f * p, 0);
            pose.Turn("torso", 0, 0, lean * 1.5f * p);
            pose.Turn("hips", 0, lean * 0.4f * p, 0);
            return;
        }
        if (preparing && s.CastMode == "guard")
        {
            // a raised guard: the off hand forward, weight back
            pose.Turn("arm_l", 0, -80, 20);
            pose.Turn("arm_r", 0, -30, 0);
            pose.Turn("hips", 0, 6, 0);
            return;
        }
        if (s.ReleaseAge < release)
        {
            var k = Ease(s.ReleaseAge / release);
            pose.Turn("arm_r", 0, -anticipation + (anticipation + follow) * k, 0);
            pose.Turn("torso", 0, 0, lean * 1.5f - lean * 3f * k);
            pose.Turn("hips", 0, -lean * k, 0);
        }
        else if (s.ReleaseAge < release + recovery)
        {
            var k = 1 - Ease((s.ReleaseAge - release) / recovery);
            pose.Turn("arm_r", 0, follow * k, 0);
            pose.Turn("torso", 0, 0, -lean * 1.5f * k);
            pose.Turn("hips", 0, -lean * k, 0);
        }
    }

    /// <summary>A cast without a weapon: the hands gather and raise with the progress, then thrust forward on release.</summary>
    private static void Gather(VisualState s, MotionLook m, Pose pose)
    {
        var raise = m.Cast("raise", 35);
        var gather = m.Cast("gather", 0.6f);
        var release = MathF.Max(0.05f, m.Cast("release", 0.2f));
        var recovery = MathF.Max(0.05f, m.Cast("recovery", 0.35f));
        if (s.Casting && s.ReleaseAge >= release + recovery)
        {
            if (s.CastMode == "guard")
            {
                pose.Turn("arm_l", 0, -90, 25);
                pose.Turn("arm_r", 0, -90, -25);
                pose.Turn("hips", 0, 5, 0);
                return;
            }
            var p = s.CastMode is "channel" ? 1f : Ease(s.CastProgress);
            var sway = s.CastMode is "channel" && !s.ReducedMotion ? MathF.Sin((float)(s.Time * 6)) * m.Cast("channel_sway", 6) : 0;
            pose.Turn("arm_l", 0, -raise * p - 20 * gather * p, 30 * gather * p + sway);
            pose.Turn("arm_r", 0, -raise * p - 20 * gather * p, -30 * gather * p - sway);
            pose.Turn("torso", 0, 0, 0);
            pose.Turn("hips", 0, 4 * p, 0);
            return;
        }
        if (s.ReleaseAge < release)
        {
            var k = Ease(s.ReleaseAge / release);
            pose.Turn("arm_l", 0, -raise + (raise - 95) * k, 0);
            pose.Turn("arm_r", 0, -raise + (raise - 95) * k, 0);
            pose.Turn("hips", 0, 4 - 10 * k, 0);
        }
        else if (s.ReleaseAge < release + recovery)
        {
            var k = 1 - Ease((s.ReleaseAge - release) / recovery);
            pose.Turn("arm_l", 0, -95 * k, 0);
            pose.Turn("arm_r", 0, -95 * k, 0);
            pose.Turn("hips", 0, -6 * k, 0);
        }
    }

    private static void Quadruped(VisualState s, MotionLook m, Pose pose, float t, float phase, bool reduced)
    {
        var H = s.Height;
        var run = MathF.Min(1, s.Speed / MathF.Max(0.1f, m.Walk("run_speed", 5)));
        if (!reduced)
        {
            var breath = MathF.Sin(t * m.Idle("rate", 0.55f) * MathF.PI * 2 + phase);
            pose.Scale("torso", 1 + breath * m.Idle("breath", 0.02f) * (1 - run));
            pose.Turn("head", 0, MathF.Sin(t * 0.35f + phase) * m.Idle("look", 8) * 0.5f * (1 - run), MathF.Sin(t * 0.25f + phase) * m.Idle("look", 8) * (1 - run));
            pose.Turn("tail", 0, 0, MathF.Sin(t * 1.3f + phase) * 12 * (1 - run * 0.5f));
        }
        if (run > 0.01f)
        {
            var swing = MathF.Sin(s.WalkPhase) * m.Walk("swing", 38) * 0.8f * run;
            pose.Turn("leg_fl", 0, swing, 0);
            pose.Turn("leg_br", 0, swing, 0);
            pose.Turn("leg_fr", 0, -swing, 0);
            pose.Turn("leg_bl", 0, -swing, 0);
            pose.Move("root", 0, MathF.Abs(MathF.Sin(s.WalkPhase)) * m.Walk("bob", 0.035f) * H * run, 0);
            pose.Turn("neck", 0, MathF.Sin(s.WalkPhase * 2) * 4 * run, 0);
        }
        if (!s.Grounded || s.Leaping)
        {
            pose.Turn("leg_fl", 0, -30, 0);
            pose.Turn("leg_fr", 0, -30, 0);
            pose.Turn("leg_bl", 0, 25, 0);
            pose.Turn("leg_br", 0, 25, 0);
        }
        pose.Turn("torso", s.LeanSide, -s.LeanForward * 0.5f, 0);
        // the lunge: rear back with the progress, drive forward on release
        var anticipation = m.Swing("anticipation", 70) * 0.35f;
        var release = MathF.Max(0.05f, m.Swing("release", 0.16f));
        var recovery = MathF.Max(0.05f, m.Swing("recovery", 0.3f));
        if (s.Casting && s.ReleaseAge >= release + recovery)
        {
            var p = Ease(s.CastProgress);
            pose.Turn("torso", 0, anticipation * 0.5f * p, 0);
            pose.Turn("neck", 0, anticipation * 0.6f * p, 0);
            pose.Move("root", -0.08f * H * p, 0, 0);
        }
        else if (s.ReleaseAge < release)
        {
            var k = Ease(s.ReleaseAge / release);
            pose.Turn("torso", 0, anticipation * 0.5f - anticipation * 1.1f * k, 0);
            pose.Turn("neck", 0, anticipation * 0.6f - anticipation * 1.4f * k, 0);
            pose.Move("root", -0.08f * H + 0.3f * H * k, 0.05f * H * MathF.Sin(k * MathF.PI), 0);
        }
        else if (s.ReleaseAge < release + recovery)
        {
            var k = 1 - Ease((s.ReleaseAge - release) / recovery);
            pose.Turn("torso", 0, -anticipation * 0.6f * k, 0);
            pose.Turn("neck", 0, -anticipation * 0.8f * k, 0);
            pose.Move("root", 0.22f * H * k, 0, 0);
        }
    }

    private static void Wisp(VisualState s, MotionLook m, Pose pose, float t, float phase, bool reduced)
    {
        var H = s.Height;
        if (!reduced)
        {
            pose.Move("root", 0, MathF.Sin(t * 1.1f + phase) * 0.04f * H, 0);
            pose.Turn("torso", MathF.Sin(t * 0.6f + phase) * 4, 0, 0);
            pose.Turn("head", 0, MathF.Sin(t * 0.5f + phase * 2) * 5, MathF.Sin(t * 0.3f + phase) * 8);
        }
        var run = MathF.Min(1, s.Speed / MathF.Max(0.1f, m.Walk("run_speed", 5)));
        pose.Turn("torso", 0, -12 * run - s.LeanForward * 0.5f, 0);
        if (s.Swings)
        {
            Swing(s, m, pose);
        }
        else
        {
            Gather(s, m, pose);
        }
    }

    private static void Crawler(VisualState s, MotionLook m, Pose pose, float t, float phase, bool reduced)
    {
        var H = s.Height;
        var run = MathF.Min(1, s.Speed / MathF.Max(0.1f, m.Walk("run_speed", 5)));
        if (!reduced)
        {
            pose.Turn("claw_l", 0, 0, MathF.Sin(t * 1.7f + phase) * 10);
            pose.Turn("claw_r", 0, 0, -MathF.Sin(t * 1.7f + phase * 2) * 10);
        }
        if (run > 0.01f)
        {
            pose.Move("root", 0, MathF.Abs(MathF.Sin(s.WalkPhase * 1.5f)) * 0.02f * H * run, 0);
            pose.Turn("shell", MathF.Sin(s.WalkPhase * 1.5f) * 4 * run, 0, 0);
        }
        var release = MathF.Max(0.05f, m.Swing("release", 0.16f));
        var recovery = MathF.Max(0.05f, m.Swing("recovery", 0.3f));
        if (s.Casting && s.ReleaseAge >= release + recovery)
        {
            var p = Ease(s.CastProgress);
            pose.Turn("claw_l", 0, 0, 40 * p);
            pose.Turn("claw_r", 0, 0, -40 * p);
        }
        else if (s.ReleaseAge < release)
        {
            var k = Ease(s.ReleaseAge / release);
            pose.Turn("claw_l", 0, 0, 40 - 80 * k);
            pose.Turn("claw_r", 0, 0, -40 + 80 * k);
            pose.Move("root", 0.2f * H * k, 0, 0);
        }
        else if (s.ReleaseAge < release + recovery)
        {
            var k = 1 - Ease((s.ReleaseAge - release) / recovery);
            pose.Turn("claw_l", 0, 0, -40 * k);
            pose.Turn("claw_r", 0, 0, 40 * k);
            pose.Move("root", 0.2f * H * k, 0, 0);
        }
    }

    /// <summary>A hit: a flinch away from where it came, a squash and a flash that all decay over the hit timings.</summary>
    private static void Hit(VisualState s, MotionLook m, Pose pose, float H)
    {
        var flinch = MathF.Max(0.01f, m.Hit("flinch", 0.14f));
        var flash = MathF.Max(0.01f, m.Hit("flash", 0.16f));
        if (s.HitAge < flash)
        {
            pose.Flash = (1 - s.HitAge / flash) * MathF.Max(0.35f, s.HitStrength);
        }
        if (s.HitAge < flinch && !s.ReducedMotion)
        {
            var k = Pulse(s.HitAge, flinch) * (0.4f + 0.6f * s.HitStrength);
            var lean = m.Hit("lean", 14) * k;
            var squash = m.Hit("squash", 0.08f) * k;
            var from = float.IsNaN(s.HitFrom) ? 0 : s.HitFrom;
            // lean away from the blow: pitch back for one from ahead, roll aside for one from the side
            pose.Turn("root", MathF.Sin(from) * lean, MathF.Cos(from) * lean, 0);
            var root = pose["root"];
            root.Scale *= 1 - squash;
            pose["root"] = root;
        }
        else if (s.HitAge < flinch)
        {
            pose.Flash = MathF.Max(pose.Flash, 1 - s.HitAge / flinch);
        }
    }

    /// <summary>Death: the body falls over its fall time and settles, then sinks and fades long after.</summary>
    private static void Death(VisualState s, MotionLook m, Pose pose)
    {
        var fall = MathF.Max(0.05f, m.Death("fall", 0.55f));
        var settle = MathF.Max(0.05f, m.Death("settle", 0.5f));
        var k = Ease(s.DeadFor / fall);
        var bounce = s.DeadFor > fall && s.DeadFor < fall + settle && !s.ReducedMotion ? MathF.Sin((s.DeadFor - fall) / settle * MathF.PI) * 6 : 0;
        var H = s.Height;
        switch (s.Rig)
        {
            case "quadruped":
            case "crawler":
                pose.Turn("root", 85 * k + bounce, 0, 0);
                pose.Move("root", 0, -H * 0.1f * k, H * 0.25f * k);
                break;
            case "wisp":
                pose.Move("root", 0, -(s.Hover + H * 0.2f) * k, 0);
                pose.Turn("root", 0, 60 * k, 0);
                pose.Opacity = 1 - 0.5f * k;
                break;
            case "tree":
            case "knot":
            case "static":
                break;
            default:
                pose.Turn("root", 0, -88 * k - bounce, 0);
                pose.Move("root", -H * 0.3f * k, H * 0.08f * k, 0);
                pose.Turn("arm_l", 0, -40 * k, 20 * k);
                pose.Turn("arm_r", 0, -20 * k, -30 * k);
                pose.Turn("head", 0, 15 * k, 20 * k);
                break;
        }
        var fadeAfter = m.Death("fade_after", 20);
        var fade = MathF.Max(0.1f, m.Death("fade", 2));
        if (s.DeadFor > fadeAfter)
        {
            pose.Opacity *= MathF.Max(0, 1 - (s.DeadFor - fadeAfter) / fade);
        }
        var sink = m.Death("sink", 0.25f);
        if (s.DeadFor > fall + settle)
        {
            pose.Move("root", 0, -MathF.Min(sink * H, (s.DeadFor - fall - settle) * 0.02f * H), 0);
        }
    }
}
