using Godot;
using HollowWake.Client.World.Logic;

namespace HollowWake.Client.World.Appearance;

/// <summary>
/// A baked directional card set for one look: eight azimuths of the rest pose rendered from the isometric
/// pitch into one row, as the rendering study proposed. Shared by every body of that look; the card's material
/// samples one cell by the camera's bearing.
/// </summary>
public sealed class CardAtlas : IDisposable
{
    public const int Cells = 8;

    public string Key { get; }
    public ImageTexture? Texture { get; private set; }
    public ShaderMaterial? Material { get; private set; }
    public bool Ready => Texture is not null;
    /// <summary>The body's drawn height and width in world units, so the card is the body's size.</summary>
    public float Height { get; private set; }
    public float Width { get; private set; }
    public int CellPixels { get; private set; }

    public CardAtlas(string key)
    {
        Key = key;
    }

    public long Bytes => Texture is null ? 0 : (long)Texture.GetWidth() * Texture.GetHeight() * 4 * 4 / 3;

    internal void Finish(Image atlas, float height, float width, int cellPixels)
    {
        atlas.GenerateMipmaps();
        Texture = ImageTexture.CreateFromImage(atlas);
        Height = height;
        Width = width;
        CellPixels = cellPixels;
        Material = (ShaderMaterial)BodyShaders.Card.Duplicate();
        Material.SetShaderParameter("atlas", Texture);
        Material.SetShaderParameter("cells", (float)Cells);
    }

    public void Dispose()
    {
        Texture?.Dispose();
        Texture = null;
        Material?.Dispose();
        Material = null;
    }
}

/// <summary>
/// Bakes card atlases a cell per frame in an offscreen viewport: an orthographic camera at the isometric pitch
/// looks at the body, which is turned to each azimuth in turn, and the frames are copied into the atlas. The
/// queue drains one job at a time so a crowd arriving at once costs a few frames each, never a stall.
/// </summary>
public partial class CardBaker : Node3D
{
    /// <summary>Pixels per world unit on the card, bounded by the largest cell a body may take.</summary>
    public int PixelsPerUnit { get; set; } = 56;
    public int MaxCellPixels { get; set; } = 192;

    private readonly Queue<(CardAtlas Atlas, BodyPlan Plan)> _queue = new();
    private SubViewport _viewport = null!;
    private Camera3D _camera = null!;
    private Node3D _stage = null!;
    private (CardAtlas Atlas, BodyNode Body, Image Image, int Cell, int Pixels, float Size) ? _job;
    private bool _waiting;

    public int Pending => _queue.Count + (_job is null ? 0 : 1);

    public override void _Ready()
    {
        _viewport = new SubViewport
        {
            Name = "bake",
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            Msaa3D = Viewport.Msaa.Msaa4X,
            OwnWorld3D = true,
        };
        AddChild(_viewport);
        _stage = new Node3D { Name = "stage" };
        _viewport.AddChild(_stage);
        var sun = new DirectionalLight3D { Name = "sun", LightEnergy = 1.1f, ShadowEnabled = false };
        sun.RotationDegrees = new Vector3(-50, 30, 0);
        _viewport.AddChild(sun);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0, 0, 0, 0),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.72f, 0.74f, 0.8f),
            AmbientLightEnergy = 0.6f,
        };
        _viewport.AddChild(new WorldEnvironment { Name = "env", Environment = env });
        _camera = new Camera3D { Name = "camera", Projection = Camera3D.ProjectionType.Orthogonal, Current = true, Near = 0.05f, Far = 100f };
        _viewport.AddChild(_camera);
    }

    public void Enqueue(CardAtlas atlas, BodyPlan plan) => _queue.Enqueue((atlas, plan));

    public override void _Process(double delta)
    {
        if (_waiting)
        {
            return;
        }
        if (_job is null)
        {
            if (_queue.Count == 0)
            {
                return;
            }
            Start(_queue.Dequeue());
        }
        _ = Step();
    }

    private void Start((CardAtlas Atlas, BodyPlan Plan) next)
    {
        var plan = next.Plan;
        var size = MathF.Max(plan.Height, plan.Width) * 1.08f;
        var pixels = Math.Clamp((int)MathF.Ceiling(size * PixelsPerUnit), 32, MaxCellPixels);
        _viewport.Size = new Vector2I(pixels, pixels);
        _camera.Size = size;
        var body = new BodyNode(plan);
        _stage.AddChild(body);
        var image = Image.CreateEmpty(pixels * CardAtlas.Cells, pixels, false, Image.Format.Rgba8);
        _job = (next.Atlas, body, image, 0, pixels, size);
    }

    private async Task Step()
    {
        if (_job is not { } job)
        {
            return;
        }
        _waiting = true;
        try
        {
            // the isometric pitch, looking at the body's middle, from the bearing of this cell
            var pitch = Mathf.DegToRad(35.264f);
            var yaw = Mathf.DegToRad(45f);
            var centre = new Vector3(0, job.Plan().Height * 0.5f, 0);
            var dir = new Vector3(Mathf.Cos(pitch) * Mathf.Sin(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Cos(yaw));
            _camera.GlobalPosition = centre + dir * 40f;
            _camera.LookAt(centre, Vector3.Up);
            // cell n shows the body facing n eighths of a turn from the camera's bearing
            job.Body.Rotation = new Vector3(0, yaw + job.Cell * Mathf.Tau / CardAtlas.Cells, 0);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            if (!IsInstanceValid(this))
            {
                return;
            }
            var frame = _viewport.GetTexture().GetImage();
            frame.Convert(Image.Format.Rgba8);
            job.Image.BlitRect(frame, new Rect2I(0, 0, job.Pixels, job.Pixels), new Vector2I(job.Cell * job.Pixels, 0));
            var cell = job.Cell + 1;
            if (cell >= CardAtlas.Cells)
            {
                job.Atlas.Finish(job.Image, job.Plan().Height, job.Plan().Width, job.Pixels);
                job.Body.QueueFree();
                _job = null;
            }
            else
            {
                _job = (job.Atlas, job.Body, job.Image, cell, job.Pixels, job.Size);
            }
        }
        finally
        {
            _waiting = false;
        }
    }
}

file static class JobExtensions
{
    public static BodyPlan Plan(this (CardAtlas Atlas, BodyNode Body, Image Image, int Cell, int Pixels, float Size) job) => job.Body.Plan;
}
