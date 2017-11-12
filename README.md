# Minesweeper for DCPU-16

This is a basic Minesweeper game for the DCPU-16.

This is a work in progress. There are still several missing features. In roughly
descending priority:

- Support user-entered text as an entropy source, in place of using the
  real-time clock, in case it isn't supported.
- Selectable sizes and mine counts.
- Time and mine count readouts.
- Play-again functionality.


## Controls

- Arrow keys to move the cursor.
- Space reveals the single tile under the cursor.
- F flags as a mine (F again to unflag).
- C is a flood-clear, that clears all empty tiles, or numbered tiles that are
  "satisfied" (that is, that have that many flags touching them).
