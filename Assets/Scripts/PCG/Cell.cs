namespace PCG
{
    public class Cell 
    {
        public bool isSky;
        public bool isWater;
        public bool isGrass;

        public Cell(bool isSky)
        {
            this.isSky = isSky;
            this.isWater = false;
            this.isGrass = false;
        }
    }
}
