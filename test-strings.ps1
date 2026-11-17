# Checks the translation table: every row has one string per language, none empty,
# and every L("key") used in the code exists. Run after building: powershell -File test-strings.ps1
$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot
$asm = [Reflection.Assembly]::LoadFrom((Join-Path $dir 'DesktopMenu.exe'))
$t = $asm.GetType('Program'); $bf = [Reflection.BindingFlags]'NonPublic,Static'
$codes = $t.GetField('LangCodes', $bf).GetValue($null)
$names = $t.GetField('LangNames', $bf).GetValue($null)
$table = $t.GetField('Text', $bf).GetValue($null)
$fail = 0
if ($names.Length -ne $codes.Length) { "LangNames has $($names.Length) entries, expected $($codes.Length)"; $fail++ }
foreach ($k in $table.Keys) {
  $row = $table[$k]
  if ($row.Length -ne $codes.Length) { "row '$k' has $($row.Length) strings, expected $($codes.Length)"; $fail++ }
  for ($i = 0; $i -lt $row.Length; $i++) { if ([string]::IsNullOrWhiteSpace($row[$i])) { "row '$k' is empty for $($codes[$i])"; $fail++ } }
}
$used = Select-String -Path (Join-Path $dir '*.cs') -Pattern 'L\("([A-Za-z0-9]+)"\)' -AllMatches |
  ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($k in $used) { if (-not $table.ContainsKey($k)) { "L(""$k"") is used but missing from the table"; $fail++ } }
if ($fail) { "FAILED: $fail problem(s)"; exit 1 }
"OK: $($table.Count) strings x $($codes.Length) languages, $($used.Count) keys used in code"
