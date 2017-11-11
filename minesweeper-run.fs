\ Minesweeper for the Techcompliant flavour of the DCPU.
\ Runs on TC-Forth, see https://github.com/shepheb/tcforth
\ (c) 2017 Braden Shepherdson. Released under the BSD3 license.
\ Design: The LEM is 32x12, and the Minesweeper board can be a
\ few different sizes:
\ - 10x10, 10 mines
\ - 12x12, 16 mines
\ - 24x12, 36 mines
\ Boards are stored as a grid of bitmapped words.
\ Each cell follows this plan: CFmt ---- ---- -sss
\ C = concealed flag, set for concealed cells
\ F = flag flag
\ m = mine flag
\ t = touched flag (for flood-clear)
\ sss = surrouning mine count. Set for all cells, even mines.

\ Random number generator
CREATE multiplier  $41c6 , $4e6d ,
$7fff CONSTANT modmask-hi
$ffff CONSTANT modmask-lo
CREATE previous-random 0 , 0 ,
12345 CONSTANT increment

: seed ( lo hi -- )
  modmask-hi and >R   modmask-lo and R>
  previous-random 1+ !
  previous-random    !
;

\ Use the real-time clock to seed the generator.
\ Interrupt $10, only A for input, pull x and z.
: clock-seed ( -- ) $10 $8014 clock find-dev hwi
  previous-random !   modmask-hi and previous-random 1+ ! ;

clock-seed


\ Generates a random number from 0 to MAXINT.
: genrandom ( -- u )
  previous-random 1+ @   multiplier 1+ @   *EX >R  ( lo  R: c )
  previous-random @      multiplier @      * R> +  ( lo hi )
  >R  increment +EX R> + ( lo' hi' )
      modmask-hi and previous-random !
  dup modmask-lo and previous-random 1+ ! ( lo )
;

\ Random number in the range 0 <= x < n
: random ( n -- random ) genrandom   swap umod ;




VARIABLE (width)   VARIABLE (height)   VARIABLE (max-mines)
24 CONSTANT max-width
12 CONSTANT max-height
CREATE board   max-width max-height * allot  \ Max sizes

: b@ ( cell_index -- cell )  board + @ ;
: b! ( cell cell_index -- )  board + ! ;
: b+ ( ci flag -- )  >R dup b@ R>        or  swap b! ;
: b- ( ci flag -- )  >R dup b@ R> invert and swap b! ;
: b? ( ci flag -- )  >r b@ r> and 0<> ;

\ 4 - Board utilities continued
: width ( -- +n ) (width) @ ;
: height ( -- +n ) (height) @ ;
: size ( -- +n ) width height * ;
: max-mines ( -- +n ) (max-mines) @ ;
: >row ( ci -- row )  width / ;
: >col ( ci -- row )  width mod ;
: >coords ( ci -- r c )  dup >row swap >col ;
: >index ( r c -- ci ) swap width * + ;
: >vram ( ci -- *vram )  dup >row 32 *  swap >col +   vram + ;

$8000 CONSTANT hidden
$4000 CONSTANT flagged
$2000 CONSTANT mine
$1000 CONSTANT touched


\ Neighbours utility - running an xt over all neighbours of a cell.
: in-bounds? ( r c  -- ? )
  0 width within >R   0 height within R> and ;

CREATE neighbours 8 allot
VARIABLE #neighbours
: +neighbour ( r c -- ) 2dup in-bounds? IF
  >index   #neighbours @   neighbours + !   1 #neighbours +!
  ELSE 2drop THEN ;

: +neighbour-sides ( r c -- )
  2dup 1- +neighbour   1+ +neighbour ;

: load-neighbours ( ci -- )
  0 #neighbours !    >coords ( r c )
  2dup >R 1- R>   2dup +neighbour   +neighbour-sides
  2dup >R 1+ R>   2dup +neighbour   +neighbour-sides
  +neighbour-sides ;

VARIABLE (each-neighbour-xt)

\ Runs an xt ( ... ci -- ... ) on all existing neighbour cells.
: each-neighbour ( ci xt -- )
  (each-neighbour-xt) @ >R   (each-neighbour-xt) !
  load-neighbours   #neighbours @ 0 DO
    i neighbours + @   (each-neighbour-xt) @ execute
  LOOP R> (each-neighbour-xt) ! ;



\ Board initialization
: generate-mine ( -- )
  BEGIN size random dup mine b? WHILE drop REPEAT
    dup . mine b+ ;

: clear-board ( -- )  size 0 DO hidden i b! LOOP ;
: populate-mines  ( -- )  max-mines 0 DO generate-mine LOOP ;

