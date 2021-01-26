const { promises: fs } = require("fs");
const path = require("path");
const semver = require("semver");

const filePath = path.join(process.cwd(), "src/AutoCode.cs");

const args = process.argv.slice(2);
const newVersion = semver.valid(args[0]);

if (newVersion === null) {
  process.exit(1);
}

(async () => {
  const fileContent = await fs.readFile(filePath, { encoding: "utf-8" });
  const updatedFileContent = fileContent.replace(
    "0.0.0-development",
    newVersion
  );
  await fs.writeFile(filePath, updatedFileContent, { encoding: "utf-8" });
})();
