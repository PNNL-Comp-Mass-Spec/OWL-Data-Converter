using System.Collections.Generic;

namespace OWLDataConverter
{
    public class OwlEntry
    {
        public enum eParentType
        {
            Unknown = 0,
            IsA = 1,
            HasDomain = 2,
            HasOrder = 3,
            HasRegExp = 4,
            HasUnits = 5,
            PartOf = 6,
            DevelopsFrom = 7
        }

        private readonly string mIdentifier;

        /// <summary>
        /// Parent Terms
        /// </summary>
        /// <remarks>
        /// Keys are parent term ids and values are parent term names
        /// </remarks>
        private readonly Dictionary<string, eParentType> mParentTerms;

        private readonly SortedSet<string> mSynonyms;

        public string Identifier => mIdentifier;

        public string Name { get; set; }

        public string Definition { get; set; }

        public string Comment { get; set; }

        /// <summary>
        /// Parent term identifiers
        /// </summary>
        /// <remarks>
        /// Keys are parent term ids and values are parent term names
        /// </remarks>
        public Dictionary<string, eParentType> ParentTerms => mParentTerms;

        /// <summary>
        /// Synonyms
        /// </summary>
        public SortedSet<string> Synonyms => mSynonyms;

        public bool IsLeaf { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="identifier"></param>
        public OwlEntry(string identifier) : this(identifier, string.Empty)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="name"></param>
        /// <param name="isLeaf"></param>
        public OwlEntry(string identifier, string name, bool isLeaf = false)
        {
            mIdentifier = identifier;
            Name = name;
            Definition = string.Empty;
            Comment = string.Empty;
            IsLeaf = isLeaf;

            mParentTerms = new Dictionary<string, eParentType>();

            mSynonyms = new SortedSet<string>();
        }

        public void AddParentTerm(string parentTermID, eParentType parentTermType)
        {
            if (mParentTerms.ContainsKey(parentTermID))
            {
                // Parent term already defined
                return;
            }

            mParentTerms.Add(parentTermID, parentTermType);
        }

        public void AddSynonym(string synonym)
        {
            if (mSynonyms.Contains(synonym))
            {
                // Synonym already defined
                return;
            }

            mSynonyms.Add(synonym);
        }

        public override string ToString()
        {
            return mIdentifier + ": " + Name;
        }

    }
}
