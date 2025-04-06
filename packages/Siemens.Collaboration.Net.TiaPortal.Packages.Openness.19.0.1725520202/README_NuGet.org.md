# TIA Portal Openness Reference Package
This transitive package will automatically reference TIA Portal Openness references into your project.

## Usage
The package will automatically reference the references from one of two sources
- Global environment variable `TiaPortalLocation` with the path to the TIA Portal installation
- Registry key of the TIA Portal installation

> Referenced Siemens.Engineering assemblies are set to `CopyLocal = false` and will not be copied into the output. Define `<SiemensEngineeringCopyLocal>true</SiemensEngineeringCopyLocal>` to copy to output.