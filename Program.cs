using System;
using System.Collections.Generic;
using PRISM;

namespace OWLDataConverter
{
    /// <summary>
    /// This program reads an Ontology file in the OWL RDF/XML format and converts the data to a tab-delimited text file.
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://www.pnnl.gov/integrative-omics
    /// </remarks>
    static class Program
    {
        public const string PROGRAM_DATE = "January 8, 2026";

        private static string mInputFilePath;
        private static string mOutputFilePath;
        private static clsOwlConverter.udtOutputOptions mOutputOptions;
        private static string mPrimaryKeySuffix;

        static int Main()
        {
            var objParseCommandLine = new clsParseCommandLine();

            mInputFilePath = string.Empty;
            mOutputFilePath = string.Empty;

            mOutputOptions = clsOwlConverter.DefaultOutputOptions();

            mPrimaryKeySuffix = clsOwlConverter.DEFAULT_PRIMARY_KEY_SUFFIX;

            try
            {
                var success = false;

                if (objParseCommandLine.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(objParseCommandLine))
                        success = true;
                }

                if (!success ||
                    objParseCommandLine.NeedToShowHelp ||
                    string.IsNullOrWhiteSpace(mInputFilePath))
                {
                    ShowProgramHelp();
                    return -1;
                }

                var converter = new clsOwlConverter(mOutputOptions, mPrimaryKeySuffix);

                converter.ErrorEvent += Converter_ErrorEvent;
                converter.StatusEvent += Converter_StatusEvent;
                converter.WarningEvent += Converter_WarningEvent;

                success = converter.ConvertOwlFile(mInputFilePath, mOutputFilePath);

                if (!success)
                {
                    ShowErrorMessage("ConvertOwlFile returned false");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred in Program->Main: " + Environment.NewLine + ex.Message);
                Console.WriteLine(ex.StackTrace);
                return -1;
            }

            return 0;

        }

        private static void Converter_ErrorEvent(string message, Exception ex)
        {
            ShowErrorMessage(message);
        }

        private static void Converter_StatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void Converter_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        private static string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine objParseCommandLine)
        {
            // Returns True if no problems; otherwise, returns false
            var lstValidParameters = new List<string> {
                "I", "O",
                "PK", "NoP", "NoG",
                "Def", "Definition",
                "Com", "Comm", "Comment",
                "Postgres"};

            try
            {
                // Make sure no invalid parameters are present
                if (objParseCommandLine.InvalidParametersPresent(lstValidParameters))
                {
                    var badArguments = new List<string>();
                    foreach (var item in objParseCommandLine.InvalidParameters(lstValidParameters))
                    {
                        badArguments.Add("/" + item);
                    }

                    ShowErrorMessage("Invalid command line parameters", badArguments);

                    return false;
                }

                // Query objParseCommandLine to see if various parameters are present
                if (objParseCommandLine.NonSwitchParameterCount > 0)
                {
                    mInputFilePath = objParseCommandLine.RetrieveNonSwitchParameter(0);
                }

                if (objParseCommandLine.NonSwitchParameterCount > 1)
                {
                    mOutputFilePath = objParseCommandLine.RetrieveNonSwitchParameter(1);
                }

                if (objParseCommandLine.RetrieveValueForParameter("I", out var inputFilePath))
                {
                    mInputFilePath = string.Copy(inputFilePath);
                }

                if (objParseCommandLine.RetrieveValueForParameter("O", out var outputFilePath))
                {
                    mOutputFilePath = string.Copy(outputFilePath);
                }

                if (objParseCommandLine.RetrieveValueForParameter("PK", out var primaryKeySuffix))
                {
                    mPrimaryKeySuffix = string.Copy(primaryKeySuffix);
                }

                if (objParseCommandLine.IsParameterPresent("NoP"))
                {
                    mOutputOptions.IncludeParentTerms = false;
                    mOutputOptions.IncludeGrandparentTerms = false;
                }

                if (objParseCommandLine.IsParameterPresent("NoG"))
                    mOutputOptions.IncludeGrandparentTerms = false;

                if (objParseCommandLine.IsParameterPresent("Def") ||
                    objParseCommandLine.IsParameterPresent("Definition"))
                {
                    mOutputOptions.IncludeDefinition = true;
                }

                if (objParseCommandLine.IsParameterPresent("Com") ||
                    objParseCommandLine.IsParameterPresent("Comm") ||
                    objParseCommandLine.IsParameterPresent("Comment"))
                {
                    mOutputOptions.IncludeComment = true;
                }

                if (objParseCommandLine.IsParameterPresent("Postgres"))
                    mOutputOptions.FormatForPostgres = true;

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters", ex);
            }

            return false;
        }

        private static void ShowErrorMessage(string message, Exception ex = null)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void ShowErrorMessage(string message, IReadOnlyCollection<string> additionalInfo)
        {
            if (additionalInfo == null || additionalInfo.Count == 0)
            {
                ConsoleMsgUtils.ShowError(message);
                return;
            }

            var formattedMessage = message + ":";

            foreach (var item in additionalInfo)
            {
                formattedMessage += Environment.NewLine + "  " + item;
            }

            ConsoleMsgUtils.ShowErrorCustom(formattedMessage, true, false);
        }

        private static void ShowProgramHelp()
        {
            var exeName = System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            try
            {
                Console.WriteLine();
                Console.WriteLine("This program reads an Ontology file in the OBO format and converts the data to a tab-delimited text file.");
                Console.WriteLine();
                Console.WriteLine("Program syntax:" + Environment.NewLine + exeName);
                Console.WriteLine(" InputFilePath [/O:OutputFilePath] [/PK:Suffix] [/NoP] [/NoG] [/Def] [/Com]");
                Console.WriteLine();
                Console.WriteLine("The input file is the OBO file to convert");
                Console.WriteLine();
                Console.WriteLine("Optionally use /O to specify the output path");
                Console.WriteLine("If not provided the output file will have extension .txt or .txt.new");
                Console.WriteLine();
                Console.WriteLine("Use /PK to specify the string to append to the ontology term identifier");
                Console.WriteLine("when creating the primary key for the Term_PK column. By default uses /PK:BTO1");
                Console.WriteLine();
                Console.WriteLine("By default the output file includes parent terms; remove them with /NoP");
                Console.WriteLine("By default the output file includes grandparent terms; remove them with /NoG");
                Console.WriteLine("Using /NoP auto-enables /NoG");
                Console.WriteLine();
                Console.WriteLine("By default the output file will not include the term definitions; include them with /Def or /Definition");
                Console.WriteLine();
                Console.WriteLine("By default the output file will not include the term comments; include them with /Com or /Comment");
                Console.WriteLine();
                Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov");
                Console.WriteLine("Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://www.pnnl.gov/integrative-omics");
                Console.WriteLine();

                // Delay for 750 msec in case the user double-clicked this file from within Windows Explorer (or started the program via a shortcut)
                System.Threading.Thread.Sleep(750);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error displaying the program syntax: " + ex.Message);
            }

        }

    }
}
