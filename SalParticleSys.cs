using Godot;
using Microsoft.Extensions.ObjectPool;
using System;

namespace Saladim.GodotParticle;

[Tool]
public partial class SalParticleSys : Node2D
{
    protected Random r = new();
    protected List<ParticleUnit> particles;
    protected ObjectPool<ParticleUnit> pool;
    protected float storedAmount;

    [Export]
    public bool LongShooting { get; set; }

    [Export]
    public float LongShootingShootAmount { get; set; }

    [Export]
    public bool LocalCoord { get; set; }

    [Export(PropertyHint.Range, "0,8,0.02,or_greater"), ExportGroup("Lifetime")]
    public float Lifetime { get; set; } = 5f;

    #region Appearance

    [Export, ExportGroup("Appearance")]
    public Texture2D Texture { get; set; }

    [Export(PropertyHint.Link)]
    public Vector2 TextureOrginal { get; set; }
    [Export]
    public Gradient Gradient { get; set; }

    #endregion

    #region Position and Speed

    [Export, ExportGroup("Position And Speed")]
    public Vector2 InitPosition { get; set; }

    [Export]
    public Vector2 InitPositionRandomness { get; set; }

    [Export]
    public Vector2 InitSpeed { get; set; }

    [Export]
    public Vector2 InitSpeedRandomness { get; set; }

    [Export]
    public Vector2 Accelerate { get; set; }

    [Export]
    public Vector2 Gravity { get; set; } = Vector2.Down * 100f;

    #endregion

    #region Rotation

    [Export(PropertyHint.Range, "-3.1415926,3.1415926,0.01"), ExportGroup("Rotation")]
    public float InitRotation { get; set; }

    [Export(PropertyHint.Range, "-3.1415926,3.1415926,0.01")]
    public float InitRotationRandomness { get; set; }

    [Export(PropertyHint.Range, "-0.1,0.1,0.0001,or_greater,or_less")]
    public float InitRotationSpeed { get; set; }

    [Export(PropertyHint.Range, "0,0.1,0.0001,or_greater")]
    public float InitRotationSpeedRandomness { get; set; }

    #endregion

    public SalParticleSys()
    {
        var policy = new ParticleUnit.PooledObjectPolicy();
        pool = new DefaultObjectPool<ParticleUnit>(policy);
        particles = new(100);
    }

    public ParticleUnit Emit()
    {
        var u = pool.Get();

        u.MaxLifeTime = Lifetime;
        u.LifeTime = u.MaxLifeTime;

        u.Position = VectorRandomize(InitPosition, InitPositionRandomness, r);

        u.Speed = VectorRandomize(InitSpeed, InitSpeedRandomness, r);

        u.Rotation = FloatRandomize(InitRotation, InitRotationRandomness, r);
        u.RotationSpeed = FloatRandomize(InitRotationSpeed, InitRotationSpeedRandomness, r);

        if (!LocalCoord)
        {
            u.Position *= Transform.Scale;
            u.Position = u.Position.Rotated(Transform.Rotation);
            u.Position += Transform.Origin;
            u.Rotation += Transform.Rotation;
            u.Speed = u.Speed.Rotated(Transform.Rotation);
        }
        particles.Add(u);
        return u;

        static float Random1m1Float(Random r) => (float)(r.NextDouble() * 2d - 1d);
        static float FloatRandomize(float input, float randomness, Random r)
            => input + randomness * Random1m1Float(r);
        static Vector2 VectorRandomize(Vector2 input, Vector2 randomness, Random r)
            => input + randomness with { X = randomness.X * Random1m1Float(r), Y = randomness.Y * Random1m1Float(r) };
    }

    public void EmitMany(int amount)
    {
        for (int i = 0; i <= amount; i++)
        {
            Emit();
        }
    }

    public override void _Process(double delta)
    {
        QueueRedraw();

        if (LongShooting)
        {
            storedAmount += LongShootingShootAmount;
            while (storedAmount >= 1f)
            {
                storedAmount -= 1;
                Emit();
            }
        }

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.Speed += Gravity * (float)delta;
            p.Speed += Accelerate * (float)delta;
            p.Position += p.Speed * (float)delta;
            p.LifeTime -= (float)delta;
            p.Color = Gradient.Sample(1f - p.LifeTime / p.MaxLifeTime);
            p.Rotation += p.RotationSpeed;
            if (p.LifeTime <= 0)
            {
                pool.Return(p);
                particles.RemoveAt(i);
            }
        }
    }

    public override void _Draw()
    {
#if TOOLS
        if (Engine.IsEditorHint())
        {
            if (Texture is null) return;
            DrawCircle(InitPosition, 2, Color.Color8(100, 100, 255));
            DrawRect(
                new Rect2(InitPosition - InitPositionRandomness, InitPositionRandomness * 2),
                Color.Color8(100, 100, 255),
                false,
                2
                );
        }
#endif
        foreach (var p in particles)
        {
            var size = Texture.GetSize();
            Transform2D trans = Transform2D.Identity;
            if (!LocalCoord) trans *= Transform.AffineInverse();
            trans *= new Transform2D(p.Rotation, Vector2.One, 0, p.Position);
            DrawSetTransformMatrix(trans);
            DrawTexture(Texture, TextureOrginal * size, p.Color);
        }
    }

    public class ParticleUnit
    {
        public float MaxLifeTime { get; set; }

        public float LifeTime { get; set; }

        public Vector2 Speed { get; set; }

        public Vector2 Position { get; set; }

        public Color Color { get; set; }

        public float Rotation { get; set; }

        public float RotationSpeed { get; set; }

        public class PooledObjectPolicy : PooledObjectPolicy<ParticleUnit>
        {
            public override ParticleUnit Create()
            {
                return new();
            }

            public override bool Return(ParticleUnit p)
            {
                p.LifeTime = default;
                p.Speed = default;
                p.Position = default;
                p.MaxLifeTime = default;
                p.Rotation = default;
                p.Color = new Color(1, 1, 1, 1);
                p.RotationSpeed = default;
                return true;
            }
        }
    }
}
