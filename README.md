# Semi Random Research: P-Fork Continued

A RimWorld 1.6 continuation of Semi Random Research: P-Fork. It adds random research-project selection and visual research analytics to make progression less predictable and more informative.

## Repository layout

- `1.6/`, `About/`, `Languages/`, and `Textures/` contain the distributable mod.
- `Source/` contains the C# project and source code.
- `SemiRandomResearchProgressionContinued.slnx` is the Visual Studio solution entry point.

## Building

The project targets .NET Framework 4.7.2 and references a local RimWorld installation plus Harmony. By default it uses the standard Steam paths. Set `RIMWORLD_DIR` or `HARMONY_DLL` to override them.

```powershell
dotnet build .\SemiRandomResearchProgressionContinued.slnx -c Release
```

Successful builds copy the mod assembly to `1.6/Assemblies`.
