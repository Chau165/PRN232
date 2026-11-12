import { spawn } from "node:child_process"
import { fileURLToPath } from "node:url"

const root = fileURLToPath(new URL("../../", import.meta.url))
const child = spawn("dotnet", ["test", "PRN232_BE/PRN232.Application.Tests/PRN232.Application.Tests.csproj", "--no-restore",
  "--configuration", "PayosE2E", "--filter", "FullyQualifiedName~PayOSFrontendEndToEndTests", "--logger", "console;verbosity=minimal"], {
  cwd: root, stdio: "inherit", windowsHide: true,
  env: { ...process.env, RUN_PAYOS_FE_E2E: "1", Logging__EventLog__LogLevel__Default: "None" },
})
child.on("error", error => { console.error(error.message); process.exitCode = 1 })
child.on("exit", code => { process.exitCode = code ?? 1 })
