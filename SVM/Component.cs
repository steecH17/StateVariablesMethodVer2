public class Component
    {
        public string Name { get; set; }
        public ComponentType Type { get; set; }
        public double Value { get; set; }
        public int Node1 { get; set; }
        public int Node2 { get; set; }
        public int ControlNode1 { get; set; }
        public int ControlNode2 { get; set; }
        public bool IsTree { get; set; }

        public Component(string name, ComponentType type, double value, int n1, int n2, int cn1 = 0, int cn2 = 0)
        {
            Name = name; Type = type; Value = value;
            Node1 = n1; Node2 = n2; ControlNode1 = cn1; ControlNode2 = cn2;
        }

        public override string ToString() => $"{Name} ({Type}) {Value}";
    }