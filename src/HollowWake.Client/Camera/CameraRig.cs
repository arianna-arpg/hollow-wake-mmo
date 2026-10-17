using Godot;

namespace HollowWake.Client.Camera;

public enum CameraMode
{
    Isometric,
    ThirdPerson,
}

/// <summary>
/// Two ways of looking at the same fully 3D world. Isometric: orthographic, fixed pitch, yaw in
/// 45 degree steps, follows the player. Third person: perspective, orbit with the right mouse button,
/// scroll to zoom. Presentation only; the simulation never knows which one is active.
/// </summary>
public partial class CameraRig : Node3D
{
    private Camera3D _camera = null!;
    private Vector3 _target;
    private float _yaw = Mathf.DegToRad(45);
    private float _pitch = Mathf.DegToRad(35.264f);
    private float _orthoSize = 28f;
    private float _distance = 14f;
    private float _thirdPitch = Mathf.DegToRad(28f);
    private bool _orbiting;
    private float _isoStep = Mathf.DegToRad(45);
    private float _isoZoomMax = 90f;
    private float _sensitivity = 1f;
    private bool _invert;
    private float _fov = 65f;
    private bool _hasTarget;

    public CameraMode Mode { get; private set; } = CameraMode.Isometric;
    public Camera3D Camera => _camera;
    public float Yaw => _yaw;

    public override void _Ready()
    {
        _camera = new Camera3D { Name = "Camera", Current = true, Far = 600f, Near = 0.2f };
        AddChild(_camera);
        Apply(force: true);
    }

    public void Follow(Vector3 worldPosition)
    {
        _hasTarget = true;
        _target = worldPosition;
    }

    public void Toggle()
    {
        Mode = Mode == CameraMode.Isometric ? CameraMode.ThirdPerson : CameraMode.Isometric;
        Apply(force: true);
    }

    public void SetMode(CameraMode mode)
    {
        if (Mode != mode)
        {
            Mode = mode;
            Apply(force: true);
        }
    }

    /// <summary>The camera options: the isometric turn and zoom-out limit, third-person drag sensitivity and inversion, and its field of view.</summary>
    public void Configure(float isoStepDegrees, float isoZoomMax, float sensitivity, bool invert, float fov)
    {
        _isoStep = Mathf.DegToRad(Mathf.Clamp(isoStepDegrees, 1, 180));
        _isoZoomMax = Mathf.Max(10, isoZoomMax);
        _orthoSize = Mathf.Min(_orthoSize, _isoZoomMax);
        _sensitivity = sensitivity;
        _invert = invert;
        _fov = Mathf.Clamp(fov, 30, 120);
    }

    public void RotateStep(int direction)
    {
        if (Mode == CameraMode.Isometric)
        {
            _yaw += _isoStep * direction;
        }
        else
        {
            _yaw += Mathf.DegToRad(15) * direction;
        }
    }

    public void Zoom(float steps)
    {
        if (Mode == CameraMode.Isometric)
        {
            _orthoSize = Mathf.Clamp(_orthoSize - steps * 3f, 8f, _isoZoomMax);
        }
        else
        {
            _distance = Mathf.Clamp(_distance - steps * 1.5f, 3f, 60f);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            {
                Zoom(1);
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            {
                Zoom(-1);
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                _orbiting = mb.Pressed;
            }
        }
        if (@event is InputEventMouseMotion mm && _orbiting && Mode == CameraMode.ThirdPerson)
        {
            _yaw -= mm.Relative.X * 0.005f * _sensitivity;
            _thirdPitch = Mathf.Clamp(_thirdPitch + mm.Relative.Y * 0.004f * _sensitivity * (_invert ? -1 : 1), Mathf.DegToRad(5), Mathf.DegToRad(80));
        }
    }

    public override void _Process(double delta)
    {
        Apply(force: false, delta: (float)delta);
    }

    /// <summary>Camera-relative forward on the ground plane (Godot XZ), for WASD movement.</summary>
    public Vector2 GroundForward()
    {
        var f = -_camera.GlobalTransform.Basis.Z;
        var g = new Vector2(f.X, f.Z);
        return g.LengthSquared() < 0.0001f ? new Vector2(0, -1) : g.Normalized();
    }

    public Vector2 GroundRight()
    {
        var f = GroundForward();
        return new Vector2(-f.Y, f.X);
    }

    private void Apply(bool force, float delta = 0)
    {
        if (!_hasTarget)
        {
            return;
        }
        Vector3 desired;
        Vector3 lookAt;
        if (Mode == CameraMode.Isometric)
        {
            _camera.Projection = Camera3D.ProjectionType.Orthogonal;
            _camera.Size = _orthoSize;
            var dir = new Vector3(Mathf.Cos(_pitch) * Mathf.Sin(_yaw), Mathf.Sin(_pitch), Mathf.Cos(_pitch) * Mathf.Cos(_yaw));
            desired = _target + dir * 120f;
            lookAt = _target;
        }
        else
        {
            _camera.Projection = Camera3D.ProjectionType.Perspective;
            _camera.Fov = _fov;
            var eye = _target + new Vector3(0, 1.6f, 0);
            var dir = new Vector3(Mathf.Cos(_thirdPitch) * Mathf.Sin(_yaw), Mathf.Sin(_thirdPitch), Mathf.Cos(_thirdPitch) * Mathf.Cos(_yaw));
            desired = eye + dir * _distance;
            lookAt = eye;
        }
        if (force || delta <= 0)
        {
            _camera.GlobalPosition = desired;
        }
        else
        {
            var t = 1f - Mathf.Exp(-delta * (Mode == CameraMode.Isometric ? 10f : 14f));
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(desired, t);
        }
        _camera.LookAt(lookAt, Vector3.Up);
    }
}
