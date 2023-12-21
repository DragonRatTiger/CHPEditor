# CHPEditor (0.1)
## An (extremely not finished) application for editing CHP (Pomyu Chara) files

Alongside being a personal project to further study C# & application development, I hope to also create a modern editor for Pomyu Charas.

At this current stage, nothing can actually be edited. The only aspect of this application that works right now is the ability to preview bitmaps & animations. This application does the best that it currently can to preview the animations as-intended by FeelingPomu2nd. The application can only display a single chara at runtime, and will only point to `chara/chara.chp`.

## Controls

### Previewing Bitmaps

`[` to switch to Bitmap mode

`1/2/3/4/5/6/7/8` for displaying CharBMP, CharBMP2P, CharFace, CharFace2P, SelectCG, SelectCG2P, CharTex, and CharTex2P respectively.

### Previewing Animations

`]` to switch to Animation mode

`Space` to toggle between 1P/2P palettes (Defaults to 1P if 2P image is missing)

`P` to pause the Animation

`1/2/3/4/5/6/7/8/9` for Animations #1 through #9 respectively

`Q/W/E/R/T/Y/U/I/O` for Animations #10 through #18 respectively
