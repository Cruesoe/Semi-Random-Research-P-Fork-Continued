# Project workflow

- Treat `C:\Users\crues\source\repos\SemiRandomResearchProgressionContinued` as the authoritative repository.
- Treat `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Semi Random ResearchP-Fork Continued` as the local Steam upload/deployment folder, not as a repository mirror.
- The local Steam mod folder must contain only files required to run and upload the mod: `1.6`, `About`, `Languages`, and `Textures`.
- Never deploy repository-only files or directories, including `.git`, `.agents`, `.codex`, `Source`, project or solution files, `AGENTS.md`, `README.md`, IDE metadata, or build intermediates.
- After every completed mod change, successfully build the mod and deploy the allowed upload content to the local Steam mod folder. Remove any files there that are not present in the allowed repository content.
- Verify deployment by comparing relative file paths and file hashes for the allowed content; completion requires zero missing, extra, or mismatched files in the local Steam mod folder.
