# Voice — Asset Specifications

## What goes here
Companion voice lines — recorded dialogue spoken by the companion through the run.

## Files required for launch (per GDD §7, §20, §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| companion_<state>_<line_id>.wav | — | WAV 44.1kHz/16-bit | One file per voice line |

~250 companion voice lines required for full launch. State matches the companion
portrait states (composed, concerned, pleading, glitched, devil, final).

## File naming convention
`companion_<state>_<line_id>.wav` — state is one of: composed, concerned, pleading,
glitched, devil, final; line_id matches the dialogue entry it voices.

## Production order (priority)
1. composed-state lines (heard first, most frequent)
2. concerned and pleading lines
3. glitched, devil, final lines as the betrayal arc lands

## Specifications
- Format: WAV
- Sample rate: 44.1 kHz
- Bit depth: 16-bit
- Mono
- Trim leading/trailing silence; consistent loudness across lines
- Glitched/devil/final states may carry processed/distorted treatment

## Placeholder status
This folder is currently empty — no voice audio is committed. The companion is silent
in-game; dialogue is text-only until recorded lines drop in here. No code changes are
required to add them.

## Integration notes
1. Drop file into this folder
2. Link the clip to its matching dialogue entry (see `Text/CompanionDialogue`)
3. Route through the Voice mixer group once `MainMixer.mixer` exists
4. Save

## GDD reference
§7 (Companion — Villain Arc), §20 (Audio), §23 (Asset Budget)
