import * as fsp from "node:fs/promises";
import assert from "node:assert";

import * as semver from "semver";

const args = process.argv.slice(2);
const newVersion = semver.valid(args[0]);

assert(newVersion !== null);
assert(/\d+\.\d+\.\d+/.test(newVersion));

const file = "AutoCode.cs";

const fileContent = await fsp.readFile(file, { encoding: "utf-8" });
const updatedFileContent = fileContent.replace(/(\[Info\("[A-Za-z0-9\-_ ]+", "[A-Za-z0-9\-_ ]+", ")\d+\.\d+\.\d+("\)\])/u, `$1${newVersion}$2`);
await fsp.writeFile(file, updatedFileContent, { encoding: "utf-8" });
