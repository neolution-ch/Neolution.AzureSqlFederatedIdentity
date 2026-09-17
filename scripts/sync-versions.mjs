import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

// Run changeset version first
execSync("npx changeset version", { stdio: "inherit" });

// Each npm workspace entry is a .NET project directory carrying a package.json
// that exists solely so changesets can track its version. Mirror that version
// into the project's .csproj so the Version Packages PR bumps both.
const repoRoot = path.resolve(import.meta.dirname, "..");
const { workspaces } = JSON.parse(fs.readFileSync(path.join(repoRoot, "package.json"), "utf8"));

for (const workspace of workspaces) {
  const projDir = path.join(repoRoot, workspace);
  const pkgJsonPath = path.join(projDir, "package.json");
  if (!fs.existsSync(pkgJsonPath)) continue;

  const { version } = JSON.parse(fs.readFileSync(pkgJsonPath, "utf8"));

  for (const csproj of fs.readdirSync(projDir).filter((f) => f.endsWith(".csproj"))) {
    const csprojPath = path.join(projDir, csproj);
    let content = fs.readFileSync(csprojPath, "utf8");
    const eol = content.includes("\r\n") ? "\r\n" : "\n";

    if (content.includes("<Version>")) {
      content = content.replace(/<Version>[^<]*<\/Version>/g, `<Version>${version}</Version>`);
    } else {
      // Add Version to the first PropertyGroup
      content = content.replace(/<PropertyGroup>/, `<PropertyGroup>${eol}    <Version>${version}</Version>`);
    }

    fs.writeFileSync(csprojPath, content);
    console.log(`Updated ${csproj} to version ${version}`);
  }
}
