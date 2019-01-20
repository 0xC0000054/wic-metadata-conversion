////////////////////////////////////////////////////////////////////////
//
// This file is part of wic-metadata-conversion, an application that
// demonstrates WIC and GDI+ meta-data support.
//
// Copyright (c) 2019 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using Mono.Options;
using System.Collections.Generic;

namespace WICMetadataDemo
{
    internal sealed class CommandLineParser
    {
        public CommandLineParser(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                OptionSet optionSet = new OptionSet
                {
                    { "d|dump", "Dump all WIC metadata to stdout.", (string value) => DumpMetadata = value != null},
                    { "h|help", "Show the help text.", (string value) => ShowHelp = value != null},
                };

                List<string> remainingOptions = optionSet.Parse(args);
                if (remainingOptions.Count == 1)
                {
                    string value = remainingOptions[0];

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        ImageFileName = value;
                    }
                }
            }
        }

        public string ImageFileName
        {
            get;
        }

        public bool DumpMetadata
        {
            get;
            private set;
        }

        public bool ShowHelp
        {
            get;
            private set;
        }
    }
}
