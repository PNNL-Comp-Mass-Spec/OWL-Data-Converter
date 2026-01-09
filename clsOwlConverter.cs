using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace OWLDataConverter
{
    class clsOwlConverter : PRISM.EventNotifier
    {
        // Ignore Spelling: cls, Postgres, udt

        public const string DEFAULT_PRIMARY_KEY_SUFFIX = "BTO1";

        public struct udtOutputOptions
        {
            /// <summary>
            /// When true, include the ontology definition
            /// </summary>
            public bool IncludeDefinition;

            /// <summary>
            /// When true, if the definition is of the form "Description of term" [Ontology:Source]
            /// The definition will be written to the output file without the double quotes and without the text in the square brackets
            /// </summary>
            public bool StripQuotesFromDefinition;

            /// <summary>
            /// When true, include the ontology comment
            /// </summary>
            public bool IncludeComment;

            /// <summary>
            /// When true, include columns Parent_term_name and Parent_term_id in the output
            /// </summary>
            public bool IncludeParentTerms;

            /// <summary>
            /// When true, include columns GrandParent_term_name and GrandParent_term_id in the output
            /// </summary>
            /// <remarks>If this is true, IncludeParentTerms is assumed to be true</remarks>
            public bool IncludeGrandparentTerms;

            // ReSharper disable once GrammarMistakeInComment

            /// <summary>
            /// When true, use \N for null values (empty columns in the output file) and escape backslashes.
            /// This allows the data file to be imported using the COPY command:
            /// COPY ont.T_Tmp_BTO FROM '/tmp/bto.txt' WITH (FORMAT TEXT, HEADER, DELIMITER E'\t');
            /// </summary>
            public bool FormatForPostgres;
        }

        /// <summary>
        /// Null value flag (either an empty string or "\N")
        /// </summary>
        private readonly string NullValueFlag;

        /// <summary>
        /// Output file options
        /// </summary>
        private udtOutputOptions OutputOptions;

        /// <summary>
        /// String appended to the ontology term identifier when creating the primary key for the Term_PK column
        /// </summary>
        public string PrimaryKeySuffix { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options">Processing options</param>
        /// <param name="primaryKeySuffix">String appended to the ontology term identifier when creating the primary key for the Term_PK column</param>
        public clsOwlConverter(udtOutputOptions options, string primaryKeySuffix = DEFAULT_PRIMARY_KEY_SUFFIX)
        {
            OutputOptions = options;

            NullValueFlag = GetNullValueFlag();

            PrimaryKeySuffix = string.IsNullOrWhiteSpace(primaryKeySuffix) ? string.Empty : primaryKeySuffix;
        }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Convert an OWL file to a tab-delimited text file
        /// </summary>
        /// <param name="owlFilePath">Path to file bto.owl</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ConvertOwlFile(string owlFilePath)
        {
            var owlFile = new FileInfo(owlFilePath);

            var outputFilePath = ConstructOutputFilePath(owlFile);

            return ConvertOwlFile(owlFilePath, outputFilePath);
        }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Convert an OWL file to a tab-delimited text file
        /// </summary>
        /// <param name="owlFilePath">Path to file bto.owl</param>
        /// <param name="outputFilePath">Output file path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ConvertOwlFile(string owlFilePath, string outputFilePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(owlFilePath))
                {
                    OnErrorEvent("owlFilePath is empty; nothing to convert");
                    return false;
                }

                var owlFile = new FileInfo(owlFilePath);

                if (!owlFile.Exists)
                {
                    OnErrorEvent("Source owl file not found: " + owlFilePath);
                    return false;
                }

                OnStatusEvent("Parsing " + owlFile.FullName);

                var outputFile = string.IsNullOrWhiteSpace(outputFilePath)
                    ? new FileInfo(ConstructOutputFilePath(owlFile))
                    : new FileInfo(outputFilePath);

                // Read the data from the Owl file
                // Track them using this list
                var ontologyEntries = new List<OwlEntry>();
                var lastTerm = string.Empty;

                using (var xmlReader = new XmlTextReader(new FileStream(owlFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    while (xmlReader.Read())
                    {
                        switch (xmlReader.NodeType)
                        {
                            case XmlNodeType.Element:
                                // Start element

                                switch (xmlReader.Name)
                                {
                                    case "owl:Class":
                                        // The Envo.owl file lists the term name as a rdf:about attribute of the owl:Class tag, for example:
                                        // <owl:Class rdf:about="http://purl.obolibrary.org/obo/ENVO_00000006">
                                        // Extract out the name, though we will override it if a oboInOwl:id element is found

                                        var classIdentifier = TryGetClassIdentifier(xmlReader, "rdf:about", string.Empty);

                                        var termName = ParseTerm(xmlReader, ontologyEntries, lastTerm, classIdentifier);
                                        if (!string.IsNullOrWhiteSpace(termName))
                                            lastTerm = termName;

                                        break;
                                }

                                break;

                            case XmlNodeType.EndElement:
                                break;

                            case XmlNodeType.Text:
                                // Important text should have already been skipped
                                break;
                        }
                    }
                }

                // Make a list of identifiers that are parents of other terms
                var parentNodes = new SortedSet<string>();

                foreach (var ontologyTerm in ontologyEntries)
                {
                    foreach (var parentTerm in ontologyTerm.ParentTerms)
                    {
                        // Add the parent node if missing
                        parentNodes.Add(parentTerm.Key);
                    }
                }

                // Update IsLeaf for the ontology entries
                // An entry is a leaf node if no other nodes reference it as a parent
                foreach (var ontologyTerm in ontologyEntries)
                {
                    ontologyTerm.IsLeaf = !parentNodes.Contains(ontologyTerm.Identifier);
                }

                Console.WriteLine();
                var success = WriteOwlInfoToFile(ontologyEntries, outputFile);

                if (success)
                {
                    Console.WriteLine();
                    OnStatusEvent("Conversion is complete");
                }

                return success;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception in ConvertOwlFile: " + ex.Message);
                return false;
            }
        }

        public static udtOutputOptions DefaultOutputOptions()
        {
            return new udtOutputOptions
            {
                IncludeDefinition = false,
                StripQuotesFromDefinition = false,
                IncludeComment = false,
                IncludeParentTerms = true,
                IncludeGrandparentTerms = true,
                FormatForPostgres = false
            };
        }

        private string ConstructOutputFilePath(FileInfo owlFile)
        {

            if (string.IsNullOrWhiteSpace(owlFile.DirectoryName))
            {
                OnErrorEvent("Unable to determine parent directory of " + owlFile.FullName);
                return string.Empty;
            }

            var outputFilePath = Path.Combine(owlFile.DirectoryName, Path.GetFileNameWithoutExtension(owlFile.Name) + ".txt");

            if (outputFilePath.Equals(owlFile.FullName, StringComparison.InvariantCultureIgnoreCase))
            {
                return outputFilePath + ".new";
            }

            return outputFilePath;
        }

        private void AddParentTerm(
            IDictionary<string, OwlEntry.eParentType> parentTerms,
            string parentTypeName,
            string parentTermId,
            string identifier)
        {
            // Replace the first underscore in the parent term with a colon
            var firstUnderscore = parentTermId.IndexOf('_');
            if (firstUnderscore > 0 && firstUnderscore < parentTermId.Length - 1)
                parentTermId = parentTermId.Substring(0, firstUnderscore) + ':' + parentTermId.Substring(firstUnderscore + 1);

            if (parentTerms.ContainsKey(parentTermId))
            {
                OnWarningEvent("Parent term specified twice; ignoring " + parentTermId + " for line " + identifier);
                return;
            }

            var parentType = OwlEntry.eParentType.Unknown;

            switch (parentTypeName)
            {
                case "is_a":
                    parentType = OwlEntry.eParentType.IsA;
                    break;
                case "has_domain":
                    parentType = OwlEntry.eParentType.HasDomain;
                    break;
                case "has_order":
                    parentType = OwlEntry.eParentType.HasOrder;
                    break;
                case "has_regexp":
                    parentType = OwlEntry.eParentType.HasRegExp;
                    break;
                case "has_units":
                    parentType = OwlEntry.eParentType.HasUnits;
                    break;
                case "part_of":
                    parentType = OwlEntry.eParentType.PartOf;
                    break;
                case "develops_from":
                    parentType = OwlEntry.eParentType.DevelopsFrom;
                    break;
            }

            parentTerms.Add(parentTermId, parentType);

        }

        private static byte BoolToTinyInt(bool value)
        {
            if (value)
                return 1;

            return 0;
        }

        private void FormatDataLine(IList<string> lineOut)
        {
            if (!OutputOptions.FormatForPostgres)
            {
                return;
            }

            for (var i = 0; i < lineOut.Count; i++)
            {
                lineOut[i] = GetValueOrNull(lineOut[i]);

                if (lineOut[i].Equals(@"\N"))
                    continue;

                // Escape backslashes with \\
                lineOut[i] = lineOut[i].Replace(@"\", @"\\");
            }
        }

        private static OwlEntry GetAncestor(IEnumerable<OwlEntry> ontologyEntries, string termIdentifier)
        {
            var query = (from item in ontologyEntries where item.Identifier == termIdentifier select item);
            return query.FirstOrDefault();
        }

        private bool GetItemFromURL(string parentUrl, out string itemName)
        {
            if (string.IsNullOrWhiteSpace(parentUrl))
            {
                itemName = string.Empty;
                return false;
            }

            var urlParts = parentUrl.Split('/');
            itemName = urlParts.Last();
            return true;
        }

        private string GetNullValueFlag()
        {
            return OutputOptions.FormatForPostgres ? @"\N" : string.Empty;
        }

        private string GetValueOrNull(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? NullValueFlag : text;
        }

        private string LookupNameById(IReadOnlyDictionary<string, string> idToNameMap, string idToFind)
        {
            if (idToNameMap.TryGetValue(idToFind, out var termName))
            {
                return termName;
            }

            return string.Empty;
        }

        private List<string> OntologyTermNoParents(OwlEntry ontologyTerm)
        {
            var suffix = string.IsNullOrWhiteSpace(PrimaryKeySuffix) ? string.Empty : PrimaryKeySuffix;

            var dataColumns = new List<string>
            {
                ontologyTerm.Identifier + suffix, // Term Primary Key
                ontologyTerm.Name, // Term Name
                ontologyTerm.Identifier, // Term Identifier
                BoolToTinyInt(ontologyTerm.IsLeaf).ToString(), // Is_Leaf
                string.Join(", ", ontologyTerm.Synonyms)
            };

            if (OutputOptions.IncludeDefinition)
                dataColumns.Add(ontologyTerm.Definition);

            if (OutputOptions.IncludeComment)
                dataColumns.Add(ontologyTerm.Comment);



            return dataColumns;
        }

        private List<string> OntologyTermWithParents(
            OwlEntry ontologyTerm,
            KeyValuePair<string, OwlEntry.eParentType> parentTerm,
            IReadOnlyDictionary<string, string> idToNameMap)
        {
            var dataColumns = OntologyTermNoParents(ontologyTerm);
            dataColumns.Add(LookupNameById(idToNameMap, parentTerm.Key)); // Get parent term Name using the ID
            dataColumns.Add(parentTerm.Key); // Parent term Identifier

            return dataColumns;
        }

        /// <summary>
        /// Parse the XML for a term
        /// </summary>
        /// <param name="xmlReader"></param>
        /// <param name="ontologyEntries"></param>
        /// <param name="mostRecentTerm"></param>
        /// <param name="classIdentifier">Identifier from the owl:Class term</param>
        /// <returns>Term name</returns>
        private string ParseTerm(XmlReader xmlReader, ICollection<OwlEntry> ontologyEntries, string mostRecentTerm, string classIdentifier = "")
        {
            try
            {
                var identifier = classIdentifier;
                var name = string.Empty;
                var definition = string.Empty;
                var comment = string.Empty;

                var parentTerms = new Dictionary<string, OwlEntry.eParentType>();
                var synonyms = new List<string>();

                var insideEquivalentClass = false;
                var insideIntersectionOf = false;
                var insideSubClassOf = false;
                var relationshipType = string.Empty;

                var termParsed = false;
                var startingDepth = xmlReader.Depth;

                while (xmlReader.Read())
                {
                    switch (xmlReader.NodeType)
                    {
                        case XmlNodeType.Element:
                            // Start element

                            switch (xmlReader.Name)
                            {
                                case "owl:Class":
                                    if (insideIntersectionOf || insideEquivalentClass || insideSubClassOf)
                                    {
                                        break;
                                    }

                                    if (!string.IsNullOrWhiteSpace(classIdentifier))
                                    {
                                        OnWarningEvent("Nested owl:Class terms; this is unexpected. See term: " + classIdentifier);
                                    }
                                    else if (string.IsNullOrEmpty(name))
                                    {
                                        OnWarningEvent("Nested owl:Class terms; this is unexpected. Most recent term: " + mostRecentTerm);
                                    }
                                    else
                                    {
                                        OnWarningEvent("Nested owl:Class terms; this is unexpected. See term: " + name);
                                    }
                                    break;

                                case "owl:intersectionOf":
                                    insideIntersectionOf = true;
                                    break;

                                case "owl:equivalentClass":
                                    insideEquivalentClass = true;
                                    break;

                                case "rdfs:label":
                                    name = xmlReader.ReadInnerXml();
                                    break;

                                case "rdfs:subClassOf":
                                    insideSubClassOf = true;
                                    relationshipType = "part_of";

                                    // Look for property rdf:resource
                                    if (xmlReader.HasAttributes)
                                    {
                                        var parentURL = xmlReader.GetAttribute("rdf:resource");
                                        if (GetItemFromURL(parentURL, out var parentTermId))
                                        {
                                            AddParentTerm(parentTerms, "is_a", parentTermId, identifier);
                                        }
                                    }
                                    else
                                    {
                                        // The subclass may have a restriction, e.g.
                                        // <rdfs:subClassOf>
                                        //     <owl:Restriction>
                                        //         <owl:onProperty rdf:resource="http://purl.obolibrary.org/obo/bto#part_of"/>
                                        //         <owl:someValuesFrom rdf:resource="http://purl.obolibrary.org/obo/BTO_0000439"/>
                                        //     </owl:Restriction>
                                        // </rdfs:subClassOf>
                                        //
                                        // We'll check for this when looking for element owl:someValuesFrom

                                    }

                                    break;

                                case "owl:onProperty":
                                    if (insideSubClassOf && xmlReader.HasAttributes)
                                    {
                                        var parentURL = xmlReader.GetAttribute("rdf:resource");
                                        if (GetItemFromURL(parentURL, out var relationshipTypeText))
                                        {
                                            var poundIndex = relationshipTypeText.IndexOf("#", StringComparison.Ordinal);
                                            if (poundIndex >= 0)
                                                relationshipType = relationshipTypeText.Substring(poundIndex + 1);
                                            else
                                                relationshipType = relationshipTypeText;
                                        }
                                    }
                                    break;

                                case "owl:someValuesFrom":
                                    if (insideSubClassOf && xmlReader.HasAttributes)
                                    {
                                        var parentURL = xmlReader.GetAttribute("rdf:resource");
                                        if (GetItemFromURL(parentURL, out var parentTermId))
                                        {
                                            AddParentTerm(parentTerms, relationshipType, parentTermId, identifier);
                                        }

                                    }
                                    break;

                                case "oboInOwl:id":
                                    identifier = xmlReader.ReadInnerXml();
                                    break;

                                case "oboInOwl:hasOBONamespace":
                                    // Could parse out the name space
                                    break;


                                case "oboInOwl:hasRelatedSynonym":
                                    var synonym = xmlReader.ReadInnerXml();
                                    synonyms.Add(synonym);
                                    break;

                                case "obo:IAO_0000115":

                                    definition = xmlReader.ReadInnerXml();
                                    break;

                                case "rdfs:comment":
                                    comment = xmlReader.ReadInnerXml();
                                    break;
                            }

                            break;

                        case XmlNodeType.EndElement:
                            switch (xmlReader.Name)
                            {
                                case "rdfs:subClassOf":
                                    insideSubClassOf = false;
                                    break;
                                case "owl:intersectionOf":
                                    insideIntersectionOf = false;
                                    break;
                                case "owl:equivalentClass":
                                    insideEquivalentClass = true;
                                    break;
                            }

                            if (xmlReader.Depth == startingDepth)
                                termParsed = true;

                            break;

                        case XmlNodeType.Text:
                            // Important text should have already been skipped
                            break;
                    }

                    if (termParsed)
                        break;
                }

                var ontologyEntry = new OwlEntry(identifier, name)
                {
                    Definition = definition,
                    Comment = comment
                };

                foreach (var parentEntry in parentTerms)
                {
                    ontologyEntry.AddParentTerm(parentEntry.Key, parentEntry.Value);
                }

                foreach (var synonym in synonyms)
                {
                    ontologyEntry.AddSynonym(synonym);
                }

                ontologyEntries.Add(ontologyEntry);

                return name;
            }
            catch (Exception ex)
            {
                throw new Exception("Exception in ParseTerm: " + ex.Message, ex);
            }
        }

        private string TryGetClassIdentifier(XmlReader xmlReader, string attributeName, string defaultValue)
        {
            if (!xmlReader.HasAttributes)
                return defaultValue;

            var aboutUrl = xmlReader.GetAttribute(attributeName);

            if (string.IsNullOrWhiteSpace(aboutUrl) || !aboutUrl.StartsWith("http"))
                return defaultValue;

            var classIdentifier = aboutUrl.Split('/').Last();
            if (string.IsNullOrWhiteSpace(classIdentifier))
                return defaultValue;

            // classIdentifier may be of the form ENVO_01001569
            // Replace the underscore with a colon
            var underscoreIndex = classIdentifier.IndexOf('_');
            if (underscoreIndex > 0)
                return classIdentifier.Substring(0, underscoreIndex) + ':' + classIdentifier.Substring(underscoreIndex + 1);

            return classIdentifier;
        }

        private bool WriteOwlInfoToFile(IList<OwlEntry> ontologyEntries, FileSystemInfo outputFile)
        {
            try
            {
                OnStatusEvent("Creating " + outputFile.FullName);

                using var writer = new StreamWriter(new FileStream(outputFile.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

                var columnHeaders = new List<string>
                {
                    "Term_PK",
                    "Term_Name",
                    "Identifier",
                    "Is_Leaf",
                    "Synonyms"
                };

                if (OutputOptions.IncludeDefinition)
                    columnHeaders.Add("Definition");

                if (OutputOptions.IncludeComment)
                    columnHeaders.Add("Comment");

                // ReSharper disable once MergeIntoPattern
                if (OutputOptions.IncludeGrandparentTerms && !OutputOptions.IncludeParentTerms)
                {
                    // Force-enable inclusion of parent terms because grandparent terms will be included
                    OutputOptions.IncludeParentTerms = true;
                }

                if (OutputOptions.IncludeParentTerms)
                {
                    columnHeaders.Add("Parent_term_name");
                    columnHeaders.Add("Parent_term_ID");
                }

                if (OutputOptions.IncludeGrandparentTerms)
                {
                    columnHeaders.Add("GrandParent_term_name");
                    columnHeaders.Add("GrandParent_term_ID");
                }

                writer.WriteLine(string.Join("\t", columnHeaders));

                var idToNameMap = new Dictionary<string, string>();

                // Make a map from term ID to term Name
                var warningCount = 0;

                foreach (var ontologyTerm in ontologyEntries)
                {
                    if (idToNameMap.ContainsKey(ontologyTerm.Identifier))
                    {
                        warningCount++;
                        if (warningCount < 5)
                        {
                            OnWarningEvent(
                                $"Identifier {ontologyTerm.Identifier} is defined multiple times in th ontology entries; " +
                                "parent name lookup will use the first occurrence");
                        }
                        continue;
                    }
                    idToNameMap.Add(ontologyTerm.Identifier, ontologyTerm.Name);
                }

                foreach (var ontologyTerm in ontologyEntries)
                {
                    if (ontologyTerm.ParentTerms.Count == 0 || !OutputOptions.IncludeParentTerms)
                    {
                        var lineOut = OntologyTermNoParents(ontologyTerm);

                        if (OutputOptions.IncludeParentTerms)
                        {
                            lineOut.Add(string.Empty); // Parent term name
                            lineOut.Add(string.Empty); // Parent term ID

                            if (OutputOptions.IncludeGrandparentTerms)
                            {
                                lineOut.Add(string.Empty); // Grandparent term name
                                lineOut.Add(string.Empty); // Grandparent term ID
                            }

                        }

                        FormatDataLine(lineOut);
                        writer.WriteLine(string.Join("\t", lineOut));
                        continue;
                    }

                    foreach (var parentTerm in ontologyTerm.ParentTerms)
                    {
                        var ancestor = GetAncestor(ontologyEntries, parentTerm.Key);

                        if (ancestor == null || ancestor.ParentTerms.Count == 0 || !OutputOptions.IncludeGrandparentTerms)
                        {
                            // No grandparents (or grandparents are disabled)
                            var lineOut = OntologyTermWithParents(ontologyTerm, parentTerm, idToNameMap);

                            if (OutputOptions.IncludeGrandparentTerms)
                            {
                                lineOut.Add(string.Empty); // Grandparent term name
                                lineOut.Add(string.Empty); // Grandparent term ID
                            }

                            FormatDataLine(lineOut);
                            writer.WriteLine(string.Join("\t", lineOut));
                            continue;
                        }

                        foreach (var grandParent in ancestor.ParentTerms)
                        {
                            var lineOut = OntologyTermWithParents(ontologyTerm, parentTerm, idToNameMap);
                            lineOut.Add(LookupNameById(idToNameMap, grandParent.Key)); // Get Grandparent Name using ID
                            lineOut.Add(grandParent.Key);                              // Grandparent ID

                            FormatDataLine(lineOut);
                            writer.WriteLine(string.Join("\t", lineOut));
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error writing to file " + outputFile.FullName + ": " + ex.Message);
                return false;
            }
        }
    }
}