: mine?+ ( n1 ci -- n2 )  mine b? IF 1+ THEN ;
: count-mine ( ci -- )
  dup mine b? IF drop EXIT THEN
  0 over ['] mine?+ each-neighbour ( ci n )
  over b@ 15 invert and or swap b! ;

: populate-counts ( -- )  size 0 DO
    i count-mine
  LOOP ;

: init-board ( rows cols mines -- )
  (max-mines) ! (width) ! (height) !
  clear-board populate-mines populate-counts ;


VARIABLE state
1 CONSTANT playing   2 CONSTANT exploded   3 CONSTANT victory

: new-game ( -- ) playing  state ! ;
: boom ( -- )     exploded state ! ;
: win ( -- )      victory  state ! ;

: exploded? ( -- ? ) state @ exploded = ;
: won? ( -- ? ) state @ victory = ;

: check-win ( -- )
  0 size 0 DO i flagged b? IF 1+ THEN LOOP max-mines =
  0 size 0 DO i hidden  b? IF 1+ THEN LOOP max-mines =
  and IF win THEN ;



\ Rendering
32 CONSTANT blank
: clear ( -- ) vram-size 0 DO blank i vram + ! LOOP ;

$0458 CONSTANT vmine \ Mines are a black X on a red background
$e046 CONSTANT vflag \ Flags are a yellow F on black
$f030 CONSTANT vnumber \ Numbers are white on black
$f020 CONSTANT vblank  \ Black space
$0820 CONSTANT vhidden \ Hidden tiles are grey space

VARIABLE row
VARIABLE col


\ Converts a cell value to its LEM form.
: >char ( cell -- x )
  dup flagged and IF drop vflag   EXIT THEN
  dup hidden  and IF drop vhidden EXIT THEN
  dup mine    and IF drop vmine   EXIT THEN
  15 and dup 0= IF drop vblank ELSE vnumber + THEN ;

\ Swap the two lines below to make hidden cells transparent for debugging.
: debug-transparency ( char cell -- char )
  \ hidden and IF $f0ff and $0700 or THEN ;
  drop ;

: paint-cell ( ci -- )  dup   b@ dup >char swap
  debug-transparency   swap >vram ! ;

: show-cursor ( -- ) row @ col @ >index >vram dup @
  $f0ff and $0b00 or swap ! ;

VARIABLE dirty?

: render ( -- )
  exploded? IF 384 0 DO $0420 i vram + ! LOOP THEN
  won?      IF 384 0 DO $0220 i vram + ! LOOP THEN
  size 0 DO i paint-cell LOOP
  show-cursor ;



\ Input handling
128 CONSTANT key-up
129 CONSTANT key-down
130 CONSTANT key-left
131 CONSTANT key-right

: handler-up    row @ 1-   0         max   row ! ;
: handler-down  row @ 1+   height 1- min   row ! ;
: handler-left  col @ 1-   0         max   col ! ;
: handler-right col @ 1+   width  1- min   col ! ;

\ Space clears the single tile.
: cursor>index row @ col @ >index ;
: handler-space cursor>index   dup hidden b-
  mine b? IF boom EXIT THEN check-win ;

\ F flags as a mine
: handler-f cursor>index flagged 2dup b? IF b- ELSE b+ THEN
  check-win ;

: flag-count ( n1 ci -- n2 ) flagged b? IF 1+ THEN ;
: satisfied? ( ci -- ? )
  dup flagged b? IF drop false EXIT THEN \ Flags are unsat
  dup mine b? IF drop false  boom EXIT THEN
  dup b@ $f and 0= IF drop true EXIT THEN \ Blanks are sat
  \ Otherwise, count surrounding flags.
  0 over ['] flag-count each-neighbour ( ci n )
  swap b@ $f and = ;


CREATE fq max-width max-height * allot
here CONSTANT fq-top
VARIABLE fq-tail VARIABLE fq-head

: fq-push ( ci -- ) fq-top fq DO
  dup i @ = IF UNLOOP EXIT THEN LOOP
  fq-tail @ !   1 fq-tail +!
  fq-head @ 0= IF fq fq-head ! THEN ;
: fq-empty? ( -- ? ) fq-head @ fq-tail @ = ;
: fq-pop ( -- ci ) fq-head @ @   1 fq-head +! ;
: fq-init ( -- )
  fq-top fq DO -1 i ! LOOP \ Set all entries to -1
  0 fq-head !   fq fq-tail ! ;

: maybe-push ( ci -- )
  dup flagged b? IF drop ELSE fq-push THEN ;
: flood-clear ( ci -- )
  fq-init fq-push ( )
  BEGIN fq-empty? not WHILE
    fq-pop
    dup hidden b- \ Reveal myself.
    dup mine b? IF drop   boom EXIT THEN
    dup satisfied? IF
      ['] maybe-push each-neighbour
    ELSE drop THEN
  REPEAT ;

: handler-c ( -- ) cursor>index flood-clear check-win ;

CREATE handlers
key-up    ,  ' handler-up ,
key-down  ,  ' handler-down ,
key-left  ,  ' handler-left ,
key-right ,  ' handler-right ,
char f    ,  ' handler-f ,
32        ,  ' handler-space ,
char c    ,  ' handler-c ,
7 CONSTANT #handlers
2 CONSTANT /handler
: handler ( i -- *h ) /handler * handlers + ;

: handle-key ( -- ) key   #handlers 0 DO
    dup i handler @ = IF drop i handler 1+ @ execute THEN
  LOOP drop ;

: input-loop clear BEGIN render handle-key AGAIN ;


\ Main method that launches the game.
: main 10 10 10 init-board input-loop ;

\ Write out the complete state to a standalone ROM.
: bootstrap key drop   ['] main (main!)    (bootstrap) ;

\ Trailing file that runs the game directly.
main
