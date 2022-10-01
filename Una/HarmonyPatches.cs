using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Serialization;

public static class HarmonyPatches
{
    private static IMonitor Monitor;

    private static bool debug = false;

    private static readonly Vector2 centerOfFarmerFromStandingPosition = new Vector2(-24f, -24f);//getStandingPosition is not centered under the farmer.
    private static readonly float tile = 16f;
    private static readonly float vacRange = 12f*tile;
    private static readonly float rangeSquared = vacRange * vacRange;
    private static readonly float targetOffset = 4f*tile;//for sanity, we want the circle to include the farmer's feet.
    //fun note: if targetOffset were equal to vacRange, the vacuum effect would happen in a circle around the player like the typical "magnet"
    //todo test different ranges for upgrades to the tool.
    //todo stronger (dark side) version of the tool should effect tiles etc much like a bomb does.
    //I think the moon mod has moon-slimes tossing bomb effects
    //todo move the benchmarks etc out of this class



    private static readonly List<Vector2> facingUnitVectors = new List<Vector2>(new Vector2[] { 
        new Vector2(0f, -1f), //FACING_UP
        new Vector2(1f, 0f), //FACING_RIGHT
        new Vector2(0f, 1f), //FACING_DOWN
        new Vector2(-1f, 0f) //FACING_LEFT
    });

    private static Vector2 getTargetAreaOfFarmer(Farmer farmer)
    {
        return getTargetArea(vacRange - targetOffset, farmer.getStandingPosition(), farmer.FacingDirection);
    }

    private static Stopwatch debug_stopwatch = new Stopwatch();
    private static int debug_debrisCount = 0;
    private static readonly int INVALID_TICK = -1;
    private static int lastTargetTick = INVALID_TICK;
    private static Vector2 invalidTargetArea = new Vector2(-1f, -1f);
    private static Vector2 lastTargetArea = invalidTargetArea;
    private static Vector2 getTargetArea(float range, Vector2 farmerPosition, int farmerFacing)
    {
        if(Game1.ticks == lastTargetTick)
        {
            debug_debrisCount++;
            return lastTargetArea;
        }
        else
        {
            if (debug)
            {
                debug_stopwatch.Stop();
                Monitor.Log($"last game tick had debrisCount:{debug_debrisCount} and took: {debug_stopwatch.ElapsedMilliseconds}ms", LogLevel.Debug);
                debug_stopwatch.Restart();
                debug_debrisCount = 0;
            }

            Vector2 facingUnitVector = facingUnitVectors[farmerFacing];
            Vector2 facingRange = Vector2.Multiply(facingUnitVector, range);
            Vector2 centeredFarmerPosition = Vector2.Add(farmerPosition, centerOfFarmerFromStandingPosition);
            Vector2 finalTarget = Vector2.Add(centeredFarmerPosition, facingRange);
            lastTargetArea = finalTarget;
            lastTargetTick = Game1.ticks;
            return finalTarget;
        }
    }

    public static class PREFIX_RETURNS
    {
        public const bool USE_DEFAULT_BEHAVIOR = true;
        public const bool USE_PREFIX_BEHAVIOR = false;
    }


    // call this method from your Entry class
    public static void Initialize(IMonitor monitor)
    {
        Monitor = monitor;
        if (debug)
        {
            for (int i = 0; i < 20; i++)
            {
                benchmark();
            }
        }
    }

    //performance notes:
    //int and Point seems like it might run faster than Vector2 and float but I do not observe that in the benchmark
    //avoiding multiplication requires absolute value, and multiplication performs slightly faster in benchmarks
    //a circle is a more intuitive zone of effect, and benchmarks better or equal to any other shape I can think of.
    private static bool isInsideCircle(Vector2 areaCenter,
                              float radiusSquared, Vector2 testPoint)
    {
        return (testPoint.X - areaCenter.X) * (testPoint.X - areaCenter.X) + (testPoint.Y - areaCenter.Y) * (testPoint.Y - areaCenter.Y) <= radiusSquared;
    }

    //not faster; benchmarked at 100000 iterations
    private static bool isInsideDiamond(Point areaCenter,
                              int radius, Point testPoint)
    {
        //this should run faster than isInsideCircle
        return Math.Abs(testPoint.X - areaCenter.X) + Math.Abs(testPoint.Y - areaCenter.Y) <= radius;
    }

    public static object InvokeMethod(Delegate method, params object[] args)
    {
        return method.DynamicInvoke(args);
    }
    private static void benchmark()
    {
        Vector2 v1 = new Vector2(1f, 1f);
        Vector2 v2 = new Vector2(2f, 2f);
        Point p1 = new Point(1, 1);
        Point p2 = new Point(2, 2);
        int r = 10;
        float rSquard = 100f;
        int N_ITERATIONS = 100000;


        var stopwatch = new Stopwatch();


        stopwatch.Restart();
        for (int i = 0; i < N_ITERATIONS; i++)
        {
            InvokeMethod(new Func<Point, int, Point, bool>(isInsideDiamond), p1, r, p2);
        }
        stopwatch.Stop();

        Monitor.Log($"isInsideDiamond ran {N_ITERATIONS} times in {stopwatch.ElapsedMilliseconds} ms", LogLevel.Debug);


        stopwatch.Restart();
        for (int i = 0; i < N_ITERATIONS; i++)
        {
            InvokeMethod(new Func<Vector2, float, Vector2, bool>(isInsideCircle), v1, rSquard, v2);
        }
        stopwatch.Stop();

        Monitor.Log($"isInsideCircle ran {N_ITERATIONS} times in {stopwatch.ElapsedMilliseconds} ms", LogLevel.Debug);

        
    }

    public static bool playerInRange_Prefix(Vector2 position, Farmer farmer, ref bool __result)
    {
        //WARNING: this method runs many times: once per GameLoop.UpdateTicked for each Debris
        if (farmer == null)
        {
            return PREFIX_RETURNS.USE_DEFAULT_BEHAVIOR;
        }

        try
        {
            //farmer.movedDuringLastTick
            if (farmer.CurrentTool is Slingshot && farmer.UsingTool)
            {
                Vector2 targetArea = getTargetAreaOfFarmer(farmer);
                //bool isInRange = isInsideCircle(targetArea, rangeSquared, position);
                bool isInRange = isInsideCircle(targetArea, rangeSquared, position);
                __result = isInRange;
            }
            else
            {
                __result = false;
            }

            return PREFIX_RETURNS.USE_PREFIX_BEHAVIOR;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed in {nameof(playerInRange_Prefix)}:\n{ex}", LogLevel.Error);
            return PREFIX_RETURNS.USE_DEFAULT_BEHAVIOR;
        }
    }

}