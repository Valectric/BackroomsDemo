---
name: screenshot
description: |
  Grab screenshot from clipboard and analyze it in conversation. Usage: Press Print Screen, then type /screenshot
---

# Screenshot Skill

Captures the current clipboard image and reads it into the conversation for analysis.

## Requirements

- **UV** - Python package manager (handles Pillow dependency automatically)

## Workflow

1. **Run the clipboard grabber script with UV** using the base directory from the skill prompt header:
   ```bash
   uv run "<BASE_DIR>/scripts/grab_clipboard.py"
   ```
   Replace `<BASE_DIR>` with the `Base directory for this skill:` path provided in the skill prompt. This is needed because `.claude/skills/` uses symlinks that Bash cannot resolve with relative paths.

2. **Capture the output path** from stdout

3. **If successful:** Use the Read tool to display the image at the captured path

4. **If error:** Inform user to press Print Screen first, then try again

5. **After displaying:** Ask the user what they'd like to know or do with the screenshot

## Error Handling

- If script returns "ERROR: No image in clipboard" - tell user to press Print Screen and try `/screenshot` again
- Images are saved to `Screenshots/` folder with timestamp filenames

## Output Location

Screenshots are saved to:
`Screenshots/clipboard_YYYY-MM-DD_HHMMSS.png`
