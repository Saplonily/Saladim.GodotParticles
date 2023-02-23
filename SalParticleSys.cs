using Godot;
using Microsoft.Extensions.ObjectPool;
using System;

namespace Saladim.GodotParticle;

[Tool]
public partial class SalParticleSys : Node2D
{
    protected Random r;
    protected List<ParticleUnit> particles;
    protected ObjectPool<ParticleUnit> pool;
    protected float storedAmount;

#if TOOLS

    [Export]
    public bool LongShooting { get; set; }

    [Export]
    public float LongShootingShootAmount { get; set; } = 1f;

#endif

    [Export]
    public bool LocalCoord { get; set; }

    #region Lifetime

    [Export(PropertyHint.Range, "0,8,0.02,or_greater"), ExportGroup("Lifetime", "Particle")]
    public float ParticleLifetime { get; set; } = 5f;

    [Export(PropertyHint.Range, "0,8,0.02,or_greater")]
    public float ParticleLifetimeRandomness { get; set; } = 0f;

    #endregion

    #region Appearance

    [Export, ExportGroup("Appearance", "Particle")]
    public Godot.Collections.Array<Texture2D> ParticleTexture { get; set; } = null!;

    [Export(PropertyHint.Link)]
    public Vector2 ParticleTextureOrginal { get; set; }

    [Export]
    public Gradient ParticleGradient { get; set; } = null!;

    #endregion

    #region Animation

    [Export(PropertyHint.Range, "0,1,0.01,or_greater"), ExportGroup("Animation", "Particle")]
    public float ParticleAnimationSpeed { get; set; }

    [Export(PropertyHint.Range, "0,1,0.01,or_greater"), ExportGroup("Animation", "Particle")]
    public float ParticleAnimationSpeedRandomness { get; set; }

    #endregion

    #region Position and Speed

    [Export, ExportGroup("Position And Speed", "Particle")]
    public Vector2 ParticlePosition { get; set; }

    [Export]
    public Vector2 ParticlePositionRandomness { get; set; }

    [Export]
    public Vector2 ParticleSpeed { get; set; }

    [Export]
    public Vector2 ParticleSpeedRandomness { get; set; }

    [Export]
    public Vector2 ParticleAccelerate { get; set; }

    [Export]
    public Vector2 ParticleGravity { get; set; } = Vector2.Down * 100f;

    #endregion

    #region Rotation

    [Export(PropertyHint.Range, "-3.1415926,3.1415926,0.01"), ExportGroup("Rotation", "Particle")]
    public float ParticleRotation { get; set; } = 0f;

    [Export(PropertyHint.Range, "-3.1415926,3.1415926,0.01")]
    public float ParticleRotationRandomness { get; set; } = 0f;

    [Export(PropertyHint.Range, "-0.1,0.1,0.0001,or_greater,or_less")]
    public float ParticleRotationSpeed { get; set; } = 0f;

    [Export(PropertyHint.Range, "0,0.1,0.0001,or_greater")]
    public float ParticleRotationSpeedRandomness { get; set; } = 0f;

    #endregion

    public SalParticleSys(Random r)
    {
        var policy = new ParticleUnit.PooledObjectPolicy();
        pool = new DefaultObjectPool<ParticleUnit>(policy);
        particles = new(32);
        this.r = r;
    }

    public SalParticleSys() : this(new())
    {
    }

    public ParticleUnit Emit()
    {
        var u = pool.Get();

        u.MaxLifeTime = ParticleLifetime;
        u.LifeTime = FloatRandomize(ParticleLifetime, ParticleLifetimeRandomness, r);

        u.Position = VectorRandomize(ParticlePosition, ParticlePositionRandomness, r);

        u.Speed = VectorRandomize(ParticleSpeed, ParticleSpeedRandomness, r);

        u.Rotation = FloatRandomize(ParticleRotation, ParticleRotationRandomness, r);
        u.RotationSpeed = FloatRandomize(ParticleRotationSpeed, ParticleRotationSpeedRandomness, r);

        u.AnimationSpeed = FloatRandomize(ParticleAnimationSpeed, ParticleAnimationSpeedRandomness, r);

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

    public override void _Ready()
    {
        ParticleGradient ??= new Gradient();
    }

    public override void _Process(double delta)
    {
        QueueRedraw();

#if TOOLS
        if (LongShooting)
        {
            storedAmount += LongShootingShootAmount;
            while (storedAmount >= 1f)
            {
                storedAmount -= 1;
                Emit();
            }
        }
#endif

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.Speed += ParticleGravity * (float)delta;
            p.Speed += ParticleAccelerate * (float)delta;
            p.Position += p.Speed * (float)delta;
            p.LifeTime -= (float)delta;
            p.Color = ParticleGradient.Sample(1f - p.LifeTime / p.MaxLifeTime);
            p.Rotation += p.RotationSpeed;
            p.AnimationProcess += p.AnimationSpeed;
            if ((int)p.AnimationProcess > ParticleTexture.Count - 1)
                p.AnimationProcess = 0f;
            if (p.AnimationProcess < 0f) 
                p.AnimationProcess = 0f;
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
            if (ParticleTexture is null) return;
            DrawCircle(ParticlePosition, 2, Color.Color8(100, 100, 255, 50));
            DrawRect(
                new Rect2(ParticlePosition - ParticlePositionRandomness, ParticlePositionRandomness * 2),
                Color.Color8(100, 100, 255, 50),
                false,
                2
                );
        }
#endif
        foreach (var p in particles)
        {
            int process = (int)p.AnimationProcess;
            var size = ParticleTexture[process].GetSize();
            Transform2D trans = Transform2D.Identity;
            if (!LocalCoord) trans *= Transform.AffineInverse();
            trans *= new Transform2D(p.Rotation, Vector2.One, 0, p.Position);
            DrawSetTransformMatrix(trans);
            DrawTexture(ParticleTexture[process], ParticleTextureOrginal * size, p.Color);
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

        public float AnimationSpeed { get; set; }

        public float AnimationProcess { get; set; }

        public class PooledObjectPolicy : PooledObjectPolicy<ParticleUnit>
        {
            public override ParticleUnit Create() => new();

            public override bool Return(ParticleUnit p)
            {
                p.LifeTime = default;
                p.Speed = default;
                p.Position = default;
                p.MaxLifeTime = default;
                p.Rotation = default;
                p.Color = new Color(1, 1, 1, 1);
                p.RotationSpeed = default;
                p.AnimationProcess = 0;
                return true;
            }
        }
    }
}
