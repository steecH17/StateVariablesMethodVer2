namespace CircuitSimulator
{
    public class PathNode
    {
        public Component Comp { get; set; }
        public int Direction { get; set; } // 1 (совпадает) или -1 (против)
    }
}