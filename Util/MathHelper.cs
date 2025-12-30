using System;

namespace Util;

public class MathHelper
{
    public static double RadiansToDegrees(double rad)
    {
        return (180 / Math.PI) * rad;
    }

    public static double DegreesToRadians(double deg)
    {
        return (Math.PI / 180) * deg;
    }
}
