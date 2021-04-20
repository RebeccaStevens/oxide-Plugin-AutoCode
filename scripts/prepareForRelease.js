const { promises: fs } = require("fs");
const path = require("path");
const semver = require("semver");

const src = "src/AutoCode.cs";
const srcPath = path.join(process.cwd(), src);
const distPath = path.join(process.cwd(), "AutoCode.cs");

const args = process.argv.slice(2);
const newVersion = semver.valid(args[0]);

if (newVersion === null) {
  process.exit(1);
}

const header = `// This is an auto generated file. Please edit "${src}" instead.`;

(async () => {
  const fileContent = await fs.readFile(srcPath, { encoding: "utf-8" });
  const updatedFileContent = `${header}\n\n${fileContent
    .replace("0.0.0-development", newVersion)
    .trimStart()}`;
  await fs.writeFile(distPath, updatedFileContent, { encoding: "utf-8" });
})();
