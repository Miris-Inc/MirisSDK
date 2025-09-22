namespace Aqua.Runtime
{
    // What depth mode to use for the far field plane
    public enum FarFieldDistanceMode
    {
        // Set the far field to a constant depth, provided by the CPU. Very fast but very inaccurate
        Constant,
        
        // Set the far field to the depth of the closest splat in the far field. Very slightly slower than Constant
        // mode, and much more accurate
        Dynamic,
    }
}
