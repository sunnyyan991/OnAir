using System;
using System.Reflection;

// Extended flight capture is an optional workstation tool, not a runtime dependency.
public static class FlightValidation
{
    public static void Run()
    {
        var runner = typeof(FlightValidation).Assembly.GetType("ContinuousWorldValidation")
            ?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        if (runner == null)
            throw new NotSupportedException("Extended flight capture tools are not installed. Run ProjectValidation.Validate for the shared project checks, or test Assets/Scenes/Main.unity in Play mode.");
        try { runner.Invoke(null, null); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
    }
}
