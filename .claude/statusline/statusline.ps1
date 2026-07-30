# StatusLine Script for Claude Code
# Shows: Model | Context Usage Bar | Git Branch | Current Directory

try {
    $input_data = $input | Out-String | ConvertFrom-Json

    # Extract data
    $model_name = $input_data.model.display_name
    $current_dir = Split-Path -Leaf $input_data.workspace.current_dir

    # Get context usage percentage
    $used_pct = 0
    if ($input_data.context_window.used_percentage -ne $null) {
        $used_pct = [math]::Round($input_data.context_window.used_percentage, 0)
    }

    # Build progress bar (10 chars wide)
    $bar_width = 10
    $filled = [math]::Floor($used_pct / 100 * $bar_width)
    $empty = $bar_width - $filled
    $bar = "[" + ("=" * $filled) + ("-" * $empty) + "]"

    # Get git branch
    $git_branch = ""
    try {
        $git_branch = & git rev-parse --abbrev-ref HEAD 2>$null
    } catch { }

    # Emojis
    $e1 = [System.Char]::ConvertFromUtf32(0x1F916)
    $e2 = [System.Char]::ConvertFromUtf32(0x1F4CA)
    $e3 = [System.Char]::ConvertFromUtf32(0x1F33F)
    $e4 = [System.Char]::ConvertFromUtf32(0x1F4C1)

    # ANSI colors
    $esc = [char]27
    $reset = "$esc[0m"
    $cyan = "$esc[36m"
    $green = "$esc[32m"
    $yellow = "$esc[33m"
    $magenta = "$esc[35m"
    $blue = "$esc[34m"

    Write-Output "$e1 $cyan$model_name$reset | $e2 $green$bar$reset $yellow$used_pct%$reset | $e3 $magenta$git_branch$reset | $e4 $blue$current_dir$reset"
}
catch {
    Write-Output "StatusLine Error: $_"
}
