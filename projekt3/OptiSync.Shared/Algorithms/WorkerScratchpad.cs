namespace OptiSync.Shared.Algorithms;

public sealed class WorkerScratchpad
{
    public int[] Mapping1 { get; }
    public int[] Mapping2 { get; }
    public bool[] Visited { get; }
    public int[] ChildA { get; }
    public int[] ChildB { get; }
    public int[] SegmentBuffer { get; }

    public WorkerScratchpad(int cityCount)
    {
        Mapping1 = new int[cityCount];
        Mapping2 = new int[cityCount];
        Visited = new bool[cityCount];
        ChildA = new int[cityCount];
        ChildB = new int[cityCount];
        SegmentBuffer = new int[cityCount];
    }
}
