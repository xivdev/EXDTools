# EXDTools

## BasicSchemaGenerator
Creates a basic schema file consisting of just unknowns for sheets whose schemas do not exist in the provided schema directory.
Generation can be performed against a game installation, or a DirectoryManager patch directory json file.

## DirectoryManager
Updates, extracts, and maintains a sort of virtual filesystem for EXD files and other necessary files for EXD schema validation.

## SchemaConverter
Converts SaintCoinach json files to EXDSchema yaml files.

## SchemaValidator
Performs validation against a set of sheets provided a json schema (for the yaml) and a directory of EXDSchema schemas.
Validation can be performed against a game installation, or a DirectoryManager patch directory json file.