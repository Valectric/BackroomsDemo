# /// script
# requires-python = ">=3.10"
# dependencies = ["pillow"]
# ///
"""
Grab screenshot from Windows clipboard and save to file.
Used by the /screenshot skill.
"""

from PIL import ImageGrab
from pathlib import Path
from datetime import datetime
import sys


def grab_screenshot(output_dir=None):
    """Grab image from clipboard and save it."""
    img = ImageGrab.grabclipboard()

    if img is None:
        print("ERROR: No image in clipboard. Press Print Screen first.", file=sys.stderr)
        sys.exit(1)

    # Default to Screenshots folder in project root
    if output_dir is None:
        output_dir = Path(__file__).parents[4] / "Screenshots"
    else:
        output_dir = Path(output_dir)

    output_dir.mkdir(exist_ok=True)

    # Timestamp filename
    timestamp = datetime.now().strftime("%Y-%m-%d_%H%M%S")
    output_path = output_dir / f"clipboard_{timestamp}.png"

    img.save(output_path, "PNG")

    # Print path for skill to capture
    print(str(output_path))
    return output_path


if __name__ == "__main__":
    # Optional: pass custom output directory as argument
    output_dir = sys.argv[1] if len(sys.argv) > 1 else None
    grab_screenshot(output_dir)
