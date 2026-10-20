namespace Aetheria.Runtime.Water
{
    internal struct WaterSpectrumParameters
    {
        internal WaterCascadeSpectrumParameters cascade0;
        internal WaterCascadeSpectrumParameters cascade1;
        internal WaterCascadeSpectrumParameters cascade2;

        internal WaterCascadeSpectrumParameters GetCascade(int index)
        {
            return index == 0 ? cascade0 : index == 1 ? cascade1 : cascade2;
        }

        internal void SetCascade(int index, WaterCascadeSpectrumParameters value)
        {
            if (index == 0) cascade0 = value;
            else if (index == 1) cascade1 = value;
            else cascade2 = value;
        }

        internal bool UsesSameWaveNumberGrid(WaterSpectrumParameters other)
        {
            return cascade0.tileLength.Equals(other.cascade0.tileLength)
                && cascade1.tileLength.Equals(other.cascade1.tileLength)
                && cascade2.tileLength.Equals(other.cascade2.tileLength);
        }

        internal bool UsesSameSeeds(WaterSpectrumParameters other)
        {
            return cascade0.seed == other.cascade0.seed
                && cascade1.seed == other.cascade1.seed
                && cascade2.seed == other.cascade2.seed;
        }

        public static bool operator ==(WaterSpectrumParameters a, WaterSpectrumParameters b)
        {
            return a.cascade0 == b.cascade0
                && a.cascade1 == b.cascade1
                && a.cascade2 == b.cascade2;
        }

        public static bool operator !=(WaterSpectrumParameters a, WaterSpectrumParameters b) => !(a == b);
        public override bool Equals(object obj) => obj is WaterSpectrumParameters other && this == other;
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + cascade0.GetHashCode();
                hash = hash * 31 + cascade1.GetHashCode();
                return hash * 31 + cascade2.GetHashCode();
            }
        }
    }
}
