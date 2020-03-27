# OWL Data Converter

The OWL Data Converter is a console application that reads an Ontology file 
in the OWL RDF/XML format and converts the data to a tab-delimited text file.

OWL files are described at https://www.w3.org/TR/owl-parsing/
and can be edited / managed using Protégé, available at https://protege.stanford.edu/products.php#desktop-protege


## Downloads

Download a .zip file with the executable from:
https://github.com/PNNL-Comp-Mass-Spec/OWL-Data-Converter/releases

### Continuous Integration

The latest version of the application is available for six months on the [AppVeyor CI server](https://ci.appveyor.com/project/PNNLCompMassSpec/owl-data-converter/build/artifacts)

[![Build status](https://ci.appveyor.com/api/projects/status/abnkox453lgtpndc?svg=true)](https://ci.appveyor.com/project/PNNLCompMassSpec/owl-data-converter)

## Syntax

```
OWLDataConverter.exe
 InputFilePath [/O:OutputFilePath] [/PK:Suffix] [/NoP] [/NoG] [/Def] [/Com]
```

The input file is the OWL file to convert (in RDF/XML format)

Optionally use /O to specify the output path
If not provided the output file will have extension .txt or .txt.new

Use /PK to specify the string to append to the ontology term identifier 
when creating the primary key for the Term_PK column. By default uses /PK:MS1

By default the output file includes parent terms; remove them with /NoP

By default the output file includes grandparent terms; remove them with /NoG
* Using /NoP auto-enables /NoG

By default the output file will not include the term definitions; include them with /Def

By default the output file will not include the term comments; include them with /Com

### Syntax Example

```
OWLDataConverter.exe bto.owl /PK:BTO1
```

## Contacts

Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2016 \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/ \
Source code: https://github.com/PNNL-Comp-Mass-Spec/OWL-Data-Converter

## License

Licensed under the Apache License, Version 2.0; you may not use this file except 
in compliance with the License.  You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0
