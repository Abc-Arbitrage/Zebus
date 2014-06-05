using System;

namespace Abc.Zebus.Testing.Measurements
{
    public class MeasureConfiguration
    {
        public int Iteration { get; set; }
        public int WarmUpIteration { get; set; }
        public string Name { get; set; }
        public Action<long> Action { get; set; }

        public int TotalIteration
        {
            get { return Iteration + WarmUpIteration; }
        }
    }
}