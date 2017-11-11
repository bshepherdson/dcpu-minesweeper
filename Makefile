.PHONY: all
default: all

EMULATOR ?= dcpu
FLAGS += -turbo
FORTH ?= forth-boot.rom

all:

minesweeper-run.fs: minesweeper.fs run.fs
	cat $^ > $@

minesweeper-bootstrap.fs: minesweeper.fs bootstrap.fs
	cat $^ > $@

run: minesweeper-run.fs FORCE
	$(EMULATOR) $(FLAGS) -disk minesweeper-run.fs forth-boot.rom

minesweeper.rom: minesweeper-bootstrap.fs bootstrap.dcs
	rm -f minesweeper.rom
	touch minesweeper.rom
	$(EMULATOR) $(FLAGS) -disk minesweeper-bootstrap.fs -script bootstrap.dcs forth-boot.rom

bootstrap: minesweeper.rom

clean: FORCE
	rm -f minesweeper.rom minesweeper-run.fs minesweeper-bootstrap.fs

FORCE:

