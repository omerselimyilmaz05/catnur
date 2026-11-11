namespace CatNur.Behaviors;

using System;
using CatNur.Core;

/// <summary>
/// When music is playing, the pet dances for 15 seconds (muzik_dans rotation loop),
/// then returns to normal wandering and idling.
/// </summary>
public class CatjamBehavior : IBehavior
{
    public string Name => "Catjam";
    public bool IsActive { get; private set; }

    /// <summary>True while the 15-second music dance sequence is playing.</summary>
    public bool IsDancing => IsActive && _danceTimer < MaxDanceDuration;

    private const double MaxDanceDuration = 15.0; // 15 seconds dance duration
    private double _danceTimer;

    public double CurrentTiltAngle => 0;

    public bool CanActivate(PetContext context) => false;

    public void Activate(PetContext context)
    {
        IsActive = true;
        _danceTimer = 0;
        Console.WriteLine("[CatjamBehavior] Music detected! Starting 15-second muzik_dans dance!");
    }

    public void Update(PetContext context, double deltaTime)
    {
        if (!IsActive) return;

        _danceTimer += deltaTime;

        if (IsDancing)
        {
            // Stop horizontal movement while dancing
            if (context.Body.IsGrounded && !context.Body.IsDragged)
            {
                var vel = context.Body.Velocity;
                vel.X = 0;
                context.Body.Velocity = vel;
            }
        }
    }

    public void Deactivate(PetContext context)
    {
        IsActive = false;
        _danceTimer = 0;
        Console.WriteLine("[CatjamBehavior] Music stopped. Deactivated.");
    }
}
