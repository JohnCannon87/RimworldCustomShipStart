namespace Rimball
{
    public class TraitEntry
    {
        public string traitDefName;
        public int degree = 0;

        public TraitEntry() { }

        public TraitEntry(string traitDefName, int degree)
        {
            this.traitDefName = traitDefName;
            this.degree = degree;
        }
    }
}
