namespace mono8.game;

/// <summary>
/// The two things the <see cref="Player"/> and the <see cref="Ball"/> move by in the same way.
///
/// Both travel one pixel at a time, x and y apart, so a fast body can never step over a thin wall.
/// A frame of travel is rarely a whole number of pixels, so the fraction is carried into the next
/// frame rather than dropped. What happens when a step is blocked is each body's own business — the
/// player stops, the ball bounces — so the walk itself stays where it is.
/// </summary>
internal static class Motion
{
    /// <summary>
    /// Banks <paramref name="amount"/> pixels of travel and hands back the whole ones to walk this
    /// frame, leaving the fraction in <paramref name="remainder"/> for the next one.
    /// </summary>
    public static int Pixels(ref float remainder, float amount)
    {
        remainder += amount;
        int steps = (int)YourGame.API.round(remainder);
        remainder -= steps;
        return steps;
    }

    /// <summary>One frame of falling, held at terminal velocity.</summary>
    public static float Fall(float velY, float gravity, float maxFall, float elapsedSeconds)
    {
        velY += gravity * elapsedSeconds;
        return velY > maxFall ? maxFall : velY;
    }
}
